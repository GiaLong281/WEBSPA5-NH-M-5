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
            // Nếu là Staff, chỉ được xem lịch của chính mình
            if (User.IsInRole("Staff"))
            {
                var currentStaffId = GetCurrentStaffId();
                if (!currentStaffId.HasValue)
                {
                    return RedirectToAction("Login", "Account");
                }
                ViewBag.SelectedStaffId = currentStaffId;
                ViewBag.Staffs = null;
            }
            else // Admin
            {
                // Lấy danh sách tất cả nhân viên (kèm chi nhánh)
                var staffs = await _context.Staffs
                    .Include(s => s.Branch)
                    .Select(s => new 
                    { 
                        s.StaffId, 
                        s.FullName, 
                        BranchName = s.Branch != null ? s.Branch.BranchName : "Chưa có chi nhánh" 
                    })
                    .ToListAsync();
                
                ViewBag.Staffs = staffs;
                
                // Nếu có staffId được chọn thì dùng, không thì lấy nhân viên đầu tiên
                if (staffId.HasValue && staffs.Any(s => s.StaffId == staffId.Value))
                {
                    ViewBag.SelectedStaffId = staffId.Value;
                }
                else if (staffs.Any())
                {
                    ViewBag.SelectedStaffId = staffs.First().StaffId;
                }
                else
                {
                    ViewBag.SelectedStaffId = null;
                }
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
