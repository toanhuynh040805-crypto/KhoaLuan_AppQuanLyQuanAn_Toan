using System.Net;
using System.Text;
using QuanAnBackend.Models;

namespace QuanAnBackend.Services;

// Tach rieng viec "SOAN" noi dung email hoa don (HTML) khoi IEmailService (chi
// lo viec GUI, khong quan tam NOI DUNG la gi) - de sau nay de sua giao dien
// email ma khong dung tay vao logic gui.
public static class InvoiceEmailBuilder
{
    public static string BuildInvoiceHtml(
        Customer customer,
        Invoice invoice,
        string tableName,
        List<OrderItem> items,
        int pointsEarned,
        int pointsRedeemed,
        MemberLevel? previousLevel)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1c1b1f'>");

        sb.Append("<h2 style='color:#ac2d00;margin-bottom:4px'>Hóa đơn thanh toán</h2>");
        sb.Append($"<p style='color:#5b4139;margin-top:0'>Mã hóa đơn: <b>#{invoice.InvoiceId}</b> &nbsp;|&nbsp; {invoice.InvoiceDate:HH:mm dd/MM/yyyy} &nbsp;|&nbsp; {WebUtility.HtmlEncode(tableName)}</p>");

        sb.Append("<p>Xin chào <b>" + WebUtility.HtmlEncode(customer.FullName) + "</b>, cảm ơn bạn đã dùng bữa tại quán. Dưới đây là chi tiết hóa đơn của bạn:</p>");

        sb.Append("<table style='width:100%;border-collapse:collapse;margin:16px 0'>");
        sb.Append("<tr style='background:#f9f9f9;text-align:left'><th style='padding:8px;border-bottom:1px solid #e0e0e0'>Món</th><th style='padding:8px;border-bottom:1px solid #e0e0e0;text-align:center'>SL</th><th style='padding:8px;border-bottom:1px solid #e0e0e0;text-align:right'>Thành tiền</th></tr>");
        foreach (var item in items)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            sb.Append("<tr>");
            sb.Append($"<td style='padding:8px;border-bottom:1px solid #f0f0f0'>{WebUtility.HtmlEncode(item.MenuItem?.ItemName ?? "")}</td>");
            sb.Append($"<td style='padding:8px;border-bottom:1px solid #f0f0f0;text-align:center'>{item.Quantity}</td>");
            sb.Append($"<td style='padding:8px;border-bottom:1px solid #f0f0f0;text-align:right'>{lineTotal:N0}đ</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        sb.Append("<table style='width:100%;margin-top:8px'>");
        sb.Append(Row("Tạm tính", $"{invoice.SubTotal:N0}đ"));
        if (invoice.DiscountAmount > 0) sb.Append(Row("Giảm giá", $"-{invoice.DiscountAmount:N0}đ"));
        sb.Append(Row("<b>Tổng cộng</b>", $"<b style='color:#ac2d00'>{invoice.TotalAmount:N0}đ</b>"));
        sb.Append("</table>");

        if (pointsEarned > 0 || pointsRedeemed > 0)
        {
            sb.Append("<div style='background:#fff3e0;border-radius:8px;padding:12px 16px;margin-top:20px'>");
            sb.Append("<p style='margin:0 0 4px 0;font-weight:bold;color:#ac2d00'>🏷️ Điểm tích lũy</p>");
            if (pointsEarned > 0) sb.Append($"<p style='margin:4px 0'>Tích lũy thêm: <b>+{pointsEarned} điểm</b></p>");
            if (pointsRedeemed > 0) sb.Append($"<p style='margin:4px 0'>Đã dùng: <b>-{pointsRedeemed} điểm</b></p>");
            sb.Append($"<p style='margin:4px 0'>Số điểm hiện có: <b>{customer.LoyaltyPoint:N0} điểm</b></p>");
            sb.Append("</div>");
        }

        // Su kien #18: chuc mung khi len hang thanh vien - luon guon vao 1 email
        // voi hoa don (khong tach rieng email #17/#18) de khach khong bi spam 2 mail.
        if (previousLevel.HasValue && previousLevel.Value != customer.MemberLevel)
        {
            sb.Append("<div style='background:#e0f2f1;border-radius:8px;padding:12px 16px;margin-top:12px'>");
            sb.Append($"<p style='margin:0;font-weight:bold;color:#00695c'>🎉 Chúc mừng! Bạn vừa lên hạng thành viên <b>{customer.MemberLevel}</b></p>");
            sb.Append("</div>");
        }

        sb.Append("<p style='margin-top:24px;color:#5b4139;font-size:13px'>Cảm ơn bạn đã đồng hành cùng quán. Hẹn gặp lại!</p>");
        sb.Append("</div>");

        return sb.ToString();
    }

    private static string Row(string label, string value)
        => $"<tr><td style='padding:2px 0'>{label}</td><td style='padding:2px 0;text-align:right'>{value}</td></tr>";
}