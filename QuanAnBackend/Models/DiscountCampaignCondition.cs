using System.Globalization;

namespace QuanAnBackend.Models;

// Một điều kiện của chiến dịch khuyến mãi. Chiến dịch có thể có NHIỀU điều kiện,
// hóa đơn phải thỏa TẤT CẢ thì mới được áp dụng. Chiến dịch không có điều kiện
// nào thì áp dụng cho mọi hóa đơn.
//
// Cách lưu giá trị theo từng loại (Value1 / Value2):
//   VisitCount  : Value1 = số lần ghé tối thiểu,  Value2 = trong X ngày gần nhất
//   MemberLevel : Value1 = tên hạng tối thiểu (ThanhVien/Bac/Vang/KimCuong)
//   Birthday    : Value1 = "Day" (đúng ngày sinh) hoặc "Month" (trong tháng sinh)
//   MinBill     : Value1 = số tiền tạm tính tối thiểu (VNĐ)
//   TimeRange   : Value1 = giờ bắt đầu "HH:mm", Value2 = giờ kết thúc "HH:mm"
//   DayOfWeek   : Value1 = danh sách thứ, cách nhau dấu phẩy (0=CN, 1=T2 ... 6=T7)
public class DiscountCampaignCondition
{
    public int ConditionId { get; set; }

    public int CampaignId { get; set; }
    public DiscountCampaign? Campaign { get; set; }

    public string ConditionType { get; set; } = string.Empty;
    public string Value1 { get; set; } = string.Empty;
    public string? Value2 { get; set; }
}

public static class CampaignConditionTypes
{
    public const string VisitCount = "VisitCount";
    public const string MemberLevel = "MemberLevel";
    public const string Birthday = "Birthday";
    public const string MinBill = "MinBill";
    public const string TimeRange = "TimeRange";
    public const string DayOfWeek = "DayOfWeek";

    public static readonly string[] All =
    {
        VisitCount, MemberLevel, Birthday, MinBill, TimeRange, DayOfWeek
    };

    // Nhãn thứ hiển thị, index trùng System.DayOfWeek (0 = Chủ nhật).
    private static readonly string[] DayNames = { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

    public static bool TryParseTime(string? text, out TimeSpan time)
        => TimeSpan.TryParseExact((text ?? string.Empty).Trim(), @"hh\:mm",
            CultureInfo.InvariantCulture, out time);

    public static bool TryParseAmount(string? text, out decimal amount)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    // Trả về tập thứ hợp lệ (0..6) từ chuỗi "1,2,3".
    public static HashSet<int> ParseDays(string? csv)
    {
        var set = new HashSet<int>();
        foreach (var part in (csv ?? string.Empty).Split(
                     ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
                && d is >= 0 and <= 6)
            {
                set.Add(d);
            }
        }
        return set;
    }

    // Trả về null nếu hợp lệ, ngược lại trả về thông báo lỗi tiếng Việt.
    public static string? Validate(string? type, string? value1, string? value2)
    {
        switch (type)
        {
            case VisitCount:
            {
                var okVisits = int.TryParse(value1, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var visits);
                var okDays = int.TryParse(value2, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var days);
                if (!okVisits || !okDays || visits <= 0 || days <= 0 || visits > 1000 || days > 3650)
                    return "Điều kiện khách quen: số lần ghé và số ngày phải là số nguyên dương hợp lý";
                return null;
            }

            case MemberLevel:
            {
                if (string.IsNullOrWhiteSpace(value1) ||
                    !Enum.GetNames<MemberLevel>().Contains(value1))
                    return "Điều kiện hạng thành viên không hợp lệ";
                return null;
            }

            case Birthday:
            {
                if (value1 != "Day" && value1 != "Month")
                    return "Điều kiện sinh nhật phải là 'Day' (đúng ngày) hoặc 'Month' (trong tháng)";
                return null;
            }

            case MinBill:
            {
                if (!TryParseAmount(value1, out var amount) || amount <= 0 || amount > 1_000_000_000m)
                    return "Điều kiện hóa đơn tối thiểu phải là số tiền lớn hơn 0";
                return null;
            }

            case TimeRange:
            {
                if (!TryParseTime(value1, out var from) || !TryParseTime(value2, out var to))
                    return "Điều kiện khung giờ phải có dạng HH:mm";
                if (from == to)
                    return "Giờ bắt đầu và giờ kết thúc không được trùng nhau";
                return null;
            }

            case DayOfWeek:
            {
                var parts = (value1 ?? string.Empty).Split(
                    ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0 || ParseDays(value1).Count == 0)
                    return "Điều kiện thứ trong tuần phải chọn ít nhất 1 ngày";
                foreach (var p in parts)
                {
                    if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
                        || d < 0 || d > 6)
                        return "Điều kiện thứ trong tuần chứa giá trị không hợp lệ";
                }
                return null;
            }

            default:
                return $"Loại điều kiện không được hỗ trợ: {type}";
        }
    }

    // Mô tả ngắn tiếng Việt, dùng để hiển thị nhãn trên WinForm/Web.
    public static string Describe(string type, string value1, string? value2)
    {
        switch (type)
        {
            case VisitCount:
                return $"Khách ghé từ {value1} lần trong {value2} ngày gần nhất";

            case MemberLevel:
                return $"Hạng thành viên từ {LevelLabel(value1)}";

            case Birthday:
                return value1 == "Day"
                    ? "Đúng ngày sinh nhật của khách"
                    : "Trong tháng sinh nhật của khách";

            case MinBill:
                return TryParseAmount(value1, out var amount)
                    ? $"Hóa đơn từ {amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.')}đ"
                    : "Hóa đơn tối thiểu";

            case TimeRange:
                return $"Khung giờ {value1} – {value2}";

            case DayOfWeek:
                var days = ParseDays(value1)
                    .OrderBy(d => (d + 6) % 7) // T2 trước ... CN sau
                    .Select(d => DayNames[d]);
                return "Các ngày: " + string.Join(", ", days);

            default:
                return type;
        }
    }

    private static string LevelLabel(string level) => level switch
    {
        "ThanhVien" => "Thành viên",
        "Bac" => "Bạc",
        "Vang" => "Vàng",
        "KimCuong" => "Kim cương",
        _ => level
    };
}