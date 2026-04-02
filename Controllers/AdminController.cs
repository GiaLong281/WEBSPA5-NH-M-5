using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SpaN5.Controllers
{

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly AuditService _audit;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        public AdminController(SpaDbContext context, AuditService audit, IPasswordHasher<User> passwordHasher, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _context = context;
            _audit = audit;
            _passwordHasher = passwordHasher;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
        }

        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalCustomers = await _context.Customers.CountAsync();
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.TotalServices = await _context.Services.CountAsync();
            ViewBag.TotalStaff = await _context.Staffs.CountAsync();
            return View();
        }

        // ================= QUẢN LÝ CHI NHÁNH =================
        public async Task<IActionResult> ManageBranches()
        {
            var branches = await _context.Branches.ToListAsync();
            return View(branches);
        }

        [HttpGet]
        public IActionResult CreateBranch() => View();

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateBranch(Branch model, Microsoft.AspNetCore.Http.IFormFile? ImageFile)
{
    if (ModelState.IsValid)
    {
        if (ImageFile != null && ImageFile.Length > 0)
        {
            var fileName = System.Guid.NewGuid().ToString() + System.IO.Path.GetExtension(ImageFile.FileName);
            var path = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches", fileName);
            
            var folder = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                await ImageFile.CopyToAsync(stream);
            }
            model.Image = "/images/branches/" + fileName;
        }

        model.CreatedAt = DateTime.Now;
        model.UpdatedAt = DateTime.Now;
        _context.Branches.Add(model);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("Create", "Branch", model.BranchId.ToString(), null, JsonSerializer.Serialize(model));
        return RedirectToAction(nameof(ManageBranches));
    }
    return View(model);
}

[HttpGet]
public async Task<IActionResult> EditBranch(int id)
{
    var branch = await _context.Branches.FindAsync(id);
    if (branch == null) return NotFound();
    return View(branch);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditBranch(int id, Branch model, Microsoft.AspNetCore.Http.IFormFile? ImageFile)
{
    if (id != model.BranchId) return NotFound();
    if (ModelState.IsValid)
    {
        var old = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);
        
        if (ImageFile != null && ImageFile.Length > 0)
        {
            var fileName = System.Guid.NewGuid().ToString() + System.IO.Path.GetExtension(ImageFile.FileName);
            var path = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches", fileName);
            
            var folder = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", "branches");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                await ImageFile.CopyToAsync(stream);
            }
            model.Image = "/images/branches/" + fileName;
        }
        else
        {
            model.Image = old?.Image;
        }

        model.UpdatedAt = DateTime.Now;
        _context.Update(model);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("Update", "Branch", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
        return RedirectToAction(nameof(ManageBranches));
    }
    return View(model);
}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                _context.Branches.Remove(branch);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Branch", id.ToString(), JsonSerializer.Serialize(branch), null);
            }
            return RedirectToAction(nameof(ManageBranches));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBranchStatus(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            var old = JsonSerializer.Serialize(branch);
            branch.IsActive = !branch.IsActive;
            branch.UpdatedAt = DateTime.Now;
            _context.Branches.Update(branch);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Update", "Branch", id.ToString(), old, JsonSerializer.Serialize(branch));
            return RedirectToAction(nameof(ManageBranches));
        }

        // ================= QUẢN LÝ NHÂN VIÊN =================
        // (Các action đã có, giữ nguyên)
        public async Task<IActionResult> ManageStaff()
        {
            var staffs = await _context.Staffs.Include(s => s.Branch).ToListAsync();
            return View(staffs);
        }

        [HttpGet]
        public async Task<IActionResult> CreateStaff()
        {
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "BranchId", "BranchName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(Staff model, string Password)
        {
            if (string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError("", "Mật khẩu đăng nhập không được để trống.");
            }

            if (ModelState.IsValid)
            {
                _context.Staffs.Add(model);
                await _context.SaveChangesAsync();

                // Tạo User cho Staff
                var user = new User
                {
                    Username = model.Phone ?? model.Email ?? model.FullName.Replace(" ", ""),
                    Role = "Staff",
                    FullName = model.FullName,
                    Email = model.Email,
                    StaffId = model.StaffId,
                    CreatedAt = DateTime.Now
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Create", "Staff", model.StaffId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageStaff));
            }
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "BranchId", "BranchName");
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
                await _audit.LogAsync("Update", "Staff", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
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
                await _audit.LogAsync("Delete", "Staff", id.ToString(), JsonSerializer.Serialize(staff), null);
            }
            return RedirectToAction(nameof(ManageStaff));
        }

        // GET: Tạo tài khoản mới
        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Roles = new List<string> { "Admin", "Staff" };
            ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
            return View();
        }

        // POST: Tạo tài khoản mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string Username, string Password, string FullName, string Email, string Role, int? BranchId)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError("", "Tên đăng nhập và mật khẩu không được để trống.");
                ViewBag.Roles = new List<string> { "Admin", "Staff" };
                ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                ViewBag.Username = Username;
                ViewBag.FullName = FullName;
                ViewBag.Email = Email;
                ViewBag.Role = Role;
                ViewBag.BranchId = BranchId;
                return View();
            }

            // Kiểm tra username đã tồn tại
            if (await _context.Users.AnyAsync(u => u.Username == Username))
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại.");
                ViewBag.Roles = new List<string> { "Admin", "Staff" };
                ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                ViewBag.Username = Username;
                ViewBag.FullName = FullName;
                ViewBag.Email = Email;
                ViewBag.Role = Role;
                ViewBag.BranchId = BranchId;
                return View();
            }

            if (string.IsNullOrWhiteSpace(FullName))
            {
                FullName = Username;
            }

            // Tạo User
            var user = new User
            {
                Username = Username,
                Role = Role,
                FullName = FullName,
                Email = Email,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, Password);

            // Nếu role Staff, tạo Staff trước để lấy StaffId
            if (Role == "Staff")
            {
                if (!BranchId.HasValue)
                {
                    ModelState.AddModelError("", "Vui lòng chọn chi nhánh cho nhân viên.");
                    ViewBag.Roles = new List<string> { "Admin", "Staff" };
                    ViewBag.Branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    ViewBag.Username = Username;
                    ViewBag.FullName = FullName;
                    ViewBag.Email = Email;
                    ViewBag.Role = Role;
                    ViewBag.BranchId = BranchId;
                    return View();
                }

                var staff = new Staff
                {
                    FullName = FullName,
                    Email = Email,
                    BranchId = BranchId,
                    Status = "active",
                    CreatedAt = DateTime.Now
                };
                _context.Staffs.Add(staff);
                await _context.SaveChangesAsync(); // để có StaffId
                user.StaffId = staff.StaffId;
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Create", "User", user.Id.ToString(), null, JsonSerializer.Serialize(user));
            TempData["SuccessMessage"] = $"Tài khoản {Username} với role {Role} đã được tạo thành công.";
            if (Role == "Staff")
                return RedirectToAction(nameof(ManageStaff));
            else
                return RedirectToAction(nameof(Dashboard));
        }

        // ================= QUẢN LÝ KHÁCH HÀNG =================
        public async Task<IActionResult> ManageCustomers()
        {
            var customers = await _context.Customers
                .Include(c => c.DiaChi)
                    .ThenInclude(d => d.Quan)
                        .ThenInclude(q => q.ThanhPho)
                .ToListAsync();
            return View(customers);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCustomer()
        {
            ViewBag.ThanhPhos = await _context.ThanhPhos.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(Customer model, int MaQuan, string SoNha, string Duong)
        {
            if (ModelState.IsValid)
            {
                // Xử lý địa chỉ
                if (MaQuan > 0)
                {
                    var diaChi = new DiaChi
                    {
                        SoNha = SoNha,
                        Duong = Duong,
                        MaQuan = MaQuan
                    };
                    _context.DiaChis.Add(diaChi);
                    await _context.SaveChangesAsync();
                    model.MaDiaChi = diaChi.MaDiaChi;
                }

                _context.Customers.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Customer", model.CustomerId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageCustomers));
            }
            ViewBag.ThanhPhos = await _context.ThanhPhos.ToListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditCustomer(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.DiaChi)
                .FirstOrDefaultAsync(c => c.CustomerId == id);
            if (customer == null) return NotFound();
            ViewBag.ThanhPhos = await _context.ThanhPhos.ToListAsync();
            ViewBag.Quans = customer.DiaChi?.Quan != null ? await _context.Quans.Where(q => q.MaThanhPho == customer.DiaChi.Quan.MaThanhPho).ToListAsync() : new List<Quan>();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(int id, Customer model, int MaQuan, string SoNha, string Duong)
        {
            if (id != model.CustomerId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
                // Cập nhật địa chỉ
                if (model.MaDiaChi.HasValue)
                {
                    var diaChi = await _context.DiaChis.FindAsync(model.MaDiaChi.Value);
                    if (diaChi != null)
                    {
                        diaChi.SoNha = SoNha;
                        diaChi.Duong = Duong;
                        diaChi.MaQuan = MaQuan;
                        _context.Update(diaChi);
                    }
                }
                else if (MaQuan > 0)
                {
                    var newDiaChi = new DiaChi { SoNha = SoNha, Duong = Duong, MaQuan = MaQuan };
                    _context.DiaChis.Add(newDiaChi);
                    await _context.SaveChangesAsync();
                    model.MaDiaChi = newDiaChi.MaDiaChi;
                }

                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Customer", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageCustomers));
            }
            ViewBag.ThanhPhos = await _context.ThanhPhos.ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Customer", id.ToString(), JsonSerializer.Serialize(customer), null);
            }
            return RedirectToAction(nameof(ManageCustomers));
        }

        // ================= QUẢN LÝ DỊCH VỤ =================
        // (Đã có, giữ nguyên)
        public async Task<IActionResult> ManageServices()
        {
            var services = await _context.Services.Include(s => s.Category).ToListAsync();
            return View(services);
        }

        [HttpGet]
        public async Task<IActionResult> CreateService()
        {
            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(Service model, Microsoft.AspNetCore.Http.IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var fileName = System.Guid.NewGuid().ToString() + System.IO.Path.GetExtension(ImageFile.FileName);
                    var path = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);
                    using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }
                    model.Image = fileName;
                }

                _context.Services.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Service", model.ServiceId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageServices));
            }
            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditService(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();
            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(int id, Service model, Microsoft.AspNetCore.Http.IFormFile? ImageFile)
        {
            if (id != model.ServiceId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == id);
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var fileName = System.Guid.NewGuid().ToString() + System.IO.Path.GetExtension(ImageFile.FileName);
                    var path = System.IO.Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);
                    using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }
                    model.Image = fileName;
                }
                else
                {
                    model.Image = old?.Image;
                }
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Service", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageServices));
            }
            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Service", id.ToString(), JsonSerializer.Serialize(service), null);
            }
            return RedirectToAction(nameof(ManageServices));
        }

        // ================= QUẢN LÝ VẬT TƯ =================
        public async Task<IActionResult> ManageMaterials()
        {
            var materials = await _context.Materials.ToListAsync();
            return View(materials);
        }

        [HttpGet]
        public IActionResult CreateMaterial() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaterial(Material model)
        {
            if (ModelState.IsValid)
            {
                _context.Materials.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Material", model.MaterialId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageMaterials));
            }
            return View(model);
        }
        [HttpGet]
public async Task<IActionResult> GetQuansByThanhPho(int maThanhPho)
{
    var quans = await _context.Quans.Where(q => q.MaThanhPho == maThanhPho).Select(q => new { q.MaQuan, q.TenQuan }).ToListAsync();
    return Json(quans);
}

        [HttpGet]
        public async Task<IActionResult> EditMaterial(int id)
        {
            var material = await _context.Materials.FindAsync(id);
            if (material == null) return NotFound();
            return View(material);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMaterial(int id, Material model)
        {
            if (id != model.MaterialId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialId == id);
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Material", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageMaterials));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            var material = await _context.Materials.FindAsync(id);
            if (material != null)
            {
                _context.Materials.Remove(material);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Material", id.ToString(), JsonSerializer.Serialize(material), null);
            }
            return RedirectToAction(nameof(ManageMaterials));
        }

        // ================= QUẢN LÝ GIAO DỊCH KHO =================
        public async Task<IActionResult> ManageStockTransactions()
        {
            var transactions = await _context.StockTransactions
                .Include(t => t.Material)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(transactions);
        }

        [HttpGet]
        public async Task<IActionResult> CreateStockTransaction()
        {
            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStockTransaction(StockTransaction model)
        {
            if (ModelState.IsValid)
            {
                // Cập nhật tồn kho vật tư
                var material = await _context.Materials.FindAsync(model.MaterialId);
                if (material != null)
                {
                    if (model.Type == "import")
                        material.CurrentStock += model.Quantity;
                    else if (model.Type == "export")
                        material.CurrentStock -= model.Quantity;

                    _context.Update(material);
                }

                model.CreatedAt = DateTime.Now;
                _context.StockTransactions.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "StockTransaction", model.TransactionId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(ManageStockTransactions));
            }
            ViewBag.Materials = await _context.Materials.ToListAsync();
            return View(model);
        }

        // ================= QUẢN LÝ LỊCH HẸN =================
        public async Task<IActionResult> ManageBookings()
        {
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Branch)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
            return View(bookings);
        }

        // ================= QUẢN LÝ HỆ THỐNG =================
        public IActionResult ManageSystem() => View();

        // ================= THỐNG KÊ =================
        public async Task<IActionResult> Statistics()
{
    // Lấy dữ liệu về client để aggregate, tránh lỗi SQLite
    var payments = await _context.Payments
        .Where(p => p.Status == PaymentStatus.Paid)
        .Select(p => new { p.Amount, p.CreatedAt })
        .ToListAsync();

    var totalRevenue = payments.Sum(p => p.Amount);

    var monthlyRevenue = payments
        .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
        .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
        .Take(12)
        .ToList();

    ViewBag.TotalRevenue = totalRevenue;
    ViewBag.MonthlyRevenue = monthlyRevenue;
    return View();
}

        // ================= LỊCH SỬ THAY ĐỔI =================
        public async Task<IActionResult> AuditLog()
        {
            var logs = await _context.AuditLogs.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync();
            return View(logs);
        }


[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> BackupDatabase()
{
    try
    {
        var connectionString = _configuration.GetConnectionString("SpaConnection");
        var dataSource = connectionString.Split(';')
            .FirstOrDefault(s => s.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=')[1];
        if (string.IsNullOrEmpty(dataSource))
        {
            TempData["Message"] = "Không xác định được đường dẫn database.";
            return RedirectToAction(nameof(ManageSystem));
        }

        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), dataSource);
        if (!System.IO.File.Exists(dbPath))
        {
            TempData["Message"] = "File database không tồn tại.";
            return RedirectToAction(nameof(ManageSystem));
        }

        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        var backupFileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var backupPath = Path.Combine(backupDir, backupFileName);
        System.IO.File.Copy(dbPath, backupPath, overwrite: true);

        TempData["Message"] = $"Backup thành công! File: {backupFileName}";
    }
    catch (Exception ex)
    {
        TempData["Message"] = $"Backup thất bại: {ex.Message}";
    }
    return RedirectToAction(nameof(ManageSystem));
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RestoreDatabase()
{
    try
    {
        var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
        if (!Directory.Exists(backupDir) || !Directory.GetFiles(backupDir, "*.db").Any())
        {
            TempData["Message"] = "Không tìm thấy file backup nào.";
            return RedirectToAction(nameof(ManageSystem));
        }

        var latestBackup = Directory.GetFiles(backupDir, "*.db")
            .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
            .First();

        var connectionString = _configuration.GetConnectionString("SpaConnection");
        var dataSource = connectionString.Split(';')
            .FirstOrDefault(s => s.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=')[1];
        if (string.IsNullOrEmpty(dataSource))
        {
            TempData["Message"] = "Không xác định được đường dẫn database.";
            return RedirectToAction(nameof(ManageSystem));
        }

        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), dataSource);
        // Tạo backup hiện tại trước khi restore (đề phòng)
        var tempBackup = dbPath + ".temp";
        System.IO.File.Copy(dbPath, tempBackup, true);
        System.IO.File.Copy(latestBackup, dbPath, true);
        System.IO.File.Delete(tempBackup);

        TempData["Message"] = "Khôi phục database thành công từ file: " + Path.GetFileName(latestBackup);
    }
    catch (Exception ex)
    {
        TempData["Message"] = $"Khôi phục thất bại: {ex.Message}";
    }
    return RedirectToAction(nameof(ManageSystem));
}


[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult ClearTempData()
{
    try
    {
        HttpContext.Session.Clear();

        var tempDir = Path.Combine(_webHostEnvironment.WebRootPath, "temp");
        if (Directory.Exists(tempDir))
        {
            foreach (var file in Directory.GetFiles(tempDir))
            {
                try { System.IO.File.Delete(file); } catch { }
            }
            foreach (var dir in Directory.GetDirectories(tempDir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        TempData["Message"] = "Dọn dẹp dữ liệu tạm thành công.";
    }
    catch (Exception ex)
    {
        TempData["Message"] = $"Lỗi khi dọn dẹp: {ex.Message}";
    }
    return RedirectToAction(nameof(ManageSystem));
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> OptimizeDatabase()
{
    try
    {
        await _context.Database.ExecuteSqlRawAsync("VACUUM;");
        TempData["Message"] = "Tối ưu cơ sở dữ liệu thành công.";
    }
    catch (Exception ex)
    {
        TempData["Message"] = $"Tối ưu thất bại: {ex.Message}";
    }
    return RedirectToAction(nameof(ManageSystem));
}
[HttpGet]
public async Task<IActionResult> Settings()
{
    var settings = await _context.Settings.ToListAsync();
    return View(settings);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Settings(Dictionary<string, string> settings)
{
    foreach (var kvp in settings)
    {
        var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == kvp.Key);
        if (setting != null)
        {
            setting.Value = kvp.Value;
            setting.UpdatedAt = DateTime.Now;
        }
        else
        {
            _context.Settings.Add(new Setting
            {
                Key = kvp.Key,
                Value = kvp.Value,
                UpdatedAt = DateTime.Now
            });
        }
    }
    await _context.SaveChangesAsync();
    TempData["Message"] = "Cài đặt đã được lưu.";
    return RedirectToAction(nameof(Settings));
}
    }
}