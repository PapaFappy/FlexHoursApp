using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        private readonly CalendarDayTile[] calendarTiles = new CalendarDayTile[42];
        private readonly Label calendarHeading = new Label(), calendarSummary = new Label(), dayHeading = new Label();
        private readonly Label dayDetails = new DayBreakdownLabel();
        private readonly TableLayoutPanel calendarBoard = new TableLayoutPanel();
        private DateTime calendarSelection;

        private TabPage BuildCalendarTab()
        {
            var tab = new TabPage("Overview") { Name = "Overview" };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            calendarHeading.Dock = DockStyle.Fill;
            calendarHeading.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
            calendarHeading.Padding = new Padding(4, 6, 0, 0);
            calendarSummary.Dock = DockStyle.Fill;
            calendarSummary.Font = new Font(Font.FontFamily, 9);
            calendarSummary.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(calendarHeading, 0, 0);
            layout.Controls.Add(calendarSummary, 1, 0);
            calendarBoard.Dock = DockStyle.Fill;
            calendarBoard.ColumnCount = 7;
            calendarBoard.RowCount = 7;
            calendarBoard.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            for (int i = 0; i < 7; i++)
            {
                calendarBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
                calendarBoard.Controls.Add(new Label { Text = new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" }[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Font.FontFamily, 8, FontStyle.Bold) }, i, 0);
            }
            for (int row = 0; row < 6; row++) calendarBoard.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
            for (int i = 0; i < calendarTiles.Length; i++)
            {
                var tile = new CalendarDayTile { Font = new Font(Font.FontFamily, 8) };
                tile.Click += (s, e) => SelectCalendarDay(((CalendarDayTile)s).Date);
                calendarTiles[i] = tile;
                calendarBoard.Controls.Add(tile, i % 7, i / 7 + 1);
            }
            layout.Controls.Add(calendarBoard, 0, 1);
            var detail = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(18), Margin = new Padding(12, 3, 3, 3) };
            dayHeading.Dock = DockStyle.Top;
            dayHeading.Height = 64;
            dayHeading.Font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            dayDetails.Dock = DockStyle.Fill;
            dayDetails.Font = new Font(Font.FontFamily, 10);
            detail.Controls.Add(dayDetails);
            detail.Controls.Add(dayHeading);
            layout.Controls.Add(detail, 1, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private void RefreshCalendarView()
        {
            if (calendarSelection.Year != selected.Year || calendarSelection.Month != selected.Month)
                calendarSelection = DateTime.Today.Year == selected.Year && DateTime.Today.Month == selected.Month ? DateTime.Today : selected;
            var days = data.ChromeSnapshot && data.ChromeDays != null ? data.ChromeDays.ToDictionary(item => item.Date) : new Dictionary<string, ChromeDay>();
            int offset = ((int)selected.DayOfWeek + 6) % 7, count = DateTime.DaysInMonth(selected.Year, selected.Month);
            int weeks = (offset + count + 6) / 7;
            for (int row = 1; row <= 6; row++) calendarBoard.RowStyles[row] = new RowStyle(SizeType.Percent, row <= weeks ? 100f / weeks : 0);
            for (int i = 0; i < calendarTiles.Length; i++)
            {
                var tile = calendarTiles[i];
                int day = i - offset + 1;
                tile.Visible = day > 0 && day <= count;
                if (day <= 0 || day > count) continue;
                tile.Date = selected.AddDays(day - 1);
                ChromeDay entry;
                days.TryGetValue(WorkHoursCalculator.Key(tile.Date), out entry);
                tile.Day = entry;
                tile.Verified = data.ChromeDailyVerified;
                tile.SelectedDay = tile.Date == calendarSelection;
                tile.AccessibleName = tile.Date.ToString("dddd dd MMMM yyyy") + ", " + (entry == null ? "Not read" : WorkHoursCalculator.Hours(entry.Total) + " paid, " + (tile.Verified ? "reconciled" : "needs review"));
                tile.Invalidate();
            }
            calendarHeading.Text = selected.ToString("MMMM yyyy");
            calendarSummary.Text = days.Count + " / " + count + " days read\nBars show work + paid leave";
            SelectCalendarDay(calendarSelection);
        }

        private void SelectCalendarDay(DateTime date)
        {
            calendarSelection = date;
            foreach (var tile in calendarTiles) { tile.SelectedDay = tile.Date == date; tile.Invalidate(); }
            dayHeading.Text = date.ToString("dddd\ndd MMM yyyy");
            var day = data.ChromeSnapshot && data.ChromeDays != null ? data.ChromeDays.FirstOrDefault(item => item.Date == WorkHoursCalculator.Key(date)) : null;
            if (day == null) { dayDetails.Text = "No daily reading yet.\n\nChrome sync will populate this day when it is captured.\n\nA missing reading does not mean zero hours."; return; }
            dayDetails.Text = (data.ChromeDailyVerified ? "RECONCILED" : "NEEDS REVIEW")
                + "\n\nWorked\t" + WorkHoursCalculator.Hours(day.Worked)
                + "\nPTO\t" + WorkHoursCalculator.Hours(day.Pto)
                + "\nSick\t" + WorkHoursCalculator.Hours(day.Sick)
                + "\nHoliday\t" + WorkHoursCalculator.Hours(day.Holiday)
                + "\nOther paid\t" + WorkHoursCalculator.Hours(day.OtherPaidLeave)
                + "\nUnpaid\t" + WorkHoursCalculator.Hours(day.Unpaid)
                + "\n\nPaid total\t" + WorkHoursCalculator.Hours(day.Total)
                + "\nExpected\t" + WorkHoursCalculator.Hours(WorkHoursCalculator.Weekday(date) ? 480 : 0)
                + (date > asof.Value.Date ? "\n\nAfter your comparison date." : "");
        }
    }
}
