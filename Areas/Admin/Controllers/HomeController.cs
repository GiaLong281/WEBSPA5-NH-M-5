using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin, Staff")] // Only internal accounts can access this area
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
