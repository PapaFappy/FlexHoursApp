using System;
using System.IO;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        public static void TestDashboardAudit()
        {
            string folder = Path.Combine(Environment.CurrentDirectory, "dashboard-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string statePath = Path.Combine(folder, "months.json");
            try
            {
                var state = new Store();
                state.View = new ViewState { SelectedMonth = new DateTime(2026, 10, 1), AsOfDate = new DateTime(2026, 10, 22), FollowToday = false };
                StateFile.Save(statePath, state);
                using (var window = new MainForm(false, statePath))
                {
                    window.Show();
                    Application.DoEvents();
                    window.contentTabs.SelectedTab = window.contentTabs.TabPages["ManualLeave"];
                    Application.DoEvents();
                    if (window.values[2].Text != "Pending" || window.values[3].Text != "Pending")
                        throw new Exception("An untouched month is displayed as a confirmed deficit");
                    window.hours.Value = 133;
                    window.minutes.Value = 34;
                    window.grid.Rows[25].Cells[3].Value = 5m;
                    if (window.values[2].Text != "+0h 34m" || window.values[3].Text != "42h 26m")
                        throw new Exception("Manual October reference calculation does not reconcile");
                    if (!window.balanceNote.Text.StartsWith("Ahead") || window.values[2].ForeColor != new ThemePalette(window.store.Appearance).Positive)
                        throw new Exception("Ahead balance was not visible in green");
                    window.hours.Value = 132;
                    if (!window.balanceNote.Text.StartsWith("Behind") || window.values[2].ForeColor != new ThemePalette(window.store.Appearance).Negative)
                        throw new Exception("Behind balance was not visible in red");
                    window.hours.Value = 133;
                    window.grid.CurrentCell = window.grid.Rows[25].Cells[3];
                    window.grid.BeginEdit(true);
                    ((DataGridViewTextBoxEditingControl)window.grid.EditingControl).Text = "-1";
                    window.month.Value = new DateTime(2026, 11, 1);
                    if (window.selected != new DateTime(2026, 10, 1)) throw new Exception("Invalid leave edit allowed month navigation");
                    ((DataGridViewTextBoxEditingControl)window.grid.EditingControl).Text = "4.5";
                    window.month.Value = new DateTime(2026, 11, 1);
                    if (window.selected != new DateTime(2026, 11, 1) || window.store.Months["2026-10"].Leave["2026-10-26"] != 270)
                        throw new Exception("Valid pending leave edit was lost when changing months");
                    window.month.Value = new DateTime(2026, 12, 1);
                    window.hours.Value = 1;
                    if (window.values[2].Text != "Check leave")
                        throw new Exception("Future holiday credits greater than the total were silently accepted");
                    window.month.Value = new DateTime(2026, 10, 1);
                    window.data.BrowserImported = true;
                    window.data.BrowserFuture = 300;
                    window.data.BrowserAsOf = window.asof.Value.Date;
                    window.data.BrowserImportedAt = DateTime.Now;
                    window.LoadMonth();
                    if (window.hours.Enabled || window.grid.Enabled || window.values[2].Text != "+0h 34m")
                        throw new Exception("Imported mode did not lock edits or reconcile totals");
                    window.asof.Value = window.asof.Value.AddDays(1);
                    if (window.values[2].Text != "Check leave" || window.values[3].Text != "42h 26m")
                        throw new Exception("Stale comparison date showed a confirmed daily balance");
                    if (window.balanceNote.Text.StartsWith("Ahead") || window.balanceNote.Text.StartsWith("Behind"))
                        throw new Exception("Unverified balance was described as a confirmed surplus or deficit");
                    window.manual.PerformClick();
                    if (!window.hours.Enabled || !window.grid.Enabled || window.data.BrowserImported)
                        throw new Exception("Manual entries did not restore editable controls");
                    string saved = File.ReadAllText(statePath);
                    File.WriteAllText(statePath, "external damage");
                    window.hours.Value = 134;
                    if (window.lastSaveSucceeded || !window.status.Text.StartsWith("Not saved:") || File.ReadAllText(statePath) != "external damage")
                        throw new Exception("Failed save was hidden or damaged data overwritten");
                    File.WriteAllText(statePath, saved);
                    window.hours.Value = 133;
                    if (!window.lastSaveSucceeded) throw new Exception("Saving did not recover after storage was repaired");
                    window.Close();
                }
                using (var window = new MainForm(false, statePath))
                {
                    window.Show();
                    Application.DoEvents();
                    if (window.data.BrowserImported || window.data.PayMinutes != 8014 || window.data.Leave["2026-10-26"] != 270)
                        throw new Exception("Audited dashboard did not restore its edits");
                    window.Close();
                }
            }
            finally
            {
                foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
}
