using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Models;
using QuanAnBackend.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("api/customers")]
[ApiController]
[Authorize(Roles = "Customer")]
public class WebProfileController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly NotificationService _notifications;

    public WebProfileController(
        AppDbContext context,
        IWebHostEnvironment environment,
        NotificationService notifications)
    {
        _context = context;
        _environment = environment;
        _notifications = notifications;
    }

    private int GetCurrentCustomerId()
    {
        var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdStr, out int customerId);
        return customerId;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0) return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var customer = await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => new
            {
                c.CustomerId,
                c.FullName,
                c.Phone,
                c.Email,
                c.LoyaltyPoint,
                c.MemberLevel,
                c.AvatarUrl
            })
            .FirstOrDefaultAsync();

        if (customer == null) return NotFound(new { message = "Không tìm thấy thông tin." });
        return Ok(new
        {
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.LoyaltyPoint,
            customer.MemberLevel,
            AvatarUrl = ToPublicAvatarUrl(customer.AvatarUrl)
        });
    }

    [HttpGet("me/orders")]
    public async Task<IActionResult> GetMyOrderHistory()
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0) return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var fullyPaidOrderIds = _context.Orders.Where(order =>
            order.Status == OrderStatus.DaThanhToan &&
            order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.DaThanhToan) &&
            !order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.ChuaThanhToan) &&
            !order.Invoices.SelectMany(invoice => invoice.Payments).Any(payment => payment.Status == PaymentStatus.HoanTien) &&
            !order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.DaThanhToan &&
                (!invoice.Payments.Any(payment => payment.Status == PaymentStatus.ThanhCong) ||
                 invoice.Payments.Where(payment => payment.Status == PaymentStatus.ThanhCong)
                    .Sum(payment => (decimal?)payment.Amount) < invoice.TotalAmount)))
            .Select(order => order.OrderId);

        var orders = await _context.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderTime)
            .Select(o => new
            {
                OrderId = o.OrderId,
                OrderTime = o.OrderTime,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(), // Ép Enum về String để gửi lên frontend
                PaymentStatus = (o.Invoices.Sum(invoice => (decimal?)invoice.TotalAmount) ?? o.TotalAmount) > 0 &&
                    (o.Invoices.SelectMany(invoice => invoice.Payments)
                        .Where(payment => payment.Status == PaymentStatus.ThanhCong)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m) >=
                    (o.Invoices.Sum(invoice => (decimal?)invoice.TotalAmount) ?? o.TotalAmount)
                        ? "ThanhCong"
                        : (o.Invoices.SelectMany(invoice => invoice.Payments)
                            .Where(payment => payment.Status == PaymentStatus.ThanhCong)
                            .Sum(payment => (decimal?)payment.Amount) ?? 0m) > 0
                            ? "ThanhToanMotPhan"
                            : o.Invoices.SelectMany(invoice => invoice.Payments)
                                .Any(payment => payment.Status == PaymentStatus.ThatBai)
                                ? "ThatBai"
                                : o.Invoices.SelectMany(invoice => invoice.Payments)
                                    .Any(payment => payment.Status == PaymentStatus.HoanTien)
                                    ? "HoanTien"
                                    : o.Status == OrderStatus.DaHuy ||
                                      (o.Invoices.Any() && o.Invoices.All(invoice => invoice.Status == InvoiceStatus.DaHuy))
                                        ? "DaHuy"
                                        : "ChuaThanhToan",
                ReviewableItemCount = o.Status == OrderStatus.DaHuy || !fullyPaidOrderIds.Contains(o.OrderId)
                    ? 0
                    : _context.OrderItems.Count(item =>
                        item.OrderId == o.OrderId &&
                        item.MenuItemId != null &&
                        item.Status == KitchenStatus.DaPhucVu),
                ReviewedItemCount = _context.OrderReviews.Count(review =>
                    review.OrderId == o.OrderId &&
                    review.OrderItemId != null &&
                    review.CustomerId == customerId),
                CanReviewExperience = o.Status != OrderStatus.DaHuy && fullyPaidOrderIds.Contains(o.OrderId) &&
                    _context.OrderItems.Any(item =>
                        item.OrderId == o.OrderId &&
                        item.MenuItemId != null &&
                        item.Status == KitchenStatus.DaPhucVu) &&
                    !_context.OrderItems.Any(item =>
                        item.OrderId == o.OrderId &&
                        item.MenuItemId != null &&
                        item.Status != KitchenStatus.DaPhucVu &&
                        item.Status != KitchenStatus.DaHuy),
                HasExperienceReview = _context.OrderReviews.Any(review =>
                    review.OrderId == o.OrderId &&
                    review.OrderItemId == null &&
                    review.CustomerId == customerId),
                OrderItems = _context.OrderItems
                    .Where(oi => oi.OrderId == o.OrderId)
                    .Select(oi => new
                    {
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        MenuItem = new
                        {
                            ItemName = oi.MenuItemId != null
                                ? _context.MenuItems.Where(m => m.MenuItemId == oi.MenuItemId).Select(m => m.ItemName).FirstOrDefault()
                                : "Món ăn"
                        }
                    }).ToList()
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("me/orders/{orderId:int}")]
    public async Task<IActionResult> GetMyOrderDetail(int orderId, CancellationToken cancellationToken)
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var order = await _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Where(o => o.OrderId == orderId && o.CustomerId == customerId)
            .Include(o => o.Table)!
                .ThenInclude(table => table!.Area)
            .Include(o => o.OrderItems)
                .ThenInclude(item => item.MenuItem)
            .Include(o => o.OrderItems)
                .ThenInclude(item => item.StatusHistory)
            .Include(o => o.Invoices)
                .ThenInclude(invoice => invoice.AppliedCampaign)
            .Include(o => o.Invoices)
                .ThenInclude(invoice => invoice.Payments)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });

        var invoices = order.Invoices
            .OrderBy(invoice => invoice.InvoiceDate)
            .ToList();
        var payments = invoices.SelectMany(invoice => invoice.Payments).ToList();
        var totalAmount = invoices.Count > 0
            ? invoices.Sum(invoice => invoice.TotalAmount)
            : order.TotalAmount;
        var paidAmount = payments
            .Where(payment => payment.Status == PaymentStatus.ThanhCong)
            .Sum(payment => payment.Amount);
        var hasFailedPayment = payments.Any(payment => payment.Status == PaymentStatus.ThatBai);
        var hasRefund = payments.Any(payment => payment.Status == PaymentStatus.HoanTien);
        var paymentStatus = totalAmount > 0 && paidAmount >= totalAmount
            ? "ThanhCong"
            : paidAmount > 0
                ? "ThanhToanMotPhan"
                : hasFailedPayment
                    ? "ThatBai"
                    : hasRefund
                        ? "HoanTien"
                        : order.Status == OrderStatus.DaHuy ||
                          (invoices.Count > 0 && invoices.All(invoice => invoice.Status == InvoiceStatus.DaHuy))
                            ? "DaHuy"
                            : "ChuaThanhToan";
        var remainingAmount = order.Status == OrderStatus.DaHuy
            ? 0m
            : Math.Max(0m, totalAmount - paidAmount);

        return Ok(new
        {
            order.OrderId,
            order.OrderTime,
            Status = order.Status.ToString(),
            PaymentStatus = paymentStatus,
            order.OrderType,
            order.TakeawayNumber,
            TakeawayCode = order.TakeawayCode,
            Table = order.Table is null
                ? null
                : new
                {
                    order.Table.TableId,
                    order.Table.TableName,
                    AreaName = order.Table.Area?.AreaName
                },
            order.Note,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            SubTotal = invoices.Count > 0
                ? invoices.Sum(invoice => invoice.SubTotal)
                : order.TotalAmount,
            DiscountAmount = invoices.Sum(invoice => invoice.DiscountAmount),
            TaxAmount = invoices.Sum(invoice => invoice.TaxAmount),
            ServiceFee = invoices.Sum(invoice => invoice.VIPRoomFee + invoice.VIPMinimumGap),
            OrderItems = order.OrderItems
                .OrderBy(item => item.OrderItemId)
                .Select(item => new
                {
                    item.OrderItemId,
                    item.MenuItemId,
                    ItemName = item.MenuItem?.ItemName ?? "Món ăn đã bị xóa",
                    ImageUrl = item.MenuItem?.ImageUrl,
                    item.Quantity,
                    item.UnitPrice,
                    LineTotal = item.Quantity * item.UnitPrice,
                    item.Note,
                    Status = item.Status.ToString(),
                    item.CancelReason,
                    StatusHistory = item.StatusHistory
                        .OrderBy(history => history.ChangedAt)
                        .Select(history => new
                        {
                            Status = history.Status.ToString(),
                            history.ChangedAt
                        })
                        .ToList()
                })
                .ToList(),
            Invoices = invoices.Select(invoice => new
            {
                invoice.InvoiceId,
                invoice.InvoiceDate,
                SubTotal = invoice.SubTotal,
                DiscountAmount = invoice.DiscountAmount,
                CampaignName = invoice.AppliedCampaign?.CampaignName,
                TaxAmount = invoice.TaxAmount,
                ServiceFee = invoice.VIPRoomFee + invoice.VIPMinimumGap,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                Payments = invoice.Payments
                    .OrderBy(payment => payment.PaymentTime)
                    .Select(payment => new
                    {
                        Method = payment.PaymentMethod.ToString(),
                        payment.Amount,
                        payment.PaymentTime,
                        Status = payment.Status.ToString()
                    })
                    .ToList()
            }).ToList()
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileRequest request)
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0) return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        var fullName = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
            return BadRequest(new { message = "Họ tên phải có ít nhất 2 ký tự." });

        if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\d{9,11}$"))
            return BadRequest(new { message = "Số điện thoại phải gồm 9 đến 11 chữ số." });

        if (email is { Length: > 100 })
            return BadRequest(new { message = "Email không được vượt quá 100 ký tự." });

        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            return BadRequest(new { message = "Email không đúng định dạng." });

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound(new { message = "Không tìm thấy thông tin khách hàng." });

        if (await _context.Customers.AnyAsync(c => c.CustomerId != customerId && c.Phone == phone))
            return Conflict(new { message = "Số điện thoại này đã được đăng ký cho khách hàng khác." });

        if (email is not null &&
            await _context.Customers.AnyAsync(c =>
                c.CustomerId != customerId && c.Email != null && c.Email == email))
        {
            return Conflict(new { message = "Email này đã được đăng ký cho khách hàng khác." });
        }

        var changed = customer.FullName != fullName ||
            customer.Phone != phone ||
            customer.Email != email;
        customer.FullName = fullName;
        customer.Phone = phone;
        customer.Email = email;

        await _context.SaveChangesAsync();

        if (changed)
        {
            await _notifications.CreateForCustomerAsync(
                customerId,
                "Thông tin tài khoản đã được cập nhật",
                "Thông tin cá nhân của bạn vừa được cập nhật.",
                NotificationType.TaiKhoan,
                $"customer-profile:{customerId}:{Guid.NewGuid():N}");
        }

        return Ok(new
        {
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.LoyaltyPoint,
            customer.MemberLevel,
            AvatarUrl = ToPublicAvatarUrl(customer.AvatarUrl)
        });
    }

    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024 + 65536)]
    public async Task<IActionResult> UploadMyAvatar(
        IFormFile? avatar,
        CancellationToken cancellationToken)
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0)
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });
        if (avatar is null || avatar.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn ảnh đại diện." });
        if (avatar.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "Ảnh đại diện không được vượt quá 5 MB." });

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (customer is null)
            return NotFound(new { message = "Không tìm thấy thông tin khách hàng." });

        var header = new byte[12];
        await using var input = avatar.OpenReadStream();
        var bytesRead = await input.ReadAsync(header.AsMemory(), cancellationToken);
        var extension = GetImageExtension(header, bytesRead);
        if (extension is null)
            return BadRequest(new { message = "Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP hợp lệ." });

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var avatarDirectory = Path.Combine(webRoot, "customer-avatars");
        Directory.CreateDirectory(avatarDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(avatarDirectory, fileName);
        input.Position = 0;
        await using (var output = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        customer.AvatarUrl = $"/customer-avatars/{fileName}";
        await _context.SaveChangesAsync(cancellationToken);

        await _notifications.CreateForCustomerAsync(
            customerId,
            "Ảnh đại diện đã được cập nhật",
            "Ảnh đại diện tài khoản của bạn vừa được thay đổi.",
            NotificationType.TaiKhoan,
            $"customer-avatar:{customerId}:{Guid.NewGuid():N}");

        return Ok(new
        {
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.LoyaltyPoint,
            customer.MemberLevel,
            AvatarUrl = ToPublicAvatarUrl(customer.AvatarUrl)
        });
    }

    private string? ToPublicAvatarUrl(string? avatarUrl)
    {
        return string.IsNullOrWhiteSpace(avatarUrl)
            ? null
            : $"{Request.Scheme}://{Request.Host}{Request.PathBase}{avatarUrl}";
    }

    private static string? GetImageExtension(byte[] header, int bytesRead)
    {
        if (bytesRead >= 8 &&
            header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return ".png";

        if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        if (bytesRead >= 12 &&
            header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }

    // =========================================================================
    // API MỚI: LẤY LỊCH SỬ ĐẶT BÀN (Sửa lỗi CustomerId)
    // =========================================================================
    [HttpGet("me/bookings")]
    public async Task<IActionResult> GetMyBookingHistory()
    {
        int customerId = GetCurrentCustomerId();
        if (customerId == 0) return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

        // Chỉ dùng khóa khách hàng đã xác thực để tránh lộ lịch của người từng sở hữu cùng số điện thoại.
        var bookings = await _context.Reservations
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.ReservationTime)
            .Select(r => new
            {
                Id = r.ReservationId,
                ReservationTime = r.ReservationTime,
                NumberOfGuests = r.NumberOfGuests,
                Status = r.Status.ToString(),
                TableName = _context.Tables
                    .Where(t => t.TableId == r.TableId)
                    .Select(t => t.TableName)
                    .FirstOrDefault() ?? "Chưa xếp bàn"
            })
            .ToListAsync();

        return Ok(bookings);
    }
}

public sealed class UpdateMyProfileRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string? FullName { get; set; }

    [Required]
    [RegularExpression(@"^\d{9,11}$")]
    public string? Phone { get; set; }

    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }
}
