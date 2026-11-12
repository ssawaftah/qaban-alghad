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
            var dialog = new Form();
            int bodyHeight = TextRenderer
                                 .MeasureText(message, UiTheme.Body, new Size(472, int.MaxValue),
                                              TextFormatFlags.WordBreak | TextFormatFlags.RightToLeft)
                                 .Height +
                             30;
            bodyHeight = Math.Max(92, Math.Min(260, bodyHeight));
            UiTheme.Dialog(dialog, title, new Size(540, bodyHeight + 142));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                                              Padding = new Padding(24, 16, 24, 16) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            var heading = UiTheme.Label(title, UiTheme.Heading);
            heading.ForeColor =
                icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning ? UiTheme.Danger : UiTheme.Navy;
            root.Controls.Add(heading, 0, 0);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White,
                                     Padding = new Padding(16) };
            var text = UiTheme.Label(message, UiTheme.Body);
            text.Dock = DockStyle.Top;
            text.Height =
                Math.Max(bodyHeight - 32, TextRenderer
                                                  .MeasureText(message, UiTheme.Body, new Size(440, int.MaxValue),
                                                               TextFormatFlags.WordBreak | TextFormatFlags.RightToLeft)
                                                  .Height +
                                              8);
            text.TextAlign = ContentAlignment.TopRight;
            text.AutoEllipsis = false;
            text.AccessibleName = message;
            scroll.Controls.Add(text);
            root.Controls.Add(scroll, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                                                RightToLeft = RightToLeft.No, WrapContents = false,
                                                Padding = new Padding(0, 8, 0, 0) };
            Action<string, DialogResult, bool> add = (caption, result, primary) =>
            {
                var button = new ActionButton();
                UiTheme.Button(button, caption, primary);
                button.Width = buttons == MessageBoxButtons.YesNoCancel ? 146 : 154;
                button.DialogResult = result;
                actions.Controls.Add(button);
                if (result == DialogResult.OK)
                    dialog.AcceptButton = button;
                if (result == DialogResult.Cancel || result == DialogResult.No)
                    dialog.CancelButton = button;
            };
            if (buttons == MessageBoxButtons.YesNoCancel)
            {
                add("حفظ وتطبيق", DialogResult.Yes, true);
                add("تجاهل التغييرات", DialogResult.No, false);
                add("البقاء هنا", DialogResult.Cancel, false);
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                add("تثبيت الآن", DialogResult.Yes, true);
                add("لاحقًا", DialogResult.No, false);
            }
            else
                add("حسنًا", DialogResult.OK, true);
            root.Controls.Add(actions, 0, 2);
            dialog.Controls.Add(root);
            return dialog;
        }
    }
}
