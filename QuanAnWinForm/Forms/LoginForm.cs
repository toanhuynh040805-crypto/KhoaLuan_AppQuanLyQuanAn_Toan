using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms;

// Man hinh dang nhap - dung chung cho tat ca Role (QuanLy, ThuNgan, Bep, QuayNuoc).
// Da noi that POST /api/auth/login. Sau khi dang nhap thanh cong, MainForm se
// tu dong loc Sidebar theo dung Role tra ve.
public partial class LoginForm : Form
{
    private readonly ApiService _api = new();

    public LoginForm()
    {
        InitializeComponent();
        btnLogin.Click += async (s, e) => await HandleLoginAsync();
        txtPassword.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) await HandleLoginAsync();
        };
        // Canh giua panelCard sau khi Form da co kich thuoc that (chuyen tu .Designer.cs sang
        // day vi Designer khong parse duoc lambda).
        Load += (s, e) =>
        {
            panelCard.Location = new Point((ClientSize.Width - panelCard.Width) / 2, (ClientSize.Height - panelCard.Height) / 2);
        };
    }

    private async Task HandleLoginAsync()
    {
        var username = txtUsername.Text.Trim();
        var password = txtPassword.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            lblError.Text = "Vui lòng nhập đủ tài khoản và mật khẩu";
            lblError.Visible = true;
            return;
        }

        btnLogin.Enabled = false;
        btnLogin.Text = "Đang đăng nhập...";

        try
        {
            var result = await _api.LoginAsync(username, password);
            if (result is null)
            {
                lblError.Text = "Sai tài khoản hoặc mật khẩu";
                lblError.Visible = true;
                return;
            }

            var main = new MainForm(result.UserId, result.FullName, result.RoleName);
            main.Show();
            Hide();
        }
        catch (Exception ex)
        {
            lblError.Text = $"Lỗi kết nối: {ex.Message}";
            lblError.Visible = true;
        }
        finally
        {
            btnLogin.Enabled = true;
            btnLogin.Text = "Vào ca";
        }
    }
}
