using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms;

// Man hinh dang nhap - dung chung cho tat ca Role (QuanLy, ThuNgan, PhucVu, Bep).
// Sau khi dang nhap thanh cong: luu Token vao SessionContext (de moi request
// API sau do tu dong dinh kem - xem AuthHeaderHandler.cs) roi mo MainForm, loc
// Sidebar theo dung Role tra ve.
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
        Load += (s, e) =>
        {
            panelCard.Location = new Point((ClientSize.Width - panelCard.Width) / 2, (ClientSize.Height - panelCard.Height) / 2);
        };
    }

    private void ResetForm()
    {
        txtUsername.Clear();
        txtPassword.Clear();
        lblError.Visible = false;
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

            // Luu phien dang nhap - AuthHeaderHandler se tu dong dinh kem Token
            // nay vao moi request API tu day tro di (o TAT CA man hinh, vi no
            // doc truc tiep tu SessionContext chu khong qua ApiService instance).
            SessionContext.Token = result.Token;
            SessionContext.UserId = result.UserId;
            SessionContext.FullName = result.FullName;
            SessionContext.RoleName = result.RoleName;

            var main = new MainForm(result.UserId, result.FullName, result.RoleName);
            main.FormClosed += (s, e) =>
            {
                if (main.LoggedOut)
                {
                    // Dang xuat -> quay lai man hinh dang nhap, xoa sach o nhap de nguoi sau khong thay.
                    ResetForm();
                    Show();
                    txtUsername.Focus();
                }
                else
                {
                    // Dong MainForm bang nut X -> thoat han ung dung (LoginForm la form chinh
                    // cua Application.Run nen Close() no se ket thuc tien trinh).
                    Close();
                }
            };
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