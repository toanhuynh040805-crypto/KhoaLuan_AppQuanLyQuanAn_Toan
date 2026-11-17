using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

public record CampaignEligibilityItem(DiscountCampaign Campaign, bool Eligible, string? Reason);

public record CampaignApplyResult(DiscountCampaign? Campaign, string? Error);

// Nơi DUY NHẤT quyết định một chiến dịch có áp dụng được cho hóa đơn hay không.
// Dùng chung cho: danh sách khuyến mãi ở màn Thanh toán, preview và xác nhận thanh toán.
// Chiến dịch có nhiều điều kiện thì phải thỏa TẤT CẢ; không có điều kiện = áp dụng mọi hóa đơn.
public class CampaignEligibilityService
{
    private readonly AppDbContext _db;

    public CampaignEligibilityService(AppDbContext db)
    {
        _db = db;
    }

    // Tất cả chiến dịch đang bật và đang trong khoảng ngày, kèm trạng thái đủ/không đủ điều kiện.
    public async Task<List<CampaignEligibilityItem>> GetCurrentCampaignsAsync(
        int? customerId,
        decimal subTotal,
        DateTime now,
        int? excludeOrderId = null)
    {
        var campaigns = await _db.DiscountCampaigns
            .AsNoTracking()
            .Include(c => c.Conditions)
            .Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now)
            .ToListAsync();

        var customer = await LoadCustomerAsync(customerId);
        var items = new List<CampaignEligibilityItem>();

        foreach (var campaign in campaigns
                     .OrderByDescending(c => c.DiscountPercent)
                     .ThenBy(c => c.CampaignName))
        {
            var unmet = await EvaluateAsync(campaign, customer, subTotal, now, excludeOrderId);
            items.Add(new CampaignEligibilityItem(
                campaign,
                unmet.Count == 0,
                unmet.Count == 0 ? null : string.Join("; ", unmet)));
        }

        return items;
    }

    // Kiểm tra 1 chiến dịch cụ thể lúc preview/thanh toán. Error != null nghĩa là không được áp dụng.
    public async Task<CampaignApplyResult> ValidateForPaymentAsync(
        int campaignId,
        int? customerId,
        decimal subTotal,
        DateTime now,
        int? excludeOrderId = null)
    {
        var campaign = await _db.DiscountCampaigns
            .AsNoTracking()
            .Include(c => c.Conditions)
            .FirstOrDefaultAsync(c => c.CampaignId == campaignId);

        if (campaign is null)
            return new CampaignApplyResult(null, "Chương trình khuyến mãi không tồn tại");
        if (!campaign.IsActive)
            return new CampaignApplyResult(null, "Chương trình khuyến mãi đã bị tắt");
        if (now < campaign.StartDate || now > campaign.EndDate)
            return new CampaignApplyResult(null, "Chương trình khuyến mãi hiện không còn hiệu lực");

        var customer = await LoadCustomerAsync(customerId);
        var unmet = await EvaluateAsync(campaign, customer, subTotal, now, excludeOrderId);
        if (unmet.Count > 0)
        {
            return new CampaignApplyResult(
                null,
                $"Chưa đủ điều kiện áp dụng \"{campaign.CampaignName}\": {string.Join("; ", unmet)}");
        }

        return new CampaignApplyResult(campaign, null);
    }

    private Task<Customer?> LoadCustomerAsync(int? customerId)
    {
        if (!customerId.HasValue)
            return Task.FromResult<Customer?>(null);

        return _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId.Value);
    }

    // Trả về danh sách lý do KHÔNG thỏa. Danh sách rỗng = thỏa tất cả điều kiện.
    private async Task<List<string>> EvaluateAsync(
        DiscountCampaign campaign,
        Customer? customer,
        decimal subTotal,
        DateTime now,
        int? excludeOrderId)
    {
        var unmet = new List<string>();

        foreach (var cond in campaign.Conditions)
        {
            switch (cond.ConditionType)
            {
                case CampaignConditionTypes.VisitCount:
                {
                    if (customer is null)
                    {
                        unmet.Add("Cần chọn khách thành viên (điều kiện khách quen)");
                        break;
                    }

                    var okMin = int.TryParse(cond.Value1, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var minVisits);
                    var okDays = int.TryParse(cond.Value2, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var lookbackDays);
                    if (!okMin || !okDays)
                    {
                        unmet.Add("Điều kiện khách quen cấu hình không hợp lệ");
                        break;
                    }

                    var since = now.AddDays(-lookbackDays);
                    var visits = await _db.Orders
                        .AsNoTracking()
                        .Where(o => o.CustomerId == customer.CustomerId
                                    && o.Status == OrderStatus.DaThanhToan
                                    && o.OrderTime >= since
                                    && o.OrderTime < now
                                    && (excludeOrderId == null || o.OrderId != excludeOrderId))
                        .CountAsync();

                    if (visits < minVisits)
                        unmet.Add($"Khách mới ghé {visits}/{minVisits} lần trong {lookbackDays} ngày gần nhất");
                    break;
                }

                case CampaignConditionTypes.MemberLevel:
                {
                    if (customer is null)
                    {
                        unmet.Add("Cần chọn khách thành viên (điều kiện hạng thành viên)");
                        break;
                    }

                    if (!Enum.TryParse<MemberLevel>(cond.Value1, out var required))
                    {
                        unmet.Add("Điều kiện hạng thành viên cấu hình không hợp lệ");
                        break;
                    }

                    if (customer.MemberLevel < required)
                        unmet.Add(CampaignConditionTypes.Describe(cond.ConditionType, cond.Value1, cond.Value2));
                    break;
                }

                case CampaignConditionTypes.Birthday:
                {
                    if (customer is null)
                    {
                        unmet.Add("Cần chọn khách thành viên (điều kiện sinh nhật)");
                        break;
                    }

                    if (!customer.DateOfBirth.HasValue)
                    {
                        unmet.Add("Khách chưa có ngày sinh trong hệ thống");
                        break;
                    }

                    var dob = customer.DateOfBirth.Value;
                    var matchMonth = dob.Month == now.Month;
                    var matchDay = cond.Value1 != "Day" || dob.Day == now.Day;
                    if (!(matchMonth && matchDay))
                        unmet.Add(CampaignConditionTypes.Describe(cond.ConditionType, cond.Value1, cond.Value2));
                    break;
                }

                case CampaignConditionTypes.MinBill:
                {
                    if (!CampaignConditionTypes.TryParseAmount(cond.Value1, out var minAmount))
                    {
                        unmet.Add("Điều kiện hóa đơn tối thiểu cấu hình không hợp lệ");
                        break;
                    }

                    if (subTotal < minAmount)
                        unmet.Add($"Hóa đơn cần từ {Money(minAmount)} (hiện tại {Money(subTotal)})");
                    break;
                }

                case CampaignConditionTypes.TimeRange:
                {
                    if (!CampaignConditionTypes.TryParseTime(cond.Value1, out var from) ||
                        !CampaignConditionTypes.TryParseTime(cond.Value2, out var to))
                    {
                        unmet.Add("Điều kiện khung giờ cấu hình không hợp lệ");
                        break;
                    }

                    var t = now.TimeOfDay;
                    // from < to: khung trong ngày; from > to: khung qua đêm (vd 22:00 - 02:00).
                    var inRange = from < to
                        ? t >= from && t < to
                        : t >= from || t < to;

                    if (!inRange)
                        unmet.Add(CampaignConditionTypes.Describe(cond.ConditionType, cond.Value1, cond.Value2));
                    break;
                }

                case CampaignConditionTypes.DayOfWeek:
                {
                    var days = CampaignConditionTypes.ParseDays(cond.Value1);
                    if (!days.Contains((int)now.DayOfWeek))
                        unmet.Add(CampaignConditionTypes.Describe(cond.ConditionType, cond.Value1, cond.Value2));
                    break;
                }

                default:
                    unmet.Add($"Điều kiện không được hỗ trợ: {cond.ConditionType}");
                    break;
            }
        }

        return unmet;
    }

    private static string Money(decimal amount)
        => amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";
}