using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;
    public TablesController(AppDbContext db) => _db = db;

    // GET api/tables -> phục vụ vẽ sơ đồ bàn trên Mobile
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tables = await _db.Tables.AsNoTracking().ToListAsync();
        return Ok(tables);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TableStatus status)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();

        table.Status = status;
        await _db.SaveChangesAsync();
        return Ok(table);
    }
}
