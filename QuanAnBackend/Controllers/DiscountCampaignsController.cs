using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record CampaignConditionRequest(string ConditionType, string Value1, string? Value2 = null);

// Conditions = null  -> (chỉ ở Update) giữ nguyên điều kiện hiện có.
// Conditions = []    -> chiến dịch không có điều kiện (áp dụng mọi hóa đơn).
public record CreateCampaignRequest(
    string CampaignName,
    DateTime StartDate,
    DateTime EndDate,
    decimal DiscountPercent,
    string? Note,
    int? UserId,
    List<CampaignConditionRequest>? Conditions = null);

public record UpdateCampaignRequest(
    string CampaignName,
    DateTime StartDate,
    DateTime EndDate,
    decimal DiscountPercent,
    string? Note,
    List<CampaignConditionRequest>? Conditions = null);

[ApiController]
[Route("api/[controller]")]
public class DiscountCampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    private readonly CampaignEligibilityService _eligibility;

    public DiscountCampaignsController(
        AppDbContext db,
        NotificationService notifications,
        CampaignEligibilityService eligibility)
    {
        _db = db;
        _notifications = notifications;
        _eligibility = eligibility;
    }

    // GET api/discountcampaigns -> TOÀN BỘ chiến dịch (kể cả đã hết hạn/đã tắt) kèm điều kiện,
    // dùng cho màn hình "Khuyến mãi" bên Quản lý.
    [HttpGet]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> GetAll()
    {
        var campaigns = await _db.DiscountCampaigns
            .AsNoTracking()
            .Include(c => c.Conditions)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        return Ok(campaigns.Select(MapCampaign));
    }

    // GET api/discountcampaigns/active -> chiến dịch đang hiệu lực và KHÔNG có điều kiện nào
    // (áp dụng được cho mọi hóa đơn). Giữ lại để tương thích; màn Thanh toán dùng /eligible.
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var now = DateTime.Now;
        var campaigns = await _db.DiscountCampaigns
            .Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now && !c.Conditions.Any())
            .OrderBy(c => c.CampaignName)
            .AsNoTracking()
            .Select(c => new { Id = c.CampaignId, c.CampaignName, c.DiscountPercent })
            .ToListAsync();

        return Ok(campaigns);
    }

    // GET api/discountcampaigns/eligible?customerId=5&subTotal=250000
    // Mọi chiến dịch đang hiệu lực, mỗi chiến dịch kèm Eligible (true/false) và Reason (lý do
    // chưa đủ điều kiện). Màn Thanh toán dùng để nạp ComboBox khuyến mãi.
    [HttpGet("eligible")]
    [Authorize(Roles = "QuanLy,ThuNgan,PhucVu")]
    public async Task<IActionResult> GetEligible([FromQuery] int? customerId, [FromQuery] decimal subTotal = 0)
    {
        var items = await _eligibility.GetCurrentCampaignsAsync(
            customerId,
            Math.Max(0, subTotal),
            DateTime.Now);

        return Ok(items.Select(i => new
        {
            Id = i.Campaign.CampaignId,
            i.Campaign.CampaignName,
            i.Campaign.DiscountPercent,
            i.Eligible,
            i.Reason,
            Conditions = i.Campaign.Conditions
                .OrderBy(x => x.ConditionId)
                .Select(x => CampaignConditionTypes.Describe(x.ConditionType, x.Value1, x.Value2))
                .ToList()
        }));
    }

    // GET api/discountcampaigns/public -> thông tin chiến dịch đang bật để khách xem trên Web.
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var campaigns = await _db.DiscountCampaigns
            .AsNoTracking()
            .Include(c => c.Conditions)
            .Where(c => c.IsActive)
            .OrderBy(c => c.StartDate)
            .ToListAsync();

        return Ok(campaigns.Select(MapCampaign));
    }

    [HttpPost]
    [Authorize(Roles = "QuanLy")]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
    {
        var error = ValidateBase(request.CampaignName, request.StartDate, request.EndDate, request.DiscountPercent)
                    ?? ValidateConditions(request.Conditions);
        if (error is not null)
            return BadRequest(new { message = error });

        var campaign = new DiscountCampaign
        {
            CampaignName = request.CampaignName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DiscountPercent = request.DiscountPercent,
            RequireMembership = false, // cột cũ, xem ghi chú ở DiscountCampaign.cs
            MinVisitCount = null,
            VisitLookbackDays = null,
            Note = request.Note,
            CreatedBy = request.UserId,
            IsActive = true,
            Conditions = BuildConditions(request.Conditions)
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

        var error = ValidateBase(request.CampaignName, request.StartDate, request.EndDate, request.DiscountPercent)
                    ?? ValidateConditions(request.Conditions);
        if (error is not null)
            return BadRequest(new { message = error });

        await using var tx = await _db.Database.BeginTransactionAsync();

        campaign.CampaignName = request.CampaignName.Trim();
        campaign.StartDate = request.StartDate;
        campaign.EndDate = request.EndDate;
        campaign.DiscountPercent = request.DiscountPercent;
        campaign.Note = request.Note;
        campaign.RequireMembership = false;
        campaign.MinVisitCount = null;
        campaign.VisitLookbackDays = null;

        // Có gửi danh sách điều kiện -> thay thế toàn bộ điều kiện cũ.
        if (request.Conditions is not null)
        {
            await _db.DiscountCampaignConditions
                .Where(x => x.CampaignId == id)
                .ExecuteDeleteAsync();

            var newConditions = BuildConditions(request.Conditions);
            foreach (var cond in newConditions)
                cond.CampaignId = id;
            _db.DiscountCampaignConditions.AddRange(newConditions);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

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

        // Điều kiện của chiến dịch tự xóa theo (ON DELETE CASCADE).
        _db.DiscountCampaigns.Remove(campaign);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string? ValidateBase(string? name, DateTime start, DateTime end, decimal percent)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Tên chiến dịch không được để trống";
        if (end <= start)
            return "Ngày kết thúc phải sau ngày bắt đầu";
        if (percent < 0 || percent > 100)
            return "Phần trăm giảm giá phải trong khoảng 0-100";
        return null;
    }

    private static string? ValidateConditions(List<CampaignConditionRequest>? conditions)
    {
        if (conditions is null) return null;
        if (conditions.Count > CampaignConditionTypes.All.Length)
            return "Số điều kiện vượt quá giới hạn cho phép";

        var seen = new HashSet<string>();
        foreach (var c in conditions)
        {
            var error = CampaignConditionTypes.Validate(
                c.ConditionType, c.Value1?.Trim(), c.Value2?.Trim());
            if (error is not null) return error;

            if (!seen.Add(c.ConditionType))
                return "Mỗi loại điều kiện chỉ được thêm một lần cho mỗi chiến dịch";
        }
        return null;
    }

    // Chuẩn hóa giá trị trước khi lưu (đã qua ValidateConditions).
    private static List<DiscountCampaignCondition> BuildConditions(List<CampaignConditionRequest>? requests)
    {
        var result = new List<DiscountCampaignCondition>();
        if (requests is null) return result;

        foreach (var r in requests)
        {
            var v1 = (r.Value1 ?? string.Empty).Trim();
            string? v2 = string.IsNullOrWhiteSpace(r.Value2) ? null : r.Value2.Trim();

            switch (r.ConditionType)
            {
                case CampaignConditionTypes.DayOfWeek:
                    v1 = string.Join(",", CampaignConditionTypes.ParseDays(v1).OrderBy(d => d));
                    v2 = null;
                    break;

                case CampaignConditionTypes.MemberLevel:
                case CampaignConditionTypes.Birthday:
                case CampaignConditionTypes.MinBill:
                    v2 = null;
                    break;
            }

            result.Add(new DiscountCampaignCondition
            {
                ConditionType = r.ConditionType,
                Value1 = v1,
                Value2 = v2
            });
        }
        return result;
    }

    // Dạng dữ liệu trả về chung cho GetAll và Public.
    // RequireMembership/MinVisitCount/VisitLookbackDays được suy ra từ điều kiện VisitCount
    // để Web (promotions.js) và WinForm cũ tiếp tục chạy bình thường.
    private static object MapCampaign(DiscountCampaign c)
    {
        var visit = c.Conditions.FirstOrDefault(x => x.ConditionType == CampaignConditionTypes.VisitCount);
        int? minVisit = null;
        int? lookback = null;
        if (visit is not null)
        {
            if (int.TryParse(visit.Value1, out var mv)) minVisit = mv;
            if (int.TryParse(visit.Value2, out var lb)) lookback = lb;
        }

        return new
        {
            Id = c.CampaignId,
            c.CampaignName,
            c.StartDate,
            c.EndDate,
            c.DiscountPercent,
            RequireMembership = visit is not null,
            MinVisitCount = minVisit,
            VisitLookbackDays = lookback,
            c.IsActive,
            c.Note,
            Conditions = c.Conditions
                .OrderBy(x => x.ConditionId)
                .Select(x => new
                {
                    x.ConditionType,
                    x.Value1,
                    x.Value2,
                    Description = CampaignConditionTypes.Describe(x.ConditionType, x.Value1, x.Value2)
                })
                .ToList()
        };
    }
}