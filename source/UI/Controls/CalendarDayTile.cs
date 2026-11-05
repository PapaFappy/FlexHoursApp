using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlexHours
{
    public sealed class CalendarDayTile : Button
    {
        public ThemePalette Palette { get; set; }
        public DateTime Date { get; set; }
        public ChromeDay Day { get; set; }
        public bool Verified { get; set; }
        public bool SelectedDay { get; set; }
        private bool hover;
        public CalendarDayTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Dock = DockStyle.Fill;
            Margin = new Padding(3);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Palette ?? new ThemePalette(AppearanceSettings.Defaults(true));
            e.Graphics.Clear(Parent.BackColor);
            if (Width < 24 || Height < 24) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var shape = SurfaceCard.Rounded(bounds, 8))
            using (var fill = new SolidBrush(hover ? p.Border : p.Surface))
            using (var edge = new Pen(SelectedDay || Focused ? p.Accent : p.Border, SelectedDay || Focused ? 2 : 1))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(edge, shape); }
            TextRenderer.DrawText(e.Graphics, Date.Day.ToString("00"), Font, new Rectangle(10, 6, Width - 20, 20),
                Date == DateTime.Today ? p.Accent : p.Muted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
            if (Day != null)
            {
                string badge = !Verified ? "Review" : Day.Pto > 0 ? "PTO" : Day.Sick > 0 ? "Sick" : Day.Holiday > 0 ? "Holiday" : Day.OtherPaidLeave > 0 ? "Leave" : Day.Unpaid > 0 ? "Unpaid" : "";
                TextRenderer.DrawText(e.Graphics, badge, Font, new Rectangle(32, 6, Width - 42, 20), Verified ? p.Positive : p.Warning,
                    TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            string hours = Day == null ? "—" : WorkHoursCalculator.Hours(Day.Total);
            using (var number = new Font(Font.FontFamily, Height >= 56 ? 12 : 10, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, hours, number, new Rectangle(10, Height >= 56 ? 28 : 21, Width - 20, 21), Day != null && Day.Total > 0 ? p.Text : p.Muted,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            string kind = Day == null ? "Not read" : !Verified ? "Needs review" : Day.Pto > 0 ? "Vacation / PTO" : Day.Sick > 0 ? "Sick leave"
                : Day.Holiday > 0 ? "Holiday" : Day.OtherPaidLeave > 0 ? "Paid leave" : Day.Unpaid > 0 ? "Unpaid leave" : Day.Worked > 0 ? "Worked" : "No hours";
            if (Height >= 76) TextRenderer.DrawText(e.Graphics, kind, Font, new Rectangle(10, 52, Width - 20, 19),
                Day != null && !Verified ? p.Warning : p.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Height >= 56 && Day != null && Day.Total > 0)
            {
                int available = Math.Max(1, Width - 20), start = 10;
                decimal paidLeave = Day.Pto + Day.Sick + Day.Holiday + Day.OtherPaidLeave;
                int work = (int)(available * Day.Worked / Math.Max(480m, Day.Total));
                int leave = (int)(available * paidLeave / Math.Max(480m, Day.Total));
                using (var brush = new SolidBrush(p.Accent)) e.Graphics.FillRectangle(brush, start, Height - 7, work, 3);
                using (var brush = new SolidBrush(p.Positive)) e.Graphics.FillRectangle(brush, start + work, Height - 7, leave, 3);
            }
        }
    }
}
