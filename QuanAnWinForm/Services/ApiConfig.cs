namespace QuanAnWinForm.Services;

// TODO: doi thanh dia chi may chay Backend trong mang LAN quan an
public static class ApiConfig
{
    public const string BaseUrl = "http://localhost:5000";
    public const string HubUrl = $"{BaseUrl}/hubs/order";
}
