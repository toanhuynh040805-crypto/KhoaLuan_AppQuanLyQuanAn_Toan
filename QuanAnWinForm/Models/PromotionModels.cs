namespace QuanAnWinForm.Models;

// Hằng số + nhãn các loại điều kiện (khớp CampaignConditionTypes bên Backend).
public static class CampaignConditionTypes
{
    public const string VisitCount = "VisitCount";
    public const string MemberLevel = "MemberLevel";
    public const string Birthday = "Birthday";
    public const string MinBill = "MinBill";
    public const string TimeRange = "TimeRange";
    public const string DayOfWeek = "DayOfWeek";

    // Thứ tự hiển thị trong ComboBox "Thêm điều kiện".
    public static readonly (string Type, string Label)[] All =
    {
        (VisitCount, "Khách quen (số lần ghé)"),
        (MemberLevel, "Hạng thành viên tối thiểu"),
        (Birthday, "Sinh nhật khách"),
        (MinBill, "Hóa đơn tối thiểu"),
        (TimeRange, "Khung giờ trong ngày"),
        (DayOfWeek, "Thứ trong tuần"),
    };

    public static string LabelOf(string type)
    {
        foreach (var item in All)
            if (item.Type == type) return item.Label;
        return type;
    }
}

// Một điều kiện của chiến dịch (Value1/Value2 xem ghi chú ở Backend/Models/DiscountCampaignCondition.cs).
public class CampaignConditionDto
{
    public string ConditionType { get; set; } = "";
    public string Value1 { get; set; } = "";
    public string? Value2 { get; set; }

    // Mô tả tiếng Việt do Backend trả về, dùng để hiển thị nhãn.
    public string Description { get; set; } = "";
}

public class DiscountCampaignDto
{
    public int Id { get; set; }
    public string CampaignName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool RequireMembership { get; set; }
    public bool IsActive { get; set; }
    public string? Note { get; set; }
    public List<CampaignConditionDto> Conditions { get; set; } = new();
}

// Giữ lại để tương thích (endpoint /active cũ). Màn Thanh toán dùng EligibleCampaignDto.
public class ActiveCampaignDto
{
    public int Id { get; set; }
    public string CampaignName { get; set; } = "";
    public decimal DiscountPercent { get; set; }

    public override string ToString() => $"{CampaignName} (-{DiscountPercent:0.#}%)";
}

// Dùng cho ComboBox chọn khuyến mãi lúc thanh toán: mỗi chiến dịch kèm trạng thái
// đủ/không đủ điều kiện với khách + tạm tính hiện tại.
public class EligibleCampaignDto
{
    public int Id { get; set; }
    public string CampaignName { get; set; } = "";
    public decimal DiscountPercent { get; set; }
    public bool Eligible { get; set; }
    public string? Reason { get; set; }
    public List<string> Conditions { get; set; } = new();

    public override string ToString()
        => Eligible
            ? $"{CampaignName} (-{DiscountPercent:0.#}%)"
            : $"{CampaignName} (-{DiscountPercent:0.#}%) - chưa đủ điều kiện";
}