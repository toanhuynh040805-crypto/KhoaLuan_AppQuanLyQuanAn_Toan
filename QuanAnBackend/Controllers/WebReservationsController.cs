using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using System.Data;
using Microsoft.AspNetCore.SignalR;
using QuanAnBackend.Hubs;
using QuanAnBackend.Services;
using Microsoft.AspNetCore.RateLimiting;
namespace QuanAnBackend.Controllers;
public record WebReservationSlot(int TableId, int NumberOfGuests);

// Gửi theo 2 kiểu: tables[] (1 hoặc nhiều bàn) hoặc tableId + numberOfGuests (1 bàn).
public record WebCreateReservationRequest(
    List<WebReservationSlot>? Tables,
    int? TableId,
    int? NumberOfGuests,
    string? CustomerName,
    string? CustomerPhone,
    DateTime? ReservationTime,
    int? DurationMinutes,
    string? Note);
// Đặt bàn cho khách web. Dùng chung tiền tố api/reservations với ReservationsController (nhân viên).
[ApiController]
[Route("api/reservations")]
public class WebReservationsController : ControllerBase
{
        private static readonly TimeSpan UpcomingThreshold = TimeSpan.FromHours(3);
    private const int MaxDaysAhead = 60;
    private const int MaxTablesPerBooking = 8;
    private const int MaxActiveRowsPerPhone = 10;
    private const int MaxNoteLength = 150;

    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    private readonly ILogger<WebReservationsController> _logger;

    public WebReservationsController(
        AppDbContext db,
        IHubContext<OrderHub> hub,
        NotificationService notifications,
        ILogger<WebReservationsController> logger)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
        _logger = logger;
    }

    // GET api/reservations/available-tables?reservationTime=2026-10-20T19:00:00&durationMinutes=30&areaId=1
    // Trả các bàn ăn tại chỗ KHÔNG trùng lịch trong khung giờ yêu cầu.
    [HttpGet("available-tables")]
    [AllowAnonymous]
    [EnableRateLimiting("web-read")]
    public async Task<IActionResult> GetAvailableTables(
        [FromQuery] DateTime? reservationTime,
        [FromQuery] int? durationMinutes,
        [FromQuery] int? areaId)
    {
        var error = ValidateSlot(reservationTime, durationMinutes, out var start, out var duration);
        if (error is not null) return error;

        var tables = await FindAvailableTablesAsync(start, duration, areaId, null);

        return Ok(tables.Select(t => new
        {
            Id = t.TableId,
            t.TableId,
            t.TableName,
            t.Capacity,
            t.AreaId,
            AreaName = t.Area?.AreaName ?? "",
            TableType = t.TableType.ToString(),
            Status = TableStatus.Trong.ToString()
        }));
    }

    // POST api/reservations/web
    // Khách vãng lai hoặc đã đăng nhập (có token thì gắn CustomerId). Trả 1 mã cho cả lượt đặt.
    [HttpPost("web")]
    [AllowAnonymous]
    [EnableRateLimiting("web-booking")]
    public async Task<IActionResult> CreateWeb([FromBody] WebCreateReservationRequest request)
    {
        var slotError = ValidateSlot(request.ReservationTime, request.DurationMinutes, out var start, out var duration);
        if (slotError is not null) return slotError;

        var slots = request.Tables is { Count: > 0 }
            ? request.Tables
            : request.TableId is int tid
                ? new List<WebReservationSlot> { new(tid, request.NumberOfGuests ?? 0) }
                : new List<WebReservationSlot>();

        if (slots.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 bàn." });
        if (slots.Count > MaxTablesPerBooking)
            return BadRequest(new { message = $"Mỗi lượt chỉ đặt tối đa {MaxTablesPerBooking} bàn. Vui lòng liên hệ quán." });

        var ids = slots.Select(s => s.TableId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return BadRequest(new { message = "Có bàn bị chọn trùng." });

        // Khách đã đăng nhập: lấy thông tin từ hồ sơ nếu form để trống.
        var customerId = CustomerTokens.GetCustomerId(User);
        Customer? customer = customerId is null
            ? null
            : await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId.Value);

        var name = request.CustomerName?.Trim();
        if (string.IsNullOrEmpty(name)) name = customer?.FullName;
        var phone = NormalizePhone(string.IsNullOrWhiteSpace(request.CustomerPhone) ? customer?.Phone : request.CustomerPhone);
        var note = request.Note?.Trim();

        if (string.IsNullOrEmpty(name) || name.Length < 2 || name.Length > 100)
            return BadRequest(new { message = "Họ và tên phải từ 2 đến 100 ký tự." });
        if (phone.Length < 9 || phone.Length > 11)
            return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        if (note is { Length: > MaxNoteLength })
            return BadRequest(new { message = $"Ghi chú tối đa {MaxNoteLength} ký tự." });

        // Chống spam: mỗi SĐT giới hạn số lượt đặt còn hiệu lực trong tương lai.
        var now = DateTime.Now;
        var active = await _db.Reservations.CountAsync(r =>
            r.CustomerPhone == phone &&
            r.Status == ReservationStatus.DaXacNhan &&
            r.ReservationTime > now);
        if (active + slots.Count > MaxActiveRowsPerPhone)
            return StatusCode(429, new { message = "Số điện thoại này đang có quá nhiều đặt bàn chưa đến. Vui lòng hủy bớt hoặc liên hệ quán." });

        List<Reservation> created;
        List<DiningTable> changedTables = new();

        // Serializable: kiểm tra bàn trống và ghi đặt bàn là một khối, tránh 2 khách đặt trùng cùng lúc.
        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var available = await FindAvailableTablesAsync(start, duration, null, ids);
            var availableIds = available.Select(t => t.TableId).ToHashSet();
            if (ids.Any(id => !availableIds.Contains(id)))
                return Conflict(new { message = "Có bàn không còn trống trong khung giờ này. Vui lòng chọn lại." });

            foreach (var slot in slots)
            {
                var t = available.First(x => x.TableId == slot.TableId);
                if (slot.NumberOfGuests < 1)
                    return BadRequest(new { message = $"Bàn {t.TableName}: số khách phải từ 1 trở lên." });
                if (slot.NumberOfGuests > t.Capacity)
                    return BadRequest(new { message = $"Bàn {t.TableName} chỉ chứa tối đa {t.Capacity} khách." });
            }

            var tables = await _db.Tables.Where(t => ids.Contains(t.TableId)).ToListAsync();
            var names = string.Join(", ", slots.Select(s => tables.First(t => t.TableId == s.TableId).TableName));
            var groupNote = slots.Count > 1 ? $"[Đặt ghép {slots.Count} bàn: {names}]" : null;
            var fullNote = string.Join(" ", new[] { note, groupNote }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var isUpcoming = start <= DateTime.Now.Add(UpcomingThreshold);

            created = new List<Reservation>();
            foreach (var slot in slots)
            {
                var table = tables.First(t => t.TableId == slot.TableId);
                created.Add(new Reservation
                {
                    TableId = table.TableId,
                    CustomerId = customerId,
                    CustomerName = name,
                    CustomerPhone = phone,
                    ReservationTime = start,
                    DurationMinutes = duration,
                    NumberOfGuests = slot.NumberOfGuests,
                    Note = string.IsNullOrWhiteSpace(fullNote) ? null : fullNote,
                    Source = ReservationSources.Web,
                    Status = ReservationStatus.DaXacNhan
                });

                if (table.Status == TableStatus.Trong && isUpcoming)
                {
                    table.Status = TableStatus.DaDat;
                    changedTables.Add(table);
                }
            }

            _db.Reservations.AddRange(created);
            await _db.SaveChangesAsync();

            // Mã đặt bàn = Id của dòng đầu; đặt ghép thì cả nhóm dùng chung mã này.
            if (created.Count > 1)
            {
                foreach (var r in created) r.BookingGroupId = created[0].ReservationId;
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
        }

        var code = created[0].ReservationId;

        // Thông báo realtime: lỗi ở đây không được làm hỏng đặt bàn đã lưu.
        try
        {
            foreach (var t in changedTables)
                await _hub.Clients.All.SendAsync("TableStatusUpdated",
                    new { TableId = t.TableId, t.TableName, Status = t.Status });

            var tableNames = string.Join(", ", changedTables.Count > 0 || created.Count == 0
                ? changedTables.Select(t => t.TableName)
                : Array.Empty<string>());
            var label = created.Count > 1 ? $"{created.Count} bàn" : "bàn";

            await _notifications.CreateForRolesAsync(
                "Có đặt bàn mới từ web",
                $"{name} đặt {label} lúc {start:HH:mm dd/MM} ({created.Sum(r => r.NumberOfGuests)} khách, mã #{code})",
                NotificationType.DatBan,
                new[] { "ThuNgan", "PhucVu" },
                relatedEntityType: "Reservation",
                relatedEntityId: code);

            await _hub.Clients.All.SendAsync(
                "ReservationUpdated",
                new
                {
                    ReservationId = code,
                    Status = ReservationStatus.DaXacNhan.ToString(),
                    Stage = "Created",
                    ReservationTime = start,
                    HoldUntil = start.AddMinutes(duration)
                });

            if (customerId.HasValue)
            {
                await _notifications.CreateForCustomerAsync(
                    customerId.Value,
                    "Đặt bàn đã được xác nhận",
                    $"Lịch đặt bàn lúc {start:HH:mm dd/MM} đã được ghi nhận. Bàn được giữ đến {start.AddMinutes(duration):HH:mm}.",
                    NotificationType.DatBan,
                    $"reservation-web-created:{code}",
                    "Reservation",
                    code);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi thông báo đặt bàn web #{Code} thất bại", code);
        }

        return Ok(new
        {
            Id = code,
            Code = code,
            ReservationCode = code,
            Count = created.Count,
            Ids = created.Select(r => r.ReservationId),
            ReservationTime = start,
            DurationMinutes = duration,
            HoldUntil = start.AddMinutes(duration),
            Status = ReservationStatus.DaXacNhan.ToString()
        });
    }

    private const string NotFoundMessage = "Không tìm thấy đặt bàn phù hợp với mã và số điện thoại.";

    // GET api/reservations/lookup?code=12&phone=0909123456
    // Đặt ghép nhiều bàn dùng chung 1 mã; trả gộp cả nhóm.
    [HttpGet("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("web-booking")]
    public async Task<IActionResult> Lookup([FromQuery] string? code, [FromQuery] string? phone)
    {
        var rows = await FindByCodeAsync(code, phone, track: false);
        if (rows.Count == 0) return NotFound(new { message = NotFoundMessage });

        var first = rows[0];
        var status = rows.Any(r => r.Status == ReservationStatus.DaXacNhan)
            ? ReservationStatus.DaXacNhan
            : first.Status;

        return Ok(new
        {
            Id = first.BookingGroupId ?? first.ReservationId,
            first.TableId,
            TableName = string.Join(", ", rows.Select(r => r.Table?.TableName ?? $"Bàn {r.TableId}")),
            first.CustomerName,
            first.CustomerPhone,
            first.ReservationTime,
            first.DurationMinutes,
            HoldUntil = first.ReservationTime.AddMinutes(first.DurationMinutes),
            IsHoldExpired = DateTime.Now >= first.ReservationTime.AddMinutes(first.DurationMinutes),
            NumberOfGuests = rows.Sum(r => r.NumberOfGuests),
            Status = (int)status,
            first.Note,
            first.Source,
            Tables = rows.Select(r => new
            {
                r.TableId,
                TableName = r.Table?.TableName ?? "",
                r.NumberOfGuests
            })
        });
    }

    // POST api/reservations/cancel?code=12&phone=0909123456
    [HttpPost("cancel")]
    [AllowAnonymous]
    [EnableRateLimiting("web-booking")]
    public async Task<IActionResult> Cancel([FromQuery] string? code, [FromQuery] string? phone)
    {
        var rows = await FindByCodeAsync(code, phone, track: true);
        if (rows.Count == 0) return NotFound(new { message = NotFoundMessage });

        var cancellable = rows.Where(r => r.Status == ReservationStatus.DaXacNhan).ToList();
        if (cancellable.Count == 0)
            return BadRequest(new { message = "Đặt bàn này không thể hủy ở trạng thái hiện tại." });

        var now = DateTime.Now;
        if (cancellable.Any(r => r.ReservationTime <= now))
            return BadRequest(new { message = "Đã đến giờ hẹn nên không thể hủy online. Vui lòng liên hệ quán." });

        var cancelledIds = cancellable.Select(r => r.ReservationId).ToList();
        foreach (var r in cancellable)
        {
            r.Status = ReservationStatus.DaHuy;
            r.CancelledAt = now;   // CancelledByUserId để null = khách tự hủy
        }

        // Nhả bàn về Trống nếu không còn lượt đặt nào đang giữ bàn trong 3 giờ tới.
        var changedTables = new List<DiningTable>();
        foreach (var table in cancellable.Select(r => r.Table).Where(t => t is not null).DistinctBy(t => t!.TableId))
        {
            if (table!.Status != TableStatus.DaDat) continue;

            var stillHeld = await _db.Reservations.AnyAsync(r =>
                r.TableId == table.TableId &&
                r.Status == ReservationStatus.DaXacNhan &&
                !cancelledIds.Contains(r.ReservationId) &&
                r.ReservationTime <= now.Add(UpcomingThreshold));

            if (!stillHeld)
            {
                table.Status = TableStatus.Trong;
                changedTables.Add(table);
            }
        }

        await _db.SaveChangesAsync();

        var codeValue = rows[0].BookingGroupId ?? rows[0].ReservationId;
        try
        {
            foreach (var t in changedTables)
                await _hub.Clients.All.SendAsync("TableStatusUpdated",
                    new { TableId = t.TableId, t.TableName, Status = t.Status });

            await _hub.Clients.All.SendAsync(
                "ReservationUpdated",
                new
                {
                    ReservationId = codeValue,
                    Status = ReservationStatus.DaHuy.ToString(),
                    Stage = "Cancelled",
                    ReservationTime = rows[0].ReservationTime,
                    HoldUntil = rows[0].ReservationTime.AddMinutes(rows[0].DurationMinutes)
                });

            await _notifications.CreateForRolesAsync(
                "Khách hủy đặt bàn",
                $"{rows[0].CustomerName} đã hủy đặt bàn #{codeValue} lúc {rows[0].ReservationTime:HH:mm dd/MM}",
                NotificationType.DatBan,
                new[] { "ThuNgan", "PhucVu" },
                relatedEntityType: "Reservation",
                relatedEntityId: codeValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi thông báo hủy đặt bàn #{Code} thất bại", codeValue);
        }

        return Ok(new
        {
            Id = codeValue,
            Status = (int)ReservationStatus.DaHuy,
            message = "Đã hủy đặt bàn."
        });
    }

    // Tìm các dòng đặt bàn theo mã (Id hoặc mã nhóm) + SĐT. Sai định dạng -> danh sách rỗng.
    private async Task<List<Reservation>> FindByCodeAsync(string? code, string? phone, bool track)
    {
        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        var p = NormalizePhone(phone);
        if (!int.TryParse(digits, out var id) || id <= 0 || p.Length < 9 || p.Length > 11)
            return new List<Reservation>();

        var query = _db.Reservations.Include(r => r.Table).AsQueryable();
        if (!track) query = query.AsNoTracking();

        return await query
            .Where(r => (r.ReservationId == id || r.BookingGroupId == id) && r.CustomerPhone == p)
            .OrderBy(r => r.ReservationId)
            .ToListAsync();
    }
    
    private static string NormalizePhone(string? value)
        => new string((value ?? "").Where(char.IsDigit).ToArray());

    // ---------------------------------------------------------------
    // Dùng chung với các action tạo đặt bàn (bước sau)
    // ---------------------------------------------------------------
    internal static IActionResult? ValidateSlot(
        DateTime? time, int? requestedDuration, out DateTime start, out int duration)
    {
        start = time ?? default;
        duration = requestedDuration ?? Reservation.DefaultDurationMinutes;

        if (time is null)
            return new BadRequestObjectResult(new { message = "Vui lòng chọn ngày giờ đến." });

        if (start < DateTime.Now.AddMinutes(-5))
            return new BadRequestObjectResult(new { message = "Giờ đến phải ở hiện tại hoặc trong tương lai." });

        if (start > DateTime.Now.AddDays(MaxDaysAhead))
            return new BadRequestObjectResult(new { message = $"Chỉ nhận đặt bàn trong vòng {MaxDaysAhead} ngày tới." });

        if (duration < Reservation.MinDurationMinutes || duration > Reservation.MaxDurationMinutes)
            return new BadRequestObjectResult(new
            {
                message = $"Thời lượng giữ bàn phải từ {Reservation.MinDurationMinutes} đến {Reservation.MaxDurationMinutes} phút."
            });

        return null;
    }

    // Bàn ăn tại chỗ còn trống trong [start, start + duration).
    // onlyTableIds != null: chỉ xét các bàn này (dùng để kiểm tra lại lúc tạo đặt bàn).
    internal async Task<List<DiningTable>> FindAvailableTablesAsync(
        DateTime start, int duration, int? areaId, ICollection<int>? onlyTableIds)
    {
        var end = start.AddMinutes(duration);

        // Loại bàn/khu "Mang về" (không phải bàn ăn tại chỗ).
        var query = _db.Tables.AsNoTracking()
            .Include(t => t.Area)
            .Where(t => !t.TableName.StartsWith("MV-") &&
                        (t.Area == null || t.Area.AreaName != "Mang về"));

        if (areaId.HasValue)
            query = query.Where(t => t.AreaId == areaId.Value);
        if (onlyTableIds is not null)
            query = query.Where(t => onlyTableIds.Contains(t.TableId));

        var tables = await query.OrderBy(t => t.AreaId).ThenBy(t => t.TableId).ToListAsync();
        if (tables.Count == 0) return tables;

        var tableIds = tables.Select(t => t.TableId).ToList();

        // Lượt đặt chỉ có thể chồng lấn nếu bắt đầu trong (start - tối đa 45', end).
        var earliest = start.AddMinutes(-Reservation.MaxDurationMinutes);
        var nearby = await _db.Reservations.AsNoTracking()
            .Where(r => tableIds.Contains(r.TableId) &&
                        (r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan) &&
                        r.ReservationTime < end &&
                        r.ReservationTime > earliest)
            .Select(r => new { r.TableId, r.ReservationTime, r.DurationMinutes })
            .ToListAsync();

        var busy = nearby
            .Where(r => start < r.ReservationTime.AddMinutes(r.DurationMinutes) && r.ReservationTime < end)
            .Select(r => r.TableId)
            .ToHashSet();

        // Giống luật của nhân viên: đặt trong 3 giờ tới thì bàn đang có khách/đang dọn chưa rõ giờ trống.
        var isUpcoming = start <= DateTime.Now.Add(UpcomingThreshold);

        return tables
            .Where(t => !busy.Contains(t.TableId) &&
                        !(isUpcoming && (t.Status == TableStatus.DangPhucVu || t.Status == TableStatus.DangDonDep)))
            .ToList();
    }
}