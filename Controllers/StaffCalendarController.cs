using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using System.Linq;
using System.Security.Claims;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class StaffCalendarController : Controller
    {
        private readonly SpaDbContext _context;

        public StaffCalendarController(SpaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? staffId)
        {
            // Nếu là Admin, có thể chọn staff; nếu là Staff thì lấy chính mình
            if (User.IsInRole("Admin"))
            {
                var staffs = await _context.Staffs
                    .Include(s => s.Branch)
                    .Select(s => new { s.StaffId, s.FullName, BranchName = s.Branch != null ? s.Branch.BranchName : "" })
                    .ToListAsync();
                
                // Chuyển sang anonymous object hoặc ViewModel cụ thể để dùng trong View
                ViewBag.Staffs = staffs;
                ViewBag.SelectedStaffId = staffId;
            }
            else
            {
                var currentStaffId = GetCurrentStaffId();
                ViewBag.SelectedStaffId = currentStaffId;
                ViewBag.Staffs = null; // không cần dropdown
            }

            return View();
        }

        private int? GetCurrentStaffId()
        {
            var claim = User.FindFirst("StaffId");
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            return null;
        }
    }
}
