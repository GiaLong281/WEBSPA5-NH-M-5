using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SpaDbContext _context;

        public HomeController(ILogger<HomeController> logger, SpaDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var service = await _context.Services
                .Include(s => s.ServiceMaterials)
                .ThenInclude(sm => sm.Material)
                .FirstOrDefaultAsync(m => m.ServiceId == id && m.IsActive);

            if (service == null)
            {
                return NotFound("Không tìm thấy dịch vụ hoặc dịch vụ không còn hoạt động.");
            }

            return View(service);
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Contact(ContactViewModel model)
        {
            if (ModelState.IsValid)
            {
                TempData["SuccessMessage"] = "Cảm ơn quý khách đã liên hệ. Chúng tôi sẽ phản hồi sớm nhất!";
                return RedirectToAction("Contact");
            }
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                Response.Cookies.Append(
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                    Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
            }

            return LocalRedirect(returnUrl ?? Url.Action("Index", "Home"));
        }

        public IActionResult Terms()
        {
            return View();
        }
    }
}
