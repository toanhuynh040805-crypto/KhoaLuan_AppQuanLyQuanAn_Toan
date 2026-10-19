using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Stations.AsNoTracking()
            .OrderBy(s => s.StationId)
            .Select(s => new { Id = s.StationId, Name = s.StationName })
            .ToListAsync());
}