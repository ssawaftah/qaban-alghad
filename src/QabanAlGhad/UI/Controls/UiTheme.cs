using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public static class UiTheme
    {
        public static readonly Color Navy = Color.FromArgb(24, 43, 64);
        public static readonly Color Teal = Color.FromArgb(0, 111, 109);
        public static readonly Color Canvas = Color.FromArgb(243, 247, 250);
        public static readonly Color Border = Color.FromArgb(217, 227, 234);
        public static readonly Color Muted = Color.FromArgb(88, 107, 124);
        public static readonly Color Soft = Color.FromArgb(233, 242, 245);
        public static readonly Color Danger = Color.FromArgb(170, 48, 64);
        public static readonly Font Body = new Font("Segoe UI", 10.5f);
        public static readonly Font Strong = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI", 16, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 9);

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
            form.AutoScaleMode = AutoScaleMode.Font;
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
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 94, 93) : Soft;
            button.FlatAppearance.MouseDownBackColor = button.FlatAppearance.MouseOverBackColor;
            button.Size = new Size(154, 44);
            button.Margin = new Padding(6);
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
            using (var path = UiTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 8 * e.Graphics.DpiX / 96))
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = UiTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3),
                                              8 * e.Graphics.DpiX / 96)) using (var brush = new SolidBrush(BackColor))
                e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -8, -2), ForeColor,
                                  TextFormatFlags.RightToLeft | TextFormatFlags.HorizontalCenter |
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
    public sealed class BrandMark : Control
    {
        public BrandMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                     true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(Width, Height), x = (Width - s) / 2, y = (Height - s) / 2;
            using (var path = UiTheme.Rounded(new RectangleF(x, y, s - 1, s - 1), 10)) using (
                var fill = new SolidBrush(UiTheme.Teal)) e.Graphics.FillPath(fill, path);
            using (var pen = new Pen(Color.White, Math.Max(1.5f, s / 22)))
            {
                e.Graphics.DrawLine(pen, x + s * .24f, y + s * .33f, x + s * .76f, y + s * .33f);
                e.Graphics.DrawLine(pen, x + s * .3f, y + s * .33f, x + s * .3f, y + s * .62f);
                e.Graphics.DrawLine(pen, x + s * .7f, y + s * .33f, x + s * .7f, y + s * .62f);
                e.Graphics.DrawLine(pen, x + s * .22f, y + s * .67f, x + s * .78f, y + s * .67f);
                e.Graphics.DrawLine(pen, x + s * .4f, y + s * .76f, x + s * .6f, y + s * .76f);
            }
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
