using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Models.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;


namespace SpaN5.Controllers
{
    public class AccountController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(SpaDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .Include(u => u.Customer)
                    .Include(u => u.Staff)
                    .FirstOrDefaultAsync(u => u.Username == model.Username);
                if (user != null)
                {
                    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                    if (result == PasswordVerificationResult.Success)
                    {
                        // Tạo claims
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.Username),
                            new Claim(ClaimTypes.Role, user.Role),
                            new Claim("FullName", user.FullName ?? user.Username),
                            new Claim("UserId", user.Id.ToString())
                        };
                        if (user.Customer != null)
                            claims.Add(new Claim("CustomerId", user.Customer.CustomerId.ToString()));
                        if (user.Staff != null)
                            claims.Add(new Claim("StaffId", user.Staff.StaffId.ToString()));

                        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                            new AuthenticationProperties { IsPersistent = model.RememberMe });

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                            return Redirect(returnUrl);
                        return RedirectToAction("Index", "Home");
                    }
                }
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid && model.AcceptTerms)
            {
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng.");
                    return View(model);
                }

                var customer = new Customer
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    CreatedAt = DateTime.Now
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                var user = new User
                {
                    Username = model.Username,
                    Role = "Customer",
                    FullName = model.FullName,
                    Email = model.Email,
                    CustomerId = customer.CustomerId,
                    CreatedAt = DateTime.Now
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("FullName", user.FullName),
                    new Claim("UserId", user.Id.ToString()),
                    new Claim("CustomerId", customer.CustomerId.ToString())
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        [HttpGet]
[Authorize]
public async Task<IActionResult> Profile()
{
    var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
    var user = await _context.Users
        .Include(u => u.Customer)
        .Include(u => u.Staff)
        .FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null) return NotFound();

    var model = new ProfileViewModel
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        FullName = user.FullName ?? "",
        Email = user.Email,
        Phone = user.Role == "Customer" ? user.Customer?.Phone : user.Staff?.Phone
    };
    return View(model);
}

[HttpPost]
[Authorize]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Profile(ProfileViewModel model)
{
    if (!ModelState.IsValid) return View(model);

    var user = await _context.Users.FindAsync(model.Id);
    if (user == null) return NotFound();

    // Cập nhật thông tin chung
    user.FullName = model.FullName;
    user.Email = model.Email;

    if (user.Role == "Customer" && user.CustomerId.HasValue)
    {
        var customer = await _context.Customers.FindAsync(user.CustomerId.Value);
        if (customer != null)
        {
            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Phone = model.Phone;
        }
    }
    else if (user.Role == "Staff" && user.StaffId.HasValue)
    {
        var staff = await _context.Staffs.FindAsync(user.StaffId.Value);
        if (staff != null)
        {
            staff.FullName = model.FullName;
            staff.Email = model.Email;
            staff.Phone = model.Phone;
        }
    }

    await _context.SaveChangesAsync();
    TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
    return RedirectToAction(nameof(Profile));
}

[HttpGet]
[Authorize]
public IActionResult ChangePassword()
{
    return View();
}

[HttpPost]
[Authorize]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
{
    if (!ModelState.IsValid) return View(model);

    var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
    var user = await _context.Users.FindAsync(userId);
    if (user == null) return NotFound();

    var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
    if (result != PasswordVerificationResult.Success)
    {
        ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
        return View(model);
    }

    user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
    return RedirectToAction("Profile");
}

    }
}