using System;
using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        private Control flexDetails;
        private readonly Label balanceNote = new Label();
        private void InitializeLayout()
        {
            Text = "FlexHours | Your month, in balance";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Size = new Size(1180, 1000);
            MinimumSize = new Size(Math.Min(1100, Screen.PrimaryScreen.WorkingArea.Width), Math.Min(780, Screen.PrimaryScreen.WorkingArea.Height));
            Size = new Size(Math.Min(1180, Screen.PrimaryScreen.WorkingArea.Width), Math.Min(1000, Screen.PrimaryScreen.WorkingArea.Height));
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, MinimumSize = new Size(1040, 900), Padding = new Padding(24), ColumnCount = 1, RowCount = 8
            };
            foreach (int height in new[] { 56, 50, 156, 30, 44 })
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = root.MinimumSize };
            viewport.Controls.Add(root);
            Controls.Add(viewport);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(new Label { Text = "FlexHours", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 21, FontStyle.Bold) });
            header.Controls.Add(new Label { Text = "YOUR TIME, AT A GLANCE", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 8, FontStyle.Bold) });
            root.Controls.Add(header);
            root.Controls.Add(BuildInputBar());

            var monthlyCards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            monthlyCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            monthlyCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            monthlyCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            monthlyCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            var hero = CreateMetric("BALANCE THROUGH DATE", out values[2], true);
            hero.Emphasized = true;
            values[2].Font = new Font(Font.FontFamily, 38, FontStyle.Bold);
            values[2].Height = 64;
            balanceNote.Dock = DockStyle.Bottom;
            balanceNote.Height = 24;
            balanceNote.Font = new Font(Font.FontFamily, 9);
            hero.Controls.Add(balanceNote);
            monthlyCards.Controls.Add(hero);
            for (int column = 0; column < 2; column++)
            {
                var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                int top = column == 0 ? 3 : 1, bottom = column == 0 ? 0 : 4;
                stack.Controls.Add(CreateMetric(column == 0 ? "LEFT THIS MONTH / BEFORE CARRY" : "EXPECTED THROUGH DATE", out values[top], false));
                stack.Controls.Add(CreateMetric(column == 0 ? "MONTHLY TARGET" : "LATER HOURS ALREADY CREDITED", out values[bottom], false));
                monthlyCards.Controls.Add(stack);
            }
            root.Controls.Add(monthlyCards);

            var progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            monthProgress.Dock = DockStyle.Fill;
            monthProgress.Margin = new Padding(3, 10, 12, 12);
            progressLabel.Dock = DockStyle.Fill;
            progressLabel.TextAlign = ContentAlignment.MiddleRight;
            progressLabel.Font = new Font(Font.FontFamily, 9);
            progress.Controls.Add(monthProgress);
            progress.Controls.Add(progressLabel);
            root.Controls.Add(progress);

            var flexCards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            flexCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            string[] flexTitles = { "MONTHLY DIFFERENCE / FULL TARGET", "PREVIOUS CARRY-OVER / MAX +/-4h", "FINAL FLEX BALANCE / UNCAPPED" };
            for (int i = 0; i < flexValues.Length; i++)
            {
                flexCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
                flexCards.Controls.Add(CreateMetric(flexTitles[i], out flexValues[i], true));
            }

            var flexLine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            flexLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            flexLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            flexNotice.Dock = DockStyle.Fill;
            complete.Dock = DockStyle.Fill;
            complete.Text = "Month total confirmed complete";
            complete.AccessibleName = complete.Text;
            flexLine.Controls.Add(flexNotice);
            flexLine.Controls.Add(complete);
            var flexLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            flexLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            flexLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            flexLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            flexLayout.Controls.Add(flexCards);
            flexLayout.Controls.Add(flexLine);
            flexDetails = flexLayout;
            instructions.Dock = DockStyle.Fill;
            instructions.Font = new Font(Font.FontFamily, 9);
            root.Controls.Add(instructions);
            root.Controls.Add(BuildTables());

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 7, 0, 0) };
            browser = CreateButton("Import fallback");
            chrome = CreateButton("Chrome sync");
            refresh = CreateButton("Refresh now");
            refresh.Tag = "Accent";
            manual = CreateButton("Manual (pauses sync)");
            appearance = CreateButton("Appearance");
            backup = CreateButton("Export backup");
            info = CreateButton("How it works");
            actions.Controls.AddRange(new Control[] { refresh, chrome, browser, manual, appearance, backup, info });
            root.Controls.Add(actions);
            status.Dock = DockStyle.Fill;
            status.Font = new Font(Font.FontFamily, 9);
            root.Controls.Add(status);
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text, AccessibleName = text, AutoSize = true,
                Padding = new Padding(8, 4, 8, 4), Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 9, 0)
            };
        }

        private SurfaceCard CreateMetric(string caption, out Label value, bool large)
        {
            var card = new SurfaceCard { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8), Margin = new Padding(0, 0, 9, 7) };
            card.Controls.Add(new Label { Name = "MetricCaption", Text = caption, Dock = DockStyle.Top, Height = 22, Font = new Font(Font.FontFamily, 8, FontStyle.Bold) });
            value = new Label { Dock = DockStyle.Bottom, Height = 38, Font = new Font(Font.FontFamily, large ? 24 : 19, FontStyle.Bold), AccessibleName = caption };
            card.Controls.Add(value);
            return card;
        }

        private Control BuildInputBar()
        {
            var inputs = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 12, 0, 0) };
            var previous = new Button { Text = "‹", Width = 28, Height = 28, AccessibleName = "Previous month" };
            var next = new Button { Text = "›", Width = 28, Height = 28, AccessibleName = "Next month" };
            previous.Click += (s, e) => NavigateMonth(-1);
            next.Click += (s, e) => NavigateMonth(1);
            month.Format = DateTimePickerFormat.Custom;
            month.CustomFormat = "MMMM yyyy";
            month.ShowUpDown = true;
            month.Width = 150;
            month.AccessibleName = "Selected month";
            asof.Format = DateTimePickerFormat.Custom;
            asof.CustomFormat = "dd MMM yyyy";
            asof.Width = 150;
            asof.AccessibleName = "Compare hours through date";
            hours.Maximum = 1000;
            hours.Width = 78;
            hours.AccessibleName = "BambooHR total hours";
            minutes.Maximum = 59;
            minutes.Width = 58;
            minutes.AccessibleName = "BambooHR total minutes";
            followToday.Text = "Today";
            followToday.AutoSize = true;
            followToday.Checked = true;
            inputs.Controls.AddRange(new Control[]
            {
                InputLabel("Month"), previous, month, next, InputLabel("  As of"), asof,
                InputLabel("  Pay Period total"), hours, InputLabel("h"), minutes,
                InputLabel("m"), followToday
            });
            return inputs;
        }

        private void NavigateMonth(int direction)
        {
            DateTime target = selected.AddMonths(direction);
            if (target >= month.MinDate && target <= month.MaxDate) month.Value = target;
        }

        private static Label InputLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        }

        private Control BuildTables()
        {
            ConfigureGrid(grid, "DailyEntries");
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", ReadOnly = true, FillWeight = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Day", HeaderText = "Day", ReadOnly = true, FillWeight = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Holiday", HeaderText = "Malta public holiday", ReadOnly = true, FillWeight = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Leave", HeaderText = "Approved leave hours (0-8)", FillWeight = 120 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Included", HeaderText = "Holiday credited in total", FillWeight = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Credit", HeaderText = "Future credit deducted", ReadOnly = true, FillWeight = 115 });

            ConfigureGrid(history, "FlexHistory");
            history.ReadOnly = true;
            history.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            history.Columns.Add("Month", "Month");
            history.Columns.Add("Difference", "Monthly difference");
            history.Columns.Add("Carry", "Carry-over");
            history.Columns.Add("Final", "Final flex balance");
            history.Columns.Add("State", "Status");
            history.Columns[4].FillWeight = 210;
            history.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) month.Value = (DateTime)history.Rows[e.RowIndex].Tag; };

            contentTabs = new ModernTabs { Dock = DockStyle.Fill };
            var entries = new TabPage("Manual fallback") { Name = "ManualLeave" };
            var balances = new TabPage("Flex history") { Name = "FlexHistory" };
            entries.Controls.Add(grid);
            flexDetails.Controls.Add(history);
            balances.Controls.Add(flexDetails);
            contentTabs.TabPages.Add(BuildCalendarTab());
            contentTabs.TabPages.Add(BuildChromeDailyTab());
            contentTabs.TabPages.Add(balances);
            contentTabs.TabPages.Add(entries);
            contentTabs.TabPages.Add(BuildUpcomingLeaveTab());
            contentTabs.SelectedIndexChanged += (s, e) =>
            {
                if (contentTabs.SelectedTab == balances && IsHandleCreated) BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && history.Visible && history.DisplayedRowCount(false) > 0 && history.Rows.Count == 12)
                        history.FirstDisplayedScrollingRowIndex = Math.Max(0, selected.Month - 8);
                }));
            };
            return contentTabs;
        }

        private static void ConfigureGrid(DataGridView control, string name)
        {
            control.Name = name;
            control.Dock = DockStyle.Fill;
            control.AllowUserToAddRows = false;
            control.AllowUserToDeleteRows = false;
            control.RowHeadersVisible = false;
            control.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            control.ColumnHeadersHeight = 36;
            control.RowTemplate.Height = 30;
        }
    }
}
