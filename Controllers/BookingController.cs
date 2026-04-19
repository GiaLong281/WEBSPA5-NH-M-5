using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Models.ViewModels;
using System.Linq;

namespace SpaN5.Controllers
{
    public class BookingController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IConfiguration _configuration;

        public BookingController(SpaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ==========================================
        // PHẦN 1: QUY TRÌNH ĐẶT LỊCH (HEAD)
        // ==========================================

        [Authorize(Roles = "Customer,Admin,Staff")]
        [HttpGet]
        public async Task<IActionResult> Create(int? serviceId)
        {
            var model = new BookingViewModel
            {
                BookingDate = DateTime.Today,
                StartTime = new TimeSpan(9, 0, 0),
                AutoAssignStaff = true
            };

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
                    model.SelectedServiceId = service.ServiceId; 
                    model.ServiceName = service.ServiceName;
                    model.Duration = service.Duration;
                    model.Price = service.Price;
                }
            }

            await LoadDropdowns(model);
            return View(model);
        }

        [Authorize(Roles = "Customer,Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            int? customerId;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == model.Phone);
            if (customer == null)
            {
                customer = new Customer 
                { 
                    FullName = model.FullName, 
                    Phone = model.Phone, 
                    Email = model.Email,
                    CreatedAt = DateTime.Now 
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật thông tin mới nhất
                if (!string.IsNullOrEmpty(model.FullName)) customer.FullName = model.FullName;
                if (!string.IsNullOrEmpty(model.Email)) customer.Email = model.Email;
                await _context.SaveChangesAsync();
            }

            customerId = customer.CustomerId;

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

            var startDateTime = model.BookingDate.Date.Add(model.StartTime);
            var endDateTime = startDateTime.AddMinutes(totalDuration);

            // Gán nhân viên
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
            }

            var bookingCode = $"BK{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
            var booking = new Booking
            {
                BookingCode = bookingCode,
                CustomerId = customerId.Value,
                BranchId = model.BranchId,
                BookingDate = model.BookingDate.Date,
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
                _context.BookingDetails.Add(new BookingDetail
                {
                    BookingId = booking.BookingId,
                    ServiceId = service.ServiceId,
                    StaffId = model.StaffId,
                    PriceAtTime = service.Price
                });
            }
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt lịch thành công! Mã đơn hàng: {bookingCode}";
            return RedirectToAction(nameof(Upcoming));
        }

        // ==========================================
        // PHẦN 2: CÁC API & TÍNH NĂNG MỚI (LONG)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index(int? serviceId)
        {
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Services = services;
            ViewBag.Staffs = await _context.Staffs.Where(s => s.Status == "active").ToListAsync();
            
            var model = new BookingViewModel();
            if (serviceId.HasValue) model.ServiceId = serviceId.Value;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceDetails(int id)
        {
            var service = await _context.Services
                .Include(s => s.ServiceMaterials).ThenInclude(sm => sm.Material)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null) return Json(new { success = false, message = "Không tìm thấy" });

            return Json(new { success = true, data = new {
                service.ServiceId, service.ServiceName, service.Description, service.Price,
                service.Duration, service.Image, service.VideoUrl, service.IsVip, service.MaxCapacity,
                Materials = service.ServiceMaterials.Select(sm => new { sm.Material?.MaterialName, sm.Quantity, sm.Material?.Unit }).ToList()
            }});
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(string date, int serviceId)
        {
            var service = await _context.Services.FindAsync(serviceId);
            int baseCapacity = (service?.MaxCapacity ?? 10) <= 0 ? 10 : service.MaxCapacity;

            // Lấy danh sách bảo trì để tính công suất THỰC TẾ
            var maintSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MaintenanceRooms");
            var maintRooms = new List<string>();
            if (maintSetting != null && !string.IsNullOrEmpty(maintSetting.Value))
            {
                try { maintRooms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(maintSetting.Value) ?? new List<string>(); } catch { }
            }
            int maintCount = maintRooms.Count(r => r.StartsWith($"{serviceId}_"));
            int effectiveCapacity = Math.Max(0, baseCapacity - maintCount);

            var standardSlots = new[] { "09:00", "09:30", "10:00", "10:30", "11:00", "11:30", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00", "16:30", "17:00", "17:30", "18:00", "18:30", "19:00" };
            
            var response = new List<object>();
            if (DateTime.TryParse(date, out DateTime selectedDate))
            {
                var now = DateTime.Now;
                var booked = await _context.Bookings
                    .Where(b => b.BookingDate.Date == selectedDate.Date && b.Status != BookingStatus.Cancelled)
                    .Where(b => b.BookingDetails.Any(bd => bd.ServiceId == serviceId))
                    .Select(b => new { b.StartTime, b.EndTime }).ToListAsync();

                foreach (var slot in standardSlots)
                {
                    DateTime slotTime = DateTime.ParseExact($"{date} {slot}", "yyyy-MM-dd HH:mm", null);
                    int current = booked.Count(b => slotTime >= b.StartTime && slotTime < b.EndTime);
                    bool isPast = selectedDate.Date == now.Date && slotTime < now;
                    response.Add(new { 
                        time = slot, 
                        isFull = current >= effectiveCapacity, 
                        isPast = isPast, 
                        remaining = Math.Max(0, effectiveCapacity - current), 
                        total = effectiveCapacity,
                        hasMaintenance = maintCount > 0
                    });
                }
            }
            return Json(new { success = true, times = response });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBooking(BookingViewModel model)
        {
            if (!ModelState.IsValid) {
                var errors = string.Join("<br/>", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == model.Phone);
            if (customer == null)
            {
                customer = new Customer { FullName = model.FullName, Phone = model.Phone, Email = model.Email, CreatedAt = DateTime.Now };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật thông tin mới nhất nếu có thay đổi
                if (!string.IsNullOrEmpty(model.FullName)) customer.FullName = model.FullName;
                if (!string.IsNullOrEmpty(model.Email)) customer.Email = model.Email;
                await _context.SaveChangesAsync();
            }

            var selectedServicesList = new List<Service>();
            if (model.SelectedServiceIds != null && model.SelectedServiceIds.Any()) {
                selectedServicesList = await _context.Services.Where(s => model.SelectedServiceIds.Contains(s.ServiceId)).ToListAsync();
            } else if (model.ServiceId.HasValue) {
                var s = await _context.Services.FindAsync(model.ServiceId.Value);
                if (s != null) selectedServicesList.Add(s);
            }

            if (!selectedServicesList.Any()) return Json(new { success = false, message = "Lỗi dịch vụ" });

            DateTime start = model.BookingDate.Date.Add(model.StartTime);
            int totalDuration = selectedServicesList.Sum(x => x.Duration);
            DateTime end = start.AddMinutes(totalDuration);
            decimal totalAmount = selectedServicesList.Sum(s => s.Price);

            // KIỂM TRA LẠI CÔNG SUẤT PHÒNG TRƯỚC KHI LƯU (Rule 10 phòng)
            var maintSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MaintenanceRooms");
            var maintRooms = new List<string>();
            if (maintSetting != null && !string.IsNullOrEmpty(maintSetting.Value))
            {
                try { maintRooms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(maintSetting.Value) ?? new List<string>(); } catch { }
            }

            foreach (var svc in selectedServicesList)
            {
                int baseCap = (svc.MaxCapacity <= 0) ? 10 : svc.MaxCapacity;
                int maintCount = maintRooms.Count(r => r.StartsWith($"{svc.ServiceId}_"));
                int effCap = Math.Max(0, baseCap - maintCount);

                int currentBooked = await _context.Bookings
                    .Where(b => b.BookingDate.Date == start.Date && b.Status != BookingStatus.Cancelled)
                    .Where(b => b.BookingDetails.Any(bd => bd.ServiceId == svc.ServiceId))
                    .CountAsync(b => start >= b.StartTime && start < b.EndTime);

                if (currentBooked >= effCap)
                {
                    return Json(new { success = false, message = $"Rất tiếc, dịch vụ {svc.ServiceName} vào khung giờ này đã vừa đủ chỗ (do giới hạn phòng hoạt động). Vui lòng chọn giờ khác." });
                }
            }

            int? finalStaffId = null;
            if (model.AutoAssignStaff)
            {
                // Simple auto-assign: first active staff
                var firstActiveStaff = await _context.Staffs.FirstOrDefaultAsync(s => s.Status == "active");
                if (firstActiveStaff != null) finalStaffId = firstActiveStaff.StaffId;
            }
            else
            {
                finalStaffId = model.StaffId;
            }

            var bookingDetails = new List<BookingDetail>();
            foreach (var svc in selectedServicesList)
            {
                bookingDetails.Add(new BookingDetail {
                    ServiceId = svc.ServiceId,
                    PriceAtTime = svc.Price,
                    StaffId = finalStaffId,
                    RoomNumber = model.RoomNumber,
                    Status = DetailStatus.Pending
                });
            }

            var booking = new Booking {
                BookingCode = "BK" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                CustomerId = customer.CustomerId, BranchId = 1, BookingDate = start.Date,
                StartTime = start, EndTime = end, Status = BookingStatus.Pending,
                TotalAmount = totalAmount, Notes = model.Notes, CreatedAt = DateTime.Now,
                BookingDetails = bookingDetails
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            return Json(new { success = true, redirectUrl = Url.Action("Success", new { bookingCode = booking.BookingCode }) });
        }

        // ==========================================
        // PHẦN 3: QUẢN LÝ LỊCH HẸN (MY BOOKINGS)
        // ==========================================

        public async Task<IActionResult> Upcoming()
        {
            var id = GetCustomerId();
            if (id == null) return RedirectToAction("Login", "Account");
            var now = DateTime.Now;
            var bookings = await _context.Bookings.Include(b => b.Branch).Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Where(b => b.CustomerId == id && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Completed && (b.BookingDate > now.Date || (b.BookingDate == now.Date && b.EndTime > now)))
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime).ToListAsync();
            return View(bookings);
        }

        public async Task<IActionResult> MyBookings()
        {
            var id = GetCustomerId();
            if (id == null) return RedirectToAction("Login", "Account");
            var items = await _context.Bookings.Include(b => b.Branch).Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Where(b => b.CustomerId == id).OrderByDescending(b => b.BookingDate).ToListAsync();
            ViewBag.Completed = items.Where(x => x.Status == BookingStatus.Completed).ToList();
            ViewBag.Cancelled = items.Where(x => x.Status == BookingStatus.Cancelled).ToList();
            return View();
        }

        public async Task<IActionResult> Details(string bookingCode)
        {
            var b = await _context.Bookings.Include(x => x.Branch).Include(x => x.Customer).Include(x => x.BookingDetails).ThenInclude(d => d.Service).Include(x => x.Payments)
                .FirstOrDefaultAsync(x => x.BookingCode == bookingCode);
            return b == null ? NotFound() : View(b);
        }

        public async Task<IActionResult> Success(string bookingCode)
        {
            var b = await _context.Bookings.Include(x => x.Customer).Include(x => x.Payments).Include(x => x.BookingDetails).ThenInclude(d => d.Service)
                .FirstOrDefaultAsync(x => x.BookingCode == bookingCode);
            return b == null ? NotFound() : View(b);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string bookingCode, string? reason)
        {
            var b = await _context.Bookings.FirstOrDefaultAsync(x => x.BookingCode == bookingCode);
            if (b == null) return NotFound();
            b.Status = BookingStatus.Cancelled; b.CancelledAt = DateTime.Now; b.CancelReason = reason;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { bookingCode });
        }

        // ==========================================
        // PHẦN 4: TIỆN ÍCH & THANH TOÁN
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(string bookingCode, PaymentMethod method)
        {
            var b = await _context.Bookings.FirstOrDefaultAsync(x => x.BookingCode == bookingCode);
            if (b == null) return NotFound();
            _context.Payments.Add(new Payment { BookingId = b.BookingId, Amount = b.TotalAmount, Method = method, Status = PaymentStatus.Paid, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            return RedirectToAction("Success", new { bookingCode });
        }

        public async Task<IActionResult> ResetData()
        {
            _context.BookingDetails.RemoveRange(await _context.BookingDetails.ToListAsync());
            _context.Payments.RemoveRange(await _context.Payments.ToListAsync());
            _context.Bookings.RemoveRange(await _context.Bookings.ToListAsync());
            foreach(var s in await _context.Services.ToListAsync()) s.MaxCapacity = 10;
            await _context.SaveChangesAsync();
            return Content("Đã Reset dữ liệu thành công.");
        }

        // ==========================================
        // HELPERS
        // ==========================================

        private async Task LoadDropdowns(BookingViewModel model)
        {
            var srv = await _context.Services.Include(s => s.Category).Where(s => s.IsActive).ToListAsync();
            ViewBag.Services = srv; // Dùng cho Index/Create Luxury UI
            ViewBag.AllServices = srv.Select(s => new ServiceInfo 
            { 
                ServiceId = s.ServiceId, 
                ServiceName = s.ServiceName, 
                Duration = s.Duration, 
                Price = s.Price 
            }).ToList();
            
            ViewBag.Branches = new SelectList(await _context.Branches.Where(b => b.IsActive).ToListAsync(), "BranchId", "BranchName", model.BranchId);
        }

        private async Task<List<SelectListItem>> BuildServiceSelectList(int? selectedId = null)
        {
            var srv = await _context.Services.Include(s => s.Category).Where(s => s.IsActive).ToListAsync();
            return srv.Select(s => new SelectListItem 
            { 
                Value = s.ServiceId.ToString(), 
                Text = $"{s.ServiceName} ({s.Duration}m - {s.Price:N0}đ)", 
                Group = new SelectListGroup { Name = s.Category?.Name ?? "Khác" }, 
                Selected = selectedId == s.ServiceId 
            }).ToList();
        }

        private async Task<int?> FindAvailableStaff(int branchId, DateTime date, DateTime start, DateTime end)
        {
            var dayOfWeek = date.DayOfWeek;
            var timeStart = start.TimeOfDay;
            var timeEnd = end.TimeOfDay;
            var buffer = TimeSpan.FromMinutes(10);

            var staffs = await _context.Staffs
                .Where(s => s.BranchId == branchId && s.Status == "active")
                .ToListAsync();

            // Lọc nhân viên đang nghỉ phép
            var staffOnLeaveIds = await _context.LeaveRequests
                .Where(lr => date.Date >= lr.FromDate.Date && date.Date <= lr.ToDate.Date && lr.Status == LeaveRequestStatus.Approved)
                .Select(lr => lr.StaffId)
                .ToListAsync();

            foreach (var s in staffs)
            {
                if (staffOnLeaveIds.Contains(s.StaffId)) continue;

                // 1. Kiểm tra Lịch làm việc (Rule 1)
                // Ưu tiên Lịch thực tế (Override) -> Nếu không có thì dùng Lịch cố định
                var actualSchedule = await _context.WorkSchedules
                    .Include(ws => ws.Shift)
                    .FirstOrDefaultAsync(ws => ws.StaffId == s.StaffId && ws.Date.Date == date.Date);

                Shift? activeShift = null;
                if (actualSchedule != null)
                {
                    if (actualSchedule.Status != WorkStatus.Working) continue; // Đang nghỉ, ốm...
                    activeShift = actualSchedule.Shift;
                }
                else
                {
                    var fixedSchedule = await _context.StaffSchedules
                        .Include(fs => fs.Shift)
                        .FirstOrDefaultAsync(fs => fs.StaffId == s.StaffId && fs.DayOfWeek == dayOfWeek);
                    
                    if (fixedSchedule == null || fixedSchedule.IsOff) continue;
                    activeShift = fixedSchedule.Shift;
                }

                if (activeShift == null) continue;

                // Kiểm tra xem giờ khách đặt có nằm trong ca làm việc không
                if (timeStart < activeShift.StartTime || timeEnd > activeShift.EndTime) continue;

                // 2. Kiểm tra trùng lịch + Buffer 10p (Rule 2 & 4)
                bool busy = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Where(bd => bd.StaffId == s.StaffId && 
                                 bd.Booking.BookingDate.Date == date.Date && 
                                 bd.Booking.Status != BookingStatus.Cancelled)
                    .AnyAsync(bd => 
                        (bd.Booking.StartTime < end.Add(buffer) && bd.Booking.EndTime.Add(buffer) > start)
                    );

                if (!busy) return s.StaffId;
            }
            return null;
        }

        private int? GetCustomerId()
        {
            if (User.Identity?.IsAuthenticated != true) return null;
            var user = _context.Users.FirstOrDefault(u => u.Username == User.Identity.Name);
            return user?.CustomerId;
        }

        [Authorize(Roles = "Admin,Receptionist,Staff")]
        public async Task<IActionResult> CustomerList()
        {
            var customers = await _context.Customers
                .Include(c => c.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(customers);
        }

        [Authorize(Roles = "Admin,Receptionist,Staff")]
        public async Task<IActionResult> CustomerDetail(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                        .ThenInclude(d => d.Service)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();
            return View(customer);
        }
    }
}
