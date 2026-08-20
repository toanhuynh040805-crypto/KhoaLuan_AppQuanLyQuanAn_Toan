using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _db;
    public MenuController(AppDbContext db) => _db = db;

    // GET api/menu -> danh sách món cho Mobile gọi món
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.MenuItems
            .Include(m => m.Category)
            .Include(m => m.Department)
            .Where(m => m.IsAvailable)
            .AsNoTracking()
            .ToListAsync();
        return Ok(items);
    }
}
