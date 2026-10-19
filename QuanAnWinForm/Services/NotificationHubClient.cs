using Microsoft.AspNetCore.SignalR.Client;
using QuanAnWinForm.Models;
using System.Text.Json;

namespace QuanAnWinForm.Services;

// Ket noi SignalR RIENG cho thong bao noi bo (chuong 🔔) - tach biet voi
// SignalRService.cs (dung cho man hinh Bep/Quay nuoc, chi ket noi/ngat khi
// vao/roi man hinh do). Ket noi nay chi mo 1 LAN DUY NHAT ngay sau khi dang
// nhap (xem MainForm.cs) va giu nguyen xuyen suot phien lam viec, bat ke dang
// o man hinh nao trong app.
public class NotificationHubClient
{
    private HubConnection? _connection;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public event Action<NotificationDto>? OnNewNotification;

    // Sự kiện realtime dùng chung cho các màn hình (vd Sơ đồ bàn) - khai báo STATIC để
    // UserControl chỉ cần += mà không phải giữ tham chiếu tới MainForm. Backend gửi các
    // sự kiện này cho TẤT CẢ client nên không cần join nhóm. Lưu ý: được gọi từ luồng
    // nền của SignalR -> nơi đăng ký phải tự BeginInvoke về luồng giao diện.
    public static event Action<TableStatusUpdatedPayload>? TableStatusUpdated;
    public static event Action<ItemStatusUpdatedPayload>? ItemStatusUpdated;
    public static event Action<OrderItemCancelledPayload>? OrderItemCancelled;

    public async Task ConnectAsync(int userId, string roleName)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(ApiConfig.HubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("NewNotification", raw =>
        {
            var noti = JsonSerializer.Deserialize<NotificationDto>(raw.GetRawText(), JsonOpts);
            if (noti is not null) OnNewNotification?.Invoke(noti);
        });

        _connection.On<JsonElement>("TableStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<TableStatusUpdatedPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) TableStatusUpdated?.Invoke(payload);
        });

        _connection.On<JsonElement>("ItemStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<ItemStatusUpdatedPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) ItemStatusUpdated?.Invoke(payload);
        });

        _connection.On<JsonElement>("OrderItemCancelled", raw =>
        {
            var payload = JsonSerializer.Deserialize<OrderItemCancelledPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) OrderItemCancelled?.Invoke(payload);
        });

        // WithAutomaticReconnect() chi tu dong noi lai DUONG TRUYEN, KHONG tu
        // dong join lai group cu -> phai tu join lai thu cong sau moi lan Reconnected.
        _connection.Reconnected += async _ => await JoinGroupsAsync(userId, roleName);

        await _connection.StartAsync();
        await JoinGroupsAsync(userId, roleName);
    }

    private async Task JoinGroupsAsync(int userId, string roleName)
    {
        if (_connection is null) return;
        await _connection.InvokeAsync("JoinUserGroup", userId);
        await _connection.InvokeAsync("JoinRoleGroup", roleName);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}

// Khớp sự kiện "TableStatusUpdated" Backend gửi: { tableId, tableName, status }
// status theo enum TableStatusEnum (0=Trống, 1=Đang phục vụ, 2=Đã đặt, 3=Đang dọn dẹp).
public class TableStatusUpdatedPayload
{
    public int TableId { get; set; }
    public string TableName { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(TableStatusJsonConverter))]
    public int Status { get; set; }
}
// Chấp nhận status dạng số (0..3) HOẶC chuỗi ("Trong", "DangPhucVu", "DaDat", "DangDonDep")
// để một payload lạ từ Backend không làm văng exception trong handler SignalR.
public class TableStatusJsonConverter : System.Text.Json.Serialization.JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32();
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (int.TryParse(text, out var n)) return n;
            return text?.Trim().ToLowerInvariant() switch
            {
                "dangphucvu" => 1,
                "dadat" => 2,
                "dangdondep" => 3,
                _ => 0
            };
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}