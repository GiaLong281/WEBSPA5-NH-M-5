using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaN5.Models;
using Microsoft.EntityFrameworkCore;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin, Receptionist, Staff")]
    public class HomeController : Controller
    {
        private readonly SpaDbContext _context;

        public HomeController(SpaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            
            // 1. Chỉ số doanh thu hôm nay
            ViewBag.TodayRevenue = (await _context.Payments
                .Where(p => p.CreatedAt.Date == today && p.Status == PaymentStatus.Paid)
                .Select(p => p.Amount)
                .ToListAsync())
                .Sum();
            
            // 2. Lấy tất cả bookings chưa hoàn thành/hủy
            var allActiveBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.Status != BookingStatus.Completed && b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                .ToListAsync();

            // 3. Chỉ số trực tuyến
            ViewBag.ActiveServicesCount = allActiveBookings.Count(b => b.Status == BookingStatus.InProgress);
            
            // Khách chờ: Tất cả lịch 'Chờ duyệt' (bất kể ngày) + Lịch 'Đã xác nhận' của hôm nay
            ViewBag.WaitingCount = allActiveBookings.Count(b => 
                b.Status == BookingStatus.Pending || 
                (b.Status == BookingStatus.Confirmed && b.BookingDate.Date == today));
            
            // 4. Lịch hẹn sắp tới (Top 10 lịch sắp diễn ra từ hôm nay trở đi)
            ViewBag.UpcomingBookings = allActiveBookings
                .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && b.BookingDate.Date >= today)
                .Take(10)
                .ToList();

            // 5. Trạng thái thực tế của nhân viên (Chỉ lấy KTV/Therapist)
            var allStaff = await _context.Staffs
                .Include(s => s.Specialization)
                .Where(s => s.Position == "Technician" || s.Position == "Therapist")
                .ToListAsync();
            var staffWorkflows = new List<StaffWorkflowInfo>();
            foreach(var s in allStaff) {
                // Chỉ tính 'Busy' nếu đang InProgress
                var currentBooking = allActiveBookings.FirstOrDefault(b => b.Status == BookingStatus.InProgress && b.BookingDetails.Any(bd => bd.StaffId == s.StaffId));
                
                // FIX: Chỉ hiện khách chờ nếu THỰC SỰ đã gán StaffId cho lịch Confirmed đó
                var nextBooking = allActiveBookings.FirstOrDefault(b => b.Status == BookingStatus.Confirmed && b.BookingDetails.Any(bd => bd.StaffId == s.StaffId && bd.Status == DetailStatus.Pending));

                staffWorkflows.Add(new StaffWorkflowInfo {
                    Staff = s,
                    IsBusy = currentBooking != null,
                    CurrentCustomerName = currentBooking?.Customer?.FullName,
                    CurrentServiceName = currentBooking?.BookingDetails.FirstOrDefault(bd => bd.StaffId == s.StaffId)?.Service?.ServiceName,
                    NextCustomerName = nextBooking?.Customer?.FullName,
                    NextServiceName = nextBooking?.BookingDetails.FirstOrDefault(bd => bd.StaffId == s.StaffId)?.Service?.ServiceName
                });
            }
            ViewBag.StaffWorkflows = staffWorkflows.OrderByDescending(w => w.Staff.Status == "active").ThenBy(w => w.IsBusy).ToList();

            // 6. Sơ đồ phòng (Bao gồm cả Đang phục vụ và Đang chờ KTV)
            var activeDetails = await _context.BookingDetails
                .Include(bd => bd.Service)
                .Include(bd => bd.Staff)
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress || (bd.Booking.Status == BookingStatus.Confirmed && bd.RoomNumber != null))
                .ToListAsync();
            ViewBag.ActiveDetails = activeDetails;
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();

            // 8. Tự động kiểm tra và seed nhân viên nếu trống (cho mục đích thử nghiệm)
            if(!allStaff.Any()) {
                await SeedPlaceholderStaff();
                return RedirectToAction("Index");
            }

            return View(allActiveBookings.Where(b => b.BookingDate.Date == today).ToList());
        }

        public async Task<IActionResult> Rooms()
        {
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            var activeDetails = await _context.BookingDetails
                .Include(bd => bd.Service)
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress)
                .ToListAsync();
            
            ViewBag.ActiveDetails = activeDetails;
            return View(services);
        }

        private async Task SeedPlaceholderStaff()
        {
            var services = await _context.Services.ToListAsync();
            var random = new Random();
            var statuses = new[] { "active", "active", "active", "off", "inactive" }; // Xác suất ngẫu nhiên

            foreach (var svc in services)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var status = statuses[random.Next(statuses.Length)];
                    var name = $"KTV {svc.ServiceName} {i:D2}";
                    
                    _context.Staffs.Add(new SpaN5.Models.Staff
                    {
                        FullName = name,
                        Status = status,
                        SpecializationId = svc.ServiceId,
                        Email = name.ToLower().Replace(" ", "") + "@spa.com",
                        Phone = "098" + random.Next(1000000, 9999999).ToString(),
                        Position = "Technician"
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // Action quản lý danh sách lịch hẹn đầy đủ
        public async Task<IActionResult> Bookings(string? search, BookingStatus? status, string? date)
        {
            var query = _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(b => b.Customer.FullName.Contains(search) || b.BookingCode.Contains(search));
            
            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);
            
            if (!string.IsNullOrEmpty(date) && DateTime.TryParseExact(date, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                query = query.Where(b => b.BookingDate.Date == parsedDate.Date);
            }

            var result = await query.OrderByDescending(b => b.BookingDate).ThenBy(b => b.StartTime).ToListAsync();
            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int bookingId, BookingStatus newStatus)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy mã lịch." });

            if (newStatus == BookingStatus.InProgress)
            {
                foreach(var detail in booking.BookingDetails)
                {
                    if (detail.RoomNumber == null)
                    {
                        var occupiedRooms = await _context.BookingDetails
                            .Where(bd => bd.Booking.Status == BookingStatus.InProgress)
                            .Select(bd => bd.RoomNumber)
                            .ToListAsync();

                        for (int i = 1; i <= 10; i++)
                        {
                            if (!occupiedRooms.Contains(i))
                            {
                                detail.RoomNumber = i;
                                break;
                            }
                        }
                    }
                }
            }
            else if (newStatus == BookingStatus.Completed || newStatus == BookingStatus.Cancelled)
            {
                foreach(var detail in booking.BookingDetails)
                {
                    detail.RoomNumber = null;
                }
            }

            booking.Status = newStatus;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(int bookingId) => await UpdateStatus(bookingId, BookingStatus.Confirmed);

        [HttpPost]
        public async Task<IActionResult> RejectBooking(int bookingId) => await UpdateStatus(bookingId, BookingStatus.Cancelled);

        [HttpPost]
        public async Task<IActionResult> CheckIn(int bookingId, int? staffId, int? roomNumber)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy mã lịch." });

            bool staffIsBusy = false;
            if(staffId.HasValue) {
                staffIsBusy = await _context.BookingDetails
                    .AnyAsync(bd => bd.StaffId == staffId.Value && bd.Booking.Status == BookingStatus.InProgress);
            }

            bool hasStartedService = false;
            foreach (var detail in booking.BookingDetails)
            {
                if (staffId.HasValue) detail.StaffId = staffId.Value;
                if (roomNumber.HasValue) detail.RoomNumber = roomNumber.Value;
                
                // Chỉ cho phép dịch vụ đầu tiên là 'InProgress' nếu KTV rảnh.
                // Các dịch vụ sau đó hoặc nếu KTV bận đều để là 'Pending' (Chờ).
                if (!staffIsBusy && !hasStartedService)
                {
                    detail.Status = DetailStatus.InProgress;
                    hasStartedService = true;
                }
                else
                {
                    detail.Status = DetailStatus.Pending;
                }
            }

            // Booking tổng chỉ được coi là 'InProgress' nếu có ít nhất 1 dịch vụ đang chạy
            booking.Status = hasStartedService ? BookingStatus.InProgress : BookingStatus.Confirmed;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isQueued = staffIsBusy });
        }

        // API lấy danh sách NV rảnh và Phòng trống theo Dịch vụ
        [HttpGet]
        public async Task<IActionResult> GetAvailableResources(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });
            
            var serviceId = booking.BookingDetails.FirstOrDefault()?.ServiceId ?? 0;

            var busyStaffIds = await _context.BookingDetails
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress)
                .Select(bd => bd.StaffId)
                .Distinct()
                .ToListAsync();

            var allActiveStaffInService = await _context.Staffs
                .Where(s => s.Status == "active" && s.SpecializationId == serviceId)
                .Select(s => new { 
                    s.StaffId, 
                    s.FullName, 
                    isBusy = busyStaffIds.Contains(s.StaffId) 
                })
                .ToListAsync();

            // Lọc phòng trống CỦA DỊCH VỤ NÀY (Chỉ tính phòng đang thực sự InProgress)
            var occupiedRoomsInService = await _context.BookingDetails
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress && bd.ServiceId == serviceId)
                .Select(bd => bd.RoomNumber)
                .ToListAsync();

            var freeRooms = new List<int>();
            for (int i = 1; i <= 10; i++)
            {
                if (!occupiedRoomsInService.Contains(i)) freeRooms.Add(i);
            }

            return Json(new { 
                success = true, 
                staff = allActiveStaffInService, 
                rooms = freeRooms, 
                serviceName = booking.BookingDetails.FirstOrDefault()?.Service?.ServiceName 
            });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteBooking(int bookingId, PaymentMethod method)
        {
            var booking = await _context.Bookings.Include(b => b.BookingDetails).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            if (booking.Status == BookingStatus.Completed) return Json(new { success = false, message = "Đơn hàng đã được hoàn thành trước đó." });

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = method,
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            booking.Status = BookingStatus.Completed;
            foreach(var detail in booking.BookingDetails)
            {
                detail.RoomNumber = null;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        public async Task<IActionResult> Inventory()
        {
            var materials = await _context.Materials.ToListAsync();
            var recentTransactions = await _context.StockTransactions
                .Include(t => t.Material)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.RecentTransactions = recentTransactions;
            return View(materials);
        }
    }

    // Helper class for dashboard
    public class StaffWorkflowInfo {
        public SpaN5.Models.Staff Staff { get; set; }
        public bool IsBusy { get; set; }
        public string? CurrentCustomerName { get; set; }
        public string? CurrentServiceName { get; set; }
        public string? NextCustomerName { get; set; }
        public string? NextServiceName { get; set; }
    }

}
