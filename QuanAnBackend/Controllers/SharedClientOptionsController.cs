using Microsoft.AspNetCore.Mvc;

namespace QuanAnBackend.Controllers;

/// <summary>
/// Các lựa chọn nghiệp vụ dùng chung cho WinForms và Mobile.
/// WinForms/backend chung là nguồn gốc; Mobile chỉ hiển thị các lựa chọn
/// được endpoint này trả về, không tự hard-code thêm phương thức riêng.
/// </summary>
[ApiController]
[Route("api/client-options")]
public class SharedClientOptionsController : ControllerBase
{
    /// <summary>
    /// ApiService của WinForms dùng method 0|1:
    /// 0 = Tiền mặt, 1 = Chuyển khoản/QR.
    /// Vì vậy Mobile cũng chỉ nhận đúng hai phương thức này.
    /// </summary>
    [HttpGet("payment-methods")]
    public IActionResult GetPaymentMethods()
    {
        return Ok(new[]
        {
            new
            {
                method = 0,
                code = "TienMat",
                label = "Tiền mặt",
                isCash = true
            },
            new
            {
                method = 1,
                code = "ChuyenKhoan",
                label = "Chuyển khoản / QR",
                isCash = false
            }
        });
    }
}
