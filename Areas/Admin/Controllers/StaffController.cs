using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public StaffController(SpaDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
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

        private bool StaffExists(int id)
        {
            return _context.Staffs.Any(e => e.StaffId == id);
        }
    }
}
