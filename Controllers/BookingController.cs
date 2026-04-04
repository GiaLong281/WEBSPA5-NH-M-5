using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Models.ViewModels;
using System.Linq;

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
            var model = new BookingViewModel
            {
                BookingDate = DateTime.Today,
                StartTime = new TimeSpan(9, 0, 0),
                AutoAssignStaff = true
            };

            // Nếu đã đăng nhập, lấy thông tin từ Customer
            if (User.Identity?.IsAuthenticated == true)
            {
                var customerId = GetCustomerId();
                if (customerId.HasValue)
                {
                    var customer = await _context.Customers.FindAsync(customerId.Value);
                    if (customer != null)
                    {
                        model.FullName = customer.FullName;
                        model.Email = customer.Email ?? "";
                        model.Phone = customer.Phone;
                    }
                }
            }

            if (serviceId.HasValue)
            {
                var service = await _context.Services.FindAsync(serviceId.Value);
                if (service != null && service.IsActive)
                {
                    model.SelectedServiceIds.Add(service.ServiceId);
                    model.SelectedServiceId = service.ServiceId; // tương thích cũ
                    model.ServiceName = service.ServiceName;
                    model.Duration = service.Duration;
                    model.Price = service.Price;
                }
            }

            ViewBag.AllServices = await _context.Services
                .Where(s => s.IsActive)
                .Select(s => new ServiceInfo
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Duration = s.Duration,
                    Price = s.Price
                }).ToListAsync();

            ViewBag.Services = await BuildServiceSelectList(model.SelectedServiceId);

            ViewBag.Branches = new SelectList(
                await _context.Branches.Where(b => b.IsActive).ToListAsync(),
                "BranchId", "BranchName");

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
            if (customerId == null)
            {
                // Nếu chưa đăng nhập, có thể tạo guest booking (tuỳ chọn)
                // Ở đây vì controller có [Authorize] nên sẽ không vào đây, giữ nguyên để an toàn
                ModelState.AddModelError("", "Vui lòng đăng nhập để đặt lịch.");
                await LoadDropdowns(model);
                return View(model);
            }

            // 1. Lấy danh sách dịch vụ đã chọn
            var selectedServices = await _context.Services
                .Where(s => model.SelectedServiceIds.Contains(s.ServiceId) && s.IsActive)
                .ToListAsync();

            if (!selectedServices.Any())
            {
                ModelState.AddModelError("SelectedServiceIds", "Vui lòng chọn ít nhất một dịch vụ.");
                await LoadDropdowns(model);
                return View(model);
            }

            var totalDuration = selectedServices.Sum(s => s.Duration);
            var totalPrice = selectedServices.Sum(s => s.Price);

            var branch = await _context.Branches.FindAsync(model.BranchId);
            if (branch == null || !branch.IsActive)
            {
                ModelState.AddModelError("BranchId", "Chi nhánh không hợp lệ");
                await LoadDropdowns(model);
                return View(model);
            }

            // Kiểm tra ngày mở cửa
            if (!IsBranchOpenOnDate(branch, model.BookingDate))
            {
                ModelState.AddModelError("BookingDate", $"Chi nhánh không hoạt động vào ngày {model.BookingDate:dd/MM/yyyy}.");
                await LoadDropdowns(model);
                return View(model);
            }

            var startDateTime = model.BookingDate.Date.Add(model.StartTime);
            var endDateTime = startDateTime.AddMinutes(totalDuration);
            model.EndTime = endDateTime;
            model.Duration = totalDuration;
            model.Price = totalPrice;

            var openTime = branch.OpeningTime;
            var closeTime = branch.ClosingTime;
            if (openTime == closeTime || closeTime <= openTime)
            {
                openTime = new TimeSpan(8, 0, 0);
                closeTime = new TimeSpan(20, 0, 0);
            }

            // Kiểm tra giờ trong khung mở cửa
            if (startDateTime.TimeOfDay < openTime || endDateTime.TimeOfDay > closeTime)
            {
                ModelState.AddModelError("StartTime", $"Giờ đặt phải trong khoảng {openTime:hh\\:mm} - {closeTime:hh\\:mm}.");
                await LoadDropdowns(model);
                return View(model);
            }

            // Xử lý nhân viên (Tự động hoặc Thủ công)
            if (model.AutoAssignStaff)
            {
                model.StaffId = await FindAvailableStaff(model.BranchId, model.BookingDate, startDateTime, endDateTime);
                if (model.StaffId == null)
                {
                    ModelState.AddModelError("", "Không tìm thấy nhân viên rảnh trong thời gian này.");
                    await LoadDropdowns(model);
                    return View(model);
                }
            }
            else
            {
                if (!model.StaffId.HasValue || model.StaffId <= 0)
                {
                    ModelState.AddModelError("StaffId", "Vui lòng chọn nhân viên");
                    await LoadDropdowns(model);
                    return View(model);
                }

                if (!await IsStaffAvailable(model.StaffId.Value, model.BookingDate, startDateTime, endDateTime))
                {
                    ModelState.AddModelError("StaffId", "Nhân viên bạn chọn đã có lịch trùng trong thời gian này.");
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
                TotalAmount = totalPrice,
                Notes = model.Notes,
                CreatedAt = DateTime.Now
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            foreach (var service in selectedServices)
            {
                var bookingDetail = new BookingDetail
                {
                    BookingId = booking.BookingId,
                    ServiceId = service.ServiceId,
                    StaffId = model.StaffId.Value,
                    PriceAtTime = service.Price
                };
                _context.BookingDetails.Add(bookingDetail);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt lịch thành công! Mã đơn hàng: {bookingCode}";
            return RedirectToAction(nameof(Upcoming));
        }

        // Helper kiểm tra ngày mở cửa
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
            
            if (!isMatch && !(workday.Contains("thứ") || workday.Contains("t2") || workday.Contains("t3") || workday.Contains("cn") || workday.Contains("day")))
            {
                return true;
            }
            
            return isMatch;
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
            ViewBag.Services = await BuildServiceSelectList(model.SelectedServiceId);

            ViewBag.AllServices = await _context.Services
                .Where(s => s.IsActive)
                .Select(s => new ServiceInfo
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Duration = s.Duration,
                    Price = s.Price
                }).ToListAsync();

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

        private async Task<List<SelectListItem>> BuildServiceSelectList(int? selectedId = null)
        {
            var services = await _context.Services
                .Include(s => s.Category)
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category.Name)
                .ThenBy(s => s.ServiceName)
                .ToListAsync();

            var items = new List<SelectListItem>();
            foreach (var s in services)
            {
                items.Add(new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = $"{s.ServiceName} ({s.Duration} phút — {s.Price:N0}đ)",
                    Group = new SelectListGroup { Name = s.Category?.Name ?? "Khác" },
                    Selected = selectedId.HasValue && selectedId.Value == s.ServiceId
                });
            }
            return items;
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