using System;

namespace QuanAnWinForm.Models;

public class AreaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Type { get; set; }

    public string TypeLabel => Type == 1 ? "Thức uống" : "Món ăn";
    public override string ToString() => Name;
}

public class StationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}

public class EmployeeDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string EmployeeCode { get; set; } = "";
    public int? StationId { get; set; }
    public string? StationName { get; set; }
    public bool IsActive { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";

    public override string ToString() => FullName;
}

public class ShiftDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class ShiftAssignmentDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = "";
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DateTime WorkDate { get; set; }

    public int? AreaId { get; set; }
    public string? AreaName { get; set; }

    public int Status { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
}

public class StaffTaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int AssignedToUserId { get; set; }
    public string AssignedToName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public int Status { get; set; }
}

public class OrderItemStatusDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string TableName { get; set; } = "";
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public int KitchenStatus { get; set; }
    public bool IsTakeaway { get; set; }
    public string? CancelReason { get; set; }
    public byte[]? RowVer { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateOrderItemStatusRequestDto
{
    public int NewStatus { get; set; }
    public int? UserId { get; set; }
    public string? CancelReason { get; set; }
    public byte[]? RowVer { get; set; }
}

public class OrderItemCancelledPayload
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string TableName { get; set; } = "";
    public string MenuItemName { get; set; } = "";
    public string? Reason { get; set; }
}