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

        // Debug endpoint - shows current user info
        [HttpGet("debug/whoami")]
        public IActionResult WhoAmI()
        {
            var staffId = GetCurrentStaffId();
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            return Ok(new
            {
                username,
                role,
                staffId,
                isAuthenticated = User.Identity?.IsAuthenticated,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
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
                             bd.Booking.StartTime <= end);

            var bookings = await bookingsQuery
                .Select(bd => new
                {
                    bd.Booking.BookingId,
                    bd.Booking.BookingCode,
                    CustomerName = bd.Booking.Customer != null ? bd.Booking.Customer.FullName : "Khách vãng lai",
                    ServiceName = bd.Service != null ? bd.Service.ServiceName : "Dịch vụ",
                    bd.Booking.StartTime,
                    bd.Booking.EndTime,
                    Status = bd.Booking.Status.ToString(),
                    StatusText = GetStatusText(bd.Booking.Status)
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
                    bookingId = b.BookingId,
                    bookingCode = b.BookingCode,
                    status = b.StatusText,
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

        private string GetStatusText(BookingStatus status)
        {
            return status switch
            {
                BookingStatus.Pending => "Chờ xác nhận",
                BookingStatus.Confirmed => "Đã xác nhận",
                BookingStatus.InProgress => "Đang thực hiện",
                BookingStatus.Completed => "Hoàn thành",
                BookingStatus.Cancelled => "Đã hủy",
                _ => "Không xác định"
            };
        }

        private int? GetCurrentStaffId()
        {
            // Try to get from StaffId claim first
            var claim = User.FindFirst("StaffId");
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;
            
            // Fallback: Try to get Staff from User in database
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int uid))
            {
                try
                {
                    var user = _context.Users
                        .Include(u => u.Staff)
                        .FirstOrDefault(u => u.Id == uid);
                    
                    if (user?.Staff != null)
                        return user.Staff.StaffId;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiStaffCalendar] Error getting StaffId from database: {ex.Message}");
                }
            }
            
            return null;
        }
    }
}
