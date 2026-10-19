namespace QuanAnBackend.Services;

// Tach rieng interface nay de sau co the doi sang Resend/API khac ma khong
// phai sua code o noi goi (PaymentsController...) - chi doi Program.cs dang ky
// implementation nao vao DI.
public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}