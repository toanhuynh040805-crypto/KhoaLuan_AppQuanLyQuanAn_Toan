using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/profile/{employeeCode}
    [HttpGet("{employeeCode}")]
    public async Task<IActionResult> GetProfile(
        string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return BadRequest(
                new { message = "Thiếu mã nhân viên" }
            );
        }

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
                    e.EmployeeCode,
                    u.FullName,
                    u.Username,
                    u.Phone,
                    u.Email,
                    e.Position,
                    e.HireDate,
                    e.Status,
                    COALESCE(
                        (
                            SELECT TOP 1 r.RoleName
                            FROM UserRoles ur
                            INNER JOIN Roles r
                                ON r.RoleId = ur.RoleId
                            WHERE ur.UserId = u.UserId
                              AND ur.IsActive = 1
                            ORDER BY ur.AssignedAt DESC
                        ),
                        e.Position
                    ) AS RoleName
                FROM Employees e
                INNER JOIN Users u
                    ON u.UserId = e.UserId
                WHERE e.EmployeeCode = @EmployeeCode;
                """;

            var parameter =
                command.CreateParameter();

            parameter.ParameterName =
                "@EmployeeCode";

            parameter.Value =
                employeeCode.Trim();

            command.Parameters.Add(parameter);

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(
                    new { message = "Không tìm thấy nhân viên" }
                );
            }

            return Ok(
                new
                {
                    employeeCode =
                        reader.GetString(0),
                    fullName =
                        reader.GetString(1),
                    username =
                        reader.GetString(2),
                    phone =
                        reader.IsDBNull(3)
                            ? ""
                            : reader.GetString(3),
                    email =
                        reader.IsDBNull(4)
                            ? ""
                            : reader.GetString(4),
                    position =
                        reader.GetString(5),
                    hireDate =
                        reader.GetDateTime(6)
                            .ToString("yyyy-MM-dd"),
                    employeeStatus =
                        reader.GetString(7),
                    roleName =
                        reader.GetString(8),
                    area = ""
                }
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
