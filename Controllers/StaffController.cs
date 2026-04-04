using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System.Linq;

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

        // GET: Staff/Calendar
        public async Task<IActionResult> Calendar(DateTime? date)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            ViewBag.SelectedStaffId = staffId;
            return View("Index");
        }

        // GET: Staff/Attendance
        public async Task<IActionResult> Attendance()
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var todayAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date == today);

            var recentAttendances = await _context.Attendances
                .Where(a => a.StaffId == staffId)
                .OrderByDescending(a => a.Date)
                .Take(30)
                .ToListAsync();

            ViewBag.TodayAttendance = todayAttendance;
            ViewBag.RecentAttendances = recentAttendances;
            return View();
        }

        // GET: Staff/Income
        public async Task<IActionResult> Income(int? month, int? year)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var completedBookings = await _context.BookingDetails
                .Include(bd => bd.Booking)
                .ThenInclude(b => b.Customer)
                .Include(bd => bd.Service)
                .Where(bd => bd.StaffId == staffId &&
                             bd.Booking.Status == BookingStatus.Completed &&
                             bd.Booking.EndTime >= startDate &&
                             bd.Booking.EndTime <= endDate)
                .ToListAsync();

            var commissionRateSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "StaffCommissionRate");
            var commissionRate = commissionRateSetting != null && decimal.TryParse(commissionRateSetting.Value, out var rate)
                ? rate : 0.3m;

            var baseSalarySetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "StaffBaseSalary");
            var baseSalary = baseSalarySetting != null && decimal.TryParse(baseSalarySetting.Value, out var salary)
                ? salary : 0m;

            var totalRevenue = completedBookings.Sum(bd => bd.PriceAtTime);
            var totalCommission = totalRevenue * commissionRate;
            var totalIncome = totalCommission + baseSalary;

            var dailyIncome = completedBookings
                .GroupBy(bd => bd.Booking.EndTime.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(b => b.PriceAtTime), Commission = g.Sum(b => b.PriceAtTime) * commissionRate })
                .OrderBy(x => x.Date)
                .ToList();

            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.CommissionRate = commissionRate;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCommission = totalCommission;
            ViewBag.TotalIncome = totalIncome;
            ViewBag.BaseSalary = baseSalary;
            ViewBag.BookingCount = completedBookings.Count;
            ViewBag.DailyIncome = dailyIncome;

            var months = Enumerable.Range(1, 12).Select(m => new { Value = m, Name = $"Tháng {m}" }).ToList();
            ViewBag.Months = months;

            return View(completedBookings);
        }

        // POST: Staff/CheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(string? note)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date == today);

            if (existing != null && existing.CheckInTime.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn đã check-in hôm nay rồi.";
                return RedirectToAction(nameof(Attendance));
            }

            if (existing == null)
            {
                existing = new Attendance
                {
                    StaffId = staffId.Value,
                    Date = today,
                    CheckInTime = DateTime.Now,
                    Note = note
                };
                _context.Attendances.Add(existing);
            }
            else
            {
                existing.CheckInTime = DateTime.Now;
                if (!string.IsNullOrEmpty(note)) existing.Note = note;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Check-in thành công lúc {DateTime.Now:HH:mm:ss}";
            return RedirectToAction(nameof(Attendance));
        }

        // POST: Staff/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(string? note)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date == today);

            if (attendance == null || !attendance.CheckInTime.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn chưa check-in hôm nay.";
                return RedirectToAction(nameof(Attendance));
            }

            if (attendance.CheckOutTime.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn đã check-out rồi.";
                return RedirectToAction(nameof(Attendance));
            }

            attendance.CheckOutTime = DateTime.Now;
            if (!string.IsNullOrEmpty(note)) attendance.Note = note;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Check-out thành công lúc {DateTime.Now:HH:mm:ss}";
            return RedirectToAction(nameof(Attendance));
        }

        // GET: Staff/AddNote/5
        public async Task<IActionResult> AddNote(int bookingId)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            ViewBag.Booking = booking;
            return View();
        }

        // POST: Staff/AddNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int bookingId, string note)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            var customerNote = new CustomerNote
            {
                CustomerId = booking.CustomerId,
                BookingId = bookingId,
                StaffId = staffId.Value,
                Note = note,
                CreatedAt = DateTime.Now
            };

            _context.CustomerNotes.Add(customerNote);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã lưu ghi chú khách hàng.";
            return RedirectToAction(nameof(Details), new { id = bookingId });
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

        // Bắt đầu thực hiện (Confirmed -> InProgress)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartBooking(int bookingId)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId &&
                                          b.BookingDetails.Any(bd => bd.StaffId == staffId));
            if (booking == null) return NotFound();

            if (booking.Status != BookingStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "Chỉ có thể bắt đầu lịch đã xác nhận.";
                return RedirectToAction(nameof(Dashboard));
            }

            booking.Status = BookingStatus.InProgress;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Start", "Booking", booking.BookingId.ToString(), null,
                System.Text.Json.JsonSerializer.Serialize(new { Status = "InProgress" }));

            TempData["SuccessMessage"] = $"Đã bắt đầu thực hiện lịch {booking.BookingCode}.";
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


        // GET: Staff/MyBookings
        public async Task<IActionResult> MyBookings(string? status = null, int page = 1, int pageSize = 10)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var query = _context.BookingDetails
                .Include(bd => bd.Booking)
                    .ThenInclude(b => b.Customer)
                .Include(bd => bd.Booking)
                    .ThenInclude(b => b.Branch)
                .Include(bd => bd.Service)
                .Where(bd => bd.StaffId == staffId);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
            {
                query = query.Where(bd => bd.Booking.Status == bookingStatus);
            }

            var totalItems = await query.CountAsync();
            var bookings = await query
                .OrderByDescending(bd => bd.Booking.BookingDate)
                .ThenByDescending(bd => bd.Booking.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(bd => new
                {
                    Booking = bd.Booking,
                    ServiceName = bd.Service.ServiceName,
                    CustomerName = bd.Booking.Customer != null ? bd.Booking.Customer.FullName : "",
                    BranchName = bd.Booking.Branch != null ? bd.Booking.Branch.BranchName : ""
                })
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            return View(bookings);
        }


        // GET: Staff/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var staffId = GetStaffId();
            if (staffId == null) return RedirectToAction("Login", "Account");

            var bookingDetail = await _context.BookingDetails
                .Include(bd => bd.Booking)
                    .ThenInclude(b => b.Customer)
                .Include(bd => bd.Booking)
                    .ThenInclude(b => b.Branch)
                .Include(bd => bd.Service)
                .Include(bd => bd.Staff)
                .FirstOrDefaultAsync(bd => bd.BookingId == id && bd.StaffId == staffId);

            if (bookingDetail == null) return NotFound();

            return View(bookingDetail.Booking);
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