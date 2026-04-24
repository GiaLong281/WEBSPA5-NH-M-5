using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;


[Route("api/[controller]")]
[ApiController]
public class ApiServiceCategoryController : ControllerBase
{
    private readonly SpaDbContext _context;
    public ApiServiceCategoryController(SpaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.ServiceCategories
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Ok(categories);
    }
}