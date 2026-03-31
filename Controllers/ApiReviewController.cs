using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class ApiReviewController : ControllerBase
    {
        private readonly SpaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiReviewController(SpaDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

[HttpPost]
public async Task<IActionResult> CreateReview([FromBody] CreateReviewModel model)
{
    if (!ModelState.IsValid)
        return BadRequest(new { message = "Dữ liệu không hợp lệ" });

    // ✅ SỬA: Lấy CustomerId từ claim
    var customerIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("CustomerId")?.Value;
    if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
        return BadRequest(new { message = "Không xác định được khách hàng" });

    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
    if (customer == null)
        return BadRequest(new { message = "Không tìm thấy thông tin khách hàng" });

            var service = await _context.Services.FindAsync(model.ServiceId);
            if (service == null)
                return BadRequest(new { message = "Dịch vụ không tồn tại" });

            var hasBooked = await _context.Bookings
                .Include(b => b.BookingDetails)
                .AnyAsync(b => b.CustomerId == customer.CustomerId && b.BookingDetails.Any(bd => bd.ServiceId == model.ServiceId) && b.Status == BookingStatus.Completed);

            if (!hasBooked)
            {
                return BadRequest(new { message = "Bạn chỉ có thể đánh giá sau khi đã sử dụng dịch vụ." });
            }

            var review = new Review
            {
                ServiceId = model.ServiceId,
                CustomerId = customer.CustomerId,
                Rating = model.Rating,
                Comment = model.Comment,
                CreatedAt = DateTime.Now,
                IsApproved = true
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đánh giá đã được gửi thành công!" });
        }
    }

    public class CreateReviewModel
    {
        public int ServiceId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}