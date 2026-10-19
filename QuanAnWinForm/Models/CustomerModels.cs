namespace QuanAnWinForm.Models;

// Khớp enum Models.MemberLevel bên Backend (QuanAnBackend/Models/Customer.cs)
public enum MemberLevelEnum
{
    ThanhVien = 0,
    Bac = 1,
    Vang = 2,
    KimCuong = 3
}

// Khớp enum Models.LoyaltyPointType bên Backend (QuanAnBackend/Models/LoyaltyPointHistory.cs)
public enum LoyaltyPointTypeEnum
{
    TichDiem = 0,
    DoiDiem = 1
}

public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int LoyaltyPoint { get; set; }
    public int MemberLevel { get; set; } // khớp enum MemberLevelEnum ở trên
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public bool HasWebAccount { get; set; }
    public bool WebAccountActive { get; set; }

    public override string ToString() => $"{FullName} - {Phone}";
}

public class CustomerSearchResultDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public int LoyaltyPoint { get; set; }
    public int MemberLevel { get; set; }

    public override string ToString() => $"{FullName} - {Phone} ({LoyaltyPoint} điểm)";
}

public class CustomerOrderSummaryDto
{
    public int Id { get; set; }
    public DateTime OrderTime { get; set; }
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
    public string TableName { get; set; } = "";
}

public class LoyaltyHistoryDto
{
    public int Id { get; set; }
    public int Points { get; set; }
    public int Type { get; set; } // khớp enum LoyaltyPointTypeEnum ở trên
    public int? RelatedInvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerDetailDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int LoyaltyPoint { get; set; }
    public int MemberLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasWebAccount { get; set; }
    public bool WebAccountActive { get; set; }
    public List<CustomerOrderSummaryDto> RecentOrders { get; set; } = new();
    public List<LoyaltyHistoryDto> LoyaltyHistory { get; set; } = new();
}