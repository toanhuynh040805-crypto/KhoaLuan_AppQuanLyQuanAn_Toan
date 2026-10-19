namespace QuanAnBackend.Models;

// Khớp bảng Users trong QuanAnManagementDB_Final.sql.
// LƯU Ý: 1 User có thể có NHIỀU Role cùng lúc -> xem UserRoles (N-N),
// không có cột RoleId đơn ở đây nữa.
public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public Employee? Employee { get; set; } // 1-1, thông tin nghiệp vụ nhân viên (nếu User này là nhân viên)
}
