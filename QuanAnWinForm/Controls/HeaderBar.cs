using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Controls;

// Thanh header tren cung: o tim kiem, chuong thong bao, avatar + ten nguoi dung.
public partial class HeaderBar : UserControl
{
    public event Action? NotificationClicked;

    public HeaderBar()
    {
        InitializeComponent();
        btnNotify.Click += (s, e) => NotificationClicked?.Invoke();
    }

    // Goi lai tu MainForm sau khi biet thong tin nguoi dang dang nhap
    public void SetUser(string fullName, string roleDisplayName)
    {
        lblUserName.Text = fullName;
        lblUserRole.Text = roleDisplayName;
        lblAvatarInitial.Text = fullName.Length > 0 ? fullName[0].ToString().ToUpper() : "?";
    }
}
