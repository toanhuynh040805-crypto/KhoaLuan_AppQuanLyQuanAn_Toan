using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

/// <summary>
/// NGHIỆP VỤ CỐT LÕI #2: Thuật toán phân luồng đơn hàng.
///
/// Khi 1 Order được tạo/thêm món từ Mobile, service này:
///   1. Nhóm các OrderItem theo DepartmentId (Bếp / Quầy nước)
///   2. Với mỗi nhóm, tạo 1 "phiếu ảo" (PrintTicket) ghi log lại
///   3. Đẩy realtime (SignalR) riêng biệt tới đúng group của từng bộ phận
///      -> màn hình Bếp CHỈ thấy món ăn, màn hình Quầy nước CHỈ thấy đồ uống
///
/// Đây là điểm khác biệt so với cách làm "gộp chung 1 phiếu" thông thường.
/// </summary>
public class OrderRoutingService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public OrderRoutingService(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task RouteNewOrderItemsAsync(int orderId, List<int> newOrderItemIds)
    {
        // Lấy các OrderItem vừa tạo, kèm thông tin món + bộ phận phụ trách
        var items = await _db.OrderItems
            .Include(oi => oi.MenuItem)!
                .ThenInclude(mi => mi!.Department)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Table)
            .Where(oi => newOrderItemIds.Contains(oi.Id))
            .ToListAsync();

        // Bước 1: phân nhóm theo bộ phận (đây là bước "phân luồng")
        var groupedByDepartment = items.GroupBy(oi => oi.MenuItem!.DepartmentId);

        foreach (var group in groupedByDepartment)
        {
            var department = await _db.Departments.FindAsync(group.Key);
            if (department is null) continue;

            var ticketPayload = group.Select(oi => new
            {
                oi.Id,
                MenuItemName = oi.MenuItem!.Name,
                oi.Quantity,
                oi.Note
            }).ToList();

            // Bước 2: ghi log phiếu cho từng món trong nhóm
            foreach (var oi in group)
            {
                _db.PrintTickets.Add(new PrintTicket
                {
                    OrderItemId = oi.Id,
                    DepartmentId = department.Id,
                    PrintedAt = DateTime.Now
                });
                oi.KitchenStatus = KitchenStatus.DaInPhieu;
            }

            await _db.SaveChangesAsync();

            // Bước 3: đẩy realtime CHỈ tới group của bộ phận này
            var tableName = items.First().Order!.Table?.TableName ?? "N/A";
            await _hub.Clients.Group($"dept-{department.Name}")
                .SendAsync("NewTicket", new
                {
                    OrderId = orderId,
                    TableName = tableName,
                    Department = department.Name,
                    Items = ticketPayload
                });
        }
    }
}
