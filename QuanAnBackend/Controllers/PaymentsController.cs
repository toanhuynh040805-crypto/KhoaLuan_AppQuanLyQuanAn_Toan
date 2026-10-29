using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Hubs;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using QuanAnBackend.Authorization;

namespace QuanAnBackend.Controllers;

// "Method" giữ nguyên kiểu int (0/1) để khớp ApiService.cs bên WinForm
// (đang gửi {orderId, method: 0|1}); 0 = TienMat, khác 0 = ChuyenKhoan (QR).
// "CampaignId" (tùy chọn): mã chiến dịch khuyến mãi Thu ngân chọn áp dụng cho hóa
// đơn này - phải đang hiệu lực (IsActive, trong khoảng ngày) và KHÔNG yêu cầu điều
// kiện khách quen (xem ghi chú ở DiscountCampaign.cs).
// "CustomerId" (tùy chọn): khách hàng thành viên áp dụng cho hóa đơn này - nếu
// Order chưa gắn khách (Order.CustomerId null) thì gán luôn tại đây; nếu khác với
// Order.CustomerId hiện tại thì GHI ĐÈ (Thu ngân chọn/đổi khách ngay lúc thanh toán).
// "RedeemPoints" (tùy chọn): số điểm khách muốn dùng để giảm giá hóa đơn này -
// bắt buộc phải xác định được khách hàng (qua CustomerId hoặc Order đã gắn sẵn).
public record CreatePaymentRequest(
    int OrderId,
    int Method,
    int? CampaignId = null,
    int? CustomerId = null,
    int? RedeemPoints = null,
    string? TransactionRef = null
);
public record RequestPaymentRequest(int TableId, string EmployeeCode);
public record PaymentPreviewDto(
    int OrderId,
    int TableId,
    DateTime OrderTime,
    string WaiterName,
    decimal SubTotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    int? AppliedCampaignId,
    string? AppliedCampaignName,
    decimal TaxAmount,
    decimal VIPRoomFee,
    decimal VIPMinimumGap,
    decimal TotalAmount,
    string QrImageUrl,
    string BankName,
    string BankAccountNo,
    string BankAccountName,
    string BankBranch,
    string TransferContent,
    string ShopName,
    string ShopAddress
);

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly NotificationService _notifications;
    private readonly IEmailService _email;
        public PaymentsController(AppDbContext db, IHubContext<OrderHub> hub, NotificationService notifications, IEmailService email)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
        _email = email;
    }

    // Mobile compatibility: xem trước hóa đơn nhưng chưa ghi thanh toán.
    [HttpGet("preview/table/{tableId}")]
    public async Task<IActionResult> PreviewForMobile(int tableId)
    {
        var orderId = await FindOpenOrderIdForTableAsync(tableId);
        var order = orderId.HasValue
            ? await _db.Orders
                .AsNoTracking()
                .Include(o => o.PaymentRequestedByUser)
                .FirstOrDefaultAsync(
                    o => o.OrderId == orderId.Value)
            : null;

        if (order is null)
            return NotFound(new { message = "Không tìm thấy order đang mở của bàn" });

        var total = await _db.OrderItems
            .Where(oi =>
                oi.OrderId == order.OrderId &&
                oi.Status != KitchenStatus.DaHuy)
            .SumAsync(oi => (decimal?)(
                oi.Quantity *
                (oi.UnitPrice > 0
                    ? oi.UnitPrice
                    : (oi.MenuItem != null
                        ? oi.MenuItem.Price
                        : 0))
            )) ?? 0;

        // Phục vụ chỉ là người thực sự bấm "Gọi thanh toán".
        // Không fallback sang người tạo Order vì có thể là Quản lý mở bàn hộ.
        var waiterName =
            order.PaymentRequestedByUser?.FullName
            ?? "";

        return Ok(new PaymentPreviewDto(
            order.OrderId,
            tableId,
            order.OrderTime,
            waiterName,
            total,
            0,
            0,
            null,
            null,
            0,
            0,
            0,
            total,
            ReceiptProfile.BuildVietQrImageUrl(total, order.OrderId),
            ReceiptProfile.BankCode,
            ReceiptProfile.BankAccountNo,
            ReceiptProfile.BankAccountName,
            ReceiptProfile.BankBranch,
            ReceiptProfile.TransferContent(order.OrderId),
            ReceiptProfile.ShopName,
            ReceiptProfile.ShopAddress
        ));
    }

    // Dùng cho bill tạm tính WinForms khi giỏ hàng có món chưa lưu.
    // Amount chỉ dùng để dựng QR xem trước, không dùng để ghi Payment.
    [HttpGet("receipt-preview")]
    public IActionResult ReceiptPreview(
        [FromQuery] decimal amount,
        [FromQuery] int orderId = 0)
    {
        if (amount < 0)
            return BadRequest(new { message = "Số tiền không hợp lệ" });

        var referenceOrderId = Math.Max(0, orderId);
        var transferContent = referenceOrderId > 0
            ? ReceiptProfile.TransferContent(referenceOrderId)
            : "THANH TOAN TAM TINH";

        var roundedAmount = decimal.ToInt64(
            decimal.Round(amount, 0, MidpointRounding.AwayFromZero));
        var description = Uri.EscapeDataString(transferContent);
        var accountName = Uri.EscapeDataString(ReceiptProfile.BankAccountName);
        var qrImageUrl =
            $"https://img.vietqr.io/image/{ReceiptProfile.BankId}-{ReceiptProfile.BankAccountNo}-qr_only.png" +
            $"?amount={roundedAmount}&addInfo={description}&accountName={accountName}";

        return Ok(new
        {
            orderId = referenceOrderId,
            totalAmount = amount,
            qrImageUrl,
            bankName = ReceiptProfile.BankCode,
            bankAccountNo = ReceiptProfile.BankAccountNo,
            bankAccountName = ReceiptProfile.BankAccountName,
            bankBranch = ReceiptProfile.BankBranch,
            transferContent,
            shopName = ReceiptProfile.ShopName,
            shopAddress = ReceiptProfile.ShopAddress
        });
    }

    // WinForms lấy ảnh QR qua Backend thay vì gọi VietQR trực tiếp.
    // Cách này tránh lỗi TLS/redirect/User-Agent khác nhau trên máy desktop.
    [HttpGet("qr-image")]
    public async Task<IActionResult> GetQrImage(
        [FromQuery] decimal amount,
        [FromQuery] int orderId = 0)
    {
        if (amount <= 0)
            return BadRequest(new { message = "Số tiền QR phải lớn hơn 0" });

        var referenceOrderId = Math.Max(0, orderId);
        var roundedAmount = decimal.ToInt64(
            decimal.Round(amount, 0, MidpointRounding.AwayFromZero));

        var transferContent = referenceOrderId > 0
            ? ReceiptProfile.TransferContent(referenceOrderId)
            : "THANH TOAN TAM TINH";

        var description = Uri.EscapeDataString(transferContent);
        var accountName = Uri.EscapeDataString(ReceiptProfile.BankAccountName);

        var url =
            $"https://img.vietqr.io/image/{ReceiptProfile.BankId}-{ReceiptProfile.BankAccountNo}-qr_only.png" +
            $"?amount={roundedAmount}&addInfo={description}&accountName={accountName}";

        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 GustoPOS/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("image/png,image/*");

            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message =
                            $"VietQR trả về lỗi {(int)response.StatusCode}"
                    });
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new { message = "VietQR không trả về ảnh QR" });
            }

            return File(bytes, "image/png");
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message =
                        $"Không tải được QR từ VietQR: {ex.Message}"
                });
        }
    }

    // WinForms: xem trước đúng số tiền trước khi xác nhận QR.
    // Chưa tạo Invoice/Payment ở bước này.
    [HttpGet("preview/order/{orderId:int}")]
    public async Task<IActionResult> PreviewForOrder(
        int orderId,
        [FromQuery] int? campaignId = null)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.PaymentRequestedByUser)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        if (order.Status == OrderStatus.DaThanhToan)
            return Conflict(new { message = "Đơn hàng đã được thanh toán" });

        var subTotal = await _db.OrderItems
            .Where(oi =>
                oi.OrderId == orderId &&
                oi.Status != KitchenStatus.DaHuy)
            .SumAsync(oi => (decimal?)(
                oi.Quantity *
                (oi.UnitPrice > 0
                    ? oi.UnitPrice
                    : (oi.MenuItem != null
                        ? oi.MenuItem.Price
                        : 0))
            )) ?? 0;

        decimal discountAmount = 0;
        if (campaignId.HasValue)
        {
            var campaign = await _db.DiscountCampaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CampaignId == campaignId.Value);

            if (campaign is null)
                return BadRequest(new { message = "Chương trình khuyến mãi không tồn tại" });
            if (!campaign.IsActive)
                return BadRequest(new { message = "Chương trình khuyến mãi đã bị tắt" });

            var now = DateTime.Now;
            if (now < campaign.StartDate || now > campaign.EndDate)
                return BadRequest(new { message = "Chương trình khuyến mãi hiện không còn hiệu lực" });
            if (campaign.RequireMembership)
                return BadRequest(new { message = "Chương trình này yêu cầu khách hàng thành viên" });

            discountAmount = Math.Round(
                subTotal * campaign.DiscountPercent / 100m,
                0,
                MidpointRounding.AwayFromZero);
        }

        var totalAmount = Math.Max(0, subTotal - discountAmount);

        return Ok(new
        {
            orderId,
            orderTime = order.OrderTime,
            waiterName = order.PaymentRequestedByUser?.FullName ?? "",
            subTotal,
            discountAmount,
            totalAmount,
            qrImageUrl = ReceiptProfile.BuildVietQrImageUrl(totalAmount, orderId),
            bankName = ReceiptProfile.BankCode,
            bankAccountNo = ReceiptProfile.BankAccountNo,
            bankAccountName = ReceiptProfile.BankAccountName,
            bankBranch = ReceiptProfile.BankBranch,
            transferContent = ReceiptProfile.TransferContent(orderId),
            shopName = ReceiptProfile.ShopName,
            shopAddress = ReceiptProfile.ShopAddress
        });
    }

    // Mobile compatibility: Phục vụ gửi yêu cầu tới Thu ngân, chưa thanh toán ngay.
    [HttpPost("request")]
    [RequireMobilePermission("payment.request")]
    public async Task<IActionResult> RequestPaymentForMobile(
        [FromBody] RequestPaymentRequest request)
    {
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var requesterUserId))
        {
            return Unauthorized(new
            {
                message = "Không xác định được tài khoản gọi thanh toán"
            });
        }

        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == requesterUserId);

        if (employee is null)
            return BadRequest(new { message = "Tài khoản hiện tại không gắn với nhân viên" });

        var requestOrderId = await FindOpenOrderIdForTableAsync(request.TableId);
                var order = requestOrderId.HasValue
            ? await _db.Orders
                .Include(o => o.Table)!.ThenInclude(t => t!.Area)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == requestOrderId.Value)
            : null;

        if (order is null)
            return NotFound(new { message = "Không tìm thấy order đang mở của bàn" });

        // Đơn mang về cho phép yêu cầu thanh toán sớm (món vẫn tiếp tục được bếp làm).
        if (!IsTakeawayOrder(order))
        {
            var unservedCount = order.OrderItems.Count(
                x => x.Status != KitchenStatus.DaPhucVu &&
                     x.Status != KitchenStatus.DaHuy);

            if (unservedCount > 0)
            {
                return Conflict(new
                {
                    message = $"Còn {unservedCount} món chưa được phục vụ. Chưa thể yêu cầu thanh toán."
                });
            }
        }

        var tableName = order.Table?.TableName ?? $"Bàn #{request.TableId}";

        // Một yêu cầu thanh toán giữ quyền trong 5 phút.
        // Trong thời gian này KHÔNG ai (kể cả chính người vừa bấm) được gọi lại.
        // Sau 5 phút mà Order vẫn chưa thanh toán thì người khác có thể tạo yêu cầu mới,
        // và người đó trở thành "Phục vụ" của hóa đơn.
        var now = DateTime.Now;
        var requestLockDuration = TimeSpan.FromMinutes(5);

        if (order.PaymentRequestedAt.HasValue)
        {
            var lockUntil = order.PaymentRequestedAt.Value.Add(requestLockDuration);
            if (now < lockUntil)
            {
                var remaining = lockUntil - now;
                var remainingSeconds = Math.Max(
                    1,
                    (int)Math.Ceiling(remaining.TotalSeconds));

                return Conflict(new
                {
                    message =
                        $"Bàn này đã có yêu cầu thanh toán. Vui lòng chờ {remainingSeconds / 60}:{remainingSeconds % 60:00} trước khi gọi lại.",
                    retryAfterSeconds = remainingSeconds,
                    lockUntil,
                    requestedByUserId = order.PaymentRequestedByUserId
                });
            }
        }

        // Lưu audit ngay trên Order: đây là nguồn duy nhất để xác định "Phục vụ".
        order.PaymentRequestedByUserId = requesterUserId;
        order.PaymentRequestedAt = now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai thiết bị bấm gần như cùng lúc: RowVer chỉ cho request đầu tiên thắng.
            await _db.Entry(order).ReloadAsync();

            var lockUntil = (order.PaymentRequestedAt ?? DateTime.Now)
                .Add(requestLockDuration);
            var remainingSeconds = Math.Max(
                1,
                (int)Math.Ceiling((lockUntil - DateTime.Now).TotalSeconds));

            return Conflict(new
            {
                message =
                    $"Bàn này vừa có người khác gọi thanh toán. Vui lòng chờ {remainingSeconds / 60}:{remainingSeconds % 60:00} trước khi gọi lại.",
                retryAfterSeconds = remainingSeconds,
                lockUntil,
                requestedByUserId = order.PaymentRequestedByUserId
            });
        }

        await _notifications.CreateForPermissionAsync(
            "Yêu cầu thanh toán",
            $"{tableName} đang yêu cầu thanh toán",
            NotificationType.DonHang,
            "payment.complete",
            relatedEntityType: "Order",
            relatedEntityId: order.OrderId
        );

        await _notifications.CreateAsync(
            "Đã gửi yêu cầu thanh toán",
            $"{tableName} đã gửi yêu cầu thanh toán đến Thu ngân.",
            NotificationType.DonHang,
            targetUserId: requesterUserId,
            relatedEntityType: "Order",
            relatedEntityId: order.OrderId
        );

        await _hub.Clients.All.SendAsync("PaymentRequested", new
        {
            TableId = order.TableId,
            TableName = tableName,
            OrderId = order.OrderId,
            TotalAmount = order.TotalAmount
        });

        return Ok(new
        {
            orderId = order.OrderId,
            tableId = request.TableId,
            requestedByUserId = requesterUserId
        });
    }

    // POST api/payments -> Thu ngân tạo hóa đơn (Invoice) + ghi nhận thanh toán
    // (Payment) + (nếu có khách hàng) tích/đổi điểm khách hàng thân thiết.
    // TODO: tích hợp thật với VietQR.io hoặc API ngân hàng ở đây
    [HttpPost]
    [RequireMobilePermission("payment.complete")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        var order = await _db.Orders
            .Include(o => o.Table)!.ThenInclude(t => t!.Area)
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId);

        if (order is null) return NotFound();
        if (order.Status == OrderStatus.DaThanhToan)
            return BadRequest(new { message = "Đơn hàng này đã được thanh toán" });

        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var cashierUserId))
        {
            return Unauthorized(new { message = "Không xác định được tài khoản xác nhận thanh toán" });
        }

        // Phục vụ = đúng tài khoản đã bấm "Gọi thanh toán".
        // Nếu chưa có yêu cầu thanh toán thì để trống, tuyệt đối không lấy người tạo Order.
        var requestedByUserId = order.PaymentRequestedByUserId;

        // Tinh truc tiep tu OrderItems de khong phu thuoc vao du lieu TotalAmount
        // cu/stale trong cac database da seed hoac nang cap truoc day.
        var total = await _db.OrderItems
            .Where(oi =>
                oi.OrderId == order.OrderId &&
                oi.Status != KitchenStatus.DaHuy)
            .SumAsync(oi => (decimal?)(
                oi.Quantity *
                (oi.UnitPrice > 0
                    ? oi.UnitPrice
                    : (oi.MenuItem != null
                        ? oi.MenuItem.Price
                        : 0))
            )) ?? 0;
        var method = request.Method switch
        {
            0 => PaymentMethod.TienMat,
            2 => PaymentMethod.TheNganHang,
            3 => PaymentMethod.ViDienTu,
            _ => PaymentMethod.ChuyenKhoan
        };

        // Áp dụng khuyến mãi (nếu Thu ngân có chọn) - validate LẠI ở đây (không tin
        // tưởng con số % mà WinForm tự tính) để tránh 1 request giả mạo/lỗi thời gian
        // (vd chiến dịch vừa hết hạn ngay lúc bấm) làm sai số tiền thật thu vào.
        decimal discountAmount = 0;
        DiscountCampaign? campaign = null;
        if (request.CampaignId.HasValue)
        {
            campaign = await _db.DiscountCampaigns.FindAsync(request.CampaignId.Value);
            if (campaign is null) return BadRequest(new { message = "Chương trình khuyến mãi không tồn tại" });
            if (!campaign.IsActive) return BadRequest(new { message = "Chương trình khuyến mãi này đã bị tắt" });
            var now = DateTime.Now;
            if (now < campaign.StartDate || now > campaign.EndDate)
                return BadRequest(new { message = "Chương trình khuyến mãi này hiện không còn hiệu lực" });
            if (campaign.RequireMembership)
                return BadRequest(new { message = "Chương trình này chỉ áp dụng cho khách hàng thân thiết - hệ thống hiện chưa hỗ trợ kiểm tra điều kiện này" });

            discountAmount = Math.Round(total * campaign.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        }

        // Xác định khách hàng thành viên áp dụng cho hóa đơn (nếu có) - ưu tiên
        // CustomerId gửi kèm request, nếu không có thì dùng khách đã gắn sẵn trên Order.
        var customerId = request.CustomerId ?? order.CustomerId;
        Customer? customer = null;
        if (customerId.HasValue)
        {
            customer = await _db.Customers.FindAsync(customerId.Value);
            if (customer is null) return BadRequest(new { message = "Khách hàng không tồn tại" });
            order.CustomerId = customer.CustomerId;
        }

        var afterCampaignTotal = total - discountAmount;

        // Đổi điểm tích lũy lấy giảm giá (nếu khách yêu cầu) - validate số điểm đổi
        // không vượt quá điểm hiện có VÀ không vượt quá giá trị hóa đơn còn lại
        // (tránh đổi điểm ra số tiền âm).
        int redeemedPoints = 0;
        decimal loyaltyDiscount = 0;
        if (request.RedeemPoints.HasValue && request.RedeemPoints.Value > 0)
        {
            if (customer is null)
                return BadRequest(new { message = "Cần xác định khách hàng thành viên để đổi điểm" });

            var maxRedeemable = Math.Min(customer.LoyaltyPoint, (int)Math.Floor(afterCampaignTotal / CustomerLoyaltyHelper.PointValueVnd));
            if (request.RedeemPoints.Value > maxRedeemable)
                return BadRequest(new { message = $"Khách chỉ có thể đổi tối đa {maxRedeemable} điểm cho hóa đơn này" });

            redeemedPoints = request.RedeemPoints.Value;
            loyaltyDiscount = redeemedPoints * CustomerLoyaltyHelper.PointValueVnd;
        }

        var netTotal = afterCampaignTotal - loyaltyDiscount;

        var invoice = new Invoice
        {
            OrderId = order.OrderId,
            InvoiceDate = DateTime.Now,
            SubTotal = total,
            DiscountAmount = discountAmount + loyaltyDiscount,
            AppliedCampaignId = campaign?.CampaignId,
            TaxAmount = 0,
            TotalAmount = netTotal,
            Status = InvoiceStatus.DaThanhToan,
            CreatedBy = cashierUserId, // legacy compatibility
            RequestedByUserId = requestedByUserId,
            ConfirmedByUserId = cashierUserId
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        // Tích/đổi điểm cho khách (nếu có) - ghi log vào LoyaltyPointHistory để tra
        // cứu lại sau này, và cập nhật hạng thành viên theo số dư điểm mới.
        int earnedPoints = 0;
        MemberLevel? previousLevel = null;
        if (customer is not null)
        {
            previousLevel = customer.MemberLevel;

            if (redeemedPoints > 0)
            {
                customer.LoyaltyPoint -= redeemedPoints;
                _db.LoyaltyPointHistories.Add(new LoyaltyPointHistory
                {
                    CustomerId = customer.CustomerId,
                    Points = -redeemedPoints,
                    Type = LoyaltyPointType.DoiDiem,
                    RelatedInvoiceId = invoice.InvoiceId,
                    CreatedAt = DateTime.Now
                });
            }

            earnedPoints = CustomerLoyaltyHelper.CalculateEarnedPoints(netTotal);
            if (earnedPoints > 0)
            {
                customer.LoyaltyPoint += earnedPoints;
                _db.LoyaltyPointHistories.Add(new LoyaltyPointHistory
                {
                    CustomerId = customer.CustomerId,
                    Points = earnedPoints,
                    Type = LoyaltyPointType.TichDiem,
                    RelatedInvoiceId = invoice.InvoiceId,
                    CreatedAt = DateTime.Now
                });
            }

                        customer.MemberLevel = CustomerLoyaltyHelper.CalculateMemberLevel(customer.LoyaltyPoint);

            if (customer.MemberLevel != previousLevel)
            {
                await _notifications.CreateForRolesAsync(
                    "Khách hàng thăng hạng thành viên",
                    $"{customer.FullName} vừa lên hạng {customer.MemberLevel}",
                    NotificationType.KhuyenMai,
                    new[] { "QuanLy", "ThuNgan" },
                    relatedEntityType: "Customer",
                    relatedEntityId: customer.CustomerId);
            }
        }

        var qrImageUrl = ReceiptProfile.BuildVietQrImageUrl(
            netTotal,
            order.OrderId);

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            PaymentMethod = method,
            Amount = netTotal,
            PaymentTime = DateTime.Now,
            Status = PaymentStatus.ThanhCong,
            TransactionRef = request.TransactionRef
        };
        _db.Payments.Add(payment);

                // Đơn mang về không có bước "phục vụ": khách nhận đồ khi thanh toán nên đóng nốt
        // các món còn lại thành "Đã phục vụ". Phải lưu các món TRƯỚC và nạp lại RowVer của
        // đơn (trigger SQL ghi lại dòng Orders làm RowVer đổi) rồi mới đổi trạng thái đơn.
        if (IsTakeawayOrder(order))
        {
            var actorUserId = int.TryParse(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var uid) ? uid : (int?)null;

            // Chỉ đóng các món Bếp ĐÃ LÀM XONG. Món còn Mới gọi / Đang chế biến phải
            // giữ nguyên để Bếp vẫn thấy và chế biến sau khi khách đã trả tiền.
            var remainingItems = await _db.OrderItems
                .Where(oi =>
                    oi.OrderId == order.OrderId &&
                    oi.Status == KitchenStatus.DaCheBien)
                .ToListAsync();

            foreach (var remaining in remainingItems)
            {
                remaining.Status = KitchenStatus.DaPhucVu;
                _db.DishStatusHistories.Add(new DishStatusHistory
                {
                    OrderItemId = remaining.OrderItemId,
                    Status = KitchenStatus.DaPhucVu,
                    ChangedAt = DateTime.Now,
                    ChangedBy = actorUserId
                });
            }

            if (remainingItems.Count > 0)
            {
                await _db.SaveChangesAsync();
                await _db.Entry(order).ReloadAsync();
            }
        }

        order.Status = OrderStatus.DaThanhToan;
        
        // Đóng các yêu cầu thanh toán của chính Order này. IsRead chỉ để dọn
        // notification; endpoint pending còn kiểm tra thêm Order.Status.
        var paymentRequestNotifications = await _db.Notifications
            .Where(n =>
                n.Title == "Yêu cầu thanh toán" &&
                n.RelatedEntityType == "Order" &&
                n.RelatedEntityId == order.OrderId)
            .ToListAsync();
        foreach (var notification in paymentRequestNotifications)
            notification.IsRead = true;

        // Trả bàn về trạng thái Trống sau khi thanh toán xong
        if (order.Table is not null)
        {
            order.Table.Status = TableStatus.Trong;

            // FIX: dong luot dat ban gan voi luot ngoi nay lai NGAY LUC NAY (neu co).
            // Truoc day khong co buoc nay -> Reservation.Status dung mai o DaNhanBan
            // du khach da tra ban tu lau, khien no tiep tuc bi tinh la "con hieu
            // luc" va chan khach MOI dat lai chinh khung gio do (xem
            // ReservationsController, phan kiem tra trung lich).
            var finishedReservations = await _db.Reservations
                .Where(r => r.TableId == order.Table.TableId && r.Status == ReservationStatus.DaNhanBan)
                .ToListAsync();
            foreach (var r in finishedReservations) r.Status = ReservationStatus.HoanTat;
        }

        await _db.SaveChangesAsync();

        // Một order có thể ghép nhiều bàn vật lý. Thanh toán một lần phải trả
        // toàn bộ bàn thuộc order về Trống.
        var linkedTableIds = await GetLinkedTableIdsAsync(order.OrderId, order.TableId);
        if (linkedTableIds.Count > 0)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                var parameterNames = new List<string>();
                for (var i = 0; i < linkedTableIds.Count; i++)
                {
                    var p = command.CreateParameter();
                    p.ParameterName = $"@p{i}";
                    p.Value = linkedTableIds[i];
                    command.Parameters.Add(p);
                    parameterNames.Add(p.ParameterName);
                }
                command.CommandText = $"UPDATE DiningTables SET Status = N'Trong' WHERE TableId IN ({string.Join(",", parameterNames)});";
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }

        if (order.Table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = order.Table.TableId,
                order.Table.TableName,
                Status = order.Table.Status
            });
        }

        foreach (var linkedTableId in linkedTableIds.Where(id => id != order.TableId))
        {
            await _hub.Clients.All.SendAsync("TableStatusUpdated", new
            {
                TableId = linkedTableId,
                Status = TableStatus.Trong
            });
        }

        // Phát cho tất cả client để cả WinForms và Mobile đều đóng trạng thái
        // chờ thanh toán/bàn ngay sau khi giao dịch hoàn tất.
        await _hub.Clients.All.SendAsync("PaymentCompleted", new
        {
            tableId = order.TableId,
            orderId = order.OrderId,
            invoiceId = invoice.InvoiceId,
            totalAmount = netTotal
        });

        // Gửi hóa đơn qua email cho khách hàng thân thiết (nếu có gắn khách và
        // khách có Email) - KHÔNG để lỗi gửi email làm hỏng cả giao dịch thanh
        // toán đã thành công, chỉ ghi log để biết mà không chặn response.
        var emailSent = false;
        if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
        {
            try
            {
                var orderItemsForEmail = await _db.OrderItems.AsNoTracking()
                    .Include(oi => oi.MenuItem)
                    .Where(oi => oi.OrderId == order.OrderId && oi.Status != KitchenStatus.DaHuy)
                    .ToListAsync();

                var html = InvoiceEmailBuilder.BuildInvoiceHtml(
                    customer,
                    invoice,
                    order.Table?.TableName ?? "Mang về",
                    orderItemsForEmail,
                    earnedPoints,
                    redeemedPoints,
                    previousLevel);

                await _email.SendAsync(customer.Email, customer.FullName, $"Hóa đơn #{invoice.InvoiceId} - Cảm ơn quý khách!", html);
                emailSent = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Gửi email hóa đơn thất bại cho khách #{customer.CustomerId}: {ex.Message}");
            }
        }

        return Ok(new
        {
            Id = payment.PaymentId,
            OrderId = order.OrderId,
            InvoiceId = invoice.InvoiceId, // de WinForm goi GET /api/invoices/{id} lay hoa don chi tiet, in bien lai
            SubTotal = total,
            DiscountAmount = discountAmount + loyaltyDiscount,
            TotalAmount = netTotal,
            Method = request.Method,
            QRCodeImageUrl = qrImageUrl,
            QRCodeContent = qrImageUrl,
            BankName = ReceiptProfile.BankCode,
            BankAccountNo = ReceiptProfile.BankAccountNo,
            BankAccountName = ReceiptProfile.BankAccountName,
            BankBranch = ReceiptProfile.BankBranch,
            TransferContent = ReceiptProfile.TransferContent(order.OrderId),
            IsPaid = payment.Status == PaymentStatus.ThanhCong,
            CustomerId = customer?.CustomerId,
            PointsEarned = earnedPoints,
            PointsRedeemed = redeemedPoints,
            LoyaltyDiscount = loyaltyDiscount,
            LoyaltyPointBalance = customer?.LoyaltyPoint,
            EmailSent = emailSent
        });
    }

    // Đơn mang về = OrderType MangVe (tạo ở WinForm) HOẶC bàn thuộc khu vực "Mang về" (tạo ở Mobile).
    // Yêu cầu order.Table và order.Table.Area đã được Include.
    private static bool IsTakeawayOrder(Order order) =>
        order.OrderType == OrderTypes.MangVe ||
        string.Equals(order.Table?.Area?.AreaName?.Trim(), "Mang về",
            StringComparison.OrdinalIgnoreCase);

    private async Task<int?> FindOpenOrderIdForTableAsync(int tableId)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP 1 o.OrderId
                FROM Orders o
                LEFT JOIN OrderTables ot ON ot.OrderId = o.OrderId
                WHERE o.Status = N'DangMo'
                  AND (o.TableId = @TableId OR ot.TableId = @TableId)
                ORDER BY o.OrderTime DESC, o.OrderId DESC;
                """;
            var p = command.CreateParameter();
            p.ParameterName = "@TableId";
            p.Value = tableId;
            command.Parameters.Add(p);
            var value = await command.ExecuteScalarAsync();
            return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private async Task<List<int>> GetLinkedTableIdsAsync(int orderId, int? primaryTableId)
    {
        var result = new HashSet<int>();
        if (primaryTableId.HasValue) result.Add(primaryTableId.Value);
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TableId FROM OrderTables WHERE OrderId = @OrderId;";
            var p = command.CreateParameter();
            p.ParameterName = "@OrderId";
            p.Value = orderId;
            command.Parameters.Add(p);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(reader.GetInt32(0));
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
        return result.ToList();
    }

}