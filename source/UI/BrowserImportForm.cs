using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class BrowserImportForm : Form
    {
        public BrowserSummary Summary;
        public decimal? FutureCredits;
        public string Source;
        private readonly DateTime selected, asof;
        private readonly Func<string, CancellationToken, Task<string>> readPage;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly TextBox company = new TextBox(), preview = new TextBox();
        private readonly NumericUpDown futureHours = new NumericUpDown(), futureMinutes = new NumericUpDown();
        private readonly CheckBox confirmed = new CheckBox(), monthConfirmed = new CheckBox();
        private readonly Label status = new Label();
        private readonly Button apply = new Button(), read = new Button(), open = new Button(), paste = new Button();
        private BrowserSummary parsed;
        private bool reading;
        private bool lifetimeDisposed;

        public BrowserImportForm(DateTime month, DateTime date)
            : this(month, date, BrowserReader.Read) { }

        internal BrowserImportForm(DateTime month, DateTime date, Func<string, CancellationToken, Task<string>> reader)
        {
            selected = month;
            asof = date.Date;
            readPage = reader;
            Text = "Import BambooHR summary";
            Size = new Size(800, 750);
            MinimumSize = Size;
            Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 10 };
            foreach (int height in new[] { 42, 60, 40, 42, 130, 50, 48, 48, 45 })
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);
            root.Controls.Add(new Label { Text = "Import your timesheet summary", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 18, FontStyle.Bold) });
            root.Controls.Add(new Label { Text = "1. Open BambooHR and sign in.  2. Open Timesheet for " + month.ToString("MMMM yyyy") + ".\n3. Read summary, verify the figures and import. Your browser session is retained.", Dock = DockStyle.Fill });
            company.Dock = DockStyle.Top;
            company.Text = ReadCompany();
            company.AccessibleName = "BambooHR company subdomain or URL";
            root.Controls.Add(company);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            open.Text = "Open BambooHR";
            read.Text = "Read summary";
            paste.Text = "Paste summary";
            foreach (var button in new[] { open, read, paste }) button.AutoSize = true;
            actions.Controls.AddRange(new Control[] { open, read, paste });
            root.Controls.Add(actions);
            preview.Multiline = true;
            preview.ReadOnly = true;
            preview.ScrollBars = ScrollBars.Vertical;
            preview.Dock = DockStyle.Fill;
            preview.Text = "Your imported figures will appear here.";
            root.Controls.Add(preview);
            monthConfirmed.Text = "I checked " + month.ToString("MMMM yyyy") + " and the worked hours are through " + asof.ToString("dd MMM yyyy") + ".\nFuture paid credits are allowed; work after that date must not be included.";
            monthConfirmed.Dock = DockStyle.Fill;
            root.Controls.Add(monthConfirmed);
            var future = new FlowLayoutPanel { Dock = DockStyle.Fill };
            future.Controls.Add(new Label { Text = "Credits AFTER " + date.ToString("dd MMM") + " already in total:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            futureHours.Maximum = 1000;
            futureHours.Width = 65;
            futureMinutes.Maximum = 59;
            futureMinutes.Width = 55;
            future.Controls.AddRange(new Control[] { futureHours, new Label { Text = "h", AutoSize = true }, futureMinutes, new Label { Text = "m", AutoSize = true } });
            root.Controls.Add(future);
            confirmed.Text = "I verified future paid leave and holiday credits (0 if none). Changing the amount clears this check.";
            confirmed.Dock = DockStyle.Fill;
            root.Controls.Add(confirmed);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill };
            apply.Text = "Import into month";
            apply.AutoSize = true;
            apply.Enabled = false;
            bottom.Controls.Add(apply);
            root.Controls.Add(bottom);
            status.Dock = DockStyle.Fill;
            status.Text = "Without confirmed future credits, the monthly remainder works; the daily balance remains unconfirmed.";
            root.Controls.Add(status);

            open.Click += (s, e) => OpenBrowser();
            read.Click += async (s, e) => await ReadSummary();
            paste.Click += (s, e) =>
            {
                ClearSummary();
                try { Parse(Clipboard.GetText(), "Copied summary"); }
                catch (Exception error) { status.Text = error.Message; }
            };
            monthConfirmed.CheckedChanged += (s, e) => UpdateApply();
            futureHours.ValueChanged += (s, e) => confirmed.Checked = false;
            futureMinutes.ValueChanged += (s, e) => confirmed.Checked = false;
            apply.Click += (s, e) => ApplySummary();
            FormClosed += (s, e) => lifetime.Cancel();
        }

        private void OpenBrowser()
        {
            try
            {
                string origin = BrowserReader.CompanyUrl(company.Text);
                BrowserReader.Open(origin);
                SaveCompany(origin);
                status.Text = "Sign in, open Timesheet, then click Read summary. Keep one BambooHR tab open.";
            }
            catch (Exception error) { status.Text = error.Message; }
        }

        private void ClearSummary()
        {
            parsed = null;
            Summary = null;
            FutureCredits = null;
            Source = null;
            monthConfirmed.Checked = confirmed.Checked = false;
            apply.Enabled = false;
            preview.Text = "No summary imported.";
        }

        private void SetReading(bool value)
        {
            reading = value;
            read.Enabled = open.Enabled = paste.Enabled = company.Enabled = !value;
            monthConfirmed.Enabled = confirmed.Enabled = futureHours.Enabled = futureMinutes.Enabled = !value;
            UpdateApply();
        }

        private void UpdateApply() { apply.Enabled = !reading && parsed != null && monthConfirmed.Checked; }

        private async Task ReadSummary()
        {
            if (reading) return;
            ClearSummary();
            SetReading(true);
            try
            {
                status.Text = "Reading visible timesheet text...";
                string origin = BrowserReader.CompanyUrl(company.Text);
                string text = await readPage(origin, lifetime.Token);
                if (IsDisposed || lifetime.IsCancellationRequested) return;
                Parse(text, origin);
            }
            catch (Exception error)
            {
                if (!IsDisposed && !lifetime.IsCancellationRequested)
                    status.Text = "Could not read the browser: " + error.Message + " Copy the summary and use Paste summary instead.";
            }
            finally { if (!IsDisposed) SetReading(false); }
        }

        private void ApplySummary()
        {
            if (reading || parsed == null || !monthConfirmed.Checked) return;
            decimal credit = futureHours.Value * 60 + futureMinutes.Value;
            try
            {
                if (confirmed.Checked) FutureCreditValidator.Validate(selected, asof, parsed.Total, credit);
                Summary = parsed;
                FutureCredits = confirmed.Checked ? (decimal?)credit : null;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ArgumentException error) { status.Text = error.Message; }
        }

        private void Parse(string text, string source)
        {
            // Do not retain an earlier usable result if the new summary is invalid.
            ClearSummary();
            parsed = SummaryParser.Parse(text, selected);
            Source = source;
            futureHours.Value = futureMinutes.Value = 0;
            preview.Text = "Period: " + parsed.Period + " (selected year: " + selected.Year + ")\r\nPay Period total: " + WorkHoursCalculator.Hours(parsed.Total)
                + "\r\nRegular paid hours: " + (parsed.HasRegular ? WorkHoursCalculator.Hours(parsed.Regular) : "not detected")
                + "\r\nTime off: " + (parsed.HasTimeOff ? WorkHoursCalculator.Hours(parsed.TimeOff) : "not detected")
                + "\r\nCompare through " + asof.ToString("dd MMM yyyy") + ".";
            if (FutureCreditValidator.Maximum(selected, asof, parsed.Total) == 0) confirmed.Checked = true;
            status.Text = "Check the month/year. The summary cannot tell us the dates of future credits.";
            UpdateApply();
        }

        private static string ConfigPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexHours", "browser-company.txt"); } }
        private static string ReadCompany()
        {
            try { return File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "yourcompany.bamboohr.com"; }
            catch { return "yourcompany"; }
        }
        private static void SaveCompany(string origin)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, origin);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !lifetimeDisposed)
            {
                lifetime.Cancel();
                lifetime.Dispose();
                lifetimeDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
