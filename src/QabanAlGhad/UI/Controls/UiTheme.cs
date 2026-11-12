using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public static class UiTheme
    {
        public static readonly Color Navy = Color.FromArgb(28, 44, 61);
        public static readonly Color Teal = Color.FromArgb(25, 105, 105);
        public static readonly Color Canvas = Color.White;
        public static readonly Color Border = Color.FromArgb(224, 228, 234);
        public static readonly Color Muted = Color.FromArgb(103, 113, 128);
        public static readonly Color Soft = Color.FromArgb(245, 246, 248);
        public static readonly Color Danger = Color.FromArgb(170, 48, 64);
        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Strong = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI", 14, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 9.5f);

        public static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Dialog(Form form, string title, Size size)
        {
            form.Text = title;
            form.ClientSize = size;
            form.Font = Body;
            form.BackColor = Canvas;
            form.ForeColor = Navy;
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.AutoScaleDimensions = new SizeF(96, 96);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.ShowInTaskbar = false;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
        }

        public static void Button(Button button, string text, bool primary)
        {
            button.Text = text;
            button.AccessibleName = text;
            button.Font = Strong;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.BackColor = primary ? Teal : Color.White;
            button.ForeColor = primary ? Color.White : Navy;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(19, 84, 84) : Soft;
            button.FlatAppearance.MouseDownBackColor = button.FlatAppearance.MouseOverBackColor;
            button.Size = new Size(126, 36);
            button.Margin = new Padding(4);
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.RightToLeft = RightToLeft.Yes;
        }

        public static Label Label(string text, Font font)
        {
            return new Label { Text = text,
                               Font = font,
                               ForeColor = Navy,
                               BackColor = Color.Transparent,
                               AutoSize = false,
                               Dock = DockStyle.Fill,
                               TextAlign = ContentAlignment.MiddleLeft,
                               RightToLeft = RightToLeft.Yes,
                               Margin = new Padding(0),
                               AutoEllipsis = true };
        }

        public static string ParityName(string value)
        {
            switch (value)
            {
            case "None":
                return "بدون تكافؤ";
            case "Even":
                return "زوجي";
            case "Odd":
                return "فردي";
            case "Mark":
                return "علامة";
            case "Space":
                return "مسافة";
            default:
                return value;
            }
        }
    }

    public class ActionButton : Button
    {
        bool hovering, pressing;
        public ActionButton()
        {
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                     true);
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressing = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressing = true;
            Invalidate();
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressing = false;
            Invalidate();
            base.OnMouseUp(e);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? UiTheme.Canvas : Parent.BackColor);
        }
        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height < 3)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled   ? Color.FromArgb(230, 236, 241)
                         : pressing ? FlatAppearance.MouseDownBackColor
                         : hovering ? FlatAppearance.MouseOverBackColor
                                    : BackColor;
            Color ink = Enabled ? ForeColor : UiTheme.Muted;
            using (var path = UiTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 5 * e.Graphics.DpiX / 96))
            {
                using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(Focused && ShowFocusCues   ? UiTheme.Teal
                                         : BackColor == Color.White ? UiTheme.Border
                                                                    : fill,
                                         Focused && ShowFocusCues ? 2 : 1)) e.Graphics.DrawPath(pen, path);
            }
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            if (RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -10, -2), ink, flags);
        }
    }

    public sealed class StatusBadge : Label
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? UiTheme.Canvas : Parent.BackColor);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height < 3)
                return;
            e.Graphics.Clear(Parent == null ? UiTheme.Canvas : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int dot = Math.Max(6, (int)(7 * e.Graphics.DpiX / 96));
            using (var brush = new SolidBrush(ForeColor))
                e.Graphics.FillEllipse(brush, Width - dot - 4, (Height - dot) / 2, dot, dot);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Math.Max(1, Width - dot - 12), Height),
                                  ForeColor,
                                  TextFormatFlags.RightToLeft | TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.EndEllipsis);
        }
    }

    public sealed class InputSurface : Panel
    {
        public bool HasError { get; set; }
        public InputSurface()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 3 || Height < 3)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = UiTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 6)) using (
                var pen = new Pen(HasError        ? UiTheme.Danger
                                  : ContainsFocus ? UiTheme.Teal
                                                  : UiTheme.Border,
                                  ContainsFocus ? 1.5f : 1)) e.Graphics.DrawPath(pen, path);
        }
    }
}

namespace WeighbridgeBridge
{
    public sealed class ProgressMeter : ProgressBar
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? UiTheme.Canvas : Parent.BackColor);
        }
        public ProgressMeter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                     true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height < 3)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var background = UiTheme.Rounded(bounds, 6)) using (var fill = new SolidBrush(UiTheme.Soft))
                e.Graphics.FillPath(fill, background);
            float fraction = (Value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
            if (fraction <= 0)
                return;
            using (var bar = UiTheme.Rounded(new RectangleF(0, 0, Math.Max(3, (Width - 1) * fraction), Height - 1),
                                             6)) using (var fill = new SolidBrush(UiTheme.Teal))
                e.Graphics.FillPath(fill, bar);
        }
    }
}

namespace WeighbridgeBridge
{
    public sealed class LogGrid : DataGridView
    {
        public string EmptyText { get; set; }
        public LogGrid()
        {
            DoubleBuffered = true;
            EmptyText = "لا توجد بيانات لعرضها.";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rows.GetFirstRow(DataGridViewElementStates.Visible) >= 0)
                return;
            var area = new Rectangle(8, ColumnHeadersHeight + 8, Math.Max(1, ClientSize.Width - 16),
                                     Math.Max(1, ClientSize.Height - ColumnHeadersHeight - 16));
            TextRenderer.DrawText(e.Graphics, Rows.Count == 0 ? EmptyText : "لا توجد نتائج مطابقة للبحث.", Font, area,
                                  UiTheme.Muted,
                                  TextFormatFlags.RightToLeft | TextFormatFlags.HorizontalCenter |
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
}

namespace WeighbridgeBridge
{
    public sealed class NavButton : ActionButton
    {
        public bool Selected { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Color.White : Parent.BackColor);
            var color = Selected ? UiTheme.Teal : UiTheme.Muted;
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -4, -2), color,
                                  TextFormatFlags.RightToLeft | TextFormatFlags.HorizontalCenter |
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Selected)
                using (var brush = new SolidBrush(UiTheme.Teal))
                    e.Graphics.FillRectangle(brush, 8, Height - 3, Width - 16, 2);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -5), color,
                                                Color.White);
        }
    }

    public sealed class WeightDisplay : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            bool empty = string.IsNullOrWhiteSpace(Text) || Text.Trim() == "كغ";
            if (empty)
            {
                using (var placeholder = new Font(Font.FontFamily, Math.Max(22, Font.Size * .55f), FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, "—", placeholder, ClientRectangle, Color.FromArgb(165, 172, 183),
                                          TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            string number = Text.EndsWith(" كغ", StringComparison.Ordinal) ? Text.Substring(0, Text.Length - 3) : Text;
            using (var unit = new Font(Font.FontFamily, Math.Max(11, Font.Size * .25f), FontStyle.Regular))
            {
                int gap = Math.Max(10, (int)(Font.Size * .25f));
                int unitWidth =
                    TextRenderer.MeasureText(e.Graphics, "كغ", unit, Size.Empty, TextFormatFlags.NoPadding).Width;
                float size = Font.Size;
                Font numberFont = new Font(Font.FontFamily, size, FontStyle.Bold);
                try
                {
                    while (size > 18 && TextRenderer.MeasureText(e.Graphics, number, numberFont, Size.Empty,
                                                                 TextFormatFlags.NoPadding)
                                                    .Width +
                                                unitWidth + gap >
                                            Width - 24)
                    {
                        size -= 2;
                        numberFont.Dispose();
                        numberFont = new Font(Font.FontFamily, size, FontStyle.Bold);
                    }
                    int numberWidth =
                        TextRenderer.MeasureText(e.Graphics, number, numberFont, Size.Empty, TextFormatFlags.NoPadding)
                            .Width;
                    int x = Math.Max(0, (Width - numberWidth - unitWidth - gap) / 2);
                    TextRenderer.DrawText(e.Graphics, number, numberFont, new Rectangle(x, 0, numberWidth + 4, Height),
                                          ForeColor,
                                          TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter |
                                              TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    TextRenderer.DrawText(e.Graphics, "كغ", unit,
                                          new Rectangle(x + numberWidth + gap, 0, unitWidth + 4, Height), UiTheme.Muted,
                                          TextFormatFlags.NoPadding | TextFormatFlags.RightToLeft |
                                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                finally
                {
                    numberFont.Dispose();
                }
            }
        }
    }
}
