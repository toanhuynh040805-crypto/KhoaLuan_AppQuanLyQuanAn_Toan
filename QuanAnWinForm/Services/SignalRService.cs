using Microsoft.AspNetCore.SignalR.Client;
using QuanAnWinForm.Models;
using System.Text.Json;

namespace QuanAnWinForm.Services;

public class SignalRService
{
    private HubConnection? _connection;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public event Action<NewTicketPayload>? OnNewTicket;
    public event Action<ItemStatusUpdatedPayload>? OnItemStatusUpdated;
    public event Action<OrderItemCancelledPayload>? OnOrderItemCancelled;

    public async Task ConnectAndJoinDepartmentAsync(string departmentName)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(ApiConfig.HubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("NewTicket", raw =>
        {
            var payload = JsonSerializer.Deserialize<NewTicketPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) OnNewTicket?.Invoke(payload);
        });

        _connection.On<JsonElement>("ItemStatusUpdated", raw =>
        {
            var payload = JsonSerializer.Deserialize<ItemStatusUpdatedPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) OnItemStatusUpdated?.Invoke(payload);
        });

        _connection.On<JsonElement>("OrderItemCancelled", raw =>
        {
            var payload = JsonSerializer.Deserialize<OrderItemCancelledPayload>(raw.GetRawText(), JsonOpts);
            if (payload is not null) OnOrderItemCancelled?.Invoke(payload);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinDepartmentGroup", departmentName);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}