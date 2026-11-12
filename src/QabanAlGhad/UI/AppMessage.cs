using System;
using System.Drawing;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public static class AppMessage
    {
        public static DialogResult Show(string message)
        {
            return Show(null, message, "قبان الغد");
        }
        public static DialogResult Show(IWin32Window owner, string message)
        {
            return Show(owner, message, "قبان الغد");
        }
        public static DialogResult Show(IWin32Window owner, string message, string title)
        {
            return Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons,
                                        MessageBoxIcon icon)
        {
            using (var dialog = Create(message, title, buttons, icon)) return owner == null ? dialog.ShowDialog()
                                                                                            : dialog.ShowDialog(owner);
        }

        public static Form Create(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            const int width = 480;
            int textHeight = TextRenderer
                                 .MeasureText(message, UiTheme.Body, new Size(width - 68, int.MaxValue),
                                              TextFormatFlags.WordBreak | TextFormatFlags.RightToLeft)
                                 .Height +
                             4;
            int visibleTextHeight = Math.Max(48, Math.Min(124, textHeight));
            var dialog = new Form();
            UiTheme.Dialog(dialog, title, new Size(width, Math.Max(190, visibleTextHeight + 132)));
            dialog.BackColor = Color.White;

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,   ColumnCount = 1,        RowCount = 3, Padding = new Padding(24, 18, 24, 16),
                BackColor = Color.White, Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var heading = UiTheme.Label(title, UiTheme.Heading);
            heading.ForeColor =
                icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning ? UiTheme.Danger : UiTheme.Navy;
            root.Controls.Add(heading, 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White,
                                     Margin = new Padding(0, 8, 0, 8) };
            var text = UiTheme.Label(message, UiTheme.Body);
            text.Dock = DockStyle.Top;
            text.Height = textHeight;
            text.TextAlign = ContentAlignment.TopLeft;
            text.AutoEllipsis = false;
            text.AccessibleName = message;
            scroll.Controls.Add(text);
            root.Controls.Add(scroll, 0, 1);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill,        FlowDirection = FlowDirection.RightToLeft,
                                                RightToLeft = RightToLeft.No, WrapContents = false,
                                                BackColor = Color.White,      Margin = new Padding(0),
                                                Padding = new Padding(0) };
            Action<string, DialogResult, bool, int> add = (caption, result, primary, buttonWidth) =>
            {
                var button = new ActionButton();
                UiTheme.Button(button, caption, primary);
                button.Size = new Size(buttonWidth, 36);
                button.Margin = new Padding(0, 6, 8, 0);
                button.DialogResult = result;
                actions.Controls.Add(button);
                if (result == DialogResult.OK)
                    dialog.AcceptButton = button;
                if (result == DialogResult.Cancel || result == DialogResult.No)
                    dialog.CancelButton = button;
            };
            if (buttons == MessageBoxButtons.YesNoCancel)
            {
                add("حفظ وتطبيق", DialogResult.Yes, true, 124);
                add("تجاهل التغييرات", DialogResult.No, false, 128);
                add("البقاء هنا", DialogResult.Cancel, false, 100);
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                add("تثبيت الآن", DialogResult.Yes, true, 124);
                add("لاحقًا", DialogResult.No, false, 96);
            }
            else
                add("حسنًا", DialogResult.OK, true, 104);
            root.Controls.Add(actions, 0, 2);
            dialog.Controls.Add(root);
            return dialog;
        }
    }
}
