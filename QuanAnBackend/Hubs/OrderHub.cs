using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Hubs;

// Hub realtime dùng chung cho WinForms, Flutter Mobile và Website khách hàng.
[AllowAnonymous]
public class OrderHub : Hub
{
    private readonly AppDbContext _db;

    public OrderHub(AppDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue && Context.User.IsInRole("Customer"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"customer-{userId.Value}");
            }
            else if (userId.HasValue && (Context.User.IsInRole("QuanLy") || Context.User.IsInRole("ThuNgan")))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "order-management");
            }
        }

        await base.OnConnectedAsync();
    }

    // Group theo bo phan / role, duoc ca WinForms va Mobile su dung.
    public async Task JoinDepartmentGroup(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return;

        if (Context.User?.IsInRole("Customer") == true)
            throw new HubException("Khách hàng không được tham gia nhóm bộ phận nội bộ.");

        if (Context.User?.Identity?.IsAuthenticated == true && !Context.User.IsInRole(departmentName.Trim()))
            throw new HubException("Bạn không có quyền tham gia nhóm bộ phận này.");

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

        if (Context.User?.IsInRole("Customer") == true)
            throw new HubException("Khách hàng không được tham gia nhóm nhân viên nội bộ.");

        var userId = GetCurrentUserId();
        if (Context.User?.Identity?.IsAuthenticated == true && userId.HasValue)
        {
            var belongsToUser = await _db.Employees.AnyAsync(employee =>
                employee.UserId == userId.Value && employee.EmployeeCode == employeeCode.Trim());
            if (!belongsToUser && !Context.User.IsInRole("QuanLy"))
                throw new HubException("Bạn không có quyền tham gia nhóm nhân viên này.");
        }

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

        if (Context.User?.IsInRole("Customer") == true)
            throw new HubException("Khách hàng không được tham gia nhóm nhân viên nội bộ.");

        var currentUserId = GetCurrentUserId();
        if (Context.User?.Identity?.IsAuthenticated == true && currentUserId.HasValue && currentUserId.Value != userId && !Context.User.IsInRole("QuanLy"))
            throw new HubException("Bạn không có quyền tham gia nhóm người dùng này.");

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

        if (Context.User?.IsInRole("Customer") == true)
            throw new HubException("Khách hàng không được tham gia nhóm vai trò nội bộ.");

        if (Context.User?.Identity?.IsAuthenticated == true && !Context.User.IsInRole(role.Trim()))
            throw new HubException("Bạn không có quyền tham gia nhóm vai trò này.");

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

    public async Task JoinOrderManagementGroup()
    {
        if (Context.User?.IsInRole("Customer") == true)
            throw new HubException("Khách hàng không được tham gia nhóm quản lý đơn hàng.");

        await Groups.AddToGroupAsync(Context.ConnectionId, "order-management");
    }

    public async Task JoinCustomerGroup(int customerId)
    {
        if (customerId <= 0)
            return;

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            if (!Context.User.IsInRole("Customer"))
                throw new HubException("Chỉ tài khoản khách hàng mới được tham gia nhóm khách hàng.");

            var currentUserId = GetCurrentUserId();
            if (currentUserId != customerId)
                throw new HubException("Bạn không có quyền tham gia nhóm của khách hàng khác.");
        }
        else
        {
            throw new HubException("Cần đăng nhập để tham gia nhóm thông báo khách hàng.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"customer-{customerId}");
    }

    private int? GetCurrentUserId()
    {
        var value = Context.User?.FindFirst("UserId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var userId) && userId > 0 ? userId : null;
    }
}
