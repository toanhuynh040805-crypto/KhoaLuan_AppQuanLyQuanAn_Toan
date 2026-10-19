namespace QuanAnBackend.Models;

public enum CategoryType
{
    MonAn,
    ThucUong
}

// Khớp bảng Categories trong QuanAnManagementDB_Final.sql
public class Category
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty; // Món khai vị, Món chính, Đồ uống...
    public CategoryType CategoryType { get; set; } = CategoryType.MonAn;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
