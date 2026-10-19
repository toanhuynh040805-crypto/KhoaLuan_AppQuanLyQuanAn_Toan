using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

// BO SUNG (timeout tu dong): chay ngam trong nen, dinh ky quet 2 truong hop can
// canh bao cho nhan vien/quan ly biet ma XU LY TAY (khong tu dong huy/thanh toan
// thay, vi do la quyet dinh nghiep vu can nguoi that xac nhan):
//
//   1) Don hang dang mo (ban Dang phuc vu) da qua lau ke tu luc Nhan ban ma van
//      chua thanh toan -> canh bao QuanLy (vd nhan vien quen bam thanh toan).
//   2) Dat ban truoc da qua het khung gio giu ban (ReservationEndTime) ma khach
//      van chua den (no-show) -> canh bao ThuNgan/PhucVu de CHU DONG huy dat ban
//      do bang tay (khong tu dong huy, tranh huy nham khi khach chi den tre).
//
// Dung bang Notifications san co lam "flag da canh bao chua" (kiem tra da ton
// tai Notification cung RelatedEntityType+RelatedEntityId+NotificationType.HeThong
// hay chua) thay vi luu them trang thai rieng - moi Order/Reservation chi bi
// canh bao 1 LAN duy nhat, tranh spam thong bao lien tuc moi vong quet.
public class TimeoutMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TimeoutMonitorService> _logger;
    private readonly TimeSpan _scanInterval;
    private readonly TimeSpan _openOrderWarningAfter;
    private readonly TimeSpan _noShowGrace;

    public TimeoutMonitorService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<TimeoutMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var section = config.GetSection("TimeoutMonitor");
        _scanInterval = TimeSpan.FromMinutes(section.GetValue("ScanIntervalMinutes", 10));
        _openOrderWarningAfter = TimeSpan.FromHours(section.GetValue("OpenOrderWarningHours", 3.0));
        _noShowGrace = TimeSpan.FromMinutes(section.GetValue("NoShowGraceMinutes", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Doi 30s luc app vua khoi dong de DbContext/migration on dinh truoc,
        // tranh quet ngay khi Backend chua san sang hoan toan.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // KHONG de 1 loi tam thoi (vd mat ket noi DB thoang qua) lam chet
                // han service nen - chi log lai va thu tiep o vong quet sau.
                _logger.LogError(ex, "TimeoutMonitorService: loi trong luc quet timeout");
            }

            try { await Task.Delay(_scanInterval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        await WarnStaleOpenOrdersAsync(db, notifications, ct);
        await WarnNoShowReservationsAsync(db, notifications, ct);
    }

    // (1) Don dang mo qua lau ke tu OrderTime (~ luc Nhan ban / len mon dau tien).
    private async Task WarnStaleOpenOrdersAsync(AppDbContext db, NotificationService notifications, CancellationToken ct)
    {
        var threshold = DateTime.Now - _openOrderWarningAfter;

        var staleOrders = await db.Orders
            .Include(o => o.Table)
            .Where(o => o.Status == OrderStatus.DangMo && o.OrderTime <= threshold)
            .ToListAsync(ct);

        foreach (var order in staleOrders)
        {
            var alreadyWarned = await db.Notifications.AnyAsync(n =>
                n.RelatedEntityType == "Order" &&
                n.RelatedEntityId == order.OrderId &&
                n.NotificationType == NotificationType.HeThong, ct);
            if (alreadyWarned) continue;

            var openHours = (DateTime.Now - order.OrderTime).TotalHours;
            await notifications.CreateForRolesAsync(
                "Đơn hàng mở quá lâu",
                $"Đơn #{order.OrderId} ({order.Table?.TableName ?? "?"}) đã mở hơn {openHours:0.#} giờ mà chưa thanh toán, vui lòng kiểm tra.",
                NotificationType.HeThong,
                new[] { "QuanLy" },
                relatedEntityType: "Order",
                relatedEntityId: order.OrderId);
        }
    }

    // (2) Dat ban truoc da qua het khung gio giu ban (+ 1 khoang gia han nho)
    // ma van chua Nhan ban (con o DaXacNhan) -> canh bao, KHONG tu huy.
    private async Task WarnNoShowReservationsAsync(AppDbContext db, NotificationService notifications, CancellationToken ct)
    {
        var now = DateTime.Now;

        // So luong dat ban dang cho khong nhieu -> loc IsOverdue o memory cho de
        // doc (ReservationEndTime la property tinh, khong dich duoc sang SQL).
        var waiting = await db.Reservations
            .Include(r => r.Table)
            .Where(r => r.Status == ReservationStatus.DaXacNhan)
            .ToListAsync(ct);

        var overdue = waiting.Where(r => now - r.ReservationEndTime >= _noShowGrace).ToList();

        foreach (var r in overdue)
        {
            var alreadyWarned = await db.Notifications.AnyAsync(n =>
                n.RelatedEntityType == "Reservation" &&
                n.RelatedEntityId == r.ReservationId &&
                n.NotificationType == NotificationType.HeThong, ct);
            if (alreadyWarned) continue;

            var lateMinutes = (int)(now - r.ReservationEndTime).TotalMinutes;
            var customerText = string.IsNullOrWhiteSpace(r.CustomerName) ? "Khách" : r.CustomerName;
            await notifications.CreateForRolesAsync(
                "Khách đặt bàn có thể không đến",
                $"{customerText} đặt {r.Table?.TableName ?? "?"} lúc {r.ReservationTime:HH:mm dd/MM} đã trễ hơn {lateMinutes} phút so với giờ giữ bàn, vui lòng liên hệ khách hoặc huỷ đặt bàn nếu cần.",
                NotificationType.HeThong,
                new[] { "ThuNgan", "PhucVu" },
                relatedEntityType: "Reservation",
                relatedEntityId: r.ReservationId);
        }
    }
}
