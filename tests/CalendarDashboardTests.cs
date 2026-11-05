using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        public static void TestCalendarDashboard()
        {
            string folder = Path.Combine(Environment.CurrentDirectory, "calendar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "months.json");
            try
            {
                var state = new Store();
                state.View = new ViewState { SelectedMonth = new DateTime(2026, 10, 1), AsOfDate = new DateTime(2026, 10, 22), FollowToday = false };
                var parsed = ChromeDayTextParser.Parse(ChromeTests.DailyFixture(), new DateTime(2026, 10, 1), 2536);
                state.Months["2026-10"] = new MonthData { PayMinutes = 2536, HasRecordedHours = true, BrowserImported = true,
                    ChromeSnapshot = true, ChromeDailyVerified = true, ChromeDays = parsed.Days, BrowserAsOf = new DateTime(2026, 10, 22), BrowserImportedAt = DateTime.Now };
                StateFile.Save(file, state);
                using (var window = new MainForm(false, file))
                {
                    window.Show(); Application.DoEvents();
                    if (window.contentTabs.SelectedTab.Name != "Overview" || window.calendarTiles.Count(tile => tile.Visible) != 31
                        || window.calendarTiles[3].Date != new DateTime(2026, 10, 1)) throw new Exception("October calendar alignment failed");
                    window.calendarTiles[4].PerformClick();
                    if (!window.dayDetails.Text.Contains("5h 00m") || !window.dayDetails.Text.Contains("7h 23m") || window.data.PayMinutes != 2536)
                        throw new Exception("Selecting a day lost its sick allocation or changed the total");
                    Draw(window, "premium-dark-preview.png");
                    window.store.Appearance = AppearanceSettings.Defaults(false); window.Recalculate();
                    Draw(window, "premium-light-preview.png");
                    window.store.Appearance = AppearanceSettings.Defaults(true); window.Recalculate();
                    window.Size = window.MinimumSize; Application.DoEvents();
                    Draw(window, "premium-compact-preview.png");
                    window.contentTabs.SelectedTab = window.contentTabs.TabPages["Timesheet"];
                    window.month.Value = new DateTime(2026, 8, 1);
                    window.contentTabs.SelectedTab = window.contentTabs.TabPages["Overview"];
                    Application.DoEvents();
                    if (window.calendarTiles.Count(tile => tile.Visible) != 31 || window.calendarTiles[35].Date != new DateTime(2026, 8, 31))
                        throw new Exception("Six-week calendar failed when changed from another tab");
                    if (!window.dayDetails.Text.Contains("No daily reading")) throw new Exception("Unknown days were displayed as recorded zeroes");
                    window.month.Value = new DateTime(2028, 2, 1);
                    if (window.calendarTiles.Count(tile => tile.Visible) != 29) throw new Exception("Leap day was omitted");
                    window.month.Value = new DateTime(2026, 10, 1);
                    window.data.ChromeDailyVerified = false;
                    window.Recalculate(); window.calendarTiles[4].PerformClick();
                    if (!window.dayDetails.Text.StartsWith("NEEDS REVIEW")) throw new Exception("Partial calendar reading was presented as reconciled");
                    window.Close();
                }
            }
            finally { foreach (string item in Directory.GetFiles(folder)) File.Delete(item); Directory.Delete(folder); }
        }
    }
}
