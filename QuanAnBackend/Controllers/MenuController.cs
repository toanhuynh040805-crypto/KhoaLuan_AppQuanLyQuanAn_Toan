using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace QuanAnBackend.Controllers;

public record MenuItemRequest(
    string Name,
    decimal Price,
    int CategoryId,
    int? DepartmentId,
    string? Unit,
    string? Description,
    bool IsAvailable);

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string ImageUrlPrefix = "/images/menu/";

    public MenuController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private string ImageFolder =>
        Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "images", "menu");

    private void DeleteImageFile(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith(ImageUrlPrefix)) return;
        var path = Path.Combine(ImageFolder, Path.GetFileName(imageUrl));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] int? categoryId, [FromQuery] bool availableOnly = false)
    {
        var query = _db.MenuItems
            .Include(m => m.Category)
            .Include(m => m.Station)
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(m => m.CategoryId == categoryId.Value);

        var rawItems = await query
            .OrderBy(m => m.MenuItemId)
            .Select(m => new
            {
                Id = m.MenuItemId,
                Name = m.ItemName,
                m.Price,
                DepartmentId = m.StationId ?? 0,
                DepartmentName = m.Station != null ? m.Station.StationName : "",
                m.CategoryId,
                CategoryName = m.Category != null ? m.Category.CategoryName : "",
                Category = m.Category != null ? m.Category.CategoryName : "",
                m.Unit,
                m.Description,
                m.ImageUrl,
                m.IsAvailable
            })
            .ToListAsync();

        // Giữ logic Mobile hiện tại: DB cũ có thể bị seed lặp, API chỉ trả
        // một bản ghi đại diện cho mỗi món trùng tên + giá + danh mục.
        var items = rawItems
            .Where(m => !string.IsNullOrWhiteSpace(m.Name))
            .GroupBy(m => (
                Name: m.Name.Trim().ToUpperInvariant(),
                m.Price,
                Category: m.Category.Trim().ToUpperInvariant()))
            .Select(g => g.OrderBy(m => m.Id).First())
            .Where(m => !availableOnly || m.IsAvailable)
            .OrderBy(m => m.Id)
            .ToList();

        return Ok(items);
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeatured()
    {
        var items = await _db.MenuItems
            .AsNoTracking()
            .Where(menuItem => menuItem.IsAvailable)
            .Select(menuItem => new
            {
                Id = menuItem.MenuItemId,
                Name = menuItem.ItemName,
                menuItem.Price,
                DepartmentId = menuItem.StationId ?? 0,
                DepartmentName = menuItem.Station != null ? menuItem.Station.StationName : "",
                menuItem.CategoryId,
                CategoryName = menuItem.Category != null ? menuItem.Category.CategoryName : "",
                Category = menuItem.Category != null ? menuItem.Category.CategoryName : "",
                menuItem.Unit,
                menuItem.Description,
                menuItem.ImageUrl,
                menuItem.IsAvailable,
                TotalQuantitySold = _db.OrderItems
                    .Where(orderItem => orderItem.MenuItemId == menuItem.MenuItemId &&
                        orderItem.Status != KitchenStatus.DaHuy)
                    .Sum(orderItem => (int?)orderItem.Quantity) ?? 0
            })
            .OrderByDescending(menuItem => menuItem.TotalQuantitySold)
            .ThenBy(menuItem => menuItem.Id)
            .Take(3)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategoryNames()
    {
        var rawCategories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.CategoryId)
            .Select(c => c.CategoryName)
            .ToListAsync();

        var categories = rawCategories
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _db.MenuItems
            .Include(x => x.Category)
            .Include(x => x.Station)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MenuItemId == id);

        if (m is null) return NotFound();

        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        var ingredients = new List<string>();
        try
        {
            if (closeConnection) await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ingredient.IngredientName
                FROM MenuItemIngredients AS recipe
                INNER JOIN Ingredients AS ingredient ON ingredient.IngredientId = recipe.IngredientId
                WHERE recipe.MenuItemId = @MenuItemId
                ORDER BY ingredient.IngredientName;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@MenuItemId";
            parameter.Value = id;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0)) ingredients.Add(reader.GetString(0));
            }
        }
        catch
        {
            // Nếu bảng MenuItemIngredients chưa tồn tại hoặc không có nguyên liệu, bỏ qua lỗi
        }
        finally
        {
            if (closeConnection && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }

        return Ok(new
        {
            Id = m.MenuItemId,
            Name = m.ItemName,
            m.Price,
            DepartmentId = m.StationId ?? 0,
            DepartmentName = m.Station != null ? m.Station.StationName : "",
            m.CategoryId,
            CategoryName = m.Category != null ? m.Category.CategoryName : "",
            Category = m.Category != null ? m.Category.CategoryName : "",
            m.Unit,
            m.Description,
            m.ImageUrl,
            m.IsAvailable,
            Ingredients = ingredients
        });
    }

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] MenuItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Vui lòng nhập tên món" });
        if (request.Price < 0)
            return BadRequest(new { message = "Giá món không hợp lệ" });
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == request.CategoryId))
            return BadRequest(new { message = "Loại món không tồn tại" });
        if (request.DepartmentId.HasValue && !await _db.Stations.AnyAsync(s => s.StationId == request.DepartmentId.Value))
            return BadRequest(new { message = "Bộ phận chế biến không tồn tại" });

        var item = new MenuItem
        {
            ItemName = request.Name.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            StationId = request.DepartmentId,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Phan" : request.Unit.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsAvailable = request.IsAvailable
        };
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { Id = item.MenuItemId });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Update(int id, [FromBody] MenuItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Vui lòng nhập tên món" });
        if (request.Price < 0)
            return BadRequest(new { message = "Giá món không hợp lệ" });

        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == request.CategoryId))
            return BadRequest(new { message = "Loại món không tồn tại" });
        if (request.DepartmentId.HasValue && !await _db.Stations.AnyAsync(s => s.StationId == request.DepartmentId.Value))
            return BadRequest(new { message = "Bộ phận chế biến không tồn tại" });

        item.ItemName = request.Name.Trim();
        item.Price = request.Price;
        item.CategoryId = request.CategoryId;
        item.StationId = request.DepartmentId;
        item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Phan" : request.Unit.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.IsAvailable = request.IsAvailable;
        await _db.SaveChangesAsync();
        return Ok(new { Id = item.MenuItemId });
    }

    [HttpPut("{id}/available")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> SetAvailable(int id, [FromBody] bool isAvailable)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();

        item.IsAvailable = isAvailable;
        await _db.SaveChangesAsync();
        return Ok(new { Id = item.MenuItemId, item.IsAvailable });
    }

    [HttpPost("{id}/image")]
    [Authorize(Roles = "QuanLy")]
    [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Chưa chọn file ảnh" });
        if (file.Length > MaxImageBytes)
            return BadRequest(new { message = "Ảnh quá lớn (tối đa 5 MB)" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = "Chỉ hỗ trợ ảnh .jpg, .jpeg, .png, .webp" });

        Directory.CreateDirectory(ImageFolder);
        var fileName = $"menu-{id}-{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var fullPath = Path.Combine(ImageFolder, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var oldUrl = item.ImageUrl;
        item.ImageUrl = ImageUrlPrefix + fileName;
        await _db.SaveChangesAsync();
        DeleteImageFile(oldUrl);

        return Ok(new { Id = item.MenuItemId, item.ImageUrl });
    }

    [HttpDelete("{id}/image")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();

        var oldUrl = item.ImageUrl;
        item.ImageUrl = null;
        await _db.SaveChangesAsync();
        DeleteImageFile(oldUrl);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.MenuItems.FindAsync(id);
        if (item is null) return NotFound();
        var isUsed = await _db.OrderItems.AnyAsync(oi => oi.MenuItemId == id);
        if (isUsed)
            return BadRequest(new { message = "Món này đã có trong đơn hàng, không thể xóa (hãy chọn Hết hàng thay vì xóa)" });
        var oldImage = item.ImageUrl;
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync();
        DeleteImageFile(oldImage);
        return NoContent();
    }
}
