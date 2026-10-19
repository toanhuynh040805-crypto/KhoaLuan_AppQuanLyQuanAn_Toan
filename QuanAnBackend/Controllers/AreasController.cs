using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;

namespace QuanAnBackend.Controllers;

public record AreaRequest(string Name, string? Description);

[ApiController]
[Route("api/[controller]")]
public class AreasController : ControllerBase
{
    private readonly AppDbContext _db;
    public AreasController(AppDbContext db) => _db = db;

    // "Name" trong JSON trả về ánh xạ từ cột thật AreaName, để khớp với
    // AreaDto.Name bên WinForm (Models/ManagementModels.cs) mà không cần sửa Frontend.
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Areas.AsNoTracking()
            .Select(a => new { Id = a.AreaId, Name = a.AreaName, a.Description })
            .ToListAsync());

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] AreaRequest request)
    {
        var area = new Area { AreaName = request.Name, Description = request.Description };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();
        return Ok(new { Id = area.AreaId, Name = area.AreaName, area.Description });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Update(int id, [FromBody] AreaRequest request)
    {
        var area = await _db.Areas.FindAsync(id);
        if (area is null) return NotFound();

        area.AreaName = request.Name;
        area.Description = request.Description;
        await _db.SaveChangesAsync();
        return Ok(new { Id = area.AreaId, Name = area.AreaName, area.Description });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Delete(int id)
    {
        var area = await _db.Areas.Include(a => a.Tables).FirstOrDefaultAsync(a => a.AreaId == id);
        if (area is null) return NotFound();
        if (area.Tables.Any()) return BadRequest(new { message = "Khu vực đang có bàn, không thể xóa" });

        _db.Areas.Remove(area);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
