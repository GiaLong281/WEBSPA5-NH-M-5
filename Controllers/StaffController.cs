using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly SpaDbContext _context;

        public StaffController(SpaDbContext context) => _context = context;

public async Task<IActionResult> Dashboard()
{
    var staffIdClaim = User.FindFirst("StaffId")?.Value;
    if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out var staffId))
        return NotFound("Không tìm thấy thông tin nhân viên");

    var staff = await _context.Staffs.Include(s => s.Branch).FirstOrDefaultAsync(s => s.StaffId == staffId);
    if (staff == null) return NotFound();

    var bookings = await _context.Bookings
        .Include(b => b.Customer)
        .Include(b => b.BookingDetails)
        .Where(b => b.BranchId == staff.BranchId 
                    && b.BookingDate.Date == DateTime.Today
                    && b.BookingDetails.Any(bd => bd.StaffId == staffId))
        .OrderBy(b => b.StartTime)
        .ToListAsync();

    ViewBag.Staff = staff;
    return View(bookings);
}
    }
}