using System.Net.Http.Headers;

namespace QuanAnWinForm.Services;

// Tu dong dinh kem header "Authorization: Bearer {token}" vao MOI request gui
// di - doc token TRUC TIEP tu SessionContext ngay truoc khi gui (khong luu san
// trong HttpClient.DefaultRequestHeaders luc khoi tao), de moi ApiService
// instance (co the duoc tao truoc hoac sau luc dang nhap) luon dung dung
// token hien tai cua phien dang nhap.
public class AuthHeaderHandler : DelegatingHandler
{
    public AuthHeaderHandler() : base(new HttpClientHandler()) { }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(SessionContext.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionContext.Token);
        }
        return base.SendAsync(request, cancellationToken);
    }
}