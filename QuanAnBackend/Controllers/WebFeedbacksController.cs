using Microsoft.AspNetCore.SignalR;
using QuanAnBackend.Hubs;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;
using QuanAnBackend.Middleware;
using QuanAnBackend.Models;
using QuanAnBackend.Services;

namespace QuanAnBackend.Controllers
{
    [Route("api/web-feedbacks")]
    [ApiController]
    [Authorize]
    public class WebFeedbacksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebReviewContentClassifier _contentClassifier;
        private readonly NotificationService _notifications;
        private readonly IHubContext<OrderHub> _hub;

        public WebFeedbacksController(
            AppDbContext context,
            IWebReviewContentClassifier contentClassifier,
            NotificationService notifications,
            IHubContext<OrderHub> hub)
        {
            _context = context;
            _contentClassifier = contentClassifier;
            _notifications = notifications;
            _hub = hub;
        }

        private int? GetCurrentCustomerId()
            => CustomerTokens.GetCustomerId(User);

        private IQueryable<Order> GetPaidOrders() => _context.Orders.Where(order =>
            order.Status == OrderStatus.DaThanhToan &&
            order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.DaThanhToan) &&
            !order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.ChuaThanhToan) &&
            !order.Invoices.SelectMany(invoice => invoice.Payments).Any(payment => payment.Status == PaymentStatus.HoanTien) &&
            !order.Invoices.Any(invoice => invoice.Status == InvoiceStatus.DaThanhToan &&
                (!invoice.Payments.Any(payment => payment.Status == PaymentStatus.ThanhCong) ||
                 invoice.Payments.Where(payment => payment.Status == PaymentStatus.ThanhCong)
                    .Sum(payment => (decimal?)payment.Amount) < invoice.TotalAmount)));

        [AllowAnonymous]
        [HttpGet("menu-items/{menuItemId:int}")]
        public async Task<IActionResult> GetMenuItemReviews(
            int menuItemId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 10,
            [FromQuery] int? rating = null,
            [FromQuery] string sort = "newest",
            CancellationToken cancellationToken = default)
        {
            if (skip < 0 || take is < 1 or > 50 || rating is < 1 or > 5 || (sort != "newest" && sort != "oldest"))
                return BadRequest(new { message = "Thông tin phân trang không hợp lệ." });

            if (!await _context.MenuItems.AnyAsync(item => item.MenuItemId == menuItemId, cancellationToken))
                return NotFound(new { message = "Không tìm thấy món ăn." });

            var reviews = _context.OrderReviews
                .AsNoTracking()
                .Where(review =>
                    review.IsVisible &&
                    review.ModerationStatus == nameof(WebReviewModerationDecision.Public) &&
                    review.OrderItem != null &&
                    review.OrderItem.MenuItemId == menuItemId &&
                    review.OrderItem.Status == KitchenStatus.DaPhucVu &&
                    GetPaidOrders().Select(order => order.OrderId).Contains(review.OrderId));

            var reviewCount = await reviews.CountAsync(cancellationToken);
            var averageRating = reviewCount == 0
                ? 0
                : await reviews.AverageAsync(review => (double)review.Rating, cancellationToken);

            var filteredReviews = rating.HasValue ? reviews.Where(review => review.Rating == rating.Value) : reviews;
            var filteredReviewCount = await filteredReviews.CountAsync(cancellationToken);
            var orderedReviews = sort == "oldest"
                ? filteredReviews.OrderBy(review => review.CreatedAt).ThenBy(review => review.ReviewId)
                : filteredReviews.OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.ReviewId);

            var items = await orderedReviews
                .Skip(skip)
                .Take(take)
                .Select(review => new
                {
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    CustomerName = review.Customer != null && !string.IsNullOrWhiteSpace(review.Customer.FullName)
                        ? review.Customer.FullName
                        : "Khách hàng GustoPOS",
                    VerifiedPurchase = true
                })
                .ToListAsync(cancellationToken);

            var ratingCounts = await reviews
                .GroupBy(r => r.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Rating, g => g.Count, cancellationToken);

            var ratingBreakdown = Enumerable.Range(1, 5)
                .ToDictionary(r => r, r => ratingCounts.GetValueOrDefault((byte)r, 0));

            return Ok(new
            {
                MenuItemId = menuItemId,
                AverageRating = Math.Round(averageRating, 1),
                ReviewCount = reviewCount,
                RatingBreakdown = ratingBreakdown,
                FilteredReviewCount = filteredReviewCount,
                Skip = skip,
                Take = take,
                HasMore = skip + items.Count < filteredReviewCount,
                Reviews = items
            });
        }

        [Authorize(Roles = "QuanLy")]
        [HttpGet("moderation")]
        public async Task<IActionResult> GetReviewsForModeration(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            [FromQuery] string? status = null,
            CancellationToken cancellationToken = default)
        {
            if (skip < 0 || take is < 1 or > 100)
                return BadRequest(new { message = "Invalid pagination." });

            var query = _context.OrderReviews.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim();
                query = query.Where(r => r.ModerationStatus == s);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var reviews = await query
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.ReviewId)
                .Skip(skip).Take(take)
                .Select(review => new
                {
                    review.ReviewId,
                    review.OrderId,
                    review.OrderItemId,
                    ItemName = review.OrderItem != null && review.OrderItem.MenuItem != null
                        ? review.OrderItem.MenuItem.ItemName
                        : "Đánh giá trải nghiệm",
                    CustomerName = review.Customer != null && !string.IsNullOrWhiteSpace(review.Customer.FullName)
                        ? review.Customer.FullName
                        : "Khách hàng",
                    CustomerEmail = review.Customer != null ? review.Customer.Email : "",
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    review.UpdatedAt,
                    review.IsVisible,
                    review.ModerationStatus
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                Total = totalCount,
                Skip = skip,
                Take = take,
                Items = reviews
            });
        }

        [Authorize(Roles = "QuanLy")]
        [HttpPut("moderation/{reviewId:int}/visibility")]
        public async Task<IActionResult> SetReviewVisibility(
            int reviewId,
            [FromBody] SetReviewVisibilityRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _context.OrderReviews.FirstOrDefaultAsync(
                candidate => candidate.ReviewId == reviewId, cancellationToken);
            if (review is null) return NotFound();
            if (review.ModerationStatus == nameof(WebReviewModerationDecision.Pending) ||
                review.ModerationStatus == nameof(WebReviewModerationDecision.Rejected))
            {
                return Conflict(new { code = "REVIEW_NOT_APPROVED", message = "Đánh giá đang chờ xử lý hoặc đã bị từ chối; chỉ đánh giá đã duyệt mới có thể ẩn hoặc hiện." });
            }

            review.IsVisible = request.IsVisible;
            if (!request.IsVisible && review.ModerationStatus == nameof(WebReviewModerationDecision.Public))
                review.ModerationStatus = nameof(WebReviewModerationDecision.Hidden);
            else if (request.IsVisible && review.ModerationStatus == nameof(WebReviewModerationDecision.Hidden))
                review.ModerationStatus = nameof(WebReviewModerationDecision.Public);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = "QuanLy")]
        [HttpPut("moderation/{reviewId:int}/decision")]
        public async Task<IActionResult> SetReviewModerationDecision(
            int reviewId,
            [FromBody] SetReviewModerationDecisionRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _context.OrderReviews.FirstOrDefaultAsync(
                candidate => candidate.ReviewId == reviewId, cancellationToken);
            if (review is null) return NotFound();
            if (review.ModerationStatus != nameof(WebReviewModerationDecision.Pending))
                return Conflict(new { code = "REVIEW_NOT_PENDING", message = "Chỉ đánh giá đang chờ duyệt mới nhận quyết định kiểm duyệt." });
            if (request.Decision is not ("Public" or "Rejected"))
                return BadRequest(new { code = "INVALID_MODERATION_DECISION", message = "Quyết định phải là Public hoặc Rejected." });

            review.ModerationStatus = request.Decision;
            review.IsVisible = request.Decision == nameof(WebReviewModerationDecision.Public);
            await _context.SaveChangesAsync(cancellationToken);
            await _hub.Clients.All.SendAsync("ReviewsChanged", new { reviewId = review.ReviewId, action = "Moderated" });

            if (review.CustomerId > 0)
            {
                var isApproved = request.Decision == nameof(WebReviewModerationDecision.Public);
                var title = isApproved ? "Đánh giá đã được duyệt" : "Đánh giá đã bị từ chối";
                var reasonText = !string.IsNullOrWhiteSpace(request.Reason) ? $": {request.Reason}" : ".";
                var content = isApproved
                    ? "Đánh giá món ăn của bạn đã được quản lý phê duyệt và hiển thị công khai."
                    : $"Đánh giá món ăn của bạn đã bị từ chối{reasonText}";

                await _notifications.CreateForCustomerAsync(
                    review.CustomerId,
                    title,
                    content,
                    NotificationType.HeThong,
                    $"review-moderated:{review.ReviewId}:{request.Decision}",
                    "Review",
                    review.ReviewId);
            }

            return Ok(new { review.ReviewId, review.ModerationStatus, review.IsVisible });
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpGet("orders/{orderId:int}/items")]
        public async Task<IActionResult> GetOrderItemsForReview(
            int orderId,
            CancellationToken cancellationToken)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var order = await _context.Orders
                .AsNoTracking()
                .Where(candidate =>
                    candidate.OrderId == orderId &&
                    candidate.CustomerId == customerId.Value)
                .Select(candidate => new
                {
                    candidate.OrderId,
                    Status = candidate.Status.ToString(),
                    IsCancelled = candidate.Status == OrderStatus.DaHuy
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null)
                return NotFound(new { message = "Không tìm thấy đơn hàng." });

            var orderIsPaid = await GetPaidOrders().AnyAsync(candidate => candidate.OrderId == orderId, cancellationToken);
            var items = await _context.OrderItems
                .AsNoTracking()
                .Where(item => item.OrderId == orderId && item.MenuItemId != null)
                .OrderBy(item => item.OrderItemId)
                .Select(item => new
                {
                    item.OrderItemId,
                    item.MenuItemId,
                    ItemName = item.MenuItem != null ? item.MenuItem.ItemName : "Món ăn",
                    item.Quantity,
                    Status = item.Status.ToString(),
                    CanReview = !order.IsCancelled && orderIsPaid && item.Status == KitchenStatus.DaPhucVu,
                    Review = _context.OrderReviews
                        .Where(review =>
                            review.OrderItemId == item.OrderItemId &&
                            review.CustomerId == customerId.Value)
                        .Select(review => new
                        {
                            review.ReviewId,
                            review.Rating,
                            review.Comment,
                            review.CreatedAt,
                            review.UpdatedAt,
                            review.IsVisible,
                            review.ModerationStatus
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);
            var canReviewExperience = !order.IsCancelled && orderIsPaid &&
                await _context.OrderItems.AnyAsync(item =>
                    item.OrderId == orderId &&
                    item.MenuItemId != null &&
                    item.Status == KitchenStatus.DaPhucVu,
                    cancellationToken) &&
                !await _context.OrderItems.AnyAsync(item =>
                    item.OrderId == orderId &&
                    item.MenuItemId != null &&
                    item.Status != KitchenStatus.DaPhucVu &&
                    item.Status != KitchenStatus.DaHuy,
                    cancellationToken);
            var experienceReview = await _context.OrderReviews
                .Where(review =>
                    review.OrderId == orderId &&
                    review.OrderItemId == null &&
                    review.CustomerId == customerId.Value)
                .Select(review => new
                {
                    review.ReviewId,
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    review.UpdatedAt,
                    review.IsVisible,
                    review.ModerationStatus
                })
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(new
            {
                order.OrderId,
                order.Status,
                order.IsCancelled,
                CanReview = canReviewExperience || items.Any(item => item.CanReview),
                CanReviewExperience = canReviewExperience,
                ExperienceReview = experienceReview,
                ReviewableItemCount = items.Count(item => item.CanReview),
                ReviewedItemCount = items.Count(item => item.Review is not null),
                Items = items
            });
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpGet("my-reviewable-items")]
        public async Task<IActionResult> GetMyReviewableItems(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var paidOrderIds = GetPaidOrders()
                .Where(order => order.CustomerId == customerId.Value)
                .Select(order => order.OrderId);

            var query = _context.OrderItems.AsNoTracking()
                .Where(item =>
                    item.Order != null &&
                    item.Order.CustomerId == customerId.Value &&
                    item.Order.Status != OrderStatus.DaHuy &&
                    paidOrderIds.Contains(item.OrderId) &&
                    item.MenuItemId != null);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(item => item.Order!.OrderTime)
                .ThenBy(item => item.OrderItemId)
                .Skip(skip)
                .Take(take)
                .Select(item => new
                {
                    item.OrderItemId,
                    item.OrderId,
                    OrderTime = item.Order!.OrderTime,
                    OrderStatus = item.Order.Status.ToString(),
                    PaymentStatus = "DaThanhToan",
                    item.MenuItemId,
                    ItemName = item.MenuItem != null ? item.MenuItem.ItemName : "Món ăn",
                    ImageUrl = item.MenuItem != null ? item.MenuItem.ImageUrl : null,
                    item.UnitPrice,
                    item.Quantity,
                    ItemServingStatus = item.Status.ToString(),
                    CanReview = item.Status == KitchenStatus.DaPhucVu,
                    Review = _context.OrderReviews
                        .Where(review =>
                            review.OrderItemId == item.OrderItemId &&
                            review.CustomerId == customerId.Value)
                        .Select(review => new
                        {
                            review.ReviewId,
                            review.Rating,
                            review.Comment,
                            review.CreatedAt,
                            review.UpdatedAt,
                            review.IsVisible,
                            review.ModerationStatus
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                Total = totalCount,
                Skip = skip,
                Take = take,
                Items = items
            });
        }

        [AllowAnonymous]
        [HttpGet("menu-items/review-summaries")]
        public async Task<IActionResult> GetMenuItemReviewSummaries(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 24,
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] string? sortBy = "highest_rating",
            CancellationToken cancellationToken = default)
        {
            if (skip < 0 || take is < 1 or > 50)
                return BadRequest(new { message = "Invalid pagination." });

            var eligibleOrderIds = GetPaidOrders().Select(order => order.OrderId);
            var query = _context.OrderReviews.AsNoTracking()
                .Where(review => review.IsVisible &&
                    review.ModerationStatus == nameof(WebReviewModerationDecision.Public) &&
                    review.OrderItem != null &&
                    review.OrderItem.MenuItemId != null &&
                    review.OrderItem.Status == KitchenStatus.DaPhucVu &&
                    eligibleOrderIds.Contains(review.OrderId));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r => r.OrderItem!.MenuItem!.ItemName.Contains(term));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(r => r.OrderItem!.MenuItem!.CategoryId == categoryId.Value);
            }

            var groupedQuery = query
                .GroupBy(review => new
                {
                    review.OrderItem!.MenuItemId,
                    review.OrderItem.MenuItem!.ItemName,
                    review.OrderItem.MenuItem.ImageUrl,
                    review.OrderItem.MenuItem.Price,
                    review.OrderItem.MenuItem.CategoryId,
                    CategoryName = review.OrderItem.MenuItem.Category != null ? review.OrderItem.MenuItem.Category.CategoryName : "Món ăn"
                })
                .Select(group => new
                {
                    MenuItemId = group.Key.MenuItemId,
                    ItemName = group.Key.ItemName,
                    ImageUrl = group.Key.ImageUrl,
                    Price = group.Key.Price,
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    AverageRating = group.Average(review => (double)review.Rating),
                    ReviewCount = group.Count()
                });

            var sortedQuery = sortBy switch
            {
                "most_reviewed" => groupedQuery.OrderByDescending(g => g.ReviewCount).ThenByDescending(g => g.AverageRating),
                "name" => groupedQuery.OrderBy(g => g.ItemName),
                _ => groupedQuery.OrderByDescending(g => g.AverageRating).ThenByDescending(g => g.ReviewCount)
            };

            var totalCount = await groupedQuery.CountAsync(cancellationToken);
            var rawItems = await sortedQuery
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var itemIds = rawItems.Where(i => i.MenuItemId.HasValue).Select(i => i.MenuItemId!.Value).ToList();
            var featuredReviews = new List<FeaturedReviewItemDto>();

            if (itemIds.Count > 0)
            {
                featuredReviews = await _context.OrderReviews.AsNoTracking()
                    .Where(r => r.IsVisible &&
                        r.ModerationStatus == nameof(WebReviewModerationDecision.Public) &&
                        r.OrderItem != null &&
                        r.OrderItem.MenuItemId != null &&
                        itemIds.Contains(r.OrderItem.MenuItemId.Value) &&
                        r.Rating >= 4 &&
                        !string.IsNullOrEmpty(r.Comment))
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new FeaturedReviewItemDto
                    {
                        MenuItemId = r.OrderItem!.MenuItemId!.Value,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt,
                        CustomerName = r.Customer != null && !string.IsNullOrWhiteSpace(r.Customer.FullName)
                            ? r.Customer.FullName
                            : "Khách hàng"
                    })
                    .ToListAsync(cancellationToken);
            }

            var items = rawItems.Select(item => new
            {
                item.MenuItemId,
                item.ItemName,
                item.ImageUrl,
                item.Price,
                item.CategoryId,
                item.CategoryName,
                AverageRating = Math.Round(item.AverageRating, 1),
                item.ReviewCount,
                FeaturedReviews = featuredReviews
                    .Where(f => f.MenuItemId == item.MenuItemId)
                    .Take(2)
                    .Select(f => new
                    {
                        f.Rating,
                        f.Comment,
                        f.CreatedAt,
                        f.CustomerName
                    })
                    .ToList()
            }).ToList();

            return Ok(new
            {
                Total = totalCount,
                Skip = skip,
                Take = take,
                Items = items
            });
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpPost("orders/{orderId:int}/items/{orderItemId:int}")]
        public async Task<IActionResult> CreateReview(
            int orderId,
            int orderItemId,
            [FromBody] SaveReviewRequest request,
            CancellationToken cancellationToken)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var item = await FindEligibleOrderItem(orderId, orderItemId, customerId.Value, cancellationToken);
            if (item.Result is not null)
                return item.Result;

            if (await _context.OrderReviews.AnyAsync(
                    review => review.OrderItemId == orderItemId,
                    cancellationToken))
                return Conflict(new { message = "Món này đã được đánh giá. Hãy tải lại để sửa đánh giá hiện có." });

            var now = DateTime.UtcNow;
            var review = new OrderReview
            {
                OrderId = orderId,
                CustomerId = customerId.Value,
                OrderItemId = orderItemId,
                Rating = (byte)request.Rating,
                Comment = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsVisible = true
            };
            ApplyModeration(review, request.Comment);
            _context.OrderReviews.Add(review);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _hub.Clients.All.SendAsync("ReviewsChanged", new { orderId, action = "Created" });
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Conflict(new { message = "Món này đã được đánh giá. Hãy tải lại để sửa đánh giá hiện có." });
            }

            return BuildWriteResponse(review, item.Value!.MenuItemId);
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpPut("orders/{orderId:int}/items/{orderItemId:int}")]
        public async Task<IActionResult> UpdateReview(
            int orderId,
            int orderItemId,
            [FromBody] SaveReviewRequest request,
            CancellationToken cancellationToken)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var item = await FindEligibleOrderItem(orderId, orderItemId, customerId.Value, cancellationToken);
            if (item.Result is not null)
                return item.Result;

            var review = await _context.OrderReviews.FirstOrDefaultAsync(
                candidate =>
                    candidate.OrderId == orderId &&
                    candidate.OrderItemId == orderItemId &&
                    candidate.CustomerId == customerId.Value,
                cancellationToken);
            if (review is null)
                return NotFound(new { message = "Chưa có đánh giá để sửa cho món này." });

            review.Rating = (byte)request.Rating;
            ApplyModeration(review, request.Comment);
            review.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return BuildWriteResponse(review, item.Value!.MenuItemId);
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpPost("orders/{orderId:int}/experience")]
        public async Task<IActionResult> CreateOrderExperienceReview(
            int orderId,
            [FromBody] SaveReviewRequest request,
            CancellationToken cancellationToken)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var order = await FindEligibleOrderForExperience(orderId, customerId.Value, cancellationToken);
            if (order.Result is not null)
                return order.Result;

            if (await _context.OrderReviews.AnyAsync(
                    review => review.OrderId == orderId && review.OrderItemId == null,
                    cancellationToken))
                return Conflict(new { message = "Trải nghiệm đơn hàng đã được đánh giá. Hãy tải lại để sửa đánh giá hiện có." });

            var now = DateTime.UtcNow;
            var review = new OrderReview
            {
                OrderId = orderId,
                CustomerId = customerId.Value,
                Rating = (byte)request.Rating,
                Comment = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsVisible = true
            };
            ApplyModeration(review, request.Comment);
            _context.OrderReviews.Add(review);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _hub.Clients.All.SendAsync("ReviewsChanged", new { orderId, action = "Created" });
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return Conflict(new { message = "Trải nghiệm đơn hàng đã được đánh giá. Hãy tải lại để sửa đánh giá hiện có." });
            }

            return BuildWriteResponse(review, null);
        }

        [Authorize(Roles = CustomerAccessMiddleware.CustomerRole)]
        [CustomerAccess]
        [HttpPut("orders/{orderId:int}/experience")]
        public async Task<IActionResult> UpdateOrderExperienceReview(
            int orderId,
            [FromBody] SaveReviewRequest request,
            CancellationToken cancellationToken)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId is null)
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });

            var order = await FindEligibleOrderForExperience(orderId, customerId.Value, cancellationToken);
            if (order.Result is not null)
                return order.Result;

            var review = await _context.OrderReviews.FirstOrDefaultAsync(
                candidate =>
                    candidate.OrderId == orderId &&
                    candidate.OrderItemId == null &&
                    candidate.CustomerId == customerId.Value,
                cancellationToken);
            if (review is null)
                return NotFound(new { message = "Chưa có đánh giá trải nghiệm để sửa." });

            review.Rating = (byte)request.Rating;
            ApplyModeration(review, request.Comment);
            review.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return BuildWriteResponse(review, null);
        }

        private async Task<(OrderItem? Value, IActionResult? Result)> FindEligibleOrderItem(
            int orderId,
            int orderItemId,
            int customerId,
            CancellationToken cancellationToken)
        {
            var item = await _context.OrderItems
                .Include(candidate => candidate.Order)
                .FirstOrDefaultAsync(candidate =>
                    candidate.OrderItemId == orderItemId &&
                    candidate.OrderId == orderId &&
                    candidate.Order != null &&
                    candidate.Order.CustomerId == customerId,
                    cancellationToken);

            if (item is null)
                return (null, NotFound(new { message = "Không tìm thấy món trong đơn hàng của bạn." }));
            if (item.Order!.Status == OrderStatus.DaHuy)
                return (null, Conflict(new { message = "Không thể đánh giá món trong đơn hàng đã hủy." }));
            if (!await GetPaidOrders().AnyAsync(candidate => candidate.OrderId == orderId, cancellationToken))
                return (null, Conflict(new { message = "Chỉ có thể đánh giá sau khi đơn hàng được thanh toán đầy đủ." }));
            if (!item.MenuItemId.HasValue)
                return (null, BadRequest(new { message = "Chỉ có thể đánh giá món ăn trong đơn hàng." }));
            if (item.Status != KitchenStatus.DaPhucVu)
                return (null, Conflict(new { message = "Chỉ có thể đánh giá món đã được phục vụ." }));

            return (item, null);
        }

        private async Task<(Order? Value, IActionResult? Result)> FindEligibleOrderForExperience(
            int orderId,
            int customerId,
            CancellationToken cancellationToken)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(
                candidate => candidate.OrderId == orderId && candidate.CustomerId == customerId,
                cancellationToken);
            if (order is null)
                return (null, NotFound(new { message = "Không tìm thấy đơn hàng của bạn." }));
            if (order.Status == OrderStatus.DaHuy)
                return (null, Conflict(new { message = "Không thể đánh giá trải nghiệm của đơn hàng đã hủy." }));

            if (!await GetPaidOrders().AnyAsync(candidate => candidate.OrderId == orderId, cancellationToken))
                return (null, Conflict(new { message = "Chỉ có thể đánh giá sau khi đơn hàng được thanh toán đầy đủ." }));
            var hasServedDish = await _context.OrderItems.AnyAsync(item =>
                item.OrderId == orderId &&
                item.MenuItemId != null &&
                item.Status == KitchenStatus.DaPhucVu,
                cancellationToken);
            var hasUnservedDish = await _context.OrderItems.AnyAsync(item =>
                item.OrderId == orderId &&
                item.MenuItemId != null &&
                item.Status != KitchenStatus.DaPhucVu &&
                item.Status != KitchenStatus.DaHuy,
                cancellationToken);
            if (!hasServedDish || hasUnservedDish)
                return (null, Conflict(new { message = "Chỉ có thể đánh giá trải nghiệm khi ít nhất một món đã được phục vụ và các món còn lại đã được phục vụ hoặc hủy." }));

            return (order, null);
        }

        private void ApplyModeration(OrderReview review, string? comment)
        {
            var storedComment = comment?.Trim();
            review.Comment = string.IsNullOrEmpty(storedComment) ? null : storedComment;
            var result = _contentClassifier.Classify(storedComment);
            review.ModerationStatus = result.Decision.ToString();
            review.IsVisible = result.Decision == WebReviewModerationDecision.Public;
        }

        private IActionResult BuildWriteResponse(OrderReview review, int? menuItemId)
        {
            var response = ToResponse(review, menuItemId);
            if (review.ModerationStatus == nameof(WebReviewModerationDecision.Rejected))
                return BadRequest(new
                {
                    code = "REVIEW_CONTENT_REJECTED",
                    message = "Nội dung có từ ngữ không phù hợp. Vui lòng chỉnh sửa rồi gửi lại.",
                    persisted = true,
                    review = response
                });
            if (review.ModerationStatus == nameof(WebReviewModerationDecision.Pending))
                return Ok(new
                {
                    code = "REVIEW_PENDING",
                    message = "Đánh giá đã được lưu và đang chờ duyệt.",
                    review = response
                });
            return Ok(response);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
            exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

        private static object ToResponse(OrderReview review, int? menuItemId) => new
        {
            review.ReviewId,
            review.OrderId,
            review.OrderItemId,
            MenuItemId = menuItemId,
            review.Rating,
            review.Comment,
            review.CreatedAt,
            review.UpdatedAt,
            review.IsVisible,
            review.ModerationStatus
        };
    }

    public sealed class FeaturedReviewItemDto
    {
        public int MenuItemId { get; set; }
        public byte Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }

    public sealed class SaveReviewRequest
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    public sealed class SetReviewVisibilityRequest
    {
        public bool IsVisible { get; set; }
    }

    public sealed class SetReviewModerationDecisionRequest
    {
        [Required]
        public string Decision { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
