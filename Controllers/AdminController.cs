using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly AuditService _audit;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(SpaDbContext context, AuditService audit, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _audit = audit;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                ViewBag.TotalCustomers = await _context.Customers.CountAsync();
                ViewBag.TotalBookings = await _context.Bookings.CountAsync();
                ViewBag.TotalServices = await _context.Services.CountAsync();
                ViewBag.TotalStaff = await _context.Staffs.CountAsync();
                return View();
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message}");
            }
        }

        // Quản lý nhân viên
        public async Task<IActionResult> ManageStaff()
        {
            var staffs = await _context.Staffs.Include(s => s.Branch).ToListAsync();
            return View(staffs);
        }

        [HttpGet]
        public IActionResult CreateStaff() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(Staff model)
        {
            if (ModelState.IsValid)
            {
                _context.Staffs.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Staff", model.StaffId.ToString(), null, Newtonsoft.Json.JsonConvert.SerializeObject(model));
                TempData["Success"] = "Thêm nhân viên thành công!";
                return RedirectToAction(nameof(ManageStaff));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditStaff(int id)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null) return NotFound();
            return View(staff);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(int id, Staff model)
        {
            if (id != model.StaffId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.StaffId == id);
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Staff", model.StaffId.ToString(),
                    Newtonsoft.Json.JsonConvert.SerializeObject(old),
                    Newtonsoft.Json.JsonConvert.SerializeObject(model));
                TempData["Success"] = "Cập nhật nhân viên thành công!";
                return RedirectToAction(nameof(ManageStaff));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff != null)
            {
                _context.Staffs.Remove(staff);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Staff", id.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(staff), null);
                TempData["Success"] = "Xóa nhân viên thành công!";
            }
            return RedirectToAction(nameof(ManageStaff));
        }

        // Quản lý dịch vụ
        public async Task<IActionResult> ManageServices()
        {
            try
            {
                var services = await _context.Services
                    .Include(s => s.Category)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
                return View(services);
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message}");
            }
        }

        [HttpGet]
public async Task<IActionResult> CreateService()
{
    try
    {
        // Lấy danh sách danh mục
        var categories = await _context.ServiceCategories.ToListAsync();
        
        // Kiểm tra và gán ViewBag
        if (categories != null && categories.Any())
        {
            ViewBag.Categories = categories;
        }
        else
        {
            ViewBag.Categories = new List<ServiceCategory>();
            TempData["Warning"] = "Chưa có danh mục dịch vụ. Vui lòng tạo danh mục trước khi thêm dịch vụ.";
        }
        
        // Khởi tạo model mới với giá trị mặc định
        var model = new Service
        {
            IsActive = true,
            IsPopular = false,
            CreatedAt = DateTime.Now
        };
        
        return View(model);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in CreateService GET: {ex.Message}");
        ViewBag.Categories = new List<ServiceCategory>();
        return View(new Service());
    }
}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(Service model, IFormFile ImageFile)
        {
            try
            {
                ModelState.Remove("Image");
                ModelState.Remove("ServiceMaterials");
                ModelState.Remove("BookingDetails");
                ModelState.Remove("Category");
                
                if (ModelState.IsValid)
                {
                    // Xử lý upload ảnh
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        try
                        {
                            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
                            string filePath = Path.Combine(uploadsFolder, fileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await ImageFile.CopyToAsync(fileStream);
                            }

                            model.Image = fileName;
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Lỗi khi tải ảnh lên: " + ex.Message);
                            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                            return View(model);
                        }
                    }

                    model.CreatedAt = DateTime.Now;
                    model.IsActive = true;
                    model.IsPopular = false;

                    _context.Services.Add(model);
                    await _context.SaveChangesAsync();
                    
                    TempData["Success"] = "Thêm dịch vụ thành công!";
                    await _audit.LogAsync("Create", "Service", model.ServiceId.ToString(), null, Newtonsoft.Json.JsonConvert.SerializeObject(model));
                    return RedirectToAction(nameof(ManageServices));
                }
                
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditService(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null) return NotFound();
                
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(service);
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(int id, Service model, IFormFile ImageFile)
        {
            try
            {
                if (id != model.ServiceId) return NotFound();
                
                ModelState.Remove("Image");
                ModelState.Remove("ServiceMaterials");
                ModelState.Remove("BookingDetails");
                ModelState.Remove("Category");
                
                if (ModelState.IsValid)
                {
                    var old = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == id);
                    if (old == null) return NotFound();
                    
                    // Xử lý upload ảnh mới
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        try
                        {
                            // Xóa ảnh cũ
                            if (!string.IsNullOrEmpty(old.Image))
                            {
                                string oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", old.Image);
                                if (System.IO.File.Exists(oldImagePath))
                                {
                                    System.IO.File.Delete(oldImagePath);
                                }
                            }

                            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
                            string filePath = Path.Combine(uploadsFolder, fileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await ImageFile.CopyToAsync(fileStream);
                            }

                            model.Image = fileName;
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("", "Lỗi khi tải ảnh lên: " + ex.Message);
                            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                            return View(model);
                        }
                    }
                    else
                    {
                        model.Image = old.Image;
                    }

                    model.CreatedAt = old.CreatedAt;

                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    
                    TempData["Success"] = "Cập nhật dịch vụ thành công!";
                    await _audit.LogAsync("Update", "Service", model.ServiceId.ToString(),
                        Newtonsoft.Json.JsonConvert.SerializeObject(old),
                        Newtonsoft.Json.JsonConvert.SerializeObject(model));
                    return RedirectToAction(nameof(ManageServices));
                }
                
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service != null)
                {
                    if (!string.IsNullOrEmpty(service.Image))
                    {
                        string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", service.Image);
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }
                    
                    _context.Services.Remove(service);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Xóa dịch vụ thành công!";
                    await _audit.LogAsync("Delete", "Service", id.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(service), null);
                }
                else
                {
                    TempData["Error"] = "Không tìm thấy dịch vụ!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xóa: " + ex.Message;
            }
            return RedirectToAction(nameof(ManageServices));
        }

        // Quản lý lịch hẹn
        public async Task<IActionResult> ManageBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Branch)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
            return View(bookings);
        }

        // Hệ thống
        public IActionResult ManageSystem() => View();

        // Thống kê
        public async Task<IActionResult> Statistics()
        {
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
                
            var monthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .Take(12)
                .ToListAsync();
                
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.MonthlyRevenue = monthlyRevenue;
            return View();
        }

        // Lịch sử thay đổi
        public async Task<IActionResult> AuditLog()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(l => l.CreatedAt)
                .Take(500)
                .ToListAsync();
            return View(logs);
        }
        public async Task<IActionResult> ManageCustomers()
{
    var customers = await _context.Customers
        .Include(c => c.Bookings)
        .OrderByDescending(c => c.CreatedAt)
        .ToListAsync();
    return View(customers);
}
    }
}