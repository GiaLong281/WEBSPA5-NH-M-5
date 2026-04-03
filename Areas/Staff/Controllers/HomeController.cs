using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using System.Security.Claims;

namespace SpaN5.Areas.Staff.Controllers
{
    [Area("Staff")]
    [Authorize(Roles = "Staff, Admin")] 
    public class HomeController : Controller
    {
        private readonly SpaDbContext _context;

        public HomeController(SpaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var staffIdClaim = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value;
            
            if (string.IsNullOrEmpty(staffIdClaim) || !int.TryParse(staffIdClaim, out int staffId))
            {
                ViewBag.Error = "Tài khoản của bạn chưa được liên kết mã Kỹ thuật viên. Vui lòng liên hệ Quản lý.";
                return View("Error");
            }

            // Lấy tất cả các Booking chưa hoàn thành/hủy, 
            // có ít nhất 1 detail thuộc về staff này hoặc chưa có ai nhận (nếu staff muốn nhận thêm)
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Where(b => b.Status != BookingStatus.Completed && b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .ToListAsync();

            // Lấy tất cả dịch vụ để hiển thị trong phần Task Dịch Vụ
            var allServices = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Services = allServices;

            return View(bookings);
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
        public async Task<IActionResult> UpdateStatus(int bookingId, BookingStatus newStatus)
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

            // Logic tự động gán staff và phòng nếu đang chuyển sang InProgress
            if (newStatus == BookingStatus.InProgress)
            {
                foreach(var detail in booking.BookingDetails)
                {
                    if (detail.StaffId == null) detail.StaffId = staffId;
                    
                    if (detail.RoomNumber == null)
                    {
                        // Tìm các phòng đang bận cho dịch vụ này
                        var occupiedRooms = await _context.BookingDetails
                            .Where(bd => bd.ServiceId == detail.ServiceId && bd.Booking.Status == BookingStatus.InProgress)
                            .Select(bd => bd.RoomNumber)
                            .ToListAsync();

                        // Tìm phòng trống đầu tiên từ 1-10
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
            else if (newStatus == BookingStatus.Confirmed)
            {
                foreach(var detail in booking.BookingDetails)
                {
                    if (detail.StaffId == null) detail.StaffId = staffId;
                }
            }
            else if (newStatus == BookingStatus.Completed || newStatus == BookingStatus.Cancelled)
            {
                // Giải phóng phòng khi hoàn tất hoặc hủy
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
        public async Task<IActionResult> CheckIn(int bookingId) => await UpdateStatus(bookingId, BookingStatus.InProgress);

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
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(b => b.BookingCode.Contains(searchTerm) || 
                                         b.Customer.FullName.Contains(searchTerm) || 
                                         b.Customer.Phone.Contains(searchTerm));
            }

            var history = await query.OrderByDescending(b => b.BookingDate).ToListAsync();
            return View(history);
        }
    }
}
