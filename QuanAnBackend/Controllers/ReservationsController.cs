using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace QuanAnBackend.Controllers;

// DurationMinutes de null (khach cu/WinForm cu chua gui len) -> Controller se tu
// dung Reservation.DefaultDurationMinutes (120 phut) thay vi bat buoc sua Client ngay.
public record CreateReservationRequest(int TableId, string? CustomerName, string? CustomerPhone, DateTime ReservationTime, int? DurationMinutes, int NumberOfGuests, decimal DepositAmount, string? Note);
public record UpdateReservationRequest(string? CustomerName, string? CustomerPhone, DateTime ReservationTime, int? DurationMinutes, int NumberOfGuests, decimal DepositAmount, string? Note);
public record ReservationTableSlot(int TableId, int NumberOfGuests);
public record CreateReservationBatchRequest(List<ReservationTableSlot> Tables, string? CustomerName, string? CustomerPhone, DateTime ReservationTime, int? DurationMinutes, decimal DepositAmount, string? Note);

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;

    public ReservationsController(AppDbContext db, IHubContext<OrderHub> hub, NotificationService notifications)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
    }

    private async Task<int?> ResolveCustomerIdAsync(string? phone)
    {
        if (User.IsInRole("Customer"))
        {
            var claim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var customerId) && customerId > 0 &&
                await _db.Customers.AnyAsync(customer => customer.CustomerId == customerId)
                    ? customerId
                    : null;
        }

        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(phone))
            return null;

        return await _db.Customers
            .Where(customer => customer.Phone == phone.Trim())
            .Select(customer => (int?)customer.CustomerId)
            .FirstOrDefaultAsync();
    }

    private async Task<Reservation?> FindPublicReservationAsync(string? code, string? phone)
    {
        var reservationCode = code?.Trim();
        if (reservationCode?.StartsWith('#') == true)
            reservationCode = reservationCode[1..].Trim();

        if (!int.TryParse(reservationCode, out var reservationId) || reservationId <= 0)
            return null;

        var suppliedPhone = NormalizePhone(phone);
        if (suppliedPhone.Length == 0)
            return null;

        var reservation = await _db.Reservations
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

        return reservation is not null &&
               suppliedPhone == NormalizePhone(reservation.CustomerPhone)
            ? reservation
            : null;
    }

    private static string NormalizePhone(string? phone) =>
        string.Concat((phone ?? string.Empty).Where(char.IsDigit));

    private static object ToPublicReservation(Reservation reservation) => new
    {
        Id = reservation.ReservationId,
        reservation.TableId,
        TableName = reservation.Table?.TableName ?? "Chưa xếp bàn",
        reservation.CustomerName,
        reservation.CustomerPhone,
        reservation.ReservationTime,
        reservation.NumberOfGuests,
        Status = (int)reservation.Status,
        StatusName = reservation.Status.ToString(),
        reservation.Note
    };

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] ReservationStatus? status, [FromQuery] string? keyword)
    {
        var query = _db.Reservations
            .Include(r => r.Table)!.ThenInclude(t => t!.Area)
            .Include(r => r.CancelledByUser)
            .AsNoTracking().AsQueryable();

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(r => r.ReservationTime >= day && r.ReservationTime < day.AddDays(1));
        }
        else
        {
            query = query.Where(r => r.ReservationTime >= DateTime.Today);
        }

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(r =>
                (r.CustomerName != null && r.CustomerName.Contains(kw)) ||
                (r.CustomerPhone != null && r.CustomerPhone.Contains(kw)) ||
                (r.Table != null && r.Table.TableName.Contains(kw)));
        }

        var result = await query
            .OrderBy(r => r.ReservationTime)
            .Select(r => new
            {
                Id = r.ReservationId,
                r.TableId,
                TableName = r.Table != null ? r.Table.TableName : "",
                AreaName = r.Table != null && r.Table.Area != null ? r.Table.Area.AreaName : "",
                r.CustomerName,
                r.CustomerPhone,
                r.ReservationTime,
                r.DurationMinutes,
                ReservationEndTime = r.ReservationTime.AddMinutes(r.DurationMinutes),
                r.NumberOfGuests,
                Status = r.Status,
                r.DepositAmount,
                r.DepositPaid,
                r.Note,
                CancelledByUserName = r.CancelledByUser != null ? r.CancelledByUser.Username : null,
                r.CancelledAt
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("last-by-phone")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLastByPhone([FromQuery] string? phone)
    {
        var p = phone?.Trim();
        if (string.IsNullOrEmpty(p)) return Ok(new { Found = false });

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Phone == p);

        var history = await _db.Reservations.AsNoTracking()
            .Include(r => r.Table)
            .Where(r => r.CustomerPhone == p)
            .OrderByDescending(r => r.ReservationTime)
            .ToListAsync();
        var last = history.FirstOrDefault();

        if (customer is null && last is null) return Ok(new { Found = false });

        return Ok(new
        {
            Found = true,
            IsMember = customer is not null,
            CustomerId = customer?.CustomerId,
            MemberLevel = (int?)customer?.MemberLevel,
            LoyaltyPoint = customer?.LoyaltyPoint,
            CustomerName = customer?.FullName ?? last?.CustomerName,
            ReservationCount = history.Count,
            LastTableName = last?.Table?.TableName,
            LastReservationTime = last?.ReservationTime,
            LastNumberOfGuests = last?.NumberOfGuests,
            LastNote = last?.Note
        });
    }

    private static readonly TimeSpan UpcomingThreshold = TimeSpan.FromHours(3);

    private static bool IsOverlapping(DateTime aStart, int aDurationMinutes, DateTime bStart, int bDurationMinutes)
    {
        var aEnd = aStart.AddMinutes(aDurationMinutes);
        var bEnd = bStart.AddMinutes(bDurationMinutes);
        return aStart < bEnd && bStart < aEnd;
    }

    private static IActionResult? ValidateDuration(int? requested, out int durationMinutes)
    {
        durationMinutes = requested.GetValueOrDefault(Reservation.DefaultDurationMinutes);
        if (durationMinutes < Reservation.MinDurationMinutes || durationMinutes > Reservation.MaxDurationMinutes)
            return new BadRequestObjectResult(new
            {
                message = $"Thời lượng giữ bàn phải từ {Reservation.MinDurationMinutes} đến {Reservation.MaxDurationMinutes} phút " +
                           $"({Reservation.MinDurationMinutes / 60.0:0.#} - {Reservation.MaxDurationMinutes / 60.0:0.#} giờ)"
            });
        return null;
    }

    [HttpGet("available-tables")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableTables(
        [FromQuery] DateTime? reservationTime,
        [FromQuery] DateTime? time,
        [FromQuery] int durationMinutes = Reservation.DefaultDurationMinutes,
        [FromQuery] int? areaId = null)
    {
        var targetTime = reservationTime ?? time ?? DateTime.Now;
        if (durationMinutes < Reservation.MinDurationMinutes || durationMinutes > Reservation.MaxDurationMinutes)
            durationMinutes = Reservation.DefaultDurationMinutes;

        var requestedEnd = targetTime.AddMinutes(durationMinutes);
        var isUpcoming = targetTime <= DateTime.Now.Add(UpcomingThreshold);

        var query = _db.Tables.Include(t => t.Area).AsNoTracking();
        if (areaId.HasValue && areaId.Value > 0)
        {
            query = query.Where(t => t.AreaId == areaId.Value);
        }
        var tables = await query.ToListAsync();

        var activeReservations = await _db.Reservations
            .Where(r => r.Status == ReservationStatus.ChoXacNhan || r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan)
            .Select(r => new
            {
                r.TableId,
                r.ReservationTime,
                r.DurationMinutes
            })
            .ToListAsync();

        var result = tables.Select(t =>
        {
            string? reason = null;
            var overlapped = activeReservations
                .Where(r => r.TableId == t.TableId)
                .FirstOrDefault(r => targetTime < r.ReservationTime.AddMinutes(r.DurationMinutes) && r.ReservationTime < requestedEnd);

            if (overlapped is not null)
                reason = $"Đã có khách đặt {overlapped.ReservationTime:HH:mm dd/MM} - {overlapped.ReservationTime.AddMinutes(overlapped.DurationMinutes):HH:mm dd/MM}";

            if (reason is null && isUpcoming && (t.Status == TableStatus.DangPhucVu || t.Status == TableStatus.DangDonDep || t.Status == TableStatus.DaDat))
            {
                reason = t.Status switch
                {
                    TableStatus.DangPhucVu => "Bàn đang có khách, chưa rõ giờ trống",
                    TableStatus.DangDonDep => "Bàn đang dọn dẹp",
                    TableStatus.DaDat => "Bàn đã được đặt trước",
                    _ => "Bàn tạm thời không khả dụng"
                };
            }

            return new
            {
                Id = t.TableId,
                TableId = t.TableId,
                TableName = t.TableName,
                Capacity = t.Capacity,
                AreaId = t.AreaId,
                AreaName = t.Area != null ? t.Area.AreaName : "",
                CurrentStatus = t.Status,
                IsAvailable = reason is null,
                Reason = reason
            };
        })
        .Where(t => t.IsAvailable)
        .OrderBy(t => t.AreaName).ThenBy(t => t.TableName)
        .ToList();

        return Ok(result);
    }

    [HttpPost("web")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateWeb([FromBody] System.Text.Json.JsonElement payload)
    {
        if (payload.TryGetProperty("tables", out var tablesProp) && tablesProp.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var batchRequest = System.Text.Json.JsonSerializer.Deserialize<CreateReservationBatchRequest>(
                payload.GetRawText(), 
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (batchRequest != null) return await CreateBatch(batchRequest);
        }
        var singleRequest = System.Text.Json.JsonSerializer.Deserialize<CreateReservationRequest>(
            payload.GetRawText(), 
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (singleRequest != null) return await Create(singleRequest);
        return BadRequest(new { message = "Dữ liệu đặt bàn không hợp lệ." });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
    {
        var table = await _db.Tables.FindAsync(request.TableId);
        if (table is null) return BadRequest(new { message = "Bàn không tồn tại" });

        if (request.NumberOfGuests < 1)
            return BadRequest(new { message = "Số lượng khách phải từ 1 trở lên" });

        if (request.NumberOfGuests > table.Capacity)
            return BadRequest(new { message = $"Bàn chỉ chứa tối đa {table.Capacity} khách" });

        if (request.ReservationTime < DateTime.Now.AddMinutes(-5))
            return BadRequest(new { message = "Giờ đến phải ở hiện tại hoặc trong tương lai" });

        var durationError = ValidateDuration(request.DurationMinutes, out var durationMinutes);
        if (durationError is not null) return durationError;

        var isUpcoming = request.ReservationTime <= DateTime.Now.Add(UpcomingThreshold);
        if (isUpcoming && (table.Status == TableStatus.DangPhucVu || table.Status == TableStatus.DangDonDep || table.Status == TableStatus.DaDat))
            return BadRequest(new { message = "Bàn đang có khách/đang dọn dẹp, chưa rõ giờ trống - vui lòng chọn giờ đến xa hơn hoặc bàn khác" });

        var conflict = await _db.Reservations
            .Where(r => r.TableId == request.TableId &&
                        (r.Status == ReservationStatus.ChoXacNhan || r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan))
            .ToListAsync();
        var overlapped = conflict.FirstOrDefault(r => IsOverlapping(request.ReservationTime, durationMinutes, r.ReservationTime, r.DurationMinutes));
        if (overlapped is not null)
            return BadRequest(new
            {
                message = $"Bàn {table.TableName} đã có lượt đặt từ {overlapped.ReservationTime:HH:mm dd/MM} " +
                           $"đến {overlapped.ReservationEndTime:HH:mm dd/MM}, vui lòng chọn khung giờ khác hoặc bàn khác"
            });

        var reservation = new Reservation
        {
            TableId = request.TableId,
            CustomerId = await ResolveCustomerIdAsync(request.CustomerPhone),
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            ReservationTime = request.ReservationTime,
            DurationMinutes = durationMinutes,
            NumberOfGuests = request.NumberOfGuests,
            DepositAmount = request.DepositAmount,
            Note = request.Note,
            Status = ReservationStatus.ChoXacNhan
        };
        _db.Reservations.Add(reservation);

        if (table.Status == TableStatus.Trong && isUpcoming)
        {
            table.Status = TableStatus.DaDat;
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new { TableId = table.TableId, table.TableName, Status = table.Status });
        }

        await _db.SaveChangesAsync();

        await _notifications.CreateForRolesAsync(
            "Có đặt bàn mới",
            $"{(string.IsNullOrWhiteSpace(request.CustomerName) ? "Khách" : request.CustomerName)} đặt {table.TableName} lúc {request.ReservationTime:HH:mm dd/MM}",
            NotificationType.DatBan,
            new[] { "ThuNgan", "PhucVu" },
            relatedEntityType: "Reservation",
            relatedEntityId: reservation.ReservationId);

        if (reservation.CustomerId.HasValue)
        {
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId.Value,
                "Đặt bàn đã được tiếp nhận",
                $"Yêu cầu đặt bàn tại {table.TableName} lúc {request.ReservationTime:HH:mm dd/MM} đã được tiếp nhận và đang chờ nhà hàng xác nhận.",
                NotificationType.DatBan,
                $"reservation-created:{reservation.ReservationId}",
                "Reservation",
                reservation.ReservationId);
        }

        return Ok(new { Id = reservation.ReservationId, reservation.TableId, reservation.DurationMinutes, Status = reservation.Status });
    }

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBatch([FromBody] CreateReservationBatchRequest request)
    {
        if (request.Tables is null || request.Tables.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 bàn" });

        var ids = request.Tables.Select(t => t.TableId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return BadRequest(new { message = "Có bàn bị chọn trùng" });

        if (request.ReservationTime < DateTime.Now.AddMinutes(-5))
            return BadRequest(new { message = "Giờ đến phải ở hiện tại hoặc trong tương lai" });

        var durationError = ValidateDuration(request.DurationMinutes, out var durationMinutes);
        if (durationError is not null) return durationError;

        var tables = await _db.Tables.Where(t => ids.Contains(t.TableId)).ToListAsync();
        if (tables.Count != ids.Count)
            return BadRequest(new { message = "Có bàn không tồn tại" });

        var isUpcoming = request.ReservationTime <= DateTime.Now.Add(UpcomingThreshold);

        var existing = await _db.Reservations
            .Where(r => ids.Contains(r.TableId) &&
                        (r.Status == ReservationStatus.ChoXacNhan || r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan))
            .ToListAsync();

        foreach (var slot in request.Tables)
        {
            var table = tables.First(t => t.TableId == slot.TableId);

            if (slot.NumberOfGuests < 1)
                return BadRequest(new { message = $"Bàn {table.TableName}: số khách phải từ 1 trở lên" });
            if (slot.NumberOfGuests > table.Capacity)
                return BadRequest(new { message = $"Bàn {table.TableName} chỉ chứa tối đa {table.Capacity} khách" });

            if (isUpcoming && (table.Status == TableStatus.DangPhucVu || table.Status == TableStatus.DangDonDep || table.Status == TableStatus.DaDat))
                return BadRequest(new { message = $"Bàn {table.TableName} đang có khách/đang dọn dẹp, vui lòng chọn bàn khác hoặc giờ đến xa hơn" });

            var overlapped = existing.FirstOrDefault(r => r.TableId == table.TableId &&
                IsOverlapping(request.ReservationTime, durationMinutes, r.ReservationTime, r.DurationMinutes));
            if (overlapped is not null)
                return BadRequest(new
                {
                    message = $"Bàn {table.TableName} đã có lượt đặt từ {overlapped.ReservationTime:HH:mm dd/MM} " +
                              $"đến {overlapped.ReservationEndTime:HH:mm dd/MM}"
                });
        }

        var names = string.Join(", ", request.Tables.Select(s => tables.First(t => t.TableId == s.TableId).TableName));
        var groupNote = request.Tables.Count > 1 ? $"[Đặt ghép {request.Tables.Count} bàn: {names}]" : null;
        var note = string.Join(" ", new[] { request.Note?.Trim(), groupNote }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var created = new List<Reservation>();
        var changedTables = new List<DiningTable>();
        var first = true;

        foreach (var slot in request.Tables)
        {
            var table = tables.First(t => t.TableId == slot.TableId);

            var reservation = new Reservation
            {
                TableId = table.TableId,
                CustomerId = await ResolveCustomerIdAsync(request.CustomerPhone),
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                ReservationTime = request.ReservationTime,
                DurationMinutes = durationMinutes,
                NumberOfGuests = slot.NumberOfGuests,
                DepositAmount = first ? request.DepositAmount : 0,
                Note = string.IsNullOrWhiteSpace(note) ? null : note,
                Status = ReservationStatus.ChoXacNhan
            };
            created.Add(reservation);
            first = false;

            if (table.Status == TableStatus.Trong && isUpcoming)
            {
                table.Status = TableStatus.DaDat;
                changedTables.Add(table);
            }
        }

        _db.Reservations.AddRange(created);
        await _db.SaveChangesAsync();

        foreach (var t in changedTables)
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new { TableId = t.TableId, t.TableName, Status = t.Status });

        await _notifications.CreateForRolesAsync(
            "Có đặt bàn mới",
            $"{(string.IsNullOrWhiteSpace(request.CustomerName) ? "Khách" : request.CustomerName)} đặt {names} lúc {request.ReservationTime:HH:mm dd/MM}",
            NotificationType.DatBan,
            new[] { "ThuNgan", "PhucVu" },
            relatedEntityType: "Reservation",
            relatedEntityId: created[0].ReservationId);

        if (created[0].CustomerId is int customerId)
        {
            await _notifications.CreateForCustomerAsync(
                customerId,
                "Đặt bàn đã được tiếp nhận",
                $"Yêu cầu đặt bàn tại {names} lúc {request.ReservationTime:HH:mm dd/MM} đã được tiếp nhận và đang chờ nhà hàng xác nhận.",
                NotificationType.DatBan,
                $"reservation-batch:{created[0].ReservationId}",
                "Reservation",
                created[0].ReservationId);
        }

        return Ok(new { Id = created[0].ReservationId, ReservationId = created[0].ReservationId, Count = created.Count, Ids = created.Select(r => r.ReservationId).ToList() });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("me/{id:int}/cancel")]
    public async Task<IActionResult> CancelMyBooking(int id)
    {
        var customerId = await ResolveCustomerIdAsync(null);
        if (customerId is null)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var reservation = await _db.Reservations
            .Include(candidate => candidate.Table)
            .FirstOrDefaultAsync(candidate =>
                candidate.ReservationId == id && candidate.CustomerId == customerId.Value);
        if (reservation is null)
            return NotFound(new { message = "Không tìm thấy đặt bàn của bạn." });

        if (reservation.ReservationTime <= DateTime.Now ||
            reservation.Status is not (ReservationStatus.ChoXacNhan or ReservationStatus.DaXacNhan or ReservationStatus.DaNhanBan))
            return BadRequest(new { message = "Đặt bàn hiện tại không đủ điều kiện để hủy." });

        reservation.Status = ReservationStatus.DaHuy;
        var tableStatusChanged = reservation.Table?.Status == TableStatus.DaDat;
        if (tableStatusChanged)
            reservation.Table!.Status = TableStatus.Trong;
        await _db.SaveChangesAsync();

        if (tableStatusChanged && reservation.Table is not null)
        {
            await _hub.Clients.All.SendAsync(
                "TableStatusUpdated",
                new { TableId = reservation.Table.TableId, reservation.Table.TableName, Status = reservation.Table.Status });
        }

        await _hub.Clients.All.SendAsync("ReservationUpdated", new { ReservationId = reservation.ReservationId, Status = reservation.Status.ToString() });
        await _notifications.CreateForCustomerAsync(
            customerId.Value,
            "Đặt bàn đã bị hủy",
            "Đặt bàn của bạn đã được hủy theo yêu cầu.",
            NotificationType.DatBan,
            $"reservation-cancelled:{reservation.ReservationId}",
            "Reservation",
            reservation.ReservationId);

        return Ok(new { Id = reservation.ReservationId, Status = reservation.Status });
    }

    [HttpGet("lookup")]
    [AllowAnonymous]
    public async Task<IActionResult> LookupBooking([FromQuery] string? code, [FromQuery] string? phone)
    {
        var reservation = await FindPublicReservationAsync(code, phone);
        if (reservation is null)
            return NotFound(new { message = "Không tìm thấy đặt bàn phù hợp với mã và số điện thoại." });

        return Ok(ToPublicReservation(reservation));
    }

    [HttpPost("cancel")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelBooking([FromQuery] string? code, [FromQuery] string? phone)
    {
        var reservation = await FindPublicReservationAsync(code, phone);
        if (reservation is null)
            return NotFound(new { message = "Không tìm thấy đặt bàn phù hợp với mã và số điện thoại." });

        if (reservation.ReservationTime <= DateTime.Now ||
            reservation.Status is not (ReservationStatus.ChoXacNhan or ReservationStatus.DaXacNhan or ReservationStatus.DaNhanBan))
            return BadRequest(new { message = "Đặt bàn hiện tại không đủ điều kiện để hủy." });

        reservation.Status = ReservationStatus.DaHuy;
        var tableStatusChanged = reservation.Table?.Status == TableStatus.DaDat;
        if (tableStatusChanged)
            reservation.Table!.Status = TableStatus.Trong;

        await _db.SaveChangesAsync();

        if (tableStatusChanged && reservation.Table is not null)
        {
            await _hub.Clients.All.SendAsync(
                "TableStatusUpdated",
                new { TableId = reservation.Table.TableId, reservation.Table.TableName, Status = reservation.Table.Status });
        }

        if (reservation.CustomerId.HasValue)
        {
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId.Value,
                "Đặt bàn đã bị hủy",
                "Đặt bàn của bạn đã được hủy theo yêu cầu.",
                NotificationType.DatBan,
                $"reservation-cancelled:{reservation.ReservationId}",
                "Reservation",
                reservation.ReservationId);
        }

        return Ok(new { Id = reservation.ReservationId, Status = reservation.Status });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateReservationRequest request)
    {
        var reservation = await _db.Reservations.FindAsync(id);
        if (reservation is null) return NotFound();
        if (reservation.Status != ReservationStatus.DaXacNhan)
            return BadRequest(new { message = "Chỉ có thể sửa đặt bàn đang ở trạng thái Đã xác nhận" });

        var table = await _db.Tables.FindAsync(reservation.TableId);
        if (table is not null && request.NumberOfGuests > table.Capacity)
            return BadRequest(new { message = $"Bàn chỉ chứa tối đa {table.Capacity} khách" });

        var durationError = ValidateDuration(request.DurationMinutes, out var durationMinutes);
        if (durationError is not null) return durationError;

        var others = await _db.Reservations
            .Where(r => r.ReservationId != id && r.TableId == reservation.TableId &&
                        (r.Status == ReservationStatus.ChoXacNhan || r.Status == ReservationStatus.DaXacNhan || r.Status == ReservationStatus.DaNhanBan))
            .ToListAsync();
        var overlapped = others.FirstOrDefault(r => IsOverlapping(request.ReservationTime, durationMinutes, r.ReservationTime, r.DurationMinutes));
        if (overlapped is not null)
            return BadRequest(new
            {
                message = $"Trùng lịch với lượt đặt từ {overlapped.ReservationTime:HH:mm dd/MM} đến {overlapped.ReservationEndTime:HH:mm dd/MM}"
            });

        var detailsChanged =
            reservation.CustomerName != request.CustomerName ||
            reservation.CustomerPhone != request.CustomerPhone ||
            reservation.ReservationTime != request.ReservationTime ||
            reservation.DurationMinutes != durationMinutes ||
            reservation.NumberOfGuests != request.NumberOfGuests ||
            reservation.DepositAmount != request.DepositAmount ||
            reservation.Note != request.Note;
        if (!string.Equals(reservation.CustomerPhone, request.CustomerPhone, StringComparison.Ordinal))
            reservation.CustomerId = null;
        reservation.CustomerName = request.CustomerName;
        reservation.CustomerPhone = request.CustomerPhone;
        reservation.ReservationTime = request.ReservationTime;
        reservation.DurationMinutes = durationMinutes;
        reservation.NumberOfGuests = request.NumberOfGuests;
        reservation.DepositAmount = request.DepositAmount;
        reservation.Note = request.Note;
        await _db.SaveChangesAsync();

        if (detailsChanged && reservation.CustomerId.HasValue)
        {
            var details = string.Join('\n',
                request.CustomerName ?? "",
                request.CustomerPhone ?? "",
                request.ReservationTime.ToString("O"),
                durationMinutes,
                request.NumberOfGuests,
                request.DepositAmount,
                request.Note ?? "");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(details)));
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId.Value,
                "Thông tin đặt bàn đã thay đổi",
                "Thời gian hoặc thông tin đặt bàn của bạn vừa được cập nhật.",
                NotificationType.DatBan,
                $"reservation-details:{reservation.ReservationId}:{hash}",
                "Reservation",
                reservation.ReservationId);
        }

        return Ok(new { Id = reservation.ReservationId });
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] ReservationStatus status)
    {
        if (!Enum.IsDefined(status))
            return BadRequest(new { message = "Trạng thái đặt bàn không hợp lệ." });

        var reservation = await _db.Reservations.Include(r => r.Table).FirstOrDefaultAsync(r => r.ReservationId == id);
        if (reservation is null) return NotFound();

        var previousStatus = reservation.Status;
        reservation.Status = status;

        if (status == ReservationStatus.DaHuy || status == ReservationStatus.KhongDen)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            reservation.CancelledByUserId = int.TryParse(userIdClaim, out var uid) ? uid : null;
            reservation.CancelledAt = DateTime.Now;
        }

        if ((status == ReservationStatus.DaHuy || status == ReservationStatus.KhongDen)
            && reservation.Table is not null && reservation.Table.Status == TableStatus.DaDat)
        {
            reservation.Table.Status = TableStatus.Trong;
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new { TableId = reservation.Table.TableId, reservation.Table.TableName, Status = reservation.Table.Status });
        }

        await _db.SaveChangesAsync();

        if (previousStatus != status && reservation.CustomerId.HasValue)
        {
            var (title, content) = status switch
            {
                ReservationStatus.DaXacNhan => ("Đặt bàn của bạn đã được xác nhận", "Yêu cầu đặt bàn của bạn đã được nhân viên nhà hàng xác nhận thành công."),
                ReservationStatus.DaNhanBan => ("Đã nhận bàn", "Nhà hàng đã ghi nhận bạn đến nhận bàn và bắt đầu dùng bữa."),
                ReservationStatus.DaHuy => ("Đặt bàn của bạn đã bị từ chối / hủy", "Đặt bàn của bạn đã bị hủy hoặc từ chối bởi nhà hàng."),
                ReservationStatus.KhongDen => ("Đặt bàn quá giờ", "Lượt đặt bàn của bạn đã bị hủy do quá thời gian nhận bàn."),
                ReservationStatus.HoanTat => ("Đặt bàn đã hoàn tất", "Lượt đặt bàn của bạn đã hoàn tất. Cảm ơn bạn đã ghé thăm GustoPOS!"),
                _ => ("Cập nhật đặt bàn", "Trạng thái đặt bàn của bạn vừa được cập nhật.")
            };
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId.Value,
                title,
                content,
                NotificationType.DatBan,
                $"reservation-status:{reservation.ReservationId}:{(int)previousStatus}:{(int)status}",
                "Reservation",
                reservation.ReservationId);
        }

        return Ok(new { Id = reservation.ReservationId, Status = reservation.Status });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var reservation = await _db.Reservations.Include(r => r.Table).FirstOrDefaultAsync(r => r.ReservationId == id);
        if (reservation is null) return NotFound();

        if (reservation.Status == ReservationStatus.DaXacNhan && reservation.Table is not null && reservation.Table.Status == TableStatus.DaDat)
        {
            reservation.Table.Status = TableStatus.Trong;
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new { TableId = reservation.Table.TableId, reservation.Table.TableName, Status = reservation.Table.Status });
        }

        _db.Reservations.Remove(reservation);
        await _db.SaveChangesAsync();
        if (reservation.CustomerId.HasValue)
        {
            await _notifications.CreateForCustomerAsync(
                reservation.CustomerId.Value,
                "Đặt bàn đã bị hủy",
                "Đặt bàn của bạn đã được hủy.",
                NotificationType.DatBan,
                $"reservation-deleted:{reservation.ReservationId}",
                "Reservation",
                reservation.ReservationId);
        }
        return NoContent();
    }
}
