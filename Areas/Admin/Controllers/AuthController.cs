using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SpaN5.Models;
using System.Security.Claims;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<User> _passwordHasher;

        public AuthController(SpaDbContext context, Microsoft.AspNetCore.Identity.IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to Dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Therapist") || User.IsInRole("Staff")) return RedirectToAction("Index", "Home", new { area = "Staff" });
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            // Đảm bảo và tạo tài khoản nhanvien (Therapist)
            if (username == "nhanvien" && password == "123456")
            {
                var staffUser = _context.Users.FirstOrDefault(x => x.Username == "nhanvien");
                if (staffUser == null)
                {
                    var newStaff = new SpaN5.Models.Staff { FullName = "Kỹ thuật viên Demo", Status = "active", Position = "Therapist" };
                    _context.Staffs.Add(newStaff);
                    _context.SaveChanges();

                    staffUser = new User { Username = "nhanvien", Role = "Therapist", StaffId = newStaff.StaffId, FullName = "Kỹ thuật viên Demo" };
                    _context.Users.Add(staffUser);
                }
                staffUser.PasswordHash = _passwordHasher.HashPassword(staffUser, "123456");
                _context.SaveChanges();
            }

            // Đảm bảo và tạo tài khoản letan (Receptionist)
            if (username == "letan" && password == "123456")
            {
                var repUser = _context.Users.FirstOrDefault(x => x.Username == "letan");
                if (repUser == null)
                {
                    repUser = new User { Username = "letan", Role = "Receptionist", FullName = "Tiếp tân Demo" };
                    _context.Users.Add(repUser);
                }
                repUser.PasswordHash = _passwordHasher.HashPassword(repUser, "123456");
                _context.SaveChanges();
            }

            // Extremely simple authentication logic for demonstration
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user != null)
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
                if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success)
                {
                    var claims = new List<System.Security.Claims.Claim>
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new System.Security.Claims.Claim("UserId", user.Id.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Username),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role)
                    };

                    if (user.StaffId.HasValue)
                    {
                        claims.Add(new System.Security.Claims.Claim("StaffId", user.StaffId.Value.ToString()));
                    }

                    var identity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new System.Security.Claims.ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                    {
                        IsPersistent = false, // Sửa thành false: Tự động đăng xuất khi tắt trình duyệt
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    });

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    
                    if (user.Role == "Therapist" || user.Role == "Staff")
                    {
                        return RedirectToAction("Index", "Home", new { area = "Staff" });
                    }

                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }
            }

            ViewBag.Error = "Tài khoản hoặc mật khẩu không chính xác.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
