using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;

namespace SpaN5.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BranchController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public BranchController(SpaDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Admin/Branch
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .Include(b => b.Staffs)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return View(branches);
        }

        // GET: Admin/Branch/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches
                .Include(b => b.Staffs)
                .FirstOrDefaultAsync(m => m.BranchId == id);

            if (branch == null) return NotFound();

            return View(branch);
        }

        // GET: Admin/Branch/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Branch/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    branch.Image = await SaveImage(imageFile);
                }

                branch.CreatedAt = DateTime.Now;
                branch.UpdatedAt = DateTime.Now;
                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: Admin/Branch/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        // POST: Admin/Branch/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch branch, IFormFile? imageFile)
        {
            if (id != branch.BranchId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBranch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);
                    if (existingBranch == null) return NotFound();

                    if (imageFile != null)
                    {
                        // Xóa ảnh cũ nếu có
                        if (!string.IsNullOrEmpty(existingBranch.Image))
                        {
                            DeleteImage(existingBranch.Image);
                        }
                        branch.Image = await SaveImage(imageFile);
                    }
                    else
                    {
                        branch.Image = existingBranch.Image;
                    }

                    branch.UpdatedAt = DateTime.Now;
                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.BranchId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // POST: Admin/Branch/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return Json(new { success = false, message = "Không tìm thấy chi nhánh" });

            // Xóa ảnh
            if (!string.IsNullOrEmpty(branch.Image))
            {
                DeleteImage(branch.Image);
            }

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.BranchId == id);
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            string wwwRootPath = _hostEnvironment.WebRootPath;
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            string path = Path.Combine(wwwRootPath, "uploads", "branches");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return fileName;
        }

        private void DeleteImage(string fileName)
        {
            string wwwRootPath = _hostEnvironment.WebRootPath;
            string path = Path.Combine(wwwRootPath, "uploads", "branches", fileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
