using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly AuditService _audit;

        public StaffController(SpaDbContext context, IConfiguration configuration, AuditService audit)
        {
            _context = context;
            _configuration = configuration;
            _audit = audit;
        }

        // Dashboard hiển thị booking trong ngày và sắp tới
        public async Task<IActionResult> Dashboard()
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var staff = await _context.Staffs
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);
            if (staff == null) return NotFound();

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Lấy các booking của chi nhánh, có phân công cho nhân viên này
            var branchBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.BranchId == staff.BranchId &&
                            b.BookingDetails.Any(bd => bd.StaffId == staffId))
                .ToListAsync();

            var todayBookings = branchBookings
                .Where(b => b.BookingDate.Date == today)
                .OrderBy(b => b.StartTime)
                .ToList();

            var upcomingBookings = branchBookings
                .Where(b => b.BookingDate.Date > today ||
                            (b.BookingDate.Date == today && b.StartTime > now))
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .ToList();

            ViewBag.Staff = staff;
            ViewBag.Today = todayBookings;
            ViewBag.Upcoming = upcomingBookings;
            ViewBag.Now = now;

            return View();
        }

        // Xác nhận lịch (Pending -> Confirmed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBooking(int bookingId)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "Chỉ có thể xác nhận lịch đang chờ.";
                return RedirectToAction(nameof(Dashboard));
            }

            booking.Status = BookingStatus.Confirmed;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Confirm", "Booking", booking.BookingId.ToString(), null,
                System.Text.Json.JsonSerializer.Serialize(new { Status = "Confirmed" }));

            TempData["SuccessMessage"] = $"Đã xác nhận lịch {booking.BookingCode}.";
            return RedirectToAction(nameof(Dashboard));
        }

        // Hoàn thành lịch (Confirmed -> Completed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteBooking(int bookingId)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            if (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.InProgress)
            {
                TempData["ErrorMessage"] = "Chỉ có thể hoàn thành lịch đã xác nhận.";
                return RedirectToAction(nameof(Dashboard));
            }

            booking.Status = BookingStatus.Completed;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Complete", "Booking", booking.BookingId.ToString(), null,
                System.Text.Json.JsonSerializer.Serialize(new { Status = "Completed" }));

            TempData["SuccessMessage"] = $"Đã hoàn thành lịch {booking.BookingCode}.";
            return RedirectToAction(nameof(Dashboard));
        }

        // Hủy lịch (bất kỳ trạng thái chưa hoàn thành)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId, string? reason)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch đã hoàn thành hoặc đã hủy.";
                return RedirectToAction(nameof(Dashboard));
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.Now;
            booking.CancelReason = reason;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Cancel", "Booking", booking.BookingId.ToString(), null,
                System.Text.Json.JsonSerializer.Serialize(new { Status = "Cancelled", Reason = reason }));

            TempData["SuccessMessage"] = $"Đã hủy lịch {booking.BookingCode}.";
            return RedirectToAction(nameof(Dashboard));
        }

        private int? GetStaffId()
        {
            var claim = User.FindFirst("StaffId");
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            return null;
        }
    }
}