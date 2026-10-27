using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

public record UpdateAttendanceSettingsRequest(
    double Latitude,
    double Longitude,
    double RadiusMeters = 100,
    double MaxAccuracyMeters = 50
);

[ApiController]
[Route("api/attendance-settings")]
[Authorize(Roles = "QuanLy")]
public class AttendanceSettingsController
    : ControllerBase
{
    private readonly AttendanceSettingsService
        _settings;

    public AttendanceSettingsController(
        AttendanceSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings =
            await _settings.GetAsync();

        return Ok(
            new
            {
                settings.IsConfigured,
                settings.Latitude,
                settings.Longitude,
                settings.RadiusMeters,
                settings.MaxAccuracyMeters,
                settings.UpdatedAt,
                settings.UpdatedBy
            }
        );
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody]
        UpdateAttendanceSettingsRequest request)
    {
        if (request.Latitude is < -90 or > 90)
        {
            return BadRequest(
                new
                {
                    message =
                        "Vĩ độ không hợp lệ."
                }
            );
        }

        if (request.Longitude is < -180 or > 180)
        {
            return BadRequest(
                new
                {
                    message =
                        "Kinh độ không hợp lệ."
                }
            );
        }

        if (request.RadiusMeters is < 10 or > 1000)
        {
            return BadRequest(
                new
                {
                    message =
                        "Bán kính điểm danh phải từ 10 đến 1000 mét."
                }
            );
        }

        if (request.MaxAccuracyMeters is < 5 or > 200)
        {
            return BadRequest(
                new
                {
                    message =
                        "Sai số GPS cho phép phải từ 5 đến 200 mét."
                }
            );
        }

        int? userId = null;

        if (int.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            ),
            out var parsedUserId))
        {
            userId = parsedUserId;
        }

        var settings =
            await _settings.UpdateAsync(
                request.Latitude,
                request.Longitude,
                request.RadiusMeters,
                request.MaxAccuracyMeters,
                userId
            );

        return Ok(
            new
            {
                message =
                    "Đã cập nhật vị trí nhà hàng.",
                settings.IsConfigured,
                settings.Latitude,
                settings.Longitude,
                settings.RadiusMeters,
                settings.MaxAccuracyMeters,
                settings.UpdatedAt,
                settings.UpdatedBy
            }
        );
    }
}
