using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed class SurfacePanel : Panel
    {
        public Color Surface = Color.White;
        public Color BorderColor = Color.FromArgb(218, 225, 231);
        public SurfacePanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Padding = new Padding(20);
            Margin = new Padding(0, 0, 0, 16);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 30 || Height < 30)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            {
                float d = 22, w = Width - 1, h = Height - 1;
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(w - d, 0, d, d, 270, 90);
                path.AddArc(w - d, h - d, d, d, 0, 90);
                path.AddArc(0, h - d, d, d, 90, 90);
                path.CloseFigure();
                using (var brush = new SolidBrush(Surface)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
