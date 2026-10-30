using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Controllers
{
    [Route("api/feedbacks")]
    [ApiController]
    [Authorize(Roles = "QuanLy,NhanVien")] // Cấp quyền cho Quản lý hoặc Nhân viên trên WinForms
    public class FeedbacksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeedbacksController(AppDbContext context)
        {
            _context = context;
        }

        // Lấy danh sách tất cả đánh giá cho WinForms
        [HttpGet]
        public async Task<IActionResult> GetAllFeedbacks()
        {
            var feedbacks = await _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Invoice)
                .Select(f => new
                {
                    FeedbackId = f.FeedbackId,
                    InvoiceId = f.InvoiceId,
                    CustomerName = f.Customer != null ? f.Customer.FullName : "Khách vãng lai",
                    CustomerPhone = f.Customer != null ? f.Customer.Phone : "",
                    Rating = f.Rating,
                    Comment = f.Comment,
                    CreatedAt = f.CreatedAt,

                    // SỬA CẢNH BÁO 1: Kiểm tra null an toàn cho Invoice
                    InvoiceTotal = f.Invoice != null ? f.Invoice.TotalAmount : 0,

                    RatedItems = _context.FeedbackItems
                        .Where(fi => fi.FeedbackId == f.FeedbackId)
                        .Select(fi => new
                        {
                            MenuItemId = fi.MenuItemId,

                            // SỬA CẢNH BÁO 2: Lấy tên món an toàn, nếu không thấy sẽ gán "Món không xác định"
                            MenuItemName = _context.MenuItems
                                .Where(m => m.MenuItemId == fi.MenuItemId)
                                .Select(m => m.ItemName)
                                .FirstOrDefault() ?? "Món không xác định",

                            ItemRating = fi.Rating
                        }).ToList()
                })
                .OrderByDescending(f => f.CreatedAt) // Sắp xếp đánh giá mới nhất lên đầu
                .ToListAsync();

            return Ok(feedbacks);
        }
    }
}