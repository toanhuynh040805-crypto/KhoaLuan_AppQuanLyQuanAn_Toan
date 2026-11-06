using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanAnBackend.Models;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/config")]
[AllowAnonymous]
public class WebSystemConfigController : ControllerBase
{
    /// <summary>
    /// Cung cấp cấu hình thời lượng giữ bàn để các client (Web/Mobile) đồng bộ động từ Backend,
    /// tránh hard-code giá trị thời gian.
    /// </summary>
    [HttpGet("booking")]
    public IActionResult GetBookingConfig()
    {
        return Ok(new
        {
            defaultDurationMinutes = Reservation.DefaultDurationMinutes,
            minDurationMinutes = Reservation.MinDurationMinutes,
            maxDurationMinutes = Reservation.MaxDurationMinutes
        });
    }
}
