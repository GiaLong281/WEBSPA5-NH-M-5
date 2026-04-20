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

        public InventoryController(SpaDbContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
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
            var consumptions = await _context.MaterialConsumptions
                .Include(c => c.Material)
                .Include(c => c.Staff)
                .Include(c => c.BookingDetail).ThenInclude(bd => bd.Service)
                .ToListAsync();

            // 1. Phân tích hao hụt theo nhân viên
            var staffWaste = consumptions
                .GroupBy(c => (c.Staff != null ? c.Staff.FullName : "Unknown"))
                .Select(g => new {
                    StaffName = g.Key,
                    TotalActual = g.Sum(c => c.ActualQuantity),
                    TotalStandard = g.Sum(c => c.StandardQuantity),
                    WasteRate = g.Sum(c => c.StandardQuantity) > 0 
                        ? (g.Sum(c => c.ActualQuantity) - g.Sum(c => c.StandardQuantity)) / g.Sum(c => c.StandardQuantity) * 100 
                        : 0
                })
                .OrderByDescending(x => x.WasteRate)
                .ToList();

            // 2. Phân tích Lợi nhuận gộp (BI)
            // Lấy doanh thu từ BookingDetails đã hoàn thành
            var financeData = await _context.BookingDetails
                .Include(bd => bd.Booking)
                .Include(bd => bd.Service)
                .Where(bd => bd.Booking.Status == BookingStatus.Completed)
                .ToListAsync();

            var profitAnalysis = financeData
                .GroupBy(bd => bd.Service.ServiceName)
                .Select(g => {
                    var revenue = g.Sum(bd => bd.PriceAtTime);
                    var materialCost = consumptions
                        .Where(c => g.Select(x => x.DetailId).Contains(c.BookingDetailId))
                        .Sum(c => (decimal)c.ActualQuantity * (c.Material?.PurchasePrice ?? 0));
                    
                    return new {
                        ServiceName = g.Key,
                        Revenue = (double)revenue,
                        Cost = (double)materialCost,
                        Profit = (double)(revenue - materialCost)
                    };
                })
                .OrderByDescending(x => x.Profit)
                .ToList();

            ViewBag.StaffWaste = staffWaste;
            ViewBag.ProfitAnalysis = profitAnalysis;
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
    }
}
