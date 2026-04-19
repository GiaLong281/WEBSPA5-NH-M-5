using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class WorkspaceController : Controller
    {
        private readonly SpaDbContext _context;

        public WorkspaceController(SpaDbContext context)
        {
            _context = context;
        }

        private async Task<Staff?> GetCurrentStaffAsync()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user?.StaffId != null)
            {
                return await _context.Staffs.FirstOrDefaultAsync(s => s.StaffId == user.StaffId);
            }
            return null;
        }

        // 1. Dashboard: Nơi làm việc chính
        public async Task<IActionResult> Dashboard()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null)
            {
                if (User.IsInRole("Admin")) return Content("Tài khoản chưa được liên kết với hồ sơ Nhân sự.");
                return RedirectToAction("Login", "Account");
            }

            ViewBag.StaffName = staff.FullName;
            ViewBag.Avatar = staff.Avatar;
            ViewBag.Position = staff.Position;

            var today = DateTime.Today;

            // Kiểm tra điểm danh hôm nay
            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.StaffId == staff.StaffId && a.Date.Date == today);
            ViewBag.Attendance = attendance;

            // Lấy danh sách task hôm nay
            var myTasks = await _context.BookingDetails
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .Include(bd => bd.Service)
                .Where(bd => bd.StaffId == staff.StaffId && bd.Booking.BookingDate.Date == today && bd.Booking.Status != BookingStatus.Cancelled)
                .OrderBy(bd => bd.Booking.StartTime)
                .ToListAsync();

            // Tính toán hiệu suất (KPI nhỏ)
            int totalCa = myTasks.Count;
            int completedCa = myTasks.Count(t => t.Status == DetailStatus.Completed);
            
            ViewBag.TotalTasks = totalCa;
            ViewBag.CompletedTasks = completedCa;
            ViewBag.Performance = totalCa > 0 ? (completedCa * 100 / totalCa) : 0;

            return View(myTasks);
        }

        // 2. Tương tác Điểm danh (Check In / Check Out)
        [HttpPost]
        public async Task<IActionResult> ToggleAttendance()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null) return Json(new { success = false, message = "Lỗi xác thực" });

            var today = DateTime.Today;
            var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.StaffId == staff.StaffId && a.Date.Date == today);

            if (attendance == null)
            {
                // Hành động Check In
                attendance = new Attendance
                {
                    StaffId = staff.StaffId,
                    Date = today,
                    CheckInTime = DateTime.Now
                };
                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();
                return Json(new { success = true, state = "checked_in", time = attendance.CheckInTime.Value.ToString("HH:mm") });
            }
            else if (attendance.CheckOutTime == null)
            {
                // Hành động Check Out
                attendance.CheckOutTime = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true, state = "checked_out", time = attendance.CheckOutTime.Value.ToString("HH:mm") });
            }

            return Json(new { success = false, message = "Bạn đã hoàn thành ca làm việc hôm nay" });
        }

        // 3. Tương tác Task (Bắt đầu, Hoàn thành, Từ chối)
        [HttpPost]
        public async Task<IActionResult> UpdateTaskStatus(int detailId, string actionType, string? note)
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null) return Json(new { success = false, message = "Not authenticated" });

            var task = await _context.BookingDetails
                .Include(bd => bd.Booking).ThenInclude(b => b.Customer)
                .FirstOrDefaultAsync(bd => bd.DetailId == detailId && bd.StaffId == staff.StaffId);

            if (task == null) return Json(new { success = false, message = "Task không tìm thấy hoặc bạn không có quyền" });

            if (actionType == "start")
            {
                task.Status = DetailStatus.InProgress;
                task.Booking.Status = BookingStatus.InProgress;
            }
            else if (actionType == "complete")
            {
                task.Status = DetailStatus.Completed;
                
                // Thu thập ghi chú
                if (!string.IsNullOrEmpty(note) && task.Booking.Customer != null)
                {
                    _context.CustomerNotes.Add(new CustomerNote {
                        CustomerId = task.Booking.Customer.CustomerId,
                        StaffId = staff.StaffId,
                        BookingId = task.Booking.BookingId,
                        Note = note,
                        CreatedAt = DateTime.Now
                    });
                }

                // Tự động kiểm tra xem Booking có nên thành Completed không?
                var otherDetails = await _context.BookingDetails
                    .Where(bd => bd.BookingId == task.BookingId && bd.DetailId != detailId)
                    .ToListAsync();
                
                if (otherDetails.All(d => d.Status == DetailStatus.Completed))
                {
                    task.Booking.Status = BookingStatus.Completed;
                }
            }
            else if (actionType == "reject")
            {
                // Trả về cho hệ thống Admin (Xóa assign staff để lễ tân assign lại)
                task.StaffId = null; 
                task.Status = DetailStatus.Pending;
                // Có thể để BookingStatus là Pending nếu cần
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // 4. Báo thiếu vật tư (Tự tạo báo cáo StockTransaction xuất kho hoặc tạo Alert)
        [HttpPost]
        public async Task<IActionResult> ReportMaterial(int materialId, decimal quantity, string note)
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null) return Json(new { success = false });

            var material = await _context.Materials.FindAsync(materialId);
            if (material == null) return Json(new { success = false, message = "Vật tư không tồn tại" });

            // Lưu lịch sử xài xuất kho / hao hụt
            var transaction = new StockTransaction
            {
                MaterialId = materialId,
                Type = "Báo thiếu/hao hụt",
                Quantity = (int)Math.Max(1, quantity), // làm tròn số lượng báo cáo hao hụt về int
                Reason = $"[{staff.FullName}] báo cáo: {note}",
                CreatedAt = DateTime.Now
            };

            // Nếu muốn cập nhật trực tiếp vô kho hàng:
            // material.QuantityInStock -= quantity;

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        
        // Modal API - lấy list Materials
        [HttpGet]
        public async Task<IActionResult> GetMaterials()
        {
            var materials = await _context.Materials.Select(m => new { id = m.MaterialId, name = m.MaterialName, unit = m.Unit }).ToListAsync();
            return Json(new { success = true, data = materials });
        }
    }
}
