using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;
using SpaN5.Services;
using System.Text.Json;

namespace SpaN5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BranchController : Controller
    {
        private readonly SpaDbContext _context;
        private readonly AuditService _audit;

        public BranchController(SpaDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // Danh sách chi nhánh (quản lý)
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches.ToListAsync();
            return View(branches);
        }

        // Thêm mới - GET
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Thêm mới - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;
                _context.Branches.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Create", "Branch", model.BranchId.ToString(), null, JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // Chỉnh sửa - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        // Chỉnh sửa - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch model)
        {
            if (id != model.BranchId) return NotFound();
            if (ModelState.IsValid)
            {
                var old = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);
                model.UpdatedAt = DateTime.Now;
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Update", "Branch", id.ToString(), JsonSerializer.Serialize(old), JsonSerializer.Serialize(model));
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // Xóa - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                _context.Branches.Remove(branch);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Branch", id.ToString(), JsonSerializer.Serialize(branch), null);
            }
            return RedirectToAction(nameof(Index));
        }

        // Bật/tắt trạng thái
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            var old = JsonSerializer.Serialize(branch);
            branch.IsActive = !branch.IsActive;
            branch.UpdatedAt = DateTime.Now;
            _context.Branches.Update(branch);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Update", "Branch", id.ToString(), old, JsonSerializer.Serialize(branch));
            return RedirectToAction(nameof(Index));
        }
    }
}