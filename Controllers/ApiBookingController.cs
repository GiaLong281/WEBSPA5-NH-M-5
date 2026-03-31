using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiBookingController : ControllerBase
    {
        private readonly SpaDbContext _context;

        public ApiBookingController(SpaDbContext context)
        {
            _context = context;
        }

        [HttpGet("GetAvailableStaff")]
        public async Task<IActionResult> GetAvailableStaff(
            int branchId,
            DateTime date,
            TimeSpan time,
            int serviceId)
        {
            var service = await _context.Services.FindAsync(serviceId);

            if (service == null || !service.IsActive)
                return BadRequest("Dịch vụ không hợp lệ");

            var startDateTime = date.Date.Add(time);
            var endDateTime = startDateTime.AddMinutes(service.Duration);

            // giới hạn giờ làm
            if (time < new TimeSpan(8, 0, 0) || time > new TimeSpan(20, 0, 0))
                return Ok(new List<object>());

            var staffs = await _context.Staffs
                .Where(s => s.BranchId == branchId && s.Status == "active")
                .ToListAsync();

            var available = new List<object>();

            foreach (var staff in staffs)
            {
                var conflict = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Where(bd =>
                        bd.StaffId == staff.StaffId &&
                        bd.Booking.BookingDate.Date == date.Date &&
                        bd.Booking.Status != BookingStatus.Cancelled)
                    .AnyAsync(bd =>
                        bd.Booking.StartTime < endDateTime &&
                        bd.Booking.EndTime > startDateTime);

                if (!conflict)
                {
                    available.Add(new
                    {
                        staff.StaffId,
                        staff.FullName,
                        staff.Position,
                        staff.Avatar
                    });
                }
            }

            return Ok(available);
        }
    }
}