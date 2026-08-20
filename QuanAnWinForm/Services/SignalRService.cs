using Microsoft.AspNetCore.SignalR.Client;
using QuanAnWinForm.Models;
using System.Text.Json;

namespace QuanAnWinForm.Services;

// Ket noi realtime toi OrderHub ben Backend.
// Man hinh Bep/Quay nuoc dung service nay de nhan phieu moi ngay lap tuc,
// khong can bam F5 hay polling lien tuc.
public class SignalRService
{
    private HubConnection? _connection;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Su kien ban WinForm (KitchenForm) dang ky de nhan phieu moi
    public event Action<NewTicketPayload>? OnNewTicket;

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

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinDepartmentGroup", departmentName);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
