using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Customer")]
    public class BookingController : Controller
    {
        public IActionResult MyBookings()
        {
            return View();
        }
    }
}