using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlexHours
{
    /// <summary>A double-buffered card. It repaints only when invalidated.</summary>
    public sealed class SurfaceCard : Panel
    {
        public Color BorderColor { get; set; }
        public bool Emphasized { get; set; }
        public Color AccentColor { get; set; }

        public SurfaceCard()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Tag = "Card";
            BorderColor = Color.FromArgb(203, 213, 225);
        }

        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            if (Width < 25 || Height < 25) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var outline = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var fill = new SolidBrush(BackColor))
            using (var border = new Pen(BorderColor))
            {
                if (Emphasized)
                {
                    Color tint = Color.FromArgb((BackColor.R * 5 + AccentColor.R) / 6, (BackColor.G * 5 + AccentColor.G) / 6, (BackColor.B * 5 + AccentColor.B) / 6);
                    using (var glow = new LinearGradientBrush(ClientRectangle, tint, BackColor, 25f)) e.Graphics.FillPath(glow, outline);
                }
                else e.Graphics.FillPath(fill, outline);
                e.Graphics.DrawPath(border, outline);
            }
        }
    }
}
