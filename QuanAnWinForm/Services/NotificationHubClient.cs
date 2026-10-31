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
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private int _userId;
    private string _roleName = "";
    private bool _disconnecting;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<NotificationDto>? OnNewNotification;

    // Sự kiện realtime dùng chung cho các màn hình (vd Sơ đồ bàn) - khai báo STATIC để
    // UserControl chỉ cần += mà không phải giữ tham chiếu tới MainForm. Backend gửi các
    // sự kiện này cho TẤT CẢ client nên không cần join nhóm. Lưu ý: được gọi từ luồng
    // nền của SignalR -> nơi đăng ký phải tự BeginInvoke về luồng giao diện.
    public static event Action<TableStatusUpdatedPayload>? TableStatusUpdated;
    public static event Action<ItemStatusUpdatedPayload>? ItemStatusUpdated;
    public static event Action<OrderItemCancelledPayload>? OrderItemCancelled;
    public static event Action<PaymentRequestedPayload>? PaymentRequested;
    public static event Action<PaymentCompletedPayload>? PaymentCompleted;
    public static event Action<OrderUpdatedPayload>? OrderUpdated;
    public static event Action<ReservationUpdatedPayload>? ReservationUpdated;
    public static event Action? ConnectionRestored;

    public async Task ConnectAsync(int userId, string roleName)
    {
        _userId = userId;
        _roleName = roleName?.Trim() ?? "";
        _disconnecting = false;
        await EnsureConnectedAsync();
    }

    public async Task EnsureConnectedAsync()
    {
        if (_disconnecting || _userId <= 0 || string.IsNullOrWhiteSpace(_roleName) || IsConnected)
            return;

        await _connectGate.WaitAsync();
        try
        {
            if (_disconnecting || IsConnected)
                return;

            if (_connection is not null)
            {
                try { await _connection.DisposeAsync(); }
                catch { /* kết nối cũ đã hỏng */ }
                _connection = null;
            }

            var connection = new HubConnectionBuilder()
            .WithUrl(ApiConfig.HubUrl, options =>
            {
                var token = SessionContext.Token;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    options.AccessTokenProvider =
                        () => Task.FromResult<string?>(token);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<JsonElement>("NewNotification", raw =>
        {
            var noti = JsonSerializer.Deserialize<NotificationDto>(raw.GetRawText(), JsonOpts);
            if (noti is not null) OnNewNotification?.Invoke(noti);
        });

        connection.On<JsonElement>("TableStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<TableStatusUpdatedPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) TableStatusUpdated?.Invoke(payload);
        });

        connection.On<JsonElement>("ItemStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<ItemStatusUpdatedPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) ItemStatusUpdated?.Invoke(payload);
        });

        connection.On<JsonElement>("OrderItemCancelled", raw =>
        {
            var payload = JsonSerializer.Deserialize<OrderItemCancelledPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) OrderItemCancelled?.Invoke(payload);
        });

        connection.On<JsonElement>("PaymentRequested", raw =>
        {
            var payload = JsonSerializer.Deserialize<PaymentRequestedPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null) PaymentRequested?.Invoke(payload);
        });

        connection.On<JsonElement>("PaymentCompleted", raw =>
        {
            var payload = JsonSerializer.Deserialize<PaymentCompletedPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null) PaymentCompleted?.Invoke(payload);
        });

        connection.On<JsonElement>("OrderUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<OrderUpdatedPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null) OrderUpdated?.Invoke(payload);
        });

        connection.On<JsonElement>("ReservationUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<ReservationUpdatedPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null) ReservationUpdated?.Invoke(payload);
        });

        // AutomaticReconnect chỉ chạy sau khi đã kết nối thành công.
        // Sau reconnect phải join lại group và yêu cầu các màn tải bù dữ liệu
        // có thể đã bị bỏ lỡ trong khoảng mất mạng.
        connection.Reconnected += async _ =>
        {
            try
            {
                await JoinGroupsAsync(connection, _userId, _roleName);
                ConnectionRestored?.Invoke();
            }
            catch
            {
                // Health-check ở MainForm sẽ thử lại.
            }
        };

        _connection = connection;

        try
        {
            await connection.StartAsync();
            await JoinGroupsAsync(connection, _userId, _roleName);
            ConnectionRestored?.Invoke();
        }
        catch
        {
            if (ReferenceEquals(_connection, connection))
                _connection = null;

            try { await connection.DisposeAsync(); } catch { }
            throw;
        }
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private static async Task JoinGroupsAsync(
        HubConnection connection,
        int userId,
        string roleName)
    {
        if (connection.State != HubConnectionState.Connected)
            return;

        await connection.InvokeAsync("JoinUserGroup", userId);
        await connection.InvokeAsync("JoinRoleGroup", roleName);
    }

    public async Task DisconnectAsync()
    {
        _disconnecting = true;
        _userId = 0;
        _roleName = "";

        await _connectGate.WaitAsync();
        try
        {
            var connection = _connection;
            _connection = null;
            if (connection is not null)
                await connection.DisposeAsync();
        }
        finally
        {
            _connectGate.Release();
        }
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

public class PaymentRequestedPayload
{
    public int? TableId { get; set; }
    public string TableName { get; set; } = "";
    public int OrderId { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PaymentCompletedPayload
{
    public int? TableId { get; set; }
    public int OrderId { get; set; }
    public int InvoiceId { get; set; }
    public decimal TotalAmount { get; set; }
}


public class OrderUpdatedPayload
{
    public int OrderId { get; set; }
}


public class ReservationUpdatedPayload
{
    public int ReservationId { get; set; }
    public string Status { get; set; } = "";
    public string Stage { get; set; } = "";
    public DateTime? ReservationTime { get; set; }
    public DateTime? HoldUntil { get; set; }
}
