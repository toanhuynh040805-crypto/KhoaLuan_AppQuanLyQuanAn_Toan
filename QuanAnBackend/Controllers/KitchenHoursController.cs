using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers;

[ApiController]
[Route("api/kitchen-hours")]
public class KitchenHoursController : ControllerBase
{
    private readonly KitchenHoursService _kitchenHours;

    public KitchenHoursController(KitchenHoursService kitchenHours)
    {
        _kitchenHours = kitchenHours;
    }

    // Công khai để Web hiển thị cảnh báo cả khi khách chưa đăng nhập.
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var status = _kitchenHours.GetStatus();

        return Ok(new
        {
            isOpen = status.IsOpen,
            openTime = status.OpenTime,
            closeTime = status.CloseTime,
            message = status.Message,
            serverTime = status.NowLocal.ToString("yyyy-MM-ddTHH:mm:ss")
        });
    }
}