using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Models.ViewModels;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Customer")]
    public class BookingController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IConfiguration _configuration;

        public BookingController(SpaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: /Booking/Create?serviceId=...
        [HttpGet]
        public async Task<IActionResult> Create(int? serviceId)
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var model = new BookingViewModel
            {
                BookingDate = DateTime.Today,
                StartTime = new TimeSpan(9, 0, 0),
                AutoAssignStaff = true
            };

            if (serviceId.HasValue)
            {
                var service = await _context.Services.FindAsync(serviceId.Value);
                if (service != null && service.IsActive)
                {
                    model.SelectedServiceId = service.ServiceId;
                    model.ServiceName = service.ServiceName;
                    model.Duration = service.Duration;
                    model.Price = service.Price;
                }
            }

            var branches = await _context.Branches
                .Where(b => b.IsActive)
                .ToListAsync();

            ViewBag.Services = new SelectList(
                await _context.Services.Where(s => s.IsActive).ToListAsync(),
                "ServiceId", "ServiceName", model.SelectedServiceId);

            ViewBag.Branches = new SelectList(
                branches,
                "BranchId", "BranchName");

            if (branches.Any())
            {
                model.BranchId = branches.First().BranchId;

                var staffs = await _context.Staffs
                    .Where(s => s.BranchId == model.BranchId && s.Status == "active")
                    .Select(s => new
                    {
                        s.StaffId,
                        s.FullName,
                        s.Position
                    })
                    .ToListAsync();

                ViewBag.Staffs = staffs;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var service = await _context.Services.FindAsync(model.SelectedServiceId);
            if (service == null || !service.IsActive)
            {
                ModelState.AddModelError("SelectedServiceId", "Dịch vụ không hợp lệ");
                await LoadDropdowns(model);
                return View(model);
            }

            var startDateTime = model.BookingDate.Date.Add(model.StartTime);
            var endDateTime = startDateTime.AddMinutes(service.Duration);
            model.EndTime = endDateTime;

            if (model.AutoAssignStaff)
            {
                model.StaffId = await FindAvailableStaff(
                    model.BranchId,
                    model.BookingDate,
                    startDateTime,
                    endDateTime);

                if (model.StaffId == null)
                {
                    ModelState.AddModelError("", "Không tìm thấy nhân viên rảnh trong thời gian này.");
                    await LoadDropdowns(model);
                    return View(model);
                }
            }
            else
            {
                if (model.StaffId == null)
                {
                    ModelState.AddModelError("StaffId", "Vui lòng chọn nhân viên.");
                    await LoadDropdowns(model);
                    return View(model);
                }

                var available = await IsStaffAvailable(
                    model.StaffId.Value,
                    model.BookingDate,
                    startDateTime,
                    endDateTime);

                if (!available)
                {
                    ModelState.AddModelError("StaffId", "Nhân viên đã có lịch vào thời gian này.");
                    await LoadDropdowns(model);
                    return View(model);
                }
            }

            var bookingCode = $"BK{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";

            var booking = new Booking
            {
                BookingCode = bookingCode,
                CustomerId = customerId.Value,
                BranchId = model.BranchId,
                BookingDate = model.BookingDate,
                StartTime = startDateTime,
                EndTime = endDateTime,
                Status = BookingStatus.Pending,
                TotalAmount = service.Price,
                Notes = model.Notes,
                CreatedAt = DateTime.Now
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            var bookingDetail = new BookingDetail
            {
                BookingId = booking.BookingId,
                ServiceId = service.ServiceId,
                StaffId = model.StaffId.Value,
                PriceAtTime = service.Price
            };

            _context.BookingDetails.Add(bookingDetail);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt lịch thành công! Mã đơn hàng: {bookingCode}";
            return RedirectToAction(nameof(Upcoming));
        }

        public async Task<IActionResult> Upcoming()
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var now = DateTime.Now;
            var today = DateTime.Today;

            var bookings = await _context.Bookings
                .Include(b => b.Branch)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.CustomerId == customerId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.Completed &&
                            (b.BookingDate.Date > today ||
                             (b.BookingDate.Date == today && b.EndTime > now)))
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .ToListAsync();

            ViewBag.Now = now;
            return View(bookings);
        }

        public async Task<IActionResult> MyBookings()
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            ViewBag.CancelHours = _configuration.GetValue<int>("Booking:CancelHoursBefore", 24);

            var bookings = await _context.Bookings
                .Include(b => b.Branch)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.CustomerId == customerId)
                .OrderByDescending(b => b.BookingDate)
                .ThenByDescending(b => b.StartTime)
                .ToListAsync();

            ViewBag.Completed = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();
            ViewBag.Cancelled = bookings.Where(b => b.Status == BookingStatus.Cancelled).ToList();
            ViewBag.All = bookings;

            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.Branch)
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingId == id && b.CustomerId == customerId);

            if (booking == null)
                return NotFound();

            return View(booking);
        }

        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Include(b => b.Branch)
                .FirstOrDefaultAsync(b => b.BookingId == id && b.CustomerId == customerId);

            if (booking == null) return NotFound();

            var cancelHours = _configuration.GetValue<int>("Booking:CancelHoursBefore", 24);
            var cancelDeadline = booking.BookingDate.Date.Add(booking.StartTime.TimeOfDay).AddHours(-cancelHours);

            if (DateTime.Now > cancelDeadline)
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch sau " + cancelDeadline.ToString("dd/MM/yyyy HH:mm");
                return RedirectToAction(nameof(Upcoming));
            }

            if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch này.";
                return RedirectToAction(nameof(Upcoming));
            }

            return View(booking);
        }

        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id, string? reason)
        {
            var customerId = GetCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == id && b.CustomerId == customerId);

            if (booking == null) return NotFound();

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.Now;
            booking.CancelReason = reason;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Lịch hẹn đã được hủy.";
            return RedirectToAction(nameof(Upcoming));
        }

        private async Task<int?> FindAvailableStaff(int branchId, DateTime date, DateTime startTime, DateTime endTime)
        {
            var staffs = await _context.Staffs
                .Where(s => s.BranchId == branchId && s.Status == "active")
                .Select(s => s.StaffId)
                .ToListAsync();

            foreach (var staffId in staffs)
            {
                if (await IsStaffAvailable(staffId, date, startTime, endTime))
                    return staffId;
            }

            return null;
        }

        private async Task<bool> IsStaffAvailable(int staffId, DateTime date, DateTime startTime, DateTime endTime)
        {
            return !await _context.BookingDetails
                .Include(bd => bd.Booking)
                .Where(bd => bd.StaffId == staffId &&
                             bd.Booking.BookingDate.Date == date.Date &&
                             bd.Booking.Status != BookingStatus.Cancelled)
                .AnyAsync(bd =>
                    bd.Booking.StartTime < endTime &&
                    bd.Booking.EndTime > startTime);
        }

        private async Task LoadDropdowns(BookingViewModel model)
        {
            ViewBag.Services = new SelectList(
                await _context.Services.Where(s => s.IsActive).ToListAsync(),
                "ServiceId", "ServiceName", model.SelectedServiceId);

            ViewBag.Branches = new SelectList(
                await _context.Branches.Where(b => b.IsActive).ToListAsync(),
                "BranchId", "BranchName", model.BranchId);

            if (model.BranchId > 0)
            {
                ViewBag.Staffs = await _context.Staffs
                    .Where(s => s.BranchId == model.BranchId && s.Status == "active")
                    .Select(s => new
                    {
                        s.StaffId,
                        s.FullName,
                        s.Position
                    })
                    .ToListAsync();
            }
        }

        private int? GetCustomerId()
        {
            var claim = User.FindFirst("CustomerId");
            if (claim != null && int.TryParse(claim.Value, out int id))
                return id;

            return null;
        }
    }
}