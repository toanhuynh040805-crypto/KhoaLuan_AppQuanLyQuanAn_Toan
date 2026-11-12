using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Middleware;
using QuanAnBackend.Services;

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
            if (IsCustomer())
            {
                var customerId = GetCurrentCustomerId();
                if (customerId.HasValue)
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        $"customer-{customerId.Value}");
                }
            }
            else
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    "internal-users");

                var userId = GetCurrentUserId();
                if (userId.HasValue &&
                    (Context.User.IsInRole("QuanLy") || Context.User.IsInRole("ThuNgan")))
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        "order-management");
                }
            }
        }

        await base.OnConnectedAsync();
    }

    // Group theo bo phan / role, duoc ca WinForms va Mobile su dung.
    public async Task JoinDepartmentGroup(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return;

        if (Context.User?.Identity?.IsAuthenticated != true)
            throw new HubException("Cần đăng nhập để tham gia nhóm bộ phận.");

        if (IsCustomer())
            throw new HubException("Khách hàng không được tham gia nhóm bộ phận nội bộ.");

        var normalizedDepartment = departmentName.Trim();

        // Quản lý được phép theo dõi mọi trạm/bộ phận.
        if (!Context.User!.IsInRole("QuanLy"))
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                throw new HubException("Phiên đăng nhập không hợp lệ.");

            // Bộ phận là Station của nhân viên, KHÔNG phải Role.
            // Chỉ cho nhân viên vào đúng Station đã được gán.
            var belongsToDepartment = await _db.Employees
                .AsNoTracking()
                .Include(employee => employee.Station)
                .AnyAsync(employee =>
                    employee.UserId == userId.Value &&
                    employee.Station != null &&
                    employee.Station.StationName == normalizedDepartment);

            if (!belongsToDepartment)
                throw new HubException("Bạn không có quyền tham gia nhóm bộ phận này.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"dept-{normalizedDepartment}"
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

        EnsureInternalAuthenticated();

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

        EnsureInternalAuthenticated();

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

        EnsureInternalAuthenticated();

        if (!Context.User!.IsInRole(role.Trim()))
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
        EnsureInternalAuthenticated();

        if (!Context.User!.IsInRole("QuanLy") &&
            !Context.User.IsInRole("ThuNgan"))
        {
            throw new HubException("Bạn không có quyền tham gia nhóm quản lý đơn hàng.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "order-management");
    }

    public async Task JoinCustomerGroup(int customerId)
    {
        if (customerId <= 0)
            return;

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            if (!IsCustomer())
                throw new HubException("Chỉ tài khoản khách hàng mới được tham gia nhóm khách hàng.");

            var currentCustomerId = GetCurrentCustomerId();
            if (currentCustomerId != customerId)
                throw new HubException("Bạn không có quyền tham gia nhóm của khách hàng khác.");
        }
        else
        {
            throw new HubException("Cần đăng nhập để tham gia nhóm thông báo khách hàng.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"customer-{customerId}");
    }

    private void EnsureInternalAuthenticated()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            throw new HubException("Cần đăng nhập để tham gia nhóm nội bộ.");

        if (IsCustomer())
            throw new HubException("Khách hàng không được tham gia nhóm nội bộ.");
    }

    private bool IsCustomer()
        => Context.User?.IsInRole(CustomerAccessMiddleware.CustomerRole) == true;

    private int? GetCurrentCustomerId()
        => Context.User is null ? null : CustomerTokens.GetCustomerId(Context.User);

    private int? GetCurrentUserId()
    {
        if (IsCustomer())
            return null;

        var value = Context.User?.FindFirst("UserId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var userId) && userId > 0 ? userId : null;
    }
}
