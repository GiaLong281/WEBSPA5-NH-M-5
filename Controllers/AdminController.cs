using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly AuditService _audit;

        public AdminController(SpaDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalCustomers = await _context.Customers.CountAsync();
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.TotalServices = await _context.Services.CountAsync();
            ViewBag.TotalStaff = await _context.Staffs.CountAsync();
            return View();
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
            }
            return RedirectToAction(nameof(ManageStaff));
        }

        // Quản lý dịch vụ
        public async Task<IActionResult> ManageServices()
        {
            var services = await _context.Services.Include(s => s.Category).ToListAsync();
            return View(services);
        }

        [HttpGet]
        public IActionResult CreateService() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(Service model)
        {
            if (ModelState.IsValid)
            {
                _context.Services.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Service", model.ServiceId.ToString(), null, Newtonsoft.Json.JsonConvert.SerializeObject(model));
                return RedirectToAction(nameof(ManageServices));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditService(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(int id, Service model)
        {
            if (id != model.ServiceId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == id);
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Service", model.ServiceId.ToString(),
                    Newtonsoft.Json.JsonConvert.SerializeObject(old),
                    Newtonsoft.Json.JsonConvert.SerializeObject(model));
                return RedirectToAction(nameof(ManageServices));
            }
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
                await _audit.LogAsync("Delete", "Service", id.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(service), null);
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
            var totalRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => p.Amount);
            var monthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .Take(12)
                .ToListAsync();
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.MonthlyRevenue = monthlyRevenue;
            return View();
        }

        // Lịch sử thay đổi
        public async Task<IActionResult> AuditLog()
        {
            var logs = await _context.AuditLogs.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync();
            return View(logs);
        }
    }
}