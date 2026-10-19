using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Controllers;

// "Type" nhận/trả về dạng số nguyên (0 = MonAn, 1 = ThucUong), khớp cách
// System.Text.Json mặc định chuyển đổi enum <-> số, và khớp cách các
// Controller khác trong project đang xử lý enum (KitchenStatus, TableStatus...).
public record CategoryRequest(string Name, CategoryType Type);

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    // GET api/categories -> dùng cho tab lọc trong màn Quản lý thực đơn
    // VÀ dropdown "Loại món" trong form Thêm/Sửa món ăn, thức uống.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Categories.AsNoTracking()
            .OrderBy(c => c.CategoryId)
            .Select(c => new { Id = c.CategoryId, Name = c.CategoryName, Type = c.CategoryType })
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Vui lòng nhập tên loại món" });

        var category = new Category { CategoryName = request.Name.Trim(), CategoryType = request.Type };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return Ok(new { Id = category.CategoryId, Name = category.CategoryName, Type = category.CategoryType });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Vui lòng nhập tên loại món" });

        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        category.CategoryName = request.Name.Trim();
        category.CategoryType = request.Type;
        await _db.SaveChangesAsync();
        return Ok(new { Id = category.CategoryId, Name = category.CategoryName, Type = category.CategoryType });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.MenuItems).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound();
        if (category.MenuItems.Any())
            return BadRequest(new { message = "Loại món đang có món ăn/thức uống, không thể xóa" });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}