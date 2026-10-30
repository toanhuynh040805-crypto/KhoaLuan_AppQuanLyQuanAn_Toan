using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using Microsoft.AspNetCore.Authorization;
using QuanAnBackend.Services;
namespace QuanAnBackend.Controllers;

public record CreateCampaignRequest(string CampaignName, DateTime StartDate, DateTime EndDate, decimal DiscountPercent, string? Note, int? UserId);
public record UpdateCampaignRequest(string CampaignName, DateTime StartDate, DateTime EndDate, decimal DiscountPercent, string? Note);

[ApiController]
[Route("api/[controller]")]
public class DiscountCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public DiscountCampaignsController(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // GET api/discountcampaigns -> TOÀN BỘ chiến dịch (kể cả đã hết hạn/đã tắt),
    // dùng cho màn hình "Khuyến mãi" bên Quản lý để xem lại lịch sử.
    [HttpGet]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> GetAll()
    {
        var campaigns = await _db.DiscountCampaigns
            .OrderByDescending(c => c.StartDate)
            .AsNoTracking()
            .Select(c => new
            {
                Id = c.CampaignId,
                c.CampaignName,
                c.StartDate,
                c.EndDate,
                c.DiscountPercent,
                c.RequireMembership,
                c.IsActive,
                c.Note
            })
            .ToListAsync();

        return Ok(campaigns);
    }

    // GET api/discountcampaigns/active -> CHỈ những chiến dịch ĐANG hiệu lực NGAY
    // BÂY GIỜ và KHÔNG yêu cầu điều kiện "khách quen" (RequireMembership) - dùng để
    // đổ vào ComboBox chọn khuyến mãi lúc thanh toán (SalesControl). Chiến dịch yêu
    // cầu khách quen không trả về ở đây vì backend hiện chưa có module theo dõi lịch
    // sử ghé quán của khách để kiểm tra điều kiện đó (xem ghi chú ở DiscountCampaign.cs).
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var now = DateTime.Now;
        var campaigns = await _db.DiscountCampaigns
            .Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now && !c.RequireMembership)
            .OrderBy(c => c.CampaignName)
            .AsNoTracking()
            .Select(c => new { Id = c.CampaignId, c.CampaignName, c.DiscountPercent })
            .ToListAsync();

        return Ok(campaigns);
    }

    // GET api/discountcampaigns/public -> thông tin chiến dịch đang bật để khách xem.
    // Giữ nguyên /active cho màn hình thu ngân vì endpoint đó chỉ trả chiến dịch dùng được ngay.
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var campaigns = await _db.DiscountCampaigns
            .Where(c => c.IsActive)
            .OrderBy(c => c.StartDate)
            .AsNoTracking()
            .Select(c => new
            {
                Id = c.CampaignId,
                c.CampaignName,
                c.StartDate,
                c.EndDate,
                c.DiscountPercent,
                c.RequireMembership,
                c.MinVisitCount,
                c.VisitLookbackDays,
                c.Note
            })
            .ToListAsync();

        return Ok(campaigns);
    }

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CampaignName))
            return BadRequest(new { message = "Tên chiến dịch không được để trống" });
        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu" });
        if (request.DiscountPercent < 0 || request.DiscountPercent > 100)
            return BadRequest(new { message = "Phần trăm giảm giá phải trong khoảng 0-100" });

        var campaign = new DiscountCampaign
        {
            CampaignName = request.CampaignName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DiscountPercent = request.DiscountPercent,
            RequireMembership = false, // xem ghi chu pham vi o DiscountCampaign.cs
            MinVisitCount = null,
            VisitLookbackDays = null,
            Note = request.Note,
            CreatedBy = request.UserId,
            IsActive = true
        };
        _db.DiscountCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _notifications.CreateForRolesAsync(
            "Chương trình khuyến mãi mới",
            $"{campaign.CampaignName} - giảm {campaign.DiscountPercent}% (từ {campaign.StartDate:dd/MM} đến {campaign.EndDate:dd/MM})",
            NotificationType.KhuyenMai,
            new[] { "QuanLy", "ThuNgan", "PhucVu" },
            relatedEntityType: "DiscountCampaign",
            relatedEntityId: campaign.CampaignId);

        return Ok(new { Id = campaign.CampaignId });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCampaignRequest request)
    {
        var campaign = await _db.DiscountCampaigns.FindAsync(id);
        if (campaign is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.CampaignName))
            return BadRequest(new { message = "Tên chiến dịch không được để trống" });
        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu" });
        if (request.DiscountPercent < 0 || request.DiscountPercent > 100)
            return BadRequest(new { message = "Phần trăm giảm giá phải trong khoảng 0-100" });

        campaign.CampaignName = request.CampaignName.Trim();
        campaign.StartDate = request.StartDate;
        campaign.EndDate = request.EndDate;
        campaign.DiscountPercent = request.DiscountPercent;
        campaign.Note = request.Note;
        await _db.SaveChangesAsync();

        return Ok(new { Id = campaign.CampaignId });
    }

    // PUT api/discountcampaigns/5/active (body: true|false) - bật/tắt nhanh 1 chiến
    // dịch mà không cần xóa (giữ lại lịch sử để tra cứu sau này).
    [HttpPut("{id}/active")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> SetActive(int id, [FromBody] bool isActive)
    {
        var campaign = await _db.DiscountCampaigns.FindAsync(id);
        if (campaign is null) return NotFound();

                var wasActive = campaign.IsActive;
        campaign.IsActive = isActive;
        await _db.SaveChangesAsync();

        if (isActive && !wasActive)
        {
            await _notifications.CreateForRolesAsync(
                "Chương trình khuyến mãi được kích hoạt",
                $"{campaign.CampaignName} - giảm {campaign.DiscountPercent}% hiện đang áp dụng",
                NotificationType.KhuyenMai,
                new[] { "QuanLy", "ThuNgan", "PhucVu" },
                relatedEntityType: "DiscountCampaign",
                relatedEntityId: campaign.CampaignId);
        }

        return Ok(new { Id = campaign.CampaignId, campaign.IsActive });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Delete(int id)
    {
        var campaign = await _db.DiscountCampaigns.FindAsync(id);
        if (campaign is null) return NotFound();

        _db.DiscountCampaigns.Remove(campaign);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}