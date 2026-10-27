using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

public record AttendanceActionRequest(
    string EmployeeCode,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null
);

public record AttendanceTodayDto(
    bool HasShift,
    int? ShiftAssignmentId,
    string? ShiftName,
    string? StartTime,
    string? EndTime,
    string Status,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    string? WorkDate,
    string? AreaName = null
);

public record AttendanceHistoryDto(
    int ShiftAssignmentId,
    string ShiftName,
    string WorkDate,
    string StartTime,
    string EndTime,
    string Status,
    DateTime CheckInTime,
    DateTime CheckOutTime
);

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AttendanceController(
        AppDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // Chỉ cho phép chấm công/xem lịch sử của CHÍNH MÌNH (mã nhân viên phải thuộc
    // user trong token). QuanLy được xem/thao tác thay để hỗ trợ nhân viên.
    private async Task<bool> CanUseEmployeeCodeAsync(string employeeCode)
    {
        if (User.IsInRole("QuanLy"))
            return true;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var userId))
            return false;

        var code = employeeCode.Trim();
        return await _db.Employees
            .AsNoTracking()
            .AnyAsync(e => e.EmployeeCode == code && e.UserId == userId);
    }

    // GET api/attendance/today?employeeCode=NV003
    [HttpGet("today")]
    public async Task<IActionResult> GetToday(
        [FromQuery] string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return BadRequest(
                new { message = "Thiếu mã nhân viên" }
            );
        }

        if (!await CanUseEmployeeCodeAsync(employeeCode))
            return Forbid();

        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText = """
                SELECT TOP 1
                    sa.ShiftAssignmentId,
                    s.ShiftName,
                    s.StartTime,
                    s.EndTime,
                    sa.Status,
                    sa.CheckInTime,
                    sa.CheckOutTime,
                    sa.WorkDate,
                    a.AreaName
                FROM ShiftAssignments sa
                INNER JOIN Employees e
                    ON e.EmployeeId = sa.EmployeeId
                INNER JOIN Shifts s
                    ON s.ShiftId = sa.ShiftId
                LEFT JOIN Areas a
                    ON a.AreaId = sa.AreaId
                WHERE e.EmployeeCode = @EmployeeCode
                  AND sa.WorkDate = CAST(GETDATE() AS DATE)
                ORDER BY s.StartTime;
                """;

            AddParameter(
                command,
                "@EmployeeCode",
                employeeCode.Trim()
            );

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Ok(
                    new AttendanceTodayDto(
                        false,
                        null,
                        null,
                        null,
                        null,
                        "ChuaXepCa",
                        null,
                        null,
                        null
                    )
                );
            }

            return Ok(
                new AttendanceTodayDto(
                    true,
                    reader.GetInt32(0),
                    reader.GetString(1),
                    FormatSqlTime(reader.GetValue(2)),
                    FormatSqlTime(reader.GetValue(3)),
                    reader.GetString(4),
                    reader.IsDBNull(5)
                        ? null
                        : reader.GetDateTime(5),
                    reader.IsDBNull(6)
                        ? null
                        : reader.GetDateTime(6),
                    reader.GetDateTime(7)
                        .ToString("yyyy-MM-dd"),
                    reader.IsDBNull(8)
                        ? null
                        : reader.GetString(8)
                )
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // POST api/attendance/check-in
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(
        [FromBody] AttendanceActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.EmployeeCode))
        {
            return BadRequest(
                new { message = "Thiếu mã nhân viên" }
            );
        }

        if (!await CanUseEmployeeCodeAsync(request.EmployeeCode))
            return Forbid();

        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            var assignment =
                await GetTodayAssignmentAsync(
                    connection,
                    transaction,
                    request.EmployeeCode.Trim()
                );

            if (assignment is null)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Hôm nay bạn chưa được xếp ca làm"
                    }
                );
            }

            if (assignment.Status == "DaHoanThanh")
            {
                return Conflict(
                    new
                    {
                        message =
                            "Ca làm hôm nay đã kết thúc"
                    }
                );
            }

            if (assignment.CheckInTime.HasValue)
            {
                await transaction.CommitAsync();

                return Ok(
                    new
                    {
                        assignmentId =
                            assignment.ShiftAssignmentId,
                        status =
                            assignment.Status,
                        checkInTime =
                            assignment.CheckInTime,
                        checkOutTime =
                            assignment.CheckOutTime
                    }
                );
            }

            var earlyMinutes = Math.Max(
                0,
                _configuration.GetValue<int?>(
                    "Attendance:CheckInEarlyMinutes")
                    ?? 30
            );

            var allowedFrom =
                assignment.ShiftStartTime.AddMinutes(
                    -earlyMinutes
                );

            if (assignment.DatabaseNow < allowedFrom)
            {
                return Conflict(
                    new
                    {
                        message =
                            $"Chưa đến thời gian điểm danh. " +
                            $"Bạn có thể điểm danh từ " +
                            $"{allowedFrom:HH:mm}."
                    }
                );
            }

            var locationError =
                ValidateAttendanceLocation(request);

            if (locationError is not null)
                return locationError;

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE ShiftAssignments
                SET CheckInTime = GETDATE(),
                    Status = N'DaDiemDanh'
                WHERE ShiftAssignmentId =
                    @ShiftAssignmentId;
                """,
                (
                    "@ShiftAssignmentId",
                    assignment.ShiftAssignmentId
                )
            );

            var checkInTime =
                await ExecuteScalarDateTimeAsync(
                    connection,
                    transaction,
                    """
                    SELECT CheckInTime
                    FROM ShiftAssignments
                    WHERE ShiftAssignmentId =
                        @ShiftAssignmentId;
                    """,
                    (
                        "@ShiftAssignmentId",
                        assignment.ShiftAssignmentId
                    )
                );

            await transaction.CommitAsync();

            return Ok(
                new
                {
                    assignmentId =
                        assignment.ShiftAssignmentId,
                    status = "DaDiemDanh",
                    checkInTime,
                    checkOutTime =
                        (DateTime?)null
                }
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // POST api/attendance/check-out
    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut(
        [FromBody] AttendanceActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.EmployeeCode))
        {
            return BadRequest(
                new { message = "Thiếu mã nhân viên" }
            );
        }

        if (!await CanUseEmployeeCodeAsync(request.EmployeeCode))
            return Forbid();

        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            var assignment =
                await GetTodayAssignmentAsync(
                    connection,
                    transaction,
                    request.EmployeeCode.Trim()
                );

            if (assignment is null)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Hôm nay bạn chưa được xếp ca làm"
                    }
                );
            }

            if (!assignment.CheckInTime.HasValue)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Bạn chưa điểm danh vào ca"
                    }
                );
            }

            if (assignment.CheckOutTime.HasValue ||
                assignment.Status == "DaHoanThanh")
            {
                await transaction.CommitAsync();

                return Ok(
                    new
                    {
                        assignmentId =
                            assignment.ShiftAssignmentId,
                        status = "DaHoanThanh",
                        checkInTime =
                            assignment.CheckInTime,
                        checkOutTime =
                            assignment.CheckOutTime
                    }
                );
            }

            var locationError =
                ValidateAttendanceLocation(request);

            if (locationError is not null)
                return locationError;

            await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE ShiftAssignments
                SET CheckOutTime = GETDATE(),
                    Status = N'DaHoanThanh'
                WHERE ShiftAssignmentId =
                    @ShiftAssignmentId;
                """,
                (
                    "@ShiftAssignmentId",
                    assignment.ShiftAssignmentId
                )
            );

            var checkOutTime =
                await ExecuteScalarDateTimeAsync(
                    connection,
                    transaction,
                    """
                    SELECT CheckOutTime
                    FROM ShiftAssignments
                    WHERE ShiftAssignmentId =
                        @ShiftAssignmentId;
                    """,
                    (
                        "@ShiftAssignmentId",
                        assignment.ShiftAssignmentId
                    )
                );

            await transaction.CommitAsync();

            return Ok(
                new
                {
                    assignmentId =
                        assignment.ShiftAssignmentId,
                    status = "DaHoanThanh",
                    checkInTime =
                        assignment.CheckInTime,
                    checkOutTime
                }
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    // GET api/attendance/history?employeeCode=NV003
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return BadRequest(
                new { message = "Thiếu mã nhân viên" }
            );
        }

        if (!await CanUseEmployeeCodeAsync(employeeCode))
            return Forbid();

        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText = """
                SELECT TOP 180
                    sa.ShiftAssignmentId,
                    s.ShiftName,
                    sa.WorkDate,
                    s.StartTime,
                    s.EndTime,
                    sa.Status,
                    sa.CheckInTime,
                    sa.CheckOutTime
                FROM ShiftAssignments sa
                INNER JOIN Employees e
                    ON e.EmployeeId = sa.EmployeeId
                INNER JOIN Shifts s
                    ON s.ShiftId = sa.ShiftId
                WHERE e.EmployeeCode = @EmployeeCode
                  AND sa.CheckInTime IS NOT NULL
                  AND sa.CheckOutTime IS NOT NULL
                ORDER BY sa.WorkDate DESC,
                         sa.CheckOutTime DESC;
                """;

            AddParameter(
                command,
                "@EmployeeCode",
                employeeCode.Trim()
            );

            var result =
                new List<AttendanceHistoryDto>();

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(
                    new AttendanceHistoryDto(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetDateTime(2)
                            .ToString("yyyy-MM-dd"),
                        FormatSqlTime(
                            reader.GetValue(3)
                        ),
                        FormatSqlTime(
                            reader.GetValue(4)
                        ),
                        reader.GetString(5),
                        reader.GetDateTime(6),
                        reader.GetDateTime(7)
                    )
                );
            }

            return Ok(result);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<TodayAssignment?>
        GetTodayAssignmentAsync(
            DbConnection connection,
            DbTransaction transaction,
            string employeeCode)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = """
            SELECT TOP 1
                sa.ShiftAssignmentId,
                sa.Status,
                sa.CheckInTime,
                sa.CheckOutTime,
                DATEADD(
                    SECOND,
                    DATEDIFF(
                        SECOND,
                        CAST('00:00:00' AS time),
                        s.StartTime
                    ),
                    CAST(sa.WorkDate AS datetime2)
                ) AS ShiftStartTime,
                GETDATE() AS DatabaseNow
            FROM ShiftAssignments sa
            INNER JOIN Employees e
                ON e.EmployeeId = sa.EmployeeId
            INNER JOIN Shifts s
                ON s.ShiftId = sa.ShiftId
            WHERE e.EmployeeCode = @EmployeeCode
              AND sa.WorkDate = CAST(GETDATE() AS DATE)
            ORDER BY s.StartTime;
            """;

        AddParameter(
            command,
            "@EmployeeCode",
            employeeCode
        );

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new TodayAssignment(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2)
                ? null
                : reader.GetDateTime(2),
            reader.IsDBNull(3)
                ? null
                : reader.GetDateTime(3),
            reader.GetDateTime(4),
            reader.GetDateTime(5)
        );
    }

    private IActionResult? ValidateAttendanceLocation(
        AttendanceActionRequest request)
    {
        if (!request.Latitude.HasValue ||
            !request.Longitude.HasValue ||
            !request.AccuracyMeters.HasValue)
        {
            return BadRequest(
                new
                {
                    message =
                        "Không lấy được vị trí GPS hiện tại. " +
                        "Hãy bật vị trí và thử lại."
                }
            );
        }

        var latitude = request.Latitude.Value;
        var longitude = request.Longitude.Value;
        var accuracy = request.AccuracyMeters.Value;

        if (latitude is < -90 or > 90 ||
            longitude is < -180 or > 180 ||
            accuracy < 0)
        {
            return BadRequest(
                new { message = "Dữ liệu vị trí GPS không hợp lệ." }
            );
        }

        var restaurantLatitude =
            _configuration.GetValue<double?>(
                "Attendance:RestaurantLatitude");

        var restaurantLongitude =
            _configuration.GetValue<double?>(
                "Attendance:RestaurantLongitude");

        if (!restaurantLatitude.HasValue ||
            !restaurantLongitude.HasValue)
        {
            return Conflict(
                new
                {
                    message =
                        "Máy chủ chưa cấu hình vị trí nhà hàng. " +
                        "Vui lòng báo quản lý."
                }
            );
        }

        var maxAccuracy = Math.Max(
            1,
            _configuration.GetValue<double?>(
                "Attendance:MaxAccuracyMeters")
                ?? 50
        );

        if (accuracy > maxAccuracy)
        {
            return Conflict(
                new
                {
                    message =
                        $"GPS chưa đủ chính xác (sai số khoảng " +
                        $"{Math.Round(accuracy)} m). " +
                        $"Cần sai số không quá " +
                        $"{Math.Round(maxAccuracy)} m."
                }
            );
        }

        var radius = Math.Max(
            1,
            _configuration.GetValue<double?>(
                "Attendance:RadiusMeters")
                ?? 100
        );

        var distance = CalculateDistanceMeters(
            latitude,
            longitude,
            restaurantLatitude.Value,
            restaurantLongitude.Value
        );

        if (distance > radius)
        {
            return Conflict(
                new
                {
                    message =
                        $"Bạn đang cách nhà hàng khoảng " +
                        $"{Math.Round(distance)} m. " +
                        $"Chỉ được điểm danh trong bán kính " +
                        $"{Math.Round(radius)} m."
                }
            );
        }

        return null;
    }

    private static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusMeters = 6371000;

        static double ToRadians(double degrees)
            => degrees * Math.PI / 180.0;

        var lat1 = ToRadians(latitude1);
        var lat2 = ToRadians(latitude2);
        var deltaLat =
            ToRadians(latitude2 - latitude1);
        var deltaLon =
            ToRadians(longitude2 - longitude1);

        var a =
            Math.Sin(deltaLat / 2) *
            Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) *
            Math.Cos(lat2) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a)
        );

        return earthRadiusMeters * c;
    }

    private static string FormatSqlTime(
        object value)
    {
        if (value is TimeSpan time)
        {
            return
                $"{time.Hours:00}:{time.Minutes:00}";
        }

        return value.ToString() ?? "--:--";
    }

    private static async Task<DateTime?>
        ExecuteScalarDateTimeAsync(
            DbConnection connection,
            DbTransaction transaction,
            string sql,
            params (string Name, object? Value)[]
                parameters)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            AddParameter(
                command,
                parameter.Name,
                parameter.Value
            );
        }

        var value =
            await command.ExecuteScalarAsync();

        if (value is null ||
            value == DBNull.Value)
            return null;

        return Convert.ToDateTime(value);
    }

    private static async Task<int>
        ExecuteAsync(
            DbConnection connection,
            DbTransaction transaction,
            string sql,
            params (string Name, object? Value)[]
                parameters)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            AddParameter(
                command,
                parameter.Name,
                parameter.Value
            );
        }

        return await command
            .ExecuteNonQueryAsync();
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter =
            command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value =
            value ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }

    private sealed record TodayAssignment(
        int ShiftAssignmentId,
        string Status,
        DateTime? CheckInTime,
        DateTime? CheckOutTime,
        DateTime ShiftStartTime,
        DateTime DatabaseNow
    );
}