using Microsoft.AspNetCore.SignalR;

namespace QuanAnBackend.Hubs;

// Hub trung tâm: đẩy sự kiện realtime tới WinForm (Bếp/Quầy nước/Thu ngân)
// và ngược lại về Mobile (khi món đã xong, khi thanh toán thành công...)
public class OrderHub : Hub
{
    // Client (WinForm) có thể tham gia 1 "group" theo bộ phận
    // để chỉ nhận đúng thông báo dành cho bộ phận mình.
    // Ví dụ: WinForm màn hình Bếp gọi JoinDepartmentGroup("Bep")
    public async Task JoinDepartmentGroup(string departmentName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept-{departmentName}");
    }

    public async Task LeaveDepartmentGroup(string departmentName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept-{departmentName}");
    }
}
