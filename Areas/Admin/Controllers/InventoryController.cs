using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Receptionist")]
    public class InventoryController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IAIntelligenceService _aiService;

        public InventoryController(SpaDbContext context, IInventoryService inventoryService, IAIntelligenceService aiService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _aiService = aiService;
        }

        public async Task<IActionResult> Index(string? search, string? filter)
        {
            var query = _context.Materials
                .Include(m => m.SupplierEntity)
                .Include(m => m.Batches)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                if (int.TryParse(search, out int id)) {
                    query = query.Where(m => m.MaterialId == id || m.MaterialName.Contains(search));
                } else {
                    query = query.Where(m => m.MaterialName.Contains(search));
                }
            }

            if (filter == "low")
            {
                query = query.Where(m => m.CurrentStock <= m.MinStock);
            }

            var materials = await query.ToListAsync();
            
            // Tính toán dự báo cho từng vật tư
            var predictions = new Dictionary<int, int>();
            foreach (var m in materials)
            {
                predictions[m.MaterialId] = await _inventoryService.PredictRemainingCapacityAsync(m.MaterialId);
            }

            ViewBag.Predictions = predictions;
            ViewBag.Search = search;
            ViewBag.Filter = filter;

            return View(materials);
        }

        public async Task<IActionResult> Batches(int id)
        {
            var material = await _context.Materials
                .Include(m => m.Batches.OrderBy(b => b.ExpiryDate))
                .FirstOrDefaultAsync(m => m.MaterialId == id);

            if (material == null) return NotFound();

            return View(material);
        }

        public async Task<IActionResult> Suppliers()
        {
            var suppliers = await _context.Suppliers.ToListAsync();
            return View(suppliers);
        }

        [HttpPost]
        public async Task<IActionResult> AddSupplier(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                supplier.CreatedAt = DateTime.Now;
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplier(int id)
        {
            var s = await _context.Suppliers.FindAsync(id);
            if (s == null) return NotFound();
            return Json(s);
        }

        [HttpPost]
        public async Task<IActionResult> EditSupplier(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                _context.Suppliers.Update(supplier);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleSupplierStatus(int id)
        {
            var s = await _context.Suppliers.FindAsync(id);
            if (s != null)
            {
                s.IsActive = !s.IsActive;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // --- Purchase Order (PO) Management ---

        public async Task<IActionResult> PurchaseOrders()
        {
            var orders = await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> CreatePO()
        {
            ViewBag.Suppliers = await _context.Suppliers.Where(s => s.IsActive).ToListAsync();
            ViewBag.Materials = await _context.Materials.Where(m => m.IsActive).ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePO(PurchaseOrder po, List<PurchaseOrderDetail> details)
        {
            po.POCode = $"PO-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
            po.OrderDate = DateTime.Now;
            po.Status = POStatus.Pending;
            
            // Tính toán lại thành tiền trên server cho an toàn
            foreach (var d in details)
            {
                d.TotalPrice = d.Quantity * d.UnitPrice;
            }
            po.TotalAmount = details.Sum(d => d.TotalPrice);

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync(); // Lưu PO trước để có ID

            foreach (var d in details)
            {
                d.POId = po.POId;
                _context.PurchaseOrderDetails.Add(d);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PurchaseOrders));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovePO(int id)
        {
            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po != null && po.Status == POStatus.Pending)
            {
                po.Status = POStatus.Approved;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReceiveGoods(int id, DateTime? expiryDate)
        {
            var result = await _inventoryService.ReceivePOAsync(id, expiryDate);
            return Json(new { success = result });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reports()
        {
            var now = DateTime.Now;
            var sevenDaysAgo = now.AddDays(-7);
            var fourteenDaysAgo = now.AddDays(-14);

            // 1. Phân tích dữ liệu tiêu thụ (vẫn giữ để vẽ biểu đồ bar/donut bên dưới)
            var consumptions = await _context.MaterialConsumptions
                .Include(c => c.Material)
                .Include(c => c.Staff)
                .Where(c => c.CreatedAt >= fourteenDaysAgo)
                .ToListAsync();

            var staffWaste = consumptions
                .GroupBy(c => (c.Staff != null ? c.Staff.FullName : "Unknown"))
                .Select(g => new {
                    StaffName = g.Key,
                    TotalActual = g.Sum(c => c.ActualQuantity),
                    TotalStandard = g.Sum(c => c.StandardQuantity),
                    WasteRate = g.Sum(c => c.StandardQuantity) > 0 
                        ? (g.Sum(c => c.ActualQuantity) - g.Sum(c => c.StandardQuantity)) / g.Sum(c => c.StandardQuantity) * 100 
                        : 0,
                    // Top lãng phí của người này
                    TopLeakedMaterials = g.GroupBy(x => x.Material?.MaterialName ?? "Khác")
                        .Select(mg => new { 
                            Name = mg.Key, 
                            Diff = mg.Sum(x => x.ActualQuantity - x.StandardQuantity) 
                        })
                        .OrderByDescending(x => x.Diff)
                        .Take(3)
                        .ToList()
                })
                .OrderByDescending(x => x.WasteRate)
                .ToList();

            // 2. Phân tích doanh thu & lợi nhuận
            var financeData = await _context.BookingDetails
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Include(bd => bd.Service)
                .Where(bd => bd.Booking.Status == BookingStatus.Completed && bd.Booking.BookingDate >= sevenDaysAgo)
                .ToListAsync();

            var profitAnalysis = financeData
                .GroupBy(bd => bd.Service.ServiceName)
                .Select(g => {
                    var revenue = g.Sum(bd => bd.PriceAtTime);
                    var materialCost = consumptions
                        .Where(c => c.CreatedAt >= sevenDaysAgo && g.Select(x => x.DetailId).Contains(c.BookingDetailId))
                        .Sum(c => (decimal)c.ActualQuantity * (c.Material?.PurchasePrice ?? 0) / 20);
                    
                    return new {
                        ServiceName = g.Key,
                        Revenue = (double)revenue,
                        Cost = (double)materialCost,
                        Profit = (double)(revenue - materialCost)
                    };
                })
                .OrderByDescending(x => x.Profit)
                .ToList();

            // 3. Top vật tư bị lãng phí toàn Spa
            var topMaterialLeaks = consumptions
                .GroupBy(c => c.Material?.MaterialName ?? "Khác")
                .Select(g => new {
                    MaterialName = g.Key,
                    TotalDiff = g.Sum(c => c.ActualQuantity - c.StandardQuantity),
                    LeakRate = g.Sum(c => c.StandardQuantity) > 0 ? (g.Sum(c => c.ActualQuantity - c.StandardQuantity) / g.Sum(c => c.StandardQuantity) * 100) : 0
                })
                .OrderByDescending(x => x.TotalDiff)
                .Take(5)
                .ToList();

            // 4. AI PRO MAX DATA
            ViewBag.StaffKPIs = await _aiService.GetStaffRadarDataAsync();
            ViewBag.HeatmapData = await _aiService.GetBookingHeatmapAsync();
            ViewBag.AIInsights = await _aiService.GetStrategicRecommendationsAsync();
            
            ViewBag.StaffWaste = staffWaste;
            ViewBag.ProfitAnalysis = profitAnalysis;
            ViewBag.TopMaterialLeaks = topMaterialLeaks;
            
            return View();
        }

        public async Task<IActionResult> ServicePackages()
        {
            var services = await _context.Services
                .Include(s => s.ServiceMaterials)
                    .ThenInclude(sm => sm.Material)
                .Where(s => s.IsActive)
                .ToListAsync();
            
            ViewBag.Materials = await _context.Materials.Where(m => m.IsActive).ToListAsync();
            return View(services);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateServiceMaterial(int serviceId, int materialId, double quantity)
        {
            var sm = await _context.ServiceMaterials
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.MaterialId == materialId);
            
            if (sm != null)
            {
                sm.Quantity = quantity;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> AddServiceMaterial(int serviceId, int materialId, double quantity)
        {
            var exists = await _context.ServiceMaterials.AnyAsync(x => x.ServiceId == serviceId && x.MaterialId == materialId);
            if (!exists)
            {
                var sm = new ServiceMaterial { ServiceId = serviceId, MaterialId = materialId, Quantity = quantity };
                _context.ServiceMaterials.Add(sm);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Vật tư này đã có trong gói!" });
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConsumptionLog(string? search, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.MaterialConsumptions
                .Include(c => c.Material)
                .Include(c => c.Staff)
                .Include(c => c.BookingDetail).ThenInclude(bd => bd.Service)
                .Include(c => c.BookingDetail).ThenInclude(bd => bd.Booking).ThenInclude(b => b.Customer)
                .OrderByDescending(c => c.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Staff.FullName.Contains(search) || 
                                         c.Material.MaterialName.Contains(search) || 
                                         c.BookingDetail.Service.ServiceName.Contains(search) ||
                                         c.BookingDetail.Booking.Customer.FullName.Contains(search));
            }

            if (fromDate.HasValue) query = query.Where(c => c.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(c => c.CreatedAt <= toDate.Value.AddDays(1));

            var logs = await query.ToListAsync();
            ViewBag.Search = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}
