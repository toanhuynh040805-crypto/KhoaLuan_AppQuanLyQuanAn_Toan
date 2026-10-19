using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.Forms
{
    public partial class ChangePasswordDialog : Form
    {
        private const int MinLength = 6;

        private readonly bool _requireOld;
        private readonly Func<string, string, Task> _submit;
        private readonly TextBox _txtOld;
        private readonly TextBox _txtNew;
        private readonly TextBox _txtConfirm;
        private readonly Label _lblError;
        private readonly Button _btnSave;

        public ChangePasswordDialog(string title, bool requireOldPassword, Func<string, string, Task> submit)
        {
            _requireOld = requireOldPassword;
            _submit = submit;

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ClientSize = new Size(360, requireOldPassword ? 330 : 270);

            var y = 16;

            _txtOld = new TextBox();
            if (requireOldPassword)
            {
                AddField("Mật khẩu hiện tại *", _txtOld, ref y);
            }

            _txtNew = new TextBox();
            AddField($"Mật khẩu mới * (tối thiểu {MinLength} ký tự)", _txtNew, ref y);

            _txtConfirm = new TextBox();
            AddField("Nhập lại mật khẩu mới *", _txtConfirm, ref y);

            _lblError = new Label
            {
                Location = new Point(20, y),
                Size = new Size(320, 36),
                ForeColor = AppColors.Error,
                Font = AppFonts.BodyMd,
                Visible = false
            };
            Controls.Add(_lblError);
            y += 40;

            _btnSave = new Button
            {
                Text = "Lưu",
                Location = new Point(150, y),
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Primary,
                ForeColor = Color.White,
                Font = AppFonts.LabelLg,
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += async (s, e) => await SaveAsync();
            Controls.Add(_btnSave);

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(250, y),
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.SurfaceContainer,
                ForeColor = AppColors.OnSurface,
                Font = AppFonts.LabelLg,
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            Controls.Add(btnCancel);

            AcceptButton = _btnSave;
            CancelButton = btnCancel;
        }

        private void AddField(string label, TextBox txt, ref int y)
        {
            Controls.Add(new Label { Text = label, Font = AppFonts.BodyMd, Location = new Point(20, y), AutoSize = true });
            txt.Location = new Point(20, y + 22);
            txt.Width = 320;
            txt.Font = AppFonts.BodyMd;
            txt.PasswordChar = '•';
            Controls.Add(txt);
            y += 62;
        }

        private void ShowError(string message)
        {
            _lblError.Text = message;
            _lblError.Visible = true;
        }

        private async Task SaveAsync()
        {
            _lblError.Visible = false;

            if (_requireOld && string.IsNullOrEmpty(_txtOld.Text))
            {
                ShowError("Vui lòng nhập mật khẩu hiện tại");
                return;
            }
            if (_txtNew.Text.Length < MinLength)
            {
                ShowError($"Mật khẩu mới phải có ít nhất {MinLength} ký tự");
                return;
            }
            if (_txtNew.Text != _txtConfirm.Text)
            {
                ShowError("Mật khẩu nhập lại không khớp");
                return;
            }

            _btnSave.Enabled = false;
            try
            {
                await _submit(_txtOld.Text, _txtNew.Text);
                DialogResult = DialogResult.OK; // tự đóng form
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                _btnSave.Enabled = true;
            }
        }
    }
}
