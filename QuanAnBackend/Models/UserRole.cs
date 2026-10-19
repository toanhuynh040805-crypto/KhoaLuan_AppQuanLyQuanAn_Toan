namespace QuanAnBackend.Models;

// Khớp bảng UserRoles (quan hệ N-N User <-> Role) trong QuanAnManagementDB_Final.sql
public class UserRole
{
    public int UserRoleId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public bool IsActive { get; set; } = true; // tạm vô hiệu 1 role mà không cần xóa (giữ lịch sử)
    public int? AssignedBy { get; set; }       // UserId của Quản lý đã gán role này
    public DateTime AssignedAt { get; set; } = DateTime.Now;
    public DateTime? RevokedAt { get; set; }
}
