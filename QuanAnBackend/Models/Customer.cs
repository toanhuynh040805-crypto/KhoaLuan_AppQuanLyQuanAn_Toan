namespace QuanAnBackend.Models;

// Khớp bảng Customers trong QuanAnManagementDB_Final.sql (nhóm 7: KHÁCH HÀNG & KHUYẾN MÃI).
// MemberLevel được tự động tính lại mỗi khi LoyaltyPoint thay đổi (xem
// Services/CustomerLoyaltyHelper.cs) - không cho sửa tay trực tiếp field này.
public enum MemberLevel
{
    ThanhVien,
    Bac,
    Vang,
    KimCuong
}

public class Customer
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int LoyaltyPoint { get; set; } = 0;
    public MemberLevel MemberLevel { get; set; } = MemberLevel.ThanhVien;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<LoyaltyPointHistory> LoyaltyPointHistories { get; set; } = new List<LoyaltyPointHistory>();
}