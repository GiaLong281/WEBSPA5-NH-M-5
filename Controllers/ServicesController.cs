using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    public class ServicesController : Controller
    {
        private readonly SpaDbContext _context;

        public ServicesController(SpaDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách dịch vụ (gọi API từ client)
        public IActionResult Index()
        {
            return View();
        }

        // Chi tiết dịch vụ
        public async Task<IActionResult> Details(int id)
        {
            var service = await _context.Services
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null)
                return NotFound();

            return View(service);
        }
    }
}