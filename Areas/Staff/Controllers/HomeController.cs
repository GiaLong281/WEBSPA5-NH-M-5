using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System.Security.Claims;

namespace SpaN5.Areas.Staff.Controllers
{
    [Area("Staff")]
    [Authorize(Roles = "Staff, Admin, Therapist")] 
    public class HomeController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IInventoryService _inventoryService;

        public HomeController(SpaDbContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index(string view = "day")
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                ViewBag.Error = "Tài khoản của bạn chưa được liên kết mã Kỹ thuật viên. Vui lòng liên hệ Quản lý.";
                return View("Error");
            }

            var today = DateTime.Today;
            DateTime startDate = today;
            DateTime endDate = today;

            if (view == "week")
            {
                startDate = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday); // Thứ 2
                endDate = startDate.AddDays(6); // Chủ nhật
            }
            else if (view == "month")
            {
                startDate = new DateTime(today.Year, today.Month, 1);
                endDate = startDate.AddMonths(1).AddDays(-1);
            }

            ViewBag.CurrentView = view;

            // Chỉ lấy các Booking trong khoảng thời gian mà TÀI KHOẢN NÀY ĐƯỢC GÁN
            var allBookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Where(b => b.BookingDate.Date >= startDate && b.BookingDate.Date <= endDate)
                .Where(b => b.BookingDetails.Any(bd => bd.StaffId == staffId)) 
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                .ToListAsync();

            // Loại bỏ các detail của KTV khác trong nội bộ 1 booking kết quả
            foreach (var b in allBookings)
            {
                b.BookingDetails = b.BookingDetails.Where(bd => bd.StaffId == staffId).ToList();
            }

            // Lấy lịch sử xin nghỉ
            ViewBag.LeaveRequests = await _context.LeaveRequests
                .Where(lr => lr.StaffId == staffId)
                .OrderByDescending(lr => lr.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Lấy TẤT CẢ đơn nghỉ ĐÃ DUYỆT trong khoảng thời gian đang xem để hiện lên lịch
            ViewBag.ApprovedLeaves = await _context.LeaveRequests
                .Where(lr => lr.StaffId == staffId && lr.Status == LeaveRequestStatus.Approved)
                .Where(lr => lr.FromDate <= endDate && lr.ToDate >= startDate)
                .ToListAsync();

            // Tính hiệu suất (Tính trên toàn bộ lịch được lấy ra để bao quát cả Tuần/Tháng)
            int totalCa = allBookings.Count;
            int hoanThanh = allBookings.Count(b => b.Status == BookingStatus.Completed);
            int percent = totalCa > 0 ? (hoanThanh * 100) / totalCa : 0;
            
            ViewBag.TotalCa = totalCa;
            ViewBag.CaHoanThanh = hoanThanh;
            ViewBag.Efficiency = percent;

            // Lấy thông tin Staff và Specialization để phân Team
            var staff = await _context.Staffs
                .Include(s => s.Specialization)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);

            // Logic Mapping Team chuyên nghiệp (Theo yêu cầu: 1 dịch vụ = 1 team)
            string teamName = "General Team";
            string teamType = "general";
            string teamColor = "#0ea5e9"; // Mặc định xanh dương
            string teamIcon = "bi-person-badge";
            string noteHint = "Ghi chú tiến trình khách hàng...";

            if (staff?.Specialization != null)
            {
                var svcName = staff.Specialization.ServiceName;
                if (svcName.Contains("Massage"))
                {
                    teamName = "Team Zen Therapy";
                    teamType = "zen";
                    teamColor = "#78350f"; // Nâu gỗ Zen
                    teamIcon = "bi-peace";
                    noteHint = "Ghi chú vùng cơ đau mỏi, lực nhấn yêu thích của khách...";
                }
                else if (svcName.Contains("Da Mặt"))
                {
                    teamName = "Team Skin Clinical";
                    teamType = "skin";
                    teamColor = "#0d9488"; // Xanh Teal Y tế
                    teamIcon = "bi-shield-plus";
                    noteHint = "Ghi chú tình trạng nền da, phản ứng với serum và tinh chất...";
                }
                else if (svcName.Contains("Thảo Dược"))
                {
                    teamName = "Team Nature Herbal";
                    teamType = "herbal";
                    teamColor = "#15803d"; // Xanh lá thảo mộc
                    teamIcon = "bi-leaf";
                    noteHint = "Ghi chú mức độ đào thải độc tố và cảm nhận sau khi ngâm thảo mộc...";
                }
                else if (svcName.Contains("VIP"))
                {
                    teamName = "Team Royal VIP";
                    teamType = "royal";
                    teamColor = "#7e22ce"; // Tím quý phái/Vàng kim
                    teamIcon = "bi-crown";
                    noteHint = "Ghi chú trải nghiệm không gian và yêu cầu đặc biệt của khách thượng lưu...";
                }
            }

            ViewBag.TeamInfo = new {
                Name = teamName,
                Type = teamType,
                Color = teamColor,
                Icon = teamIcon,
                NoteHint = noteHint
            };

            // Ca chuẩn bị tiếp theo (Ca chưa hoàn thành và gần nhất)
            var nextBooking = allBookings.FirstOrDefault(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);
            ViewBag.NextBooking = nextBooking;

            // View chỉ hiển thị ca chưa hoàn thành hoặc đang làm
            var activeBookings = allBookings;

            // Lấy tất cả dịch vụ để hiển thị trong phần Task Dịch Vụ
            var allServices = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Services = allServices;

            return View(activeBookings);
        }

        public async Task<IActionResult> Profile()
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                return RedirectToAction("Login", "Auth", new { area = "Admin" });
            }

            var staff = await _context.Staffs
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.StaffId == staffId);

            if (staff == null)
            {
                ViewBag.Error = "Không tìm thấy dữ liệu nhân viên.";
                return View("Error");
            }

            return View(staff);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });
            }

            if (avatarFile == null || avatarFile.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn ảnh." });
            }

            try
            {
                var staff = await _context.Staffs.FindAsync(staffId);
                if (staff == null) return Json(new { success = false, message = "Nhân viên không tồn tại." });

                // Create directory if not exists
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Delete old avatar if exists
                if (!string.IsNullOrEmpty(staff.Avatar))
                {
                    var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", staff.Avatar.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                // Save new avatar
                var fileName = $"staff_{staffId}_{DateTime.Now.Ticks}{Path.GetExtension(avatarFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                staff.Avatar = $"/uploads/avatars/{fileName}";
                await _context.SaveChangesAsync();

                return Json(new { success = true, avatarUrl = staff.Avatar });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu ảnh: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AcceptBooking(int bookingId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy mã lịch." });
            
            if (booking.Status != BookingStatus.Confirmed)
                return Json(new { success = false, message = "Lịch hẹn hiện không trong trạng thái chờ nhận." });

            booking.Status = BookingStatus.Accepted;
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, message = "Đã nhận nhiệm vụ thành công." });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitLeaveRequest(DateTime fromDate, DateTime toDate, string reason)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });
            }

            if (fromDate.Date < DateTime.Today)
                return Json(new { success = false, message = "Không thể xin nghỉ cho ngày trong quá khứ." });

            if (toDate < fromDate)
                return Json(new { success = false, message = "Ngày kết thúc không hợp lệ." });

            var request = new LeaveRequest
            {
                StaffId = staffId,
                FromDate = fromDate,
                ToDate = toDate,
                Reason = reason,
                Status = LeaveRequestStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã gửi đơn xin nghỉ. Vui lòng chờ Admin phê duyệt." });
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(int bookingId)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });
            }

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false, message = "Không tìm thấy mã lịch." });

            // Accepted or Confirmed is okay to start, but we prefer Accepted now
            if (booking.Status != BookingStatus.Accepted && booking.Status != BookingStatus.Confirmed)
                return Json(new { success = false, message = "Lịch hẹn không hợp lệ để bắt đầu." });

            // Logic tự động gán staff và phòng nếu đang chuyển sang InProgress
            // [NÂNG CAO] Kiểm tra xem KTV này có đang bận thực hiện ca nào khác không?
            var busyBooking = await _context.BookingDetails
                .AnyAsync(bd => bd.StaffId == staffId && bd.Booking.Status == BookingStatus.InProgress && bd.BookingId != bookingId);
            
            if (busyBooking)
            {
                return Json(new { success = false, message = "Bạn đang có một dịch vụ đang thực hiện. Hãy hoàn thành hoặc chuyển ca trước khi bắt đầu dịch vụ mới." });
            }

            booking.Status = BookingStatus.InProgress;
            foreach (var detail in booking.BookingDetails)
            {
                if (detail.StaffId == null) detail.StaffId = staffId;
                detail.Status = DetailStatus.InProgress;

                if (detail.RoomNumber == null)
                {
                    var occupiedRooms = await _context.BookingDetails
                        .Where(bd => bd.Booking.Status == BookingStatus.InProgress)
                        .Select(bd => bd.RoomNumber)
                        .ToListAsync();

                    for (int i = 1; i <= 20; i++)
                    {
                        if (!occupiedRooms.Contains(i))
                        {
                            detail.RoomNumber = i;
                            break;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }


        [HttpGet]
        public async Task<IActionResult> GetAllServices()
        {
            var services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            return Json(services);
        }

        [HttpPost]
        public async Task<IActionResult> AddServiceDetail(int bookingId, int serviceId)
        {
            var booking = await _context.Bookings.Include(b => b.BookingDetails).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return Json(new { success = false, message = "Dịch vụ không tồn tại" });

            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            int? staffId = int.TryParse(staffIdClaim, out int sId) ? sId : null;

            var detail = new BookingDetail
            {
                BookingId = bookingId,
                ServiceId = serviceId,
                PriceAtTime = service.Price,
                StaffId = staffId
            };

            _context.BookingDetails.Add(detail);
            
            // Cập nhật lại tổng tiền của Booking
            booking.TotalAmount += service.Price;
            
            await _context.SaveChangesAsync();

            return Json(new { success = true, totalPrice = booking.TotalAmount });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteBooking(int bookingId, PaymentMethod method)
        {
            var booking = await _context.Bookings.Include(b => b.BookingDetails).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            // 1. Tạo bản ghi thanh toán
            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = method,
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            // 2. Chuyển trạng thái booking sang Completed và giải phóng phòng
            booking.Status = BookingStatus.Completed;
            foreach(var detail in booking.BookingDetails)
            {
                detail.RoomNumber = null;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetailStatus(int detailId, DetailStatus newStatus)
        {
            var detail = await _context.BookingDetails.FindAsync(detailId);
            if (detail == null) return Json(new { success = false, message = "Không tìm thấy dịch vụ" });

            detail.Status = newStatus;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetailNote(int detailId, string note)
        {
            var detail = await _context.BookingDetails.FindAsync(detailId);
            if (detail == null) return Json(new { success = false, message = "Không tìm thấy dịch vụ" });

            detail.TechnicianNote = note;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public async Task<IActionResult> History(string searchTerm)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                return RedirectToAction("Login", "Auth", new { area = "Admin" });
            }

            var query = _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Cancelled)
                .Where(b => b.BookingDetails.Any(bd => bd.StaffId == staffId))
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(b => b.BookingCode.Contains(searchTerm) || 
                                         b.Customer.FullName.Contains(searchTerm) || 
                                         b.Customer.Phone.Contains(searchTerm));
            }

            var history = await query.OrderByDescending(b => b.BookingDate).ToListAsync();
            
            // Loại bỏ các detail không thuộc KTV này
            foreach (var b in history)
            {
                b.BookingDetails = b.BookingDetails.Where(bd => bd.StaffId == staffId).ToList();
            }

            return View(history);
        }

        // Tương tác Điểm danh (Check In / Check Out) Update từ Cổng Nội Bộ Sang
        [HttpPost]
        public async Task<IActionResult> ToggleAttendance()
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId)) return Json(new { success = false, message = "Lỗi xác thực" });

            var today = DateTime.Today;
            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.StaffId == staffId && a.Date.Date == today);

            if (attendance == null)
            {
                attendance = new Attendance { StaffId = staffId, Date = today, CheckInTime = DateTime.Now };
                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();
                return Json(new { success = true, state = "checked_in", time = attendance.CheckInTime.Value.ToString("HH:mm") });
            }
            else if (attendance.CheckOutTime == null)
            {
                attendance.CheckOutTime = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true, state = "checked_out", time = attendance.CheckOutTime.Value.ToString("HH:mm") });
            }

            return Json(new { success = false, message = "Bạn đã hoàn thành ca làm việc hôm nay" });
        }

        // Modal API - lấy list Materials
        [HttpGet]
        public async Task<IActionResult> GetMaterials()
        {
            var materials = await _context.Materials.Select(m => new { id = m.MaterialId, name = m.MaterialName, unit = m.Unit }).ToListAsync();
            return Json(new { success = true, data = materials });
        }

        // Báo thiếu vật tư
        [HttpPost]
        public async Task<IActionResult> ReportMaterial(int materialId, int quantity, string note)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId)) return Json(new { success = false });

            var staff = await _context.Staffs.FindAsync(staffId);
            var material = await _context.Materials.FindAsync(materialId);
            if (material == null || staff == null) return Json(new { success = false, message = "Vật tư hoặc nhân sự không tồn tại" });

            var transaction = new StockTransaction
            {
                MaterialId = materialId,
                Type = "Báo thiếu/hao hụt",
                Quantity = Math.Max(1, quantity),
                Reason = $"[{staff.FullName}] báo cáo: {note}",
                CreatedAt = DateTime.Now
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookingMaterials(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                        .ThenInclude(s => s.ServiceMaterials)
                            .ThenInclude(sm => sm.Material)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return Json(new { success = false });

            // Lấy dịch vụ chính của nhân viên này trong booking
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            int? staffId = int.TryParse(staffIdClaim, out int sId) ? sId : null;
            
            var detail = booking.BookingDetails.FirstOrDefault(bd => bd.StaffId == staffId);
            var serviceName = detail?.Service?.ServiceName ?? "Dịch vụ của bạn";

            var materials = booking.BookingDetails
                .Where(bd => bd.StaffId == staffId)
                .SelectMany(bd => bd.Service.ServiceMaterials)
                .Select(sm => new {
                    id = sm.MaterialId,
                    name = sm.Material.MaterialName,
                    unit = sm.Material.Unit,
                    standardQty = sm.Quantity
                })
                .GroupBy(m => m.id)
                .Select(g => g.First())
                .ToList();

            return Json(new { success = true, serviceName, materials });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteWithUsage(int bookingId, string note, List<MaterialUsageInput> usage)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId)) 
                return Json(new { success = false, message = "Phiên hết hạn" });

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if(booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            // 1. Trừ kho thông qua InventoryService (Hệ thống FEFO và Transaction nằm ở đây)
            var consumptions = usage?.Select(u => (u.MaterialId, u.ActualQuantity)).ToList() ?? new List<(int, double)>();
            
            // Lấy DetailId chính của KTV này (Giả định mỗi booking có 1 detail per staff trong flow hiện tại)
            var mainDetail = booking.BookingDetails.FirstOrDefault(bd => bd.StaffId == staffId);
            if (mainDetail != null && consumptions.Any())
            {
                var stockResult = await _inventoryService.DeductStockAsync(mainDetail.DetailId, consumptions);
                if (!stockResult) return Json(new { success = false, message = "Lỗi khi trừ kho nguyên liệu. Vui lòng kiểm tra lại số tồn." });
            }

            // 2. Hoàn tất Booking
            booking.Status = BookingStatus.Completed;
            booking.EndTime = DateTime.Now;
            foreach (var d in booking.BookingDetails) d.Status = DetailStatus.Completed;

            if(!string.IsNullOrEmpty(note) && booking.Customer != null)
            {
                _context.CustomerNotes.Add(new CustomerNote {
                    CustomerId = booking.CustomerId, StaffId = staffId, BookingId = bookingId, Note = note, CreatedAt = DateTime.Now
                });
            }

            // Giải phóng phòng
            foreach(var detail in booking.BookingDetails) detail.RoomNumber = null;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class MaterialUsageInput
        {
            public int MaterialId { get; set; }
            public double ActualQuantity { get; set; }
        }

        // Xin chuyển ca (Release staff and notify)
        [HttpPost]
        public async Task<IActionResult> RequestTransfer(int bookingId)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId)) return Json(new { success = false });

            var booking = await _context.Bookings.Include(b => b.BookingDetails).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if(booking == null) return Json(new { success = false, message = "Không tìm thấy booking" });

            // 1. Phục hồi trạng thái về Confirmed để Lễ tân gán người khác
            booking.Status = BookingStatus.Confirmed;
            
            // 2. Gỡ StaffId khỏi các detail của booking này
            foreach(var d in booking.BookingDetails)
            {
                if(d.StaffId == staffId) d.StaffId = null;
            }

            // 3. Ghi vào AuditLog để báo cho Lễ tân
            var staff = await _context.Staffs.FindAsync(staffId);
            _context.AuditLogs.Add(new AuditLog {
                UserName = staff?.FullName ?? "Staff",
                Action = "ShiftTransferRequest",
                EntityName = "Booking",
                EntityId = bookingId.ToString(),
                NewValues = $"Nhân viên {staff?.FullName} yêu cầu chuyển ca cho Booking #{booking.BookingCode}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã gửi yêu cầu chuyển ca tới Lễ tân." });
        }
        [HttpGet]
        public async Task<IActionResult> ConsumptionInput(int bookingId)
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
                return RedirectToAction("Login", "Auth", new { area = "Admin" });

            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service).ThenInclude(s => s.ServiceMaterials).ThenInclude(sm => sm.Material)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return NotFound();

            // Lấy thông tin Staff để hiện Team
            var staff = await _context.Staffs.Include(s => s.Specialization).FirstOrDefaultAsync(s => s.StaffId == staffId);
            ViewBag.Staff = staff;

            return View(booking);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConsumption(int bookingId, string note, List<MaterialUsageInput> usage)
        {
            // Tận dụng logic đã có trong CompleteWithUsage nhưng tách ra để linh hoạt
            return await CompleteWithUsage(bookingId, note, usage);
        }
        [HttpGet]
        public async Task<IActionResult> CustomerDetails(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Bookings).ThenInclude(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(c => c.Bookings).ThenInclude(b => b.BookingDetails).ThenInclude(bd => bd.Staff)
                .Include(c => c.Reviews)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            // Lấy thêm các ghi chú kỹ thuật từ bảng CustomerNote (nếu có)
            ViewBag.HistoryNotes = await _context.CustomerNotes
                .Include(cn => cn.Staff)
                .Where(cn => cn.CustomerId == id)
                .OrderByDescending(cn => cn.CreatedAt)
                .ToListAsync();

            return View(customer);
        }
    }
}
