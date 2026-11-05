using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class BrowserImportForm
    {
        private const string Fixture = "133h 34m Pay Period Oct 1-31, 2026 Paid Hours 128h 34m REG 5h Time Off";
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Pump(Task task)
        {
            var start = DateTime.UtcNow;
            while (!task.IsCompleted && (DateTime.UtcNow - start).TotalSeconds < 3)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
            if (!task.IsCompleted) throw new Exception("Import test timed out");
            task.GetAwaiter().GetResult();
        }

        public static void TestImportWorkflow()
        {
            var month = new DateTime(2026, 10, 1);
            var date = new DateTime(2026, 10, 22);
            var pending = new TaskCompletionSource<string>();
            using (var dialog = new BrowserImportForm(month, date, (url, token) => pending.Task))
            {
                dialog.Show();
                dialog.company.Text = "example";
                dialog.Parse(Fixture, "fixture");
                dialog.monthConfirmed.Checked = true;
                Check(dialog.apply.Enabled, "Valid summary did not enable import");
                Task read = dialog.ReadSummary();
                Check(dialog.parsed == null && !dialog.apply.Enabled && !dialog.paste.Enabled && !dialog.company.Enabled, "Old result stayed available during read");
                dialog.monthConfirmed.Checked = true;
                Check(!dialog.apply.Enabled, "Confirmation reenabled import during read");
                pending.SetException(new Exception("Simulated browser unavailable"));
                Pump(read);
                Check(dialog.parsed == null && !dialog.apply.Enabled && dialog.read.Enabled, "Failure retained stale summary or blocked retry");
                dialog.Parse(Fixture, "fixture");
                dialog.monthConfirmed.Checked = true;
                dialog.futureHours.Value = 5;
                dialog.confirmed.Checked = true;
                dialog.futureMinutes.Value = 1;
                Check(!dialog.confirmed.Checked, "Editing credits retained old confirmation");
                dialog.futureMinutes.Value = 0;
                dialog.confirmed.Checked = true;
                dialog.ApplySummary();
                Check(dialog.Summary.Total == 8014 && dialog.FutureCredits == 300, "Confirmed import returned incorrect totals");
            }
            using (var dialog = new BrowserImportForm(month, date))
            {
                dialog.Parse(Fixture, "fixture");
                dialog.monthConfirmed.Checked = true;
                dialog.ApplySummary();
                Check(dialog.Summary != null && dialog.FutureCredits == null, "Unconfirmed credits must allow monthly-only import");
            }
            using (var dialog = new BrowserImportForm(month, new DateTime(2026, 10, 31)))
            {
                dialog.Parse(Fixture, "fixture");
                Check(dialog.confirmed.Checked, "Month-end zero future credits not recognised");
                dialog.monthConfirmed.Checked = true;
                dialog.futureHours.Value = 1;
                dialog.confirmed.Checked = true;
                dialog.ApplySummary();
                Check(dialog.Summary == null && dialog.status.Text.Contains("Maximum"), "Impossible future credit imported");
            }
            pending = new TaskCompletionSource<string>();
            CancellationToken observed = CancellationToken.None;
            using (var dialog = new BrowserImportForm(month, date, (url, token) => { observed = token; return pending.Task; }))
            {
                dialog.Show();
                dialog.company.Text = "example";
                Task read = dialog.ReadSummary();
                dialog.Close();
                Check(observed.IsCancellationRequested, "Closing import did not cancel pending browser read");
                pending.SetResult(Fixture);
                Pump(read);
                Check(dialog.Summary == null, "Closed dialog imported a late result");
            }
        }
    }
}
