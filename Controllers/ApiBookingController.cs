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

        [HttpGet("GetTimeSlots")]
        public async Task<IActionResult> GetTimeSlots(int branchId, DateTime date, int serviceId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) return BadRequest("Chi nhánh không tồn tại");

            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return BadRequest("Dịch vụ không hợp lệ");

            // Kiểm tra ngày mở cửa
            if (!IsBranchOpenOnDate(branch, date))
                return Ok(new List<object>());

            var openTime = branch.OpeningTime;
            var closeTime = branch.ClosingTime;
            
            // Fix: If times are invalid or 00:00-00:00 (default), fallback to 08:00 - 20:00
            if (openTime == closeTime || closeTime <= openTime)
            {
                openTime = new TimeSpan(8, 0, 0);
                closeTime = new TimeSpan(20, 0, 0);
            }

            var step = TimeSpan.FromMinutes(30);
            var slots = new List<object>();

            for (var t = openTime; t < closeTime; t = t.Add(step))
            {
                var start = t;
                var end = start.Add(TimeSpan.FromMinutes(service.Duration));
                if (end > closeTime) continue;

                var currentStartDateTime = date.Date.Add(start);
                var currentEndDateTime = date.Date.Add(end);

                // Kiểm tra xung đột với booking đã có (giải quyết lỗi SQLite TimeOfDay translation)
                var conflict = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Where(bd => bd.Booking.BranchId == branchId &&
                                 bd.Booking.BookingDate.Date == date.Date &&
                                 bd.Booking.Status != BookingStatus.Cancelled)
                    .AnyAsync(bd =>
                        (bd.Booking.StartTime < currentEndDateTime && bd.Booking.EndTime > currentStartDateTime));

                if (!conflict)
                {
                    slots.Add(new
                    {
                        Value = start,
                        Time = start.ToString(@"hh\:mm")
                    });
                }
            }

            return Ok(slots);
        }

        private bool IsBranchOpenOnDate(Branch branch, DateTime date)
        {
            if (string.IsNullOrEmpty(branch.Workday))
                return true;
            
            var workday = branch.Workday.ToLower();
            if (workday.Contains("cả tuần") || workday.Contains("all") || workday.Contains("mọi ngày") || workday.Contains("hàng ngày"))
                return true;

            var dayOfWeek = date.DayOfWeek;
            var isMatch = dayOfWeek switch
            {
                DayOfWeek.Monday => workday.Contains("thứ 2") || workday.Contains("t2") || workday.Contains("monday"),
                DayOfWeek.Tuesday => workday.Contains("thứ 3") || workday.Contains("t3") || workday.Contains("tuesday"),
                DayOfWeek.Wednesday => workday.Contains("thứ 4") || workday.Contains("t4") || workday.Contains("wednesday"),
                DayOfWeek.Thursday => workday.Contains("thứ 5") || workday.Contains("t5") || workday.Contains("thursday"),
                DayOfWeek.Friday => workday.Contains("thứ 6") || workday.Contains("t6") || workday.Contains("friday"),
                DayOfWeek.Saturday => workday.Contains("thứ 7") || workday.Contains("t7") || workday.Contains("saturday"),
                DayOfWeek.Sunday => workday.Contains("chủ nhật") || workday.Contains("cn") || workday.Contains("sunday"),
                _ => true
            };
            
            // Nếu không khớp ngày nào nhưng nội dung cũng không chứa từ khóa chỉ định ngày (nghĩa là free-text), ta cứ cho là mở cửa
            if (!isMatch && !(workday.Contains("thứ") || workday.Contains("t2") || workday.Contains("t3") || workday.Contains("cn") || workday.Contains("day")))
            {
                return true;
            }
            
            return isMatch;
        }
    }
}