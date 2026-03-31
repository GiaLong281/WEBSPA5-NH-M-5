using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiServiceController : ControllerBase
    {
        private readonly SpaDbContext _context;

        public ApiServiceController(SpaDbContext context)
        {
            _context = context;
        }

        // GET: api/ApiService
        [HttpGet]
        public async Task<IActionResult> GetServices(
            string? keyword = null,
            int? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            bool? isActive = true,
            int page = 1,
            int pageSize = 9)
        {
            var query = _context.Services
                .Include(s => s.Category)
                .Where(s => !isActive.HasValue || s.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(s => s.ServiceName.Contains(keyword));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(s => s.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(s => s.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(s => s.Price <= maxPrice.Value);
            }

            var totalItems = await query.CountAsync();

            var services = await query
                .OrderBy(s => s.ServiceName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.ServiceId,
                    s.ServiceName,
                    s.Price,
                    s.Duration,
                    s.Image,
                    s.IsActive,
                    s.Description,
                    CategoryName = s.Category != null ? s.Category.Name : string.Empty
                })
                .ToListAsync();

            return Ok(new
            {
                items = services,
                totalItems = totalItems,
                totalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                currentPage = page
            });
        }

        // GET: api/ApiService/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetServiceDetail(int id)
        {
            var service = await _context.Services
                .Include(s => s.Category)
                .Include(s => s.Reviews)
                    .ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null)
                return NotFound();

            var result = new
            {
                service.ServiceId,
                service.ServiceName,
                service.Description,
                service.Price,
                service.Duration,
                service.Image,
                service.IsActive,
                CategoryName = service.Category?.Name,
                Reviews = service.Reviews
                    .Where(r => r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.Rating,
                        r.Comment,
                        r.CreatedAt,
                        CustomerName = r.Customer?.FullName,
                        CustomerAvatar = (string?)null
                    })
            };

            return Ok(result);
        }
    }
}