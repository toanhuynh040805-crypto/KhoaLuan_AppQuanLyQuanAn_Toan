namespace QuanAnWinForm.Utils;

public static class TimeDisplay
{
    public static string FormatElapsed(DateTime value)
    {
        var elapsed = DateTime.Now - value;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed.TotalMinutes < 1)
            return "chưa đến 1 phút";

        if (elapsed.TotalHours < 1)
            return $"{(int)elapsed.TotalMinutes} phút";

        if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            var minutes = elapsed.Minutes;
            return minutes > 0
                ? $"{hours} giờ {minutes} phút"
                : $"{hours} giờ";
        }

        var days = (int)elapsed.TotalDays;
        var remainHours = elapsed.Hours;
        return remainHours > 0
            ? $"{days} ngày {remainHours} giờ"
            : $"{days} ngày";
    }

    public static string FormatElapsedCompact(DateTime value)
    {
        var elapsed = DateTime.Now - value;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed.TotalMinutes < 1)
            return "0ph";

        if (elapsed.TotalHours < 1)
            return $"{(int)elapsed.TotalMinutes}ph";

        if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            var minutes = elapsed.Minutes;
            return minutes > 0
                ? $"{hours}h{minutes}"
                : $"{hours}h";
        }

        var days = (int)elapsed.TotalDays;
        var remainHours = elapsed.Hours;
        var remainMinutes = elapsed.Minutes;

        if (remainHours > 0 && remainMinutes > 0)
            return $"{days}n{remainHours}h{remainMinutes}";
        if (remainHours > 0)
            return $"{days}n{remainHours}h";
        if (remainMinutes > 0)
            return $"{days}n{remainMinutes}ph";

        return $"{days}n";
    }

    public static string FormatAgo(DateTime value)
    {
        var elapsed = DateTime.Now - value;
        if (elapsed >= TimeSpan.Zero && elapsed.TotalMinutes < 1)
            return "vừa xong";

        return $"{FormatElapsed(value)} trước";
    }

    public static string FormatClock(DateTime value)
        => value.ToString("HH:mm");
}
