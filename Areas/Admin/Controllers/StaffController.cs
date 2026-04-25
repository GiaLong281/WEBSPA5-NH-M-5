using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using SpaN5.Models;
using StaffModel = SpaN5.Models.Staff;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin, Receptionist")]
    public class StaffController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IDataProtector _protector;

        public StaffController(SpaDbContext context, IPasswordHasher<User> passwordHasher, IDataProtectionProvider provider)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _protector = provider.CreateProtector("SpaN5.QR.Protector");
        }

        [HttpGet]
        public IActionResult GetDynamicQR()
        {
            // Sinh token chứa Role và Thời gian (Ticks) để chống gian lận
            var time = DateTime.UtcNow.Ticks.ToString();
            
            var ktvToken = _protector.Protect($"KTV|{time}");
            var letanToken = _protector.Protect($"LETAN|{time}");

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var ktvLink = baseUrl + $"/Staff/Home/CheckInQR?token={Uri.EscapeDataString(ktvToken)}";
            var letanLink = baseUrl + $"/Staff/Home/CheckInQR?token={Uri.EscapeDataString(letanToken)}";

            var ktvQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(ktvLink)}";
            var letanQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(letanLink)}";

            return Json(new { success = true, ktvQrUrl, letanQrUrl });
        }

        // GET: Admin/Staff
        public async Task<IActionResult> Index()
        {
            var staffList = await _context.Staffs
                .Include(s => s.Specialization)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            
            // Lấy danh sách tài khoản tương ứng
            ViewBag.Users = await _context.Users.ToListAsync();
            
            return View(staffList);
        }

        // GET: Admin/Staff/Create
        public IActionResult Create()
        {
            ViewBag.Services = _context.Services.Where(s => s.IsActive).ToList();
            return View();
        }

        // POST: Admin/Staff/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffModel staff, string Username, string Password, string Role)
        {
            if (ModelState.IsValid)
            {
                // 1. Lưu thông tin Nhân sự
                _context.Staffs.Add(staff);
                await _context.SaveChangesAsync();

                // 2. Tạo tài khoản đăng nhập
                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    var user = new User
                    {
                        Username = Username,
                        Role = Role,
                        FullName = staff.FullName,
                        StaffId = staff.StaffId,
                        CreatedAt = DateTime.Now
                    };
                    user.PasswordHash = _passwordHasher.HashPassword(user, Password);
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            ViewBag.Services = _context.Services.Where(s => s.IsActive).ToList();
            return View(staff);
        }

        // GET: Admin/Staff/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null) return NotFound();

            ViewBag.User = await _context.Users.FirstOrDefaultAsync(u => u.StaffId == id);
            ViewBag.Services = _context.Services.Where(s => s.IsActive).ToList();
            
            return View(staff);
        }

        // POST: Admin/Staff/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaffModel staff, string Role, string? NewPassword)
        {
            if (id != staff.StaffId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(staff);
                    
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.StaffId == id);
                    if (user != null)
                    {
                        user.Role = Role;
                        user.FullName = staff.FullName;
                        if (!string.IsNullOrEmpty(NewPassword))
                        {
                            user.PasswordHash = _passwordHasher.HashPassword(user, NewPassword);
                        }
                        _context.Update(user);
                    }
                    
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StaffExists(staff.StaffId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Services = _context.Services.Where(s => s.IsActive).ToList();
            return View(staff);
        }

        // POST: Admin/Staff/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null) return Json(new { success = false });

            // Xóa user trước (quan hệ 1-1)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.StaffId == id);
            if (user != null) _context.Users.Remove(user);

            _context.Staffs.Remove(staff);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // GET: Admin/Staff/Timesheet/5
        public async Task<IActionResult> Timesheet(int id, int? month, int? year)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null) return NotFound();

            var currentMonth = month ?? DateTime.Today.Month;
            var currentYear = year ?? DateTime.Today.Year;
            var startDate = new DateTime(currentYear, currentMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var attendances = await _context.Attendances
                .Where(a => a.StaffId == id && a.Date >= startDate && a.Date <= endDate)
                .ToListAsync();

            var leaves = await _context.LeaveRequests
                .Where(l => l.StaffId == id && l.Status == LeaveRequestStatus.Approved && l.FromDate <= endDate && l.ToDate >= startDate)
                .ToListAsync();

            var bookings = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Where(b => b.BookingDate >= startDate && b.BookingDate <= endDate && b.Status == BookingStatus.Completed)
                .Where(b => b.BookingDetails.Any(bd => bd.StaffId == id))
                .ToListAsync();

            ViewBag.Staff = staff;
            ViewBag.Month = currentMonth;
            ViewBag.Year = currentYear;
            ViewBag.Attendances = attendances;
            ViewBag.Leaves = leaves;
            ViewBag.BookingsCount = bookings.Count;

            return View();
        }

        // GET: Admin/Staff/AttendanceQR
        public IActionResult AttendanceQR(string role = "ktv")
        {
            ViewBag.Role = role.ToLower();
            return View();
        }

        private bool StaffExists(int id)
        {
            return _context.Staffs.Any(e => e.StaffId == id);
        }
    }
}
