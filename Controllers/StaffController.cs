using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}