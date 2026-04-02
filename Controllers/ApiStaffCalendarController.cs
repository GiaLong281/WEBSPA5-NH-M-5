using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using System.Linq;
using System.Security.Claims;

namespace SpaN5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Staff,Admin")]
    public class ApiStaffCalendarController : ControllerBase
    {
        private readonly SpaDbContext _context;

        public ApiStaffCalendarController(SpaDbContext context)
        {
            _context = context;
        }

        // GET: api/ApiStaffCalendar/GetBookings?staffId=1&start=2025-04-01&end=2025-04-30
        [HttpGet("GetBookings")]
        public async Task<IActionResult> GetBookings(int? staffId, DateTime start, DateTime end)
        {
            // Nếu là Staff, chỉ được xem lịch của chính mình
            var currentStaffId = GetCurrentStaffId();
            
            // Nếu target staffId không được gửi lên, mặc định lấy của chính mình
            int targetStaffId = staffId ?? currentStaffId ?? 0;
            
            if (User.IsInRole("Staff") && targetStaffId != currentStaffId)
                return Forbid();

            if (targetStaffId == 0)
                return BadRequest("Không xác định được nhân viên.");

            var bookingsQuery = _context.BookingDetails
                .Include(bd => bd.Booking)
                    .ThenInclude(b => b.Customer)
                .Include(bd => bd.Service)
                .Where(bd => bd.StaffId == targetStaffId &&
                             bd.Booking.Status != BookingStatus.Cancelled &&
                             bd.Booking.StartTime >= start &&
                             bd.Booking.EndTime <= end);

            var bookings = await bookingsQuery
                .Select(bd => new
                {
                    bd.Booking.BookingId,
                    bd.Booking.BookingCode,
                    CustomerName = bd.Booking.Customer != null ? bd.Booking.Customer.FullName : "Khách vãng lai",
                    ServiceName = bd.Service != null ? bd.Service.ServiceName : "Dịch vụ",
                    bd.Booking.StartTime,
                    bd.Booking.EndTime,
                    Status = bd.Booking.Status.ToString()
                })
                .ToListAsync();

            var events = bookings.Select(b => new
            {
                id = b.BookingId,
                title = $"{b.CustomerName} - {b.ServiceName}",
                start = b.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = b.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                extendedProps = new
                {
                    bookingCode = b.BookingCode,
                    status = b.Status,
                    customer = b.CustomerName,
                    service = b.ServiceName
                },
                color = GetStatusColor(b.Status)
            });

            return Ok(events);
        }

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Pending" => "#ffc107",    // vàng
                "Confirmed" => "#17a2b8", // xanh dương nhạt
                "InProgress" => "#007bff",// xanh dương
                "Completed" => "#28a745", // xanh lá
                "Cancelled" => "#dc3545", // đỏ
                _ => "#6c757d"
            };
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
