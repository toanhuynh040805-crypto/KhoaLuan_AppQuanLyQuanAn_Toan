namespace QuanAnWinForm.Services;

public static class ApiConfig
{
    public static string BaseUrl { get; set; } = Load();
    public static string HubUrl => $"{BaseUrl}/hubs/order";

    private static string Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "apiurl.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "http://localhost:5000";
    }
}