using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaN5.Models;  // ← PHẢI CÓ DÒNG NÀY
using System.Linq;
using System.Threading.Tasks;

namespace SpaN5.Controllers
{
    public class ServiceCategoryController : Controller
    {
        private readonly SpaDbContext _context;

        public ServiceCategoryController(SpaDbContext context)
        {
            _context = context;
        }

        // Danh sách
        public async Task<IActionResult> Index()
        {
            var categories = await _context.ServiceCategories
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        // Thêm mới - GET
        public IActionResult Create()
        {
            return View();
        }

        // Thêm mới - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Name)
        {
            if (string.IsNullOrEmpty(Name))
            {
                TempData["Error"] = "Vui lòng nhập tên loại dịch vụ";
                return RedirectToAction(nameof(Create));
            }

            // Kiểm tra trùng
            var exists = await _context.ServiceCategories
                .AnyAsync(c => c.Name == Name);
                
            if (exists)
            {
                TempData["Error"] = "Tên loại dịch vụ đã tồn tại";
                return RedirectToAction(nameof(Create));
            }
            var category = new ServiceCategory
             {
                 Name = Name
             };

            _context.ServiceCategories.Add(category);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Thêm loại dịch vụ thành công!";
            return RedirectToAction(nameof(Index));
        }

        // Sửa - GET
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.ServiceCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // Sửa - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string Name)
        {
            var category = await _context.ServiceCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(Name))
            {
                TempData["Error"] = "Vui lòng nhập tên loại dịch vụ";
                return View(category);
            }

            // Kiểm tra trùng
            var exists = await _context.ServiceCategories
                .AnyAsync(c => c.Name == Name && c.Id != id);
                
            if (exists)
            {
                TempData["Error"] = "Tên loại dịch vụ đã tồn tại";
                return View(category);
            }

            category.Name = Name;
            _context.ServiceCategories.Update(category);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Cập nhật loại dịch vụ thành công!";
            return RedirectToAction(nameof(Index));
        }

        // Xóa - GET
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.ServiceCategories
                .Include(c => c.Services)
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (category == null)
            {
                return NotFound();
            }
            
            return View(category);
        }

        // Xóa - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.ServiceCategories.FindAsync(id);
            if (category != null)
            {
                _context.ServiceCategories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa loại dịch vụ thành công!";
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
}