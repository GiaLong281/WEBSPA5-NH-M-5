using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Controllers
{
    public class BranchesController : Controller
    {
        private readonly SpaDbContext _context;

        public BranchesController(SpaDbContext context)
        {
            _context = context;
        }

        // GET: Branches
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .Where(b => b.IsActive) // chỉ hiển thị chi nhánh đang hoạt động
                .OrderBy(b => b.BranchName)
                .ToListAsync();
            return View(branches);
        }

        // GET: Branches/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }
    }
}