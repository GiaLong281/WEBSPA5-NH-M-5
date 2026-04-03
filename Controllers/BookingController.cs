using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    public class BookingController : Controller
    {
        private readonly SpaDbContext _context;

        public BookingController(SpaDbContext context)
        {
            _context = context;
        }

        // GET: Booking
        public async Task<IActionResult> Index(int? serviceId)
        {
            // Hiển thị danh sách dịch vụ để người dùng chọn
            var services = await _context.Services
                .Where(s => s.IsActive)
                .ToListAsync();
            
            ViewBag.Services = services;
            
            var model = new BookingViewModel();
            if (serviceId.HasValue)
            {
                model.ServiceId = serviceId.Value;
            }

            return View(model);
        }

        // API GET: /Booking/GetServiceDetails/1
        [HttpGet]
        public async Task<IActionResult> GetServiceDetails(int id)
        {
            var service = await _context.Services
                .Include(s => s.ServiceMaterials)
                .ThenInclude(sm => sm.Material)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null) return Json(new { success = false, message = "Không tìm thấy dịch vụ" });

            var result = new
            {
                service.ServiceId,
                service.ServiceName,
                service.Description,
                service.Price,
                service.Duration,
                service.Image,
                service.VideoUrl,
                service.IsVip,
                service.MaxCapacity,
                Materials = service.ServiceMaterials.Select(sm => new
                {
                    sm.Material?.MaterialName,
                    sm.Quantity,
                    sm.Material?.Unit
                }).ToList()
            };

            return Json(new { success = true, data = result });
        }

        // API GET: /Booking/GetAvailableTimes?date=2024-05-20&serviceId=1
        [HttpGet]
        public async Task<IActionResult> GetAvailableTimes(string date, int serviceId)
        {
            var service = await _context.Services.FindAsync(serviceId);
            // Bắt buộc sức chứa tối thiểu là 10 nếu đang để 0 hoặc null
            int maxCapacity = (service?.MaxCapacity ?? 10) <= 0 ? 10 : service.MaxCapacity;

            // Lấy ra các khung giờ hoạt động tiêu chuẩn
            var standardSlots = new List<string>
            {
                "09:00", "09:30", "10:00", "10:30", "11:00", "11:30",
                "13:00", "13:30", "14:00", "14:30", "15:00", "15:30",
                "16:00", "16:30", "17:00", "17:30", "18:00", "18:30", "19:00"
            };

            var timeSlotsResponse = new List<object>();

            if (DateTime.TryParse(date, out DateTime selectedDate))
            {
                var now = DateTime.Now;
                // Chốt lại danh sách booking hợp lệ trong ngày cho dịch vụ này
                var bookedBookings = await _context.Bookings
                    .Where(b => b.BookingDate.Date == selectedDate.Date && b.Status != BookingStatus.Cancelled)
                    .Where(b => b.BookingDetails.Any(bd => bd.ServiceId == serviceId))
                    .Select(b => new { b.StartTime, b.EndTime })
                    .ToListAsync();
                
                foreach (var slot in standardSlots)
                {
                    DateTime slotTime = DateTime.ParseExact($"{date} {slot}", "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                    
                    bool isPast = selectedDate.Date == now.Date && slotTime < now;
                    // Một người chiếm chỗ từ StartTime đến EndTime (không bao gồm EndTime)
                    int currentOccupancy = bookedBookings.Count(b => slotTime >= b.StartTime && slotTime < b.EndTime);
                    
                    bool isFull = currentOccupancy >= maxCapacity;

                    timeSlotsResponse.Add(new
                    {
                        time = slot,
                        isFull = isFull,
                        isPast = isPast,
                        remaining = isPast ? 0 : Math.Max(0, maxCapacity - currentOccupancy),
                        total = maxCapacity
                    });
                }
            }
            else
            {
                foreach (var slot in standardSlots)
                {
                    timeSlotsResponse.Add(new { time = slot, isFull = false, remaining = maxCapacity, total = maxCapacity });
                }
            }

            return Json(new { success = true, times = timeSlotsResponse });
        }


        // POST: Booking/SubmitBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitBooking(BookingViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra xem khách hàng đã tồn tại chưa (Dựa vào SĐT)
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == model.PhoneNumber);
                
                if (customer == null)
                {
                    customer = new Customer
                    {
                        FullName = model.CustomerName,
                        Phone = model.PhoneNumber,
                        Email = model.Email,
                        CreatedAt = DateTime.Now
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                var service = await _context.Services.FindAsync(model.ServiceId);
                if (service == null) return Json(new { success = false, message = "Không tìm thấy dịch vụ" });

                // Chuyển đổi ngày giờ an toàn
                DateTime bookingDateTime;
                try {
                    bookingDateTime = DateTime.ParseExact($"{model.Date} {model.Time}", "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                } catch {
                    return Json(new { success = false, message = "Định dạng thời gian không hợp lệ." });
                }

                int duration = service.Duration > 0 ? service.Duration : 60;
                DateTime bookingEndTime = bookingDateTime.AddMinutes(duration);

                // KIỂM TRA SỨC CHỨA: Đếm số lượng booking đang chiếm chỗ
                int currentOccupancy = await _context.Bookings
                    .Where(b => b.BookingDate.Date == bookingDateTime.Date && b.Status != BookingStatus.Cancelled)
                    .Where(b => b.BookingDetails.Any(bd => bd.ServiceId == model.ServiceId))
                    .CountAsync(b => 
                        (bookingDateTime >= b.StartTime && bookingDateTime < b.EndTime) || 
                        (bookingEndTime > b.StartTime && bookingEndTime <= b.EndTime) ||
                        (bookingDateTime <= b.StartTime && bookingEndTime >= b.EndTime)
                    );

                // Nếu sức chứa trong DB là 0 hoặc null, mặc định là 10
                int maxCapacity = service.MaxCapacity > 0 ? service.MaxCapacity : 10;

                if (currentOccupancy >= maxCapacity)
                {
                    return Json(new { success = false, message = "Xin lỗi, khung giờ này cho dịch vụ này đã hết phòng. Vui lòng chọn khung giờ khác." });
                }

                // Tạo Booking
                var booking = new Booking
                {
                    BookingCode = "BK" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    CustomerId = customer.CustomerId,
                    BranchId = 1, 
                    BookingDate = DateTime.Parse(model.Date),
                    StartTime = bookingDateTime,
                    EndTime = bookingEndTime,
                    Status = BookingStatus.Pending,
                    TotalAmount = service.Price,
                    Notes = model.Notes,
                    CreatedAt = DateTime.Now,
                    BookingDetails = new List<BookingDetail>
                    {
                        new BookingDetail
                        {
                            ServiceId = service.ServiceId,
                            PriceAtTime = service.Price
                        }
                    }
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                return Json(new { success = true, redirectUrl = Url.Action("Success", new { bookingCode = booking.BookingCode }) });
            }

            var errors = string.Join("<br>", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Json(new { success = false, message = "Lỗi dữ liệu:<br>" + errors });
        }

        public async Task<IActionResult> Success(string bookingCode)
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Payments)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // POST: Booking/ProcessPayment
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(string bookingCode, PaymentMethod method)
        {
            var booking = await _context.Bookings
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode);

            if (booking == null) return NotFound();

            // Nếu là chuyển khoản, tạo bản ghi thanh toán mới
            if (method == PaymentMethod.BankTransfer)
            {
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.TotalAmount,
                    Method = PaymentMethod.BankTransfer,
                    Status = PaymentStatus.Paid, // Giả sử đã thanh toán thành công trong demo
                    CreatedAt = DateTime.Now
                };

                _context.Payments.Add(payment);
                
                // Cập nhật trạng thái Booking nếu cần
                // booking.Status = BookingStatus.Confirmed;
                
                await _context.SaveChangesAsync();
            }
            else if (method == PaymentMethod.Cash)
            {
                // Đối với tiền mặt, có thể chỉ cần ghi nhận hoặc tạo bản ghi Pending
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = booking.TotalAmount,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTime.Now
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Success", new { bookingCode = booking.BookingCode });
        }

        // GET: Booking/Details/BK123456
        public async Task<IActionResult> Details(string bookingCode)
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Payments)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode);

            if (booking == null) return NotFound();

            return View(booking);
        }

        // GET: Booking/Cancel
        [HttpPost]
        public async Task<IActionResult> Cancel(string bookingCode)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingCode == bookingCode);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch hẹn" });

            if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled)
            {
                return Json(new { success = false, message = "Không thể hủy lịch hẹn ở trạng thái này" });
            }

            booking.Status = BookingStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Hủy lịch hẹn thành công" });
        }

        // GET: Booking/CustomerList
        public async Task<IActionResult> CustomerList()
        {
            // Lấy danh sách khách hàng và đếm tổng số dịch vụ họ đã đặt
            var customers = await _context.Customers
                .Include(c => c.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                .ToListAsync();

            return View(customers);
        }

        // GET: Booking/CustomerDetail/1
        public async Task<IActionResult> CustomerDetail(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            return View(customer);
        }
        // GET: Booking/ResetData
        // TEMPORARY DEVELOPER TOOL: Resets all bookings and sets service capacities to 10.
        public async Task<IActionResult> ResetData()
        {
            try
            {
                // 1. Clear all booking related data
                var bookingDetails = await _context.BookingDetails.ToListAsync();
                _context.BookingDetails.RemoveRange(bookingDetails);

                var payments = await _context.Payments.ToListAsync();
                _context.Payments.RemoveRange(payments);

                var bookings = await _context.Bookings.ToListAsync();
                _context.Bookings.RemoveRange(bookings);

                // 2. Reset all services to MaxCapacity = 10
                var services = await _context.Services.ToListAsync();
                foreach(var s in services)
                {
                    s.MaxCapacity = 10;
                }

                await _context.SaveChangesAsync();
                return Content("Đã Reset toàn bộ dữ liệu Lịch hẹn và cập nhật Sức chứa (10 phòng/suất). Bạn có thể quay lại trang Đặt lịch để thử lại.");
            }
            catch (Exception ex)
            {
                return Content("Lỗi khi Reset: " + ex.Message);
            }
        }
    }
}
