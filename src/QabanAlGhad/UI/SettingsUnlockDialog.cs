using System;
using System.Drawing;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed class SettingsUnlockDialog : Form
    {
        static int attempts;
        static DateTime blockedUntil;
        public SettingsUnlockDialog()
        {
            UiTheme.Dialog(this, "فتح الإعدادات", new Size(430, 300));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                                              Padding = new Padding(26, 18, 26, 18) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.Controls.Add(UiTheme.Label("الإعدادات المحمية", UiTheme.Heading), 0, 0);
            var prompt = UiTheme.Label("أدخل كلمة المرور للمتابعة.", UiTheme.Small);
            prompt.ForeColor = UiTheme.Muted;
            root.Controls.Add(prompt, 0, 1);
            var password = new TextBox { UseSystemPasswordChar = true,
                                         TextAlign = HorizontalAlignment.Center,
                                         BorderStyle = BorderStyle.None,
                                         Font = new Font("Segoe UI", 12),
                                         MaxLength = 128,
                                         RightToLeft = RightToLeft.No,
                                         AccessibleName = "كلمة مرور الإعدادات" };
            var host = new InputSurface { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0) };
            host.Controls.Add(password);
            host.Layout += (sender, e) => password.SetBounds(16, (host.ClientSize.Height - password.Height) / 2,
                                                             Math.Max(1, host.ClientSize.Width - 32), password.Height);
            password.Enter += (sender, e) => host.Invalidate();
            password.Leave += (sender, e) => host.Invalidate();
            root.Controls.Add(host, 0, 2);
            var error = UiTheme.Label("", UiTheme.Small);
            error.ForeColor = UiTheme.Danger;
            error.TextAlign = ContentAlignment.MiddleCenter;
            root.Controls.Add(error, 0, 3);
            var enter = new ActionButton();
            var cancel = new ActionButton();
            UiTheme.Button(enter, "فتح الإعدادات", true);
            UiTheme.Button(cancel, "إلغاء", false);
            cancel.DialogResult = DialogResult.Cancel;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                                                RightToLeft = RightToLeft.No, WrapContents = false,
                                                Padding = new Padding(0, 4, 0, 0) };
            actions.Controls.Add(enter);
            actions.Controls.Add(cancel);
            root.Controls.Add(actions, 0, 4);
            Controls.Add(root);
            AcceptButton = enter;
            CancelButton = cancel;
            Shown += (sender, e) => password.Focus();
            enter.Click += (sender, e) =>
            {
                if (DateTime.UtcNow < blockedUntil)
                {
                    error.Text = "يرجى الانتظار قليلًا ثم المحاولة مجددًا.";
                    return;
                }
                if (SettingsGate.Verify(password.Text))
                {
                    attempts = 0;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                password.Clear();
                error.Text = "كلمة المرور غير صحيحة.";
                if (++attempts >= 5)
                {
                    attempts = 0;
                    blockedUntil = DateTime.UtcNow.AddSeconds(30);
                    error.Text = "محاولات متكررة. أعد المحاولة بعد ٣٠ ثانية.";
                }
                password.Focus();
            };
            FormClosed += (sender, e) => password.Clear();
        }
    }
}
