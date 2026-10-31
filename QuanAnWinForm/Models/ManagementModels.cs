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

    // AreaId/AreaName được giữ để tương thích dữ liệu cũ.
    // AreaIds/AreaNames là nguồn mới khi một ca phụ trách nhiều khu.
    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
    public List<int> AreaIds { get; set; } = new();
    public List<string> AreaNames { get; set; } = new();

    public int Status { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public string CheckInStatus { get; set; } = "ChuaDiemDanh";
    public string CheckOutStatus { get; set; } = "ChuaKetThuc";
    public string AttendanceStatus { get; set; } = "ChuaDiemDanh";

    public string AreaLabel =>
        AreaNames.Count > 0
            ? string.Join(", ", AreaNames)
            : AreaName ?? "";

    public string AttendanceStatusLabel => AttendanceStatus switch
    {
        "VangMat" => "Vắng mặt",
        "DiTre_VeSom" => "Đi trễ & về sớm",
        "DiTre" => "Đi trễ",
        "VeSom" => "Về sớm",
        "DungGio_DuGio" => "Đúng giờ · Đủ giờ",
        "DangLam_DiTre" => LateMinutes > 0
            ? $"Đang làm · Đi trễ {LateMinutes} phút"
            : "Đang làm · Đi trễ",
        "DangLam_DungGio" => "Đang làm · Đúng giờ",
        _ => Status switch
        {
            2 => "Vắng mặt",
            3 => "Đã hoàn thành",
            1 => "Đã điểm danh",
            _ => "Đã xếp lịch",
        },
    };
}

public class AttendanceLocationSettingsDto
{
    public bool IsConfigured { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double RadiusMeters { get; set; } = 100;
    public double MaxAccuracyMeters { get; set; } = 50;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
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
    public int TableId { get; set; }
    public string TableName { get; set; } = "";
    public string MenuItemName { get; set; } = "";
    public string? Reason { get; set; }
}