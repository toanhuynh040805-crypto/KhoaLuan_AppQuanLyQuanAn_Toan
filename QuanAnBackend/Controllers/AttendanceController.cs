using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Services;

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
    string? AreaName = null,
    int LateMinutes = 0,
    int EarlyLeaveMinutes = 0,
    string CheckInStatus = "ChuaDiemDanh",
    string CheckOutStatus = "ChuaKetThuc",
    string AttendanceStatus = "ChuaDiemDanh"
);

public record AttendanceHistoryDto(
    int ShiftAssignmentId,
    string ShiftName,
    string WorkDate,
    string StartTime,
    string EndTime,
    string Status,
    DateTime CheckInTime,
    DateTime CheckOutTime,
    int LateMinutes,
    int EarlyLeaveMinutes,
    string CheckInStatus,
    string CheckOutStatus,
    string AttendanceStatus
);

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly AttendanceSettingsService
        _attendanceSettings;

    public AttendanceController(
        AppDbContext db,
        IConfiguration configuration,
        AttendanceSettingsService
            attendanceSettings)
    {
        _db = db;
        _configuration = configuration;
        _attendanceSettings =
            attendanceSettings;
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
                    COALESCE(
                        assignedAreas.AreaNames,
                        a.AreaName
                    ) AS AreaName
                FROM ShiftAssignments sa
                INNER JOIN Employees e
                    ON e.EmployeeId = sa.EmployeeId
                INNER JOIN Shifts s
                    ON s.ShiftId = sa.ShiftId
                LEFT JOIN Areas a
                    ON a.AreaId = sa.AreaId
                OUTER APPLY
                (
                    SELECT STRING_AGG(
                        ar.AreaName,
                        N', '
                    ) AS AreaNames
                    FROM ShiftAssignmentAreas saa
                    INNER JOIN Areas ar
                        ON ar.AreaId = saa.AreaId
                    WHERE saa.ShiftAssignmentId =
                        sa.ShiftAssignmentId
                ) assignedAreas
                WHERE e.EmployeeCode = @EmployeeCode
                  AND (
                    sa.WorkDate = CAST(GETDATE() AS DATE)
                    OR (
                      sa.WorkDate = DATEADD(
                        DAY,
                        -1,
                        CAST(GETDATE() AS DATE)
                      )
                      AND s.EndTime <= s.StartTime
                      AND sa.CheckInTime IS NOT NULL
                      AND sa.CheckOutTime IS NULL
                    )
                  )
                ORDER BY
                  CASE
                    WHEN sa.CheckInTime IS NOT NULL
                         AND sa.CheckOutTime IS NULL
                      THEN 0
                    WHEN sa.CheckOutTime IS NULL
                         AND sa.Status NOT IN (
                           N'DaHoanThanh',
                           N'VangMat'
                         )
                      THEN 1
                    ELSE 2
                  END,
                  ABS(
                    DATEDIFF(
                      MINUTE,
                      DATEADD(
                        SECOND,
                        DATEDIFF(
                          SECOND,
                          CAST('00:00:00' AS time),
                          s.StartTime
                        ),
                        CAST(sa.WorkDate AS datetime2)
                      ),
                      GETDATE()
                    )
                  ),
                  s.StartTime;
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

            var workDate = reader.GetDateTime(7);
            var startTime = ReadSqlTime(reader.GetValue(2));
            var endTime = ReadSqlTime(reader.GetValue(3));
            var checkInTime = reader.IsDBNull(5)
                ? (DateTime?)null
                : reader.GetDateTime(5);
            var checkOutTime = reader.IsDBNull(6)
                ? (DateTime?)null
                : reader.GetDateTime(6);

            var shiftStart = BuildShiftDateTime(
                workDate,
                startTime
            );
            var shiftEnd = BuildShiftDateTime(
                workDate,
                endTime
            );

            if (shiftEnd <= shiftStart)
                shiftEnd = shiftEnd.AddDays(1);

            var attendance =
                reader.GetString(4) == "VangMat"
                    ? new AttendanceStatusResult(
                        0,
                        0,
                        "VangMat",
                        "KhongApDung",
                        "VangMat"
                    )
                    : BuildAttendanceStatus(
                        shiftStart,
                        shiftEnd,
                        checkInTime,
                        checkOutTime
                    );

            return Ok(
                new AttendanceTodayDto(
                    true,
                    reader.GetInt32(0),
                    reader.GetString(1),
                    FormatSqlTime(reader.GetValue(2)),
                    FormatSqlTime(reader.GetValue(3)),
                    reader.GetString(4),
                    checkInTime,
                    checkOutTime,
                    workDate.ToString("yyyy-MM-dd"),
                    reader.IsDBNull(8)
                        ? null
                        : reader.GetString(8),
                    attendance.LateMinutes,
                    attendance.EarlyLeaveMinutes,
                    attendance.CheckInStatus,
                    attendance.CheckOutStatus,
                    attendance.AttendanceStatus
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

            if (assignment.Status == "VangMat")
            {
                return Conflict(
                    new
                    {
                        message =
                            "Ca làm đã được đánh dấu vắng mặt. " +
                            "Vui lòng liên hệ quản lý nếu cần điều chỉnh."
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

            if (assignment.DatabaseNow >=
                assignment.ShiftEndTime)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Ca làm đã kết thúc. " +
                            "Không thể điểm danh vào ca này."
                    }
                );
            }

            var locationError =
                await ValidateAttendanceLocationAsync(
                    request,
                    connection,
                    transaction
                );

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

            var attendance = BuildAttendanceStatus(
                assignment.ShiftStartTime,
                assignment.ShiftEndTime,
                checkInTime,
                null
            );

            return Ok(
                new
                {
                    assignmentId =
                        assignment.ShiftAssignmentId,
                    status = "DaDiemDanh",
                    checkInTime,
                    checkOutTime =
                        (DateTime?)null,
                    attendance.LateMinutes,
                    attendance.EarlyLeaveMinutes,
                    attendance.CheckInStatus,
                    attendance.CheckOutStatus,
                    attendance.AttendanceStatus
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
                await ValidateAttendanceLocationAsync(
                    request,
                    connection,
                    transaction
                );

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

            var attendance = BuildAttendanceStatus(
                assignment.ShiftStartTime,
                assignment.ShiftEndTime,
                assignment.CheckInTime,
                checkOutTime
            );

            return Ok(
                new
                {
                    assignmentId =
                        assignment.ShiftAssignmentId,
                    status = "DaHoanThanh",
                    checkInTime =
                        assignment.CheckInTime,
                    checkOutTime,
                    attendance.LateMinutes,
                    attendance.EarlyLeaveMinutes,
                    attendance.CheckInStatus,
                    attendance.CheckOutStatus,
                    attendance.AttendanceStatus
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
                var workDate = reader.GetDateTime(2);
                var startTime = ReadSqlTime(reader.GetValue(3));
                var endTime = ReadSqlTime(reader.GetValue(4));
                var checkInTime = reader.GetDateTime(6);
                var checkOutTime = reader.GetDateTime(7);

                var shiftStart = BuildShiftDateTime(
                    workDate,
                    startTime
                );
                var shiftEnd = BuildShiftDateTime(
                    workDate,
                    endTime
                );

                if (shiftEnd <= shiftStart)
                    shiftEnd = shiftEnd.AddDays(1);

                var attendance = BuildAttendanceStatus(
                    shiftStart,
                    shiftEnd,
                    checkInTime,
                    checkOutTime
                );

                result.Add(
                    new AttendanceHistoryDto(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        workDate.ToString("yyyy-MM-dd"),
                        FormatSqlTime(reader.GetValue(3)),
                        FormatSqlTime(reader.GetValue(4)),
                        reader.GetString(5),
                        checkInTime,
                        checkOutTime,
                        attendance.LateMinutes,
                        attendance.EarlyLeaveMinutes,
                        attendance.CheckInStatus,
                        attendance.CheckOutStatus,
                        attendance.AttendanceStatus
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
                CASE
                    WHEN s.EndTime <= s.StartTime
                    THEN DATEADD(
                        DAY,
                        1,
                        DATEADD(
                            SECOND,
                            DATEDIFF(
                                SECOND,
                                CAST('00:00:00' AS time),
                                s.EndTime
                            ),
                            CAST(sa.WorkDate AS datetime2)
                        )
                    )
                    ELSE DATEADD(
                        SECOND,
                        DATEDIFF(
                            SECOND,
                            CAST('00:00:00' AS time),
                            s.EndTime
                        ),
                        CAST(sa.WorkDate AS datetime2)
                    )
                END AS ShiftEndTime,
                GETDATE() AS DatabaseNow
            FROM ShiftAssignments sa
            INNER JOIN Employees e
                ON e.EmployeeId = sa.EmployeeId
            INNER JOIN Shifts s
                ON s.ShiftId = sa.ShiftId
            WHERE e.EmployeeCode = @EmployeeCode
              AND (
                sa.WorkDate = CAST(GETDATE() AS DATE)
                OR (
                  sa.WorkDate = DATEADD(
                    DAY,
                    -1,
                    CAST(GETDATE() AS DATE)
                  )
                  AND s.EndTime <= s.StartTime
                  AND sa.CheckInTime IS NOT NULL
                  AND sa.CheckOutTime IS NULL
                )
              )
            ORDER BY
              CASE
                WHEN sa.CheckInTime IS NOT NULL
                     AND sa.CheckOutTime IS NULL
                  THEN 0
                WHEN sa.CheckOutTime IS NULL
                     AND sa.Status NOT IN (
                       N'DaHoanThanh',
                       N'VangMat'
                     )
                  THEN 1
                ELSE 2
              END,
              ABS(
                DATEDIFF(
                  MINUTE,
                  DATEADD(
                    SECOND,
                    DATEDIFF(
                      SECOND,
                      CAST('00:00:00' AS time),
                      s.StartTime
                    ),
                    CAST(sa.WorkDate AS datetime2)
                  ),
                  GETDATE()
                )
              ),
              s.StartTime;
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
            reader.GetDateTime(5),
            reader.GetDateTime(6)
        );
    }

    private async Task<IActionResult?>
        ValidateAttendanceLocationAsync(
            AttendanceActionRequest request,
            DbConnection connection,
            DbTransaction transaction)
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
                new
                {
                    message =
                        "Dữ liệu vị trí GPS không hợp lệ."
                }
            );
        }

        var settings =
            await _attendanceSettings.GetAsync(
                connection,
                transaction
            );

        if (!settings.IsConfigured ||
            !settings.Latitude.HasValue ||
            !settings.Longitude.HasValue)
        {
            return Conflict(
                new
                {
                    message =
                        "Quản lý chưa thiết lập vị trí nhà hàng. " +
                        "Vui lòng cấu hình trong Cá nhân > " +
                        "Vị trí điểm danh."
                }
            );
        }

        var maxAccuracy = Math.Max(
            1,
            settings.MaxAccuracyMeters
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
            settings.RadiusMeters
        );

        var distance = CalculateDistanceMeters(
            latitude,
            longitude,
            settings.Latitude.Value,
            settings.Longitude.Value
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

    private static TimeSpan ReadSqlTime(
        object value)
    {
        if (value is TimeSpan time)
            return time;

        return TimeSpan.TryParse(
            value.ToString(),
            out var parsed)
            ? parsed
            : TimeSpan.Zero;
    }

    private static DateTime BuildShiftDateTime(
        DateTime workDate,
        TimeSpan time)
    {
        return workDate.Date.Add(time);
    }

    private static AttendanceStatusResult
        BuildAttendanceStatus(
            DateTime shiftStart,
            DateTime shiftEnd,
            DateTime? checkInTime,
            DateTime? checkOutTime)
    {
        var lateMinutes = 0;
        var earlyLeaveMinutes = 0;

        if (checkInTime.HasValue &&
            checkInTime.Value > shiftStart)
        {
            lateMinutes = Math.Max(
                1,
                (int)Math.Ceiling(
                    (checkInTime.Value - shiftStart)
                    .TotalMinutes
                )
            );
        }

        if (checkOutTime.HasValue &&
            checkOutTime.Value < shiftEnd)
        {
            earlyLeaveMinutes = Math.Max(
                1,
                (int)Math.Ceiling(
                    (shiftEnd - checkOutTime.Value)
                    .TotalMinutes
                )
            );
        }

        var checkInStatus =
            !checkInTime.HasValue
                ? "ChuaDiemDanh"
                : lateMinutes > 0
                    ? $"DiTre:{lateMinutes}"
                    : "DungGio";

        var checkOutStatus =
            !checkOutTime.HasValue
                ? "ChuaKetThuc"
                : earlyLeaveMinutes > 0
                    ? $"VeSom:{earlyLeaveMinutes}"
                    : "DuGio";

        var attendanceStatus =
            !checkInTime.HasValue
                ? "ChuaDiemDanh"
                : !checkOutTime.HasValue
                    ? lateMinutes > 0
                        ? "DangLam_DiTre"
                        : "DangLam_DungGio"
                    : lateMinutes > 0 &&
                        earlyLeaveMinutes > 0
                        ? "DiTre_VeSom"
                        : lateMinutes > 0
                            ? "DiTre"
                            : earlyLeaveMinutes > 0
                                ? "VeSom"
                                : "DungGio_DuGio";

        return new AttendanceStatusResult(
            lateMinutes,
            earlyLeaveMinutes,
            checkInStatus,
            checkOutStatus,
            attendanceStatus
        );
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
        DateTime ShiftEndTime,
        DateTime DatabaseNow
    );

    private sealed record AttendanceStatusResult(
        int LateMinutes,
        int EarlyLeaveMinutes,
        string CheckInStatus,
        string CheckOutStatus,
        string AttendanceStatus
    );
}