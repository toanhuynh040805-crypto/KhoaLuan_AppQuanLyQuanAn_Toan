using Microsoft.AspNetCore.SignalR;

namespace QuanAnBackend.Hubs;

// Hub realtime dung chung cho WinForms va Flutter Mobile.
public class OrderHub : Hub
{
    // Group theo bo phan / role, duoc ca WinForms va Mobile su dung.
    public async Task JoinDepartmentGroup(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"dept-{departmentName.Trim()}"
        );
    }

    public async Task LeaveDepartmentGroup(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"dept-{departmentName.Trim()}"
        );
    }

    // Group theo EmployeeCode - contract cua Flutter Mobile.
    public async Task JoinEmployeeGroup(string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"employee-{employeeCode.Trim()}"
        );
    }

    public async Task LeaveEmployeeGroup(string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"employee-{employeeCode.Trim()}"
        );
    }

    // Group theo UserId/Role - contract cua Backend/WinForms moi.
    public async Task JoinUserGroup(int userId)
    {
        if (userId <= 0)
            return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"user-{userId}"
        );
    }

    public async Task LeaveUserGroup(int userId)
    {
        if (userId <= 0)
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"user-{userId}"
        );
    }

    public async Task JoinRoleGroup(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return;

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"role-{role.Trim()}"
        );
    }

    public async Task LeaveRoleGroup(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"role-{role.Trim()}"
        );
    }
}
