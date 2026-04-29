using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaN5.Models;
using Microsoft.EntityFrameworkCore;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Receptionist,Staff")]
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
            
            // 2. Lấy tất cả bookings đang hoạt động hoặc đã xong nhưng chưa thanh toán
            var allActiveBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Include(b => b.Payments)
                .Where(b => b.Status != BookingStatus.Cancelled && 
                           (b.Status != BookingStatus.Completed || !b.Payments.Any(p => p.Status == PaymentStatus.Paid)))
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                .ToListAsync();

            // 3. Chỉ số trực tuyến
            ViewBag.ActiveServicesCount = allActiveBookings.Count(b => b.Status == BookingStatus.InProgress);
            
            // Khách chờ: Tất cả lịch 'Chờ duyệt' (bất kể ngày) + Lịch 'Đã xác nhận/Đã nhận' của hôm nay
            ViewBag.WaitingCount = allActiveBookings.Count(b => 
                b.Status == BookingStatus.Pending || 
                ((b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Accepted) && b.BookingDate.Date == today));
            
            // 4. Lịch hẹn sắp tới (Top 10 lịch sắp diễn ra từ hôm nay trở đi)
            ViewBag.UpcomingBookings = allActiveBookings
                .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Accepted) && b.BookingDate.Date >= today)
                .Take(10)
                .ToList();

            // 5. Trạng thái thực tế của nhân viên (Chỉ lấy KTV/Therapist)
            var allStaff = await _context.Staffs
                .Include(s => s.Specialization)
                .Where(s => s.Position == "Technician" || s.Position == "Therapist")
                .ToListAsync();
            var staffWorkflows = new List<StaffWorkflowInfo>();
            // Lấy danh sách nhân viên nghỉ phép hôm nay (đã duyệt)
            var todayLeaveStaffIds = await _context.LeaveRequests
                .Where(lr => lr.Status == LeaveRequestStatus.Approved && today >= lr.FromDate.Date && today <= lr.ToDate.Date)
                .Select(lr => lr.StaffId)
                .ToListAsync();

            foreach(var s in allStaff) {
                // Chỉ tính 'Busy' nếu đang InProgress
                var currentBooking = allActiveBookings.FirstOrDefault(b => b.Status == BookingStatus.InProgress && b.BookingDetails.Any(bd => bd.StaffId == s.StaffId));
                
                // FIX: Chỉ hiện khách chờ nếu THỰC SỰ đã gán StaffId cho lịch Confirmed/Accepted đó
                var nextBooking = allActiveBookings.FirstOrDefault(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Accepted) && b.BookingDetails.Any(bd => bd.StaffId == s.StaffId && bd.Status == DetailStatus.Pending));

                staffWorkflows.Add(new StaffWorkflowInfo {
                    Staff = s,
                    IsBusy = currentBooking != null,
                    IsOnLeave = todayLeaveStaffIds.Contains(s.StaffId),
                    CurrentCustomerName = currentBooking?.Customer?.FullName,
                    CurrentServiceName = currentBooking?.BookingDetails.FirstOrDefault(bd => bd.StaffId == s.StaffId)?.Service?.ServiceName,
                    NextCustomerName = nextBooking?.Customer?.FullName,
                    NextServiceName = nextBooking?.BookingDetails.FirstOrDefault(bd => bd.StaffId == s.StaffId)?.Service?.ServiceName
                });
            }
            ViewBag.StaffWorkflows = staffWorkflows.OrderByDescending(w => w.Staff.Status == "active").ThenBy(w => w.IsOnLeave).ThenBy(w => w.IsBusy).ToList();

            // 6. Sơ đồ phòng (Bao gồm cả Đang phục vụ và Đang chờ KTV)
            var activeDetails = await _context.BookingDetails
                .Include(bd => bd.Service)
                .Include(bd => bd.Staff)
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress || ((bd.Booking.Status == BookingStatus.Confirmed || bd.Booking.Status == BookingStatus.Accepted) && bd.RoomNumber != null))
                .ToListAsync();
            ViewBag.ActiveDetails = activeDetails;
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            
            ViewBag.LeaveRequests = await _context.LeaveRequests
                .Include(lr => lr.Staff)
                .Where(lr => lr.Status == LeaveRequestStatus.Pending)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToListAsync();

            // 8. Dọn dẹp nhân viên mẫu (Xử lý an toàn để tránh lỗi Foreign Key)
            try
            {
                var placeholderStaff = await _context.Staffs
                    .Where(s => s.Email != null && s.Email.EndsWith("@spa.com"))
                    .ToListAsync();

                if (placeholderStaff.Any())
                {
                    var ids = placeholderStaff.Select(s => s.StaffId).ToList();

                    // 1. Xóa dữ liệu Chấm công liên quan
                    var relatedAtts = await _context.Attendances.Where(a => ids.Contains(a.StaffId)).ToListAsync();
                    if (relatedAtts.Any()) _context.Attendances.RemoveRange(relatedAtts);

                    // 2. Gỡ liên kết trong BookingDetails
                    var relatedDetails = await _context.BookingDetails
                        .Where(bd => bd.StaffId != null && ids.Contains(bd.StaffId.Value))
                        .ToListAsync();
                    foreach (var d in relatedDetails) d.StaffId = null;

                    // 3. Gỡ liên kết trong Users
                    var relatedUsers = await _context.Users
                        .Where(u => u.StaffId != null && ids.Contains(u.StaffId.Value))
                        .ToListAsync();
                    foreach (var u in relatedUsers) u.StaffId = null;

                    // 4. Xóa nhân viên
                    _context.Staffs.RemoveRange(placeholderStaff);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Ghi log nếu cần, nhưng không làm sập trang Dashboard
                System.Diagnostics.Debug.WriteLine("Cleanup error: " + ex.Message);
            }

            // 9. Dữ liệu biểu đồ doanh thu (7 ngày qua)
            var chartLabels = new List<string>();
            var chartValues = new List<decimal>();
            for (int i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                chartLabels.Add(d.ToString("dd/MM"));
                var dayRevenue = (await _context.Payments
                    .Where(p => p.CreatedAt.Date == d.Date && p.Status == PaymentStatus.Paid)
                    .Select(p => p.Amount)
                    .ToListAsync())
                    .Sum();
                chartValues.Add(dayRevenue);
            }
            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartData = chartValues;

            // 10. Smart Inventory Alerts (MỚI)
            var lowStockMaterials = await _context.Materials
                .Where(m => m.IsActive && m.CurrentStock <= m.MinStock)
                .Select(m => new { m.MaterialName, m.CurrentStock, m.Unit })
                .ToListAsync();
            ViewBag.LowStockAlerts = lowStockMaterials;

            return View(allActiveBookings.Where(b => b.BookingDate.Date == today).ToList());
        }

        public async Task<IActionResult> Rooms()
        {
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            var activeDetails = await _context.BookingDetails
                .Include(bd => bd.Service)
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Where(bd => (bd.Status == DetailStatus.InProgress || bd.Booking.Status == BookingStatus.InProgress) && bd.RoomNumber != null)
                .ToListAsync();

            // LẤY DANH SÁCH LỊCH HẸN SẮP TỚI TRONG NGÀY (Confirmed, Accepted)
            var today = DateTime.Today;
            var upcomingBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.BookingDate.Date == today && 
                           (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Pending))
                .OrderBy(b => b.StartTime)
                .ToListAsync();
            
            ViewBag.UpcomingGrouped = upcomingBookings;
            
            var maintSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MaintenanceRooms");
            var maintRooms = new List<string>();
            if (maintSetting != null && !string.IsNullOrEmpty(maintSetting.Value)) {
                try { maintRooms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(maintSetting.Value) ?? new List<string>(); } catch {}
            }

            ViewBag.ActiveDetails = activeDetails;
            ViewBag.MaintenanceRooms = maintRooms;
            return View(services);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleMaintenanceRoom(int serviceId, int roomNum)
        {
            var keyStr = $"{serviceId}_{roomNum}";
            var maintSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MaintenanceRooms");
            if (maintSetting == null)
            {
                maintSetting = new Setting { Key = "MaintenanceRooms", Value = "[]" };
                _context.Settings.Add(maintSetting);
            }
            var maintRooms = new List<string>();
            try { maintRooms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(maintSetting.Value) ?? new List<string>(); } catch {}
            
            if (maintRooms.Contains(keyStr)) maintRooms.Remove(keyStr);
            else maintRooms.Add(keyStr);

            maintSetting.Value = System.Text.Json.JsonSerializer.Serialize(maintRooms);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetTimelineData(string? date)
        {
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.ParseExact(date, "yyyy-MM-dd", null).Date;
            
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            var resources = new List<object>();
            
            foreach(var svc in services) {
                resources.Add(new { id = $"svc_{svc.ServiceId}", title = svc.ServiceName, isGroup = true });
                for(int i = 1; i <= 10; i++) {
                    resources.Add(new { id = $"{svc.ServiceId}_{i}", title = $"P.{i:D2}", parentId = $"svc_{svc.ServiceId}" });
                }
            }

            var dbBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Where(b => b.BookingDate.Date == targetDate && b.Status != BookingStatus.Cancelled)
                .ToListAsync();

            var events = new List<object>();
            var unassigned = new List<object>();

            foreach(var b in dbBookings) {
                var currentStartTime = b.StartTime;
                foreach(var bd in b.BookingDetails) {
                    var duration = bd.Service.Duration;
                    var currentEndTime = currentStartTime.AddMinutes(duration);

                    var eventObj = new {
                        id = bd.DetailId.ToString(),
                        title = b.Customer.FullName,
                        start = b.BookingDate.ToString("yyyy-MM-dd") + "T" + currentStartTime.ToString("HH:mm:ss"),
                        end = b.BookingDate.ToString("yyyy-MM-dd") + "T" + currentEndTime.ToString("HH:mm:ss"),
                        extendedProps = new {
                            customer = b.Customer.FullName,
                            service = bd.Service.ServiceName,
                            staff = bd.Staff?.FullName ?? "Chưa gán",
                            status = bd.Status.ToString(),
                            code = b.BookingCode
                        }
                    };

                    if (bd.RoomNumber != null) {
                        events.Add(new {
                            id = eventObj.id,
                            resourceId = $"{bd.ServiceId}_{bd.RoomNumber}",
                            title = eventObj.title,
                            start = eventObj.start,
                            end = eventObj.end,
                            color = bd.Status == DetailStatus.InProgress ? "#6366f1" : (bd.Status == DetailStatus.Completed ? "#10b981" : "#f59e0b"),
                            extendedProps = eventObj.extendedProps
                        });
                    } else if (b.Status != BookingStatus.Completed) {
                        unassigned.Add(new {
                            id = bd.DetailId.ToString(),
                            title = b.Customer.FullName,
                            serviceId = bd.ServiceId,
                            serviceName = bd.Service.ServiceName,
                            startTime = currentStartTime.ToString("HH:mm"),
                            duration = duration // Phút
                        });
                    }
                    currentStartTime = currentEndTime;
                }
            }

            return Json(new { resources, events, unassigned });
        }

        [HttpPost]
        public async Task<IActionResult> AssignRoomFromTimeline(int detailId, string resourceId, string start, string end)
        {
            var detail = await _context.BookingDetails
                .Include(bd => bd.Booking).ThenInclude(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(bd => bd.DetailId == detailId);

            if (detail == null) return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });

            var parts = resourceId.Split('_');
            if (parts.Length != 2) return Json(new { success = false, message = "Dữ liệu phòng không hợp lệ." });

            int newRoomNum = int.Parse(parts[1]);
            detail.RoomNumber = newRoomNum;
            
            // Cập nhật lại thời gian của CẢ ĐƠN HÀNG dựa trên vị trí mới của dịch vụ này
            if (DateTime.TryParse(start, out var startDate))
            {
                // Giả định: Dịch vụ được kéo là dịch vụ đầu tiên hoặc ta căn chỉnh StartTime của đơn theo nó
                // Để đơn giản và chính xác, ta tính toán ngược lại StartTime của Booking
                // Tìm vị trí của detail này trong danh sách
                var details = detail.Booking.BookingDetails.OrderBy(d => d.DetailId).ToList();
                var index = details.IndexOf(detail);
                
                var minutesBefore = details.Take(index).Sum(d => d.Service.Duration);
                detail.Booking.StartTime = startDate.AddMinutes(-minutesBefore);
                
                // Tính toán lại EndTime tổng
                var totalMinutes = details.Sum(d => d.Service.Duration);
                detail.Booking.EndTime = detail.Booking.StartTime.AddMinutes(totalMinutes);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomSchedule(int serviceId, int roomNum, string? date)
        {
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.ParseExact(date, "yyyy-MM-dd", null).Date;
            
            // Lấy tất cả lịch của phòng này trong ngày mục tiêu
            // PLUS: Luôn lấy lịch Đang phục vụ (InProgress) hiện tại bất kể ngày nào
            var dbSchedules = await _context.BookingDetails
                .Include(bd => bd.Staff)
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Where(bd => bd.ServiceId == serviceId && bd.RoomNumber == roomNum 
                          && (bd.Booking.BookingDate.Date == targetDate || bd.Status == DetailStatus.InProgress)
                          && bd.Booking.Status != BookingStatus.Cancelled)
                .ToListAsync();

            var schedules = dbSchedules.Select(bd => new {
                    start = bd.Booking.StartTime.ToString("HH:mm"),
                    end = bd.Booking.EndTime.ToString("HH:mm"),
                    customer = bd.Booking.Customer?.FullName ?? "Khách không xác định",
                    technician = bd.Staff?.FullName ?? "Đang chờ điều phối",
                    status = bd.Status.ToString(),
                    statusText = bd.Status == DetailStatus.Completed ? "Đã xong" : 
                                 bd.Status == DetailStatus.InProgress ? "Đang phục vụ" : "Chờ phục vụ"
                })
                .OrderBy(x => x.start)
                .ToList();
                
            return Json(new { success = true, data = schedules });
        }



        // Action quản lý danh sách lịch hẹn đầy đủ
        public async Task<IActionResult> Bookings(string? search, BookingStatus? status, string? date)
        {
            var query = _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.Payments)
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
                        detail.RoomNumber = await GetNextAvailableRoom(detail.ServiceId);
                    }
                }
            }
            else if (newStatus == BookingStatus.Completed || newStatus == BookingStatus.Cancelled)
            {
                var targetDetailStatus = newStatus == BookingStatus.Completed ? DetailStatus.Completed : DetailStatus.Cancelled;
                foreach(var detail in booking.BookingDetails)
                {
                    // STOP clearing RoomNumber to preserve history
                    detail.Status = targetDetailStatus;
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
                // Chỉ cho phép dịch vụ đầu tiên là 'InProgress' nếu KTV rảnh.
                // Các dịch vụ sau đó hoặc nếu KTV bận đều để là 'Pending' (Chờ).
                if (!staffIsBusy && !hasStartedService)
                {
                    if (staffId.HasValue) detail.StaffId = staffId.Value;
                    
                    // SYSTEM AUTOMATICALLY ASSIGNS ROOM - Skipping maintenance and busy rooms
                    detail.RoomNumber = await GetNextAvailableRoom(detail.ServiceId);
                    
                    if (detail.RoomNumber == null)
                    {
                        return Json(new { success = false, message = "Rất tiếc, đã hết phòng khả dụng cho dịch vụ này (do bận hoặc đang bảo trì)." });
                    }
                    
                    detail.Status = DetailStatus.InProgress;
                    hasStartedService = true;
                }
                else
                {
                    detail.Status = DetailStatus.Pending;
                    // Chú ý: Không gán RoomNumber và StaffId cho dịch vụ đang Pending ở trạng thái chờ
                }
            }

            // Booking tổng chỉ được coi là 'InProgress' nếu có ít nhất 1 dịch vụ đang chạy
            booking.Status = hasStartedService ? BookingStatus.InProgress : BookingStatus.Confirmed;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isQueued = staffIsBusy });
        }

        [HttpPost]
        public async Task<IActionResult> NextService(int bookingId, int staffId, int roomNumber)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy mã lịch." });

            var currentDetail = booking.BookingDetails.FirstOrDefault(d => d.Status == DetailStatus.InProgress);
            if (currentDetail != null) {
                currentDetail.Status = DetailStatus.Completed;
                // STOP clearing RoomNumber to preserve history
            }

            var nextDetail = booking.BookingDetails.FirstOrDefault(d => d.Status == DetailStatus.Pending);
            if (nextDetail == null) return Json(new { success = false, message = "Không còn dịch vụ nào chờ." });

            nextDetail.StaffId = staffId;
            nextDetail.RoomNumber = await GetNextAvailableRoom(nextDetail.ServiceId);
            if (nextDetail.RoomNumber == null) return Json(new { success = false, message = "Không còn phòng trống cho dịch vụ tiếp theo." });
            
            nextDetail.Status = DetailStatus.InProgress;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // API lấy danh sách NV rảnh và Phòng trống theo Dịch vụ
        [HttpGet]
        public async Task<IActionResult> GetAvailableResources(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch." });
            
            // Tìm dịch vụ Đang chờ (Pending) đầu tiên nếu đã Check-in, hoặc dịch vụ đầu tiên nếu chưa
            var targetDetail = booking.BookingDetails.FirstOrDefault(d => d.Status == DetailStatus.Pending) 
                            ?? booking.BookingDetails.FirstOrDefault();
                            
            var serviceId = targetDetail?.ServiceId ?? 0;

            var busyStaffIds = await _context.BookingDetails
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress)
                .Select(bd => bd.StaffId)
                .Distinct()
                .ToListAsync();

            // Lọc nhân viên: Phải Active + KHÔNG có đơn nghỉ đã duyệt hôm nay
            var today = DateTime.Today;
            var staffOnLeaveIds = await _context.LeaveRequests
                .Where(lr => today >= lr.FromDate.Date && today <= lr.ToDate.Date && lr.Status == LeaveRequestStatus.Approved)
                .Select(lr => lr.StaffId)
                .ToListAsync();

            // Lấy CategoryId của dịch vụ mục tiêu để lọc nhân viên theo chuyên môn
            var targetService = await _context.Services.FindAsync(serviceId);
            var targetCategoryId = targetService?.CategoryId ?? 0;

            var allActiveStaffInService = await _context.Staffs
                .Include(s => s.Specialization)
                .Where(s => s.Status == "active" && !staffOnLeaveIds.Contains(s.StaffId) 
                         && (s.SpecializationId == null 
                             || s.SpecializationId == serviceId 
                             || (s.Specialization != null && s.Specialization.CategoryId == targetCategoryId)))
                .Select(s => new { 
                    s.StaffId, 
                    s.FullName,
                    specialization = s.Specialization != null ? s.Specialization.ServiceName : "Tổng quát",
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
                serviceName = targetDetail?.Service?.ServiceName 
            });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteBooking(int bookingId, PaymentMethod method)
        {
            var booking = await _context.Bookings
                .Include(b => b.Payments)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            // Kiểm tra xem đã thanh toán chưa (dựa trên PaymentStatus)
            if (booking.Payments != null && booking.Payments.Any(p => p.Status == PaymentStatus.Paid)) 
            {
                return Json(new { success = false, message = "Đơn hàng này đã được thanh toán đầy đủ." });
            }

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = method,
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            // Chuyển trạng thái booking sang Completed nếu chưa có
            booking.Status = BookingStatus.Completed;
            foreach(var detail in booking.BookingDetails)
            {
                detail.Status = DetailStatus.Completed;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleBooking(int bookingId, string newDate, string newTime)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy lịch hẹn." });

            if (booking.Status == BookingStatus.InProgress || booking.Status == BookingStatus.Completed)
                return Json(new { success = false, message = "Không thể dời lịch cho đơn đã thực hiện hoặc hoàn thành." });

            if (!DateTime.TryParseExact(newDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                return Json(new { success = false, message = "Ngày không hợp lệ." });

            if (!TimeSpan.TryParse(newTime, out var time))
                return Json(new { success = false, message = "Giờ không hợp lệ." });

            booking.BookingDate = date.Date;
            booking.StartTime = date.Date.Add(time);

            // Tự động tính lại thời gian kết thúc dựa trên tổng thời lượng dịch vụ
            int totalMinutes = booking.BookingDetails.Sum(d => (int?)d.Service?.Duration ?? 0);
            booking.EndTime = booking.StartTime.AddMinutes(totalMinutes);

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

        public async Task<IActionResult> Payments()
        {
            var payments = await _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.Customer)
                .Include(p => p.Booking)
                .ThenInclude(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .Include(p => p.Booking)
                .ThenInclude(b => b.BookingDetails)
                .ThenInclude(bd => bd.Staff)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(payments);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int requestId)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Staff)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) return Json(new { success = false, message = "Không tìm thấy yêu cầu." });

            request.Status = LeaveRequestStatus.Approved;

            // 1. Tạo bản ghi WorkSchedule để đánh dấu nghỉ thực tế
            for (var dt = request.FromDate.Date; dt <= request.ToDate.Date; dt = dt.AddDays(1))
            {
                var workEntry = new WorkSchedule
                {
                    StaffId = request.StaffId,
                    Date = dt,
                    Status = WorkStatus.Leave,
                    Note = "Nghỉ phép (Đã duyệt)"
                };
                _context.WorkSchedules.Add(workEntry);

                // 2. [AUTO-REASSIGN] Tìm các booking bị ảnh hưởng và gán lại KTV khác
                var affectedDetails = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Include(bd => bd.Service)
                    .Where(bd => bd.StaffId == request.StaffId && 
                                 bd.Booking.BookingDate.Date == dt &&
                                 bd.Booking.Status != BookingStatus.Cancelled &&
                                 bd.Booking.Status != BookingStatus.Completed)
                    .ToListAsync();

                foreach (var detail in affectedDetails)
                {
                    // Tìm KTV thay thế dựa trên các Rule mới
                    var alternativeStaffId = await FindReplacementStaff(
                        detail.Booking.BranchId, 
                        dt, 
                        detail.Booking.StartTime, 
                        detail.Booking.EndTime,
                        request.StaffId // Loại trừ KTV đang xin nghỉ
                    );

                    if (alternativeStaffId.HasValue)
                    {
                        detail.StaffId = alternativeStaffId;
                    }
                    else
                    {
                        detail.StaffId = null; // Cần Admin gán thủ công nếu không tìm thấy ai rảnh
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã duyệt đơn nghỉ và tự động điều phối lại nhân viên." });
        }

        private async Task<int?> FindReplacementStaff(int branchId, DateTime date, DateTime start, DateTime end, int excludedStaffId)
        {
            var dayOfWeek = date.DayOfWeek;
            var timeStart = start.TimeOfDay;
            var timeEnd = end.TimeOfDay;
            var buffer = TimeSpan.FromMinutes(10);

            var staffs = await _context.Staffs
                .Where(s => s.BranchId == branchId && s.Status == "active" && s.StaffId != excludedStaffId)
                .ToListAsync();

            foreach (var s in staffs)
            {
                // Kiểm tra ca làm việc
                var actualSchedule = await _context.WorkSchedules
                    .Include(ws => ws.Shift)
                    .FirstOrDefaultAsync(ws => ws.StaffId == s.StaffId && ws.Date.Date == date.Date);

                Shift? activeShift = null;
                if (actualSchedule != null)
                {
                    if (actualSchedule.Status != WorkStatus.Working) continue;
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
                if (timeStart < activeShift.StartTime || timeEnd > activeShift.EndTime) continue;

                // Kiểm tra trùng lịch
                bool busy = await _context.BookingDetails
                    .Include(bd => bd.Booking)
                    .Where(bd => bd.StaffId == s.StaffId && 
                                 bd.Booking.BookingDate.Date == date.Date && 
                                 bd.Booking.Status != BookingStatus.Cancelled)
                    .AnyAsync(bd => (bd.Booking.StartTime < end.Add(buffer) && bd.Booking.EndTime.Add(buffer) > start));

                if (!busy) return s.StaffId;
            }
            return null;
        }

        private async Task<int?> GetNextAvailableRoom(int serviceId)
        {
            // 1. Lấy danh sách phòng đang bảo trì
            var maintSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MaintenanceRooms");
            var maintRooms = new List<string>();
            if (maintSetting != null && !string.IsNullOrEmpty(maintSetting.Value))
            {
                try { maintRooms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(maintSetting.Value) ?? new List<string>(); } catch { }
            }

            // 2. Lấy danh sách phòng đang bận thực sự của dịch vụ này
            var occupiedRooms = await _context.BookingDetails
                .Where(bd => bd.Booking.Status == BookingStatus.InProgress && bd.ServiceId == serviceId)
                .Select(bd => bd.RoomNumber)
                .ToListAsync();

            // 3. Tìm phòng trống đầu tiên (1-10) không bận và không bảo trì
            for (int i = 1; i <= 10; i++)
            {
                var roomKey = $"{serviceId}_{i}";
                if (!maintRooms.Contains(roomKey) && !occupiedRooms.Contains(i))
                {
                    return i;
                }
            }
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransferRequests()
        {
            var cutoff = DateTime.Now.AddHours(-24);
            var transfers = await _context.AuditLogs
                .Where(a => a.Action == "ShiftTransferRequest" && a.CreatedAt >= cutoff)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new {
                    id = a.Id,
                    staff = a.UserName,
                    bookingId = a.EntityId,
                    message = a.NewValues,
                    time = a.CreatedAt.ToString("HH:mm:ss")
                })
                .ToListAsync();
            
            return Json(new { success = true, data = transfers });
        }
    }

    // Helper class for dashboard
    public class StaffWorkflowInfo {
        public SpaN5.Models.Staff Staff { get; set; }
        public bool IsBusy { get; set; }
        public bool IsOnLeave { get; set; }
        public string? CurrentCustomerName { get; set; }
        public string? CurrentServiceName { get; set; }
        public string? NextCustomerName { get; set; }
        public string? NextServiceName { get; set; }
    }
}
