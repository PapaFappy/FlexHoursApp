using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    public sealed class ModernTabs : TabControl
    {
        public ThemePalette Palette { get; set; }
        public ModernTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = TabDrawMode.OwnerDrawFixed;
            Appearance = TabAppearance.FlatButtons;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(152, 38);
            Padding = new Point(12, 6);
            SelectedIndexChanged += (s, e) => Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Palette ?? new ThemePalette(AppearanceSettings.Defaults(true));
            e.Graphics.Clear(p.Background);
            for (int i = 0; i < TabCount; i++)
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, DrawItemState.Default));
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var p = Palette ?? new ThemePalette(AppearanceSettings.Defaults(true));
            bool active = e.Index == SelectedIndex;
            using (var brush = new SolidBrush(active ? p.Surface : p.Background)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds, active ? p.Text : p.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (active) using (var pen = new Pen(p.Accent, 3))
                e.Graphics.DrawLine(pen, e.Bounds.Left + 12, e.Bounds.Bottom - 3, e.Bounds.Right - 12, e.Bounds.Bottom - 3);
        }
    }
}
