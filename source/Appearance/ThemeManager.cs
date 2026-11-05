using System;
using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    /// <summary>One palette for the main window, import window and settings.</summary>
    public static class ThemeManager
    {
        public static void Apply(Control root, AppearanceSettings settings)
        {
            var palette = new ThemePalette(settings);
            Paint(root, palette);
            root.Invalidate(true);
        }

        private static void Paint(Control control, ThemePalette palette)
        {
            string role = Convert.ToString(control.Tag);
            Color background = role == "Card" ? palette.Surface : palette.Background;
            if (control.Parent != null && Convert.ToString(control.Parent.Tag) == "Card")
                background = palette.Surface;
            control.BackColor = background;
            control.ForeColor = palette.Text;
            var card = control as SurfaceCard;
            if (card != null) { card.BorderColor = card.Emphasized ? palette.Accent : palette.Border; card.AccentColor = palette.Accent; }
            var tabs = control as ModernTabs;
            if (tabs != null) tabs.Palette = palette;
            var tile = control as CalendarDayTile;
            if (tile != null) tile.Palette = palette;
            var progress = control as AnimatedProgress;
            if (progress != null)
            {
                progress.TrackColor = palette.Border;
                progress.FillColor = palette.Accent;
            }

            var label = control as Label;
            if (label != null)
            {
                var parentCard = control.Parent as SurfaceCard;
                if (parentCard != null && parentCard.Emphasized) label.BackColor = Color.Transparent;
                if (role == "Positive") label.ForeColor = palette.Positive;
                else if (role == "Negative") label.ForeColor = palette.Negative;
                else if (role == "Warning") label.ForeColor = palette.Warning;
                else if (label.Font.Size < 10) label.ForeColor = palette.Muted;
            }

            var button = control as Button;
            if (button != null)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = palette.Border;
                button.BackColor = role == "Accent" ? palette.Accent : palette.Surface;
                button.ForeColor = role == "Accent" ? ThemePalette.ReadableText(palette.Accent) : palette.Text;
                button.UseVisualStyleBackColor = false;
            }

            if (control is TextBoxBase || control is NumericUpDown || control is ComboBox)
            {
                control.BackColor = palette.Surface;
                control.ForeColor = palette.Text;
            }

            var picker = control as DateTimePicker;
            if (picker != null)
            {
                picker.CalendarMonthBackground = palette.Surface;
                picker.CalendarForeColor = palette.Text;
                picker.CalendarTitleBackColor = palette.Accent;
                picker.CalendarTitleForeColor = ThemePalette.ReadableText(palette.Accent);
            }

            var grid = control as DataGridView;
            if (grid != null) PaintGrid(grid, palette);

            foreach (Control child in control.Controls)
                Paint(child, palette);
        }

        private static void PaintGrid(DataGridView grid, ThemePalette palette)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = palette.Surface;
            grid.GridColor = palette.Border;
            grid.BorderStyle = BorderStyle.None;
            grid.DefaultCellStyle.BackColor = palette.Surface;
            grid.DefaultCellStyle.ForeColor = palette.Text;
            grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
            grid.DefaultCellStyle.SelectionForeColor = ThemePalette.ReadableText(palette.Accent);
            grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Background;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Muted;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.Background;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.Muted;

            var holidaysByYear = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<DateTime, string>>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.DefaultCellStyle.BackColor = palette.Surface;
                row.DefaultCellStyle.ForeColor = palette.Text;
                if (!(row.Tag is DateTime)) continue;
                var date = (DateTime)row.Tag;
                System.Collections.Generic.Dictionary<DateTime, string> holidays;
                if (!holidaysByYear.TryGetValue(date.Year, out holidays))
                    holidaysByYear[date.Year] = holidays = WorkHoursCalculator.Holidays(date.Year);
                bool special = grid.Name == "FlexHistory"
                    ? FlexLedger.IsReset(date)
                    : holidays.ContainsKey(date);
                if (special) row.DefaultCellStyle.BackColor = palette.SpecialRow;
                else if (!WorkHoursCalculator.Weekday(date) && grid.Name != "FlexHistory")
                    row.DefaultCellStyle.BackColor = palette.Background;
            }
        }
    }
}
