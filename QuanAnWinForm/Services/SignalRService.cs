using Microsoft.AspNetCore.SignalR.Client;
using QuanAnWinForm.Models;
using System.Diagnostics;
using System.Text.Json;

namespace QuanAnWinForm.Services;

public sealed class SignalRService
{
    private HubConnection? _connection;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private CancellationTokenSource _lifetimeCts = new();
    private string? _departmentName;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public event Action<NewTicketPayload>? OnNewTicket;
    public event Action<ItemStatusUpdatedPayload>? OnItemStatusUpdated;
    public event Action<OrderItemCancelledPayload>? OnOrderItemCancelled;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAndJoinDepartmentAsync(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            throw new ArgumentException("Department name is required.", nameof(departmentName));

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected &&
                string.Equals(_departmentName, departmentName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DisposeConnectionCoreAsync();

            _departmentName = departmentName;
            _lifetimeCts.Dispose();
            _lifetimeCts = new CancellationTokenSource();

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
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            RegisterHandlers(connection);

            connection.Reconnected += async _ =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_departmentName))
                    {
                        await connection.InvokeAsync(
                            "JoinDepartmentGroup",
                            _departmentName);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"SignalR rejoin department failed: {ex.Message}");
                }
            };

            connection.Closed += ex =>
            {
                if (ex is not null)
                    Debug.WriteLine($"SignalR closed: {ex.Message}");
                return Task.CompletedTask;
            };

            _connection = connection;

            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _lifetimeCts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            await connection.StartAsync(timeoutCts.Token);
            await connection.InvokeAsync(
                "JoinDepartmentGroup",
                departmentName,
                timeoutCts.Token);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<JsonElement>("NewTicket", raw =>
        {
            var payload = JsonSerializer.Deserialize<NewTicketPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null)
                OnNewTicket?.Invoke(payload);
        });

        connection.On<JsonElement>("ItemStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<ItemStatusUpdatedPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null)
                OnItemStatusUpdated?.Invoke(payload);
        });

        connection.On<JsonElement>("OrderItemCancelled", raw =>
        {
            var payload = JsonSerializer.Deserialize<OrderItemCancelledPayload>(
                raw.GetRawText(),
                JsonOpts);
            if (payload is not null)
                OnOrderItemCancelled?.Invoke(payload);
        });
    }

    public async Task DisconnectAsync()
    {
        await _connectGate.WaitAsync();
        try
        {
            if (!_lifetimeCts.IsCancellationRequested)
                _lifetimeCts.Cancel();

            await DisposeConnectionCoreAsync();
            _departmentName = null;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task DisposeConnectionCoreAsync()
    {
        var connection = _connection;
        _connection = null;

        if (connection is null)
            return;

        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR dispose ignored: {ex.Message}");
        }
    }
}
