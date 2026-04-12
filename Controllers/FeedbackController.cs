using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly SpaDbContext _context;

        public FeedbackController(SpaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy danh sách đánh giá mới nhất, sắp xếp theo ngày tạo giảm dần
            var feedbacks = await _context.Feedbacks
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return View(feedbacks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Feedback feedback)
        {
            if (ModelState.IsValid)
            {
                feedback.CreatedAt = DateTime.Now;
                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();
                
                // Hiển thị thông báo thành công (có thể dùng TempData)
                TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá!";
                
                return RedirectToAction(nameof(Index));
            }
            
            // Nếu có lỗi, load lại trang Index và hiển thị list đánh giá cùng lỗi
            var feedbacks = await _context.Feedbacks
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
            
            return View("Index", feedbacks);
        }
    }
}
