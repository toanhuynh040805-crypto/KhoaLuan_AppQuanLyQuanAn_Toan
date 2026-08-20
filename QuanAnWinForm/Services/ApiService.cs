using System.Net.Http.Json;
using System.Text.Json;
using QuanAnWinForm.Models;

namespace QuanAnWinForm.Services;

// Toan bo giao tiep REST voi Backend di qua day, giong hoc ben Mobile
public class ApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiService()
    {
        _http = new HttpClient { BaseAddress = new Uri(ApiConfig.BaseUrl) };
    }

    public async Task<List<TableDto>> GetTablesAsync()
    {
        var res = await _http.GetFromJsonAsync<List<TableDto>>("/api/tables", JsonOpts);
        return res ?? new List<TableDto>();
    }

    public async Task<List<MenuItemDto>> GetMenuAsync()
    {
        var res = await _http.GetFromJsonAsync<List<MenuItemDto>>("/api/menu", JsonOpts);
        return res ?? new List<MenuItemDto>();
    }

    // Dang nhap that - dung chung cho tat ca Role (QuanLy, ThuNgan, Bep, QuayNuoc)
    public async Task<LoginResultDto?> LoginAsync(string username, string password)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<LoginResultDto>(JsonOpts);
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId)
    {
        return await _http.GetFromJsonAsync<OrderDto>($"/api/orders/{orderId}", JsonOpts);
    }

    // Thu ngan tao hoa don, method: 0 = TienMat, 1 = VietQR
    public async Task<PaymentDto?> CreatePaymentAsync(int orderId, int method)
    {
        var res = await _http.PostAsJsonAsync("/api/payments", new { orderId, method });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PaymentDto>(JsonOpts);
    }
}
