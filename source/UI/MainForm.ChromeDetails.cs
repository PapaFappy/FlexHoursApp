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
        private readonly DataGridView chromeDailyGrid = new DataGridView();
        private readonly Label chromeDailyNotice = new Label(), chromeDailyTotals = new Label();
        private readonly DataGridView upcomingLeaveGrid = new DataGridView();
        private readonly Label upcomingLeaveNotice = new Label();
        private readonly ComboBox dailyFilter = new ComboBox();

        private TabPage BuildChromeDailyTab()
        {
            ConfigureGrid(chromeDailyGrid, "ChromeDailyDetails");
            chromeDailyGrid.ReadOnly = true;
            chromeDailyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            foreach (string title in new[] { "Date", "Day", "Expected", "Worked", "PTO", "Sick", "Holiday", "Other paid leave", "Unpaid", "Paid total", "Difference", "Status" })
                chromeDailyGrid.Columns.Add(title, title);
            chromeDailyGrid.Columns[0].FillWeight = 125;
            chromeDailyGrid.Columns[1].FillWeight = 60;
            chromeDailyGrid.Columns[11].FillWeight = 140;
            chromeDailyGrid.ColumnHeadersHeight = 42;
            int[] minimumWidths = { 110, 48, 72, 72, 65, 65, 72, 92, 70, 82, 90, 114 };
            for (int i = 0; i < minimumWidths.Length; i++)
            {
                chromeDailyGrid.Columns[i].MinimumWidth = minimumWidths[i];
                chromeDailyGrid.Columns[i].FillWeight = minimumWidths[i];
                if (i >= 2 && i <= 10) chromeDailyGrid.Columns[i].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                chromeDailyGrid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            dailyFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            dailyFilter.AccessibleName = "Filter timesheet days";
            dailyFilter.Width = 150;
            dailyFilter.Items.AddRange(new object[] { "All days", "Leave days", "Unread days" });
            dailyFilter.SelectedIndex = 0;
            dailyFilter.SelectedIndexChanged += (s, e) => { if (data != null && !loading) { RefreshChromeDailyGrid(); ThemeManager.Apply(this, store.Appearance); } };
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            filters.Controls.Add(new Label { Text = "Show", AutoSize = true, Padding = new Padding(6, 5, 4, 0) });
            filters.Controls.Add(dailyFilter);
            chromeDailyNotice.Dock = chromeDailyTotals.Dock = DockStyle.Fill;
            chromeDailyNotice.Padding = new Padding(6, 5, 6, 2);
            chromeDailyTotals.Padding = new Padding(6, 5, 6, 2);
            chromeDailyNotice.Font = chromeDailyTotals.Font = new Font(Font.FontFamily, 9);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(chromeDailyNotice);
            layout.Controls.Add(filters);
            layout.Controls.Add(chromeDailyGrid);
            layout.Controls.Add(chromeDailyTotals);
            var tab = new TabPage("Timesheet days") { Name = "Timesheet" };
            tab.Controls.Add(layout);
            return tab;
        }

        private TabPage BuildUpcomingLeaveTab()
        {
            ConfigureGrid(upcomingLeaveGrid, "UpcomingLeave");
            upcomingLeaveGrid.ReadOnly = true;
            foreach (string title in new[] { "Date", "Day", "Type", "Hours", "Status" }) upcomingLeaveGrid.Columns.Add(title, title);
            upcomingLeaveGrid.Columns[4].FillWeight = 180;
            upcomingLeaveNotice.Dock = DockStyle.Top;
            upcomingLeaveNotice.Height = 56;
            upcomingLeaveNotice.Padding = new Padding(6);
            upcomingLeaveNotice.Font = new Font(Font.FontFamily, 9);
            var layout = new Panel { Dock = DockStyle.Fill };
            layout.Controls.Add(upcomingLeaveGrid);
            layout.Controls.Add(upcomingLeaveNotice);
            var tab = new TabPage("Upcoming leave") { Name = "UpcomingLeave" };
            tab.Controls.Add(layout);
            return tab;
        }

        private void RefreshChromeDailyGrid()
        {
            int scroll = chromeDailyGrid.FirstDisplayedScrollingRowIndex;
            chromeDailyGrid.Rows.Clear();
            bool hasSource = data != null && data.ChromeSnapshot;
            var days = hasSource && data.ChromeDays != null ? data.ChromeDays.ToDictionary(day => day.Date) : new Dictionary<string, ChromeDay>();
            for (int i = 0; i < DateTime.DaysInMonth(selected.Year, selected.Month); i++)
            {
                var date = selected.AddDays(i);
                decimal expected = WorkHoursCalculator.Weekday(date) ? 480 : 0;
                ChromeDay day;
                bool found = days.TryGetValue(WorkHoursCalculator.Key(date), out day);
                if (dailyFilter.SelectedIndex == 1 && (!found || day.Pto + day.Sick + day.Holiday + day.OtherPaidLeave + day.Unpaid == 0)) continue;
                if (dailyFilter.SelectedIndex == 2 && found) continue;
                bool future = date > asof.Value.Date;
                string state = !found ? "Not read" : data.ChromeDailyVerified ? "Synced" : "Not reconciled";
                string difference = !found || !data.ChromeDailyVerified ? "-" : future ? "After as-of" : FlexLedger.Display(day.Total - expected);
                int index = chromeDailyGrid.Rows.Add(date.ToString("dd MMM yyyy"), date.ToString("ddd"), WorkHoursCalculator.Hours(expected),
                    found ? WorkHoursCalculator.Hours(day.Worked) : "-", found ? WorkHoursCalculator.Hours(day.Pto) : "-",
                    found ? WorkHoursCalculator.Hours(day.Sick) : "-", found ? WorkHoursCalculator.Hours(day.Holiday) : "-",
                    found ? WorkHoursCalculator.Hours(day.OtherPaidLeave) : "-", found ? WorkHoursCalculator.Hours(day.Unpaid) : "-",
                    found ? WorkHoursCalculator.Hours(day.Total) : "-", difference, state);
                chromeDailyGrid.Rows[index].Tag = date;
            }
            if (scroll >= 0 && scroll < chromeDailyGrid.Rows.Count && chromeDailyGrid.DisplayedRowCount(false) > 0) chromeDailyGrid.FirstDisplayedScrollingRowIndex = scroll;
            chromeDailyNotice.Text = !hasSource ? "Every date is shown below. Chrome sync will fill the hours automatically; no PTO/sick entry is needed here.\nA monthly total alone cannot supply each day's hours."
                : data.ChromeDailyVerified ? "Synced from BambooHR at " + data.BrowserImportedAt.ToString("dd MMM HH:mm") + ". Daily allocations match the Pay Period total.\nPTO, sick leave and holidays are populated automatically. Differences compare through " + asof.Value.ToString("dd MMM yyyy") + "."
                : "Summary synced, but daily rows are not fully reconciled: " + (data.ChromeDailyWarning ?? "Waiting for dated entries.") + "\nAvailable readings are shown for review. Missing values are unknown, not zero.";
            chromeDailyNotice.Tag = hasSource && data.ChromeDailyVerified ? "Positive" : "Warning";
            var values = days.Values;
            chromeDailyTotals.Text = days.Count == 0 ? "No daily hours imported yet. Monthly totals above remain available."
                : (data.ChromeDailyVerified ? "Month totals: " : "Read totals (not reconciled): ")
                    + "Worked " + WorkHoursCalculator.Hours(values.Sum(day => day.Worked))
                    + " | PTO " + WorkHoursCalculator.Hours(values.Sum(day => day.Pto))
                    + " | Sick " + WorkHoursCalculator.Hours(values.Sum(day => day.Sick))
                    + " | Holiday " + WorkHoursCalculator.Hours(values.Sum(day => day.Holiday))
                    + " | Other leave " + WorkHoursCalculator.Hours(values.Sum(day => day.OtherPaidLeave))
                    + " | Paid total " + WorkHoursCalculator.Hours(values.Sum(day => day.Total));
            if (dailyFilter.SelectedIndex != 0) chromeDailyTotals.Text = chromeDailyGrid.Rows.Count + " matching day(s). " + chromeDailyTotals.Text;
            RefreshUpcomingLeave();
        }

        private void RefreshUpcomingLeave()
        {
            upcomingLeaveGrid.Rows.Clear();
            int monthsRead = 0;
            foreach (var month in store.Months.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var source = month.Value;
                if (!source.ChromeSnapshot || source.ChromeDays == null || source.ChromeDays.Count == 0) continue;
                monthsRead++;
                foreach (var day in source.ChromeDays.OrderBy(item => item.Date, StringComparer.Ordinal))
                {
                    DateTime date = DateTime.ParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (date <= DateTime.Today) continue;
                    string[] types = { "PTO / vacation", "Sick leave", "Holiday", "Other paid leave", "Unpaid leave" };
                    decimal[] amounts = { day.Pto, day.Sick, day.Holiday, day.OtherPaidLeave, day.Unpaid };
                    for (int i = 0; i < types.Length; i++)
                    {
                        if (amounts[i] == 0) continue;
                        int row = upcomingLeaveGrid.Rows.Add(date.ToString("dd MMM yyyy"), date.ToString("ddd"), types[i],
                            WorkHoursCalculator.Hours(amounts[i]), source.ChromeDailyVerified ? "Synced allocation" : "Read; not reconciled");
                        upcomingLeaveGrid.Rows[row].Tag = date;
                    }
                }
            }
            upcomingLeaveNotice.Text = "Future leave and holidays from " + monthsRead + " month(s) with dated BambooHR readings.\nAdd December (or another month) to the extension's watched months to include its leave. No future leave is added to the current month's balance.";
            if (upcomingLeaveGrid.Rows.Count == 0) upcomingLeaveNotice.Text = "No future dated leave has been read yet. This does not mean you have none booked.\nOpen the future month's Timesheet in Chrome, select that month in the extension and connect it once.";
        }
    }
}
