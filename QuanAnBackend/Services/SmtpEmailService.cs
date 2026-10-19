using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace QuanAnBackend.Services;

// Gui email qua SMTP cua Gmail, dung thu vien MailKit (SmtpClient chuan cua
// .NET (System.Net.Mail) da bi Microsoft khuyen cao KHONG NEN DUNG cho SMTP
// thuc te nua - MailKit la lua chon thay the pho bien nhat).
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;

    public SmtpEmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var section = _config.GetSection("Email");
        var host = section["SmtpHost"] ?? "smtp.gmail.com";
        var port = int.TryParse(section["SmtpPort"], out var p) ? p : 587;
        var senderEmail = section["SenderEmail"] ?? throw new InvalidOperationException("Thiếu cấu hình Email:SenderEmail trong appsettings.json");
        var senderName = section["SenderName"] ?? "Quán Ăn";
        var appPassword = section["AppPassword"] ?? throw new InvalidOperationException("Thiếu cấu hình Email:AppPassword trong appsettings.json");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(senderEmail, appPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}