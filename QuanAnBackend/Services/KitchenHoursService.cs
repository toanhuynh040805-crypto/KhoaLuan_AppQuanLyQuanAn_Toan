using System.Globalization;

namespace QuanAnBackend.Services;

public record KitchenHoursInfo(
    bool IsOpen,
    string OpenTime,
    string CloseTime,
    string Message,
    DateTime NowLocal);

/// <summary>
/// Giờ nhận món của bếp. Cấu hình trong appsettings: Kitchen:OpenTime, Kitchen:CloseTime, Kitchen:TimeZone.
/// </summary>
public class KitchenHoursService
{
    private readonly TimeOnly _open;
    private readonly TimeOnly _close;
    private readonly TimeZoneInfo _timeZone;

    public KitchenHoursService(IConfiguration config)
    {
        _open = ParseTime(config["Kitchen:OpenTime"], new TimeOnly(6, 0));
        _close = ParseTime(config["Kitchen:CloseTime"], new TimeOnly(22, 0));
        _timeZone = ResolveTimeZone(config["Kitchen:TimeZone"]);
    }

    public KitchenHoursInfo GetStatus(DateTime? utcNow = null)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow ?? DateTime.UtcNow, DateTimeKind.Utc),
            _timeZone);
        var time = TimeOnly.FromDateTime(nowLocal);

        // Hỗ trợ cả trường hợp giờ mở > giờ đóng (qua nửa đêm).
        var isOpen = _open <= _close
            ? time >= _open && time < _close
            : time >= _open || time < _close;

        var openText = _open.ToString("HH:mm", CultureInfo.InvariantCulture);
        var closeText = _close.ToString("HH:mm", CultureInfo.InvariantCulture);

        var message = isOpen
            ? $"Bếp nhận món đến {closeText}."
            : $"Bếp đã đóng cửa. Giờ nhận món: {openText} - {closeText}.";

        return new KitchenHoursInfo(isOpen, openText, closeText, message, nowLocal);
    }

    /// <summary>True nếu bếp đang mở; nếu đóng thì trả về câu thông báo.</summary>
    public bool IsOpenNow(out string message)
    {
        var status = GetStatus();
        message = status.Message;
        return status.IsOpen;
    }

    private static TimeOnly ParseTime(string? value, TimeOnly fallback)
        => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : fallback;

    private static TimeZoneInfo ResolveTimeZone(string? configured)
    {
        var candidates = new[]
        {
            configured,
            "SE Asia Standard Time",   // Windows
            "Asia/Ho_Chi_Minh"         // Linux / Docker
        };

        foreach (var id in candidates)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }
}