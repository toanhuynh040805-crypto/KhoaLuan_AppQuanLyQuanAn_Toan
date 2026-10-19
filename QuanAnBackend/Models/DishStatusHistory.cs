namespace QuanAnBackend.Models;

// Khớp bảng DishStatusHistory trong QuanAnManagementDB_Final.sql.
// Lưu lại TOÀN BỘ lịch sử đổi trạng thái của từng món -> dùng để tính
// món nào đang trễ (so sánh ChangedAt của lần đổi gần nhất với hiện tại)
// cho màn hình Bếp/Quầy nước (KitchenBoardControl bên WinForm).
public class DishStatusHistory
{
    public int HistoryId { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public KitchenStatus Status { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    public int? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }
}
