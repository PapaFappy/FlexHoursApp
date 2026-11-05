using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        public static void TestChromeDesktop()
        {
            string folder = Path.Combine(Environment.CurrentDirectory, "chrome-desktop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "months.json");
            try
            {
                DateTime today = DateTime.Today;
                var state = new Store { ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" } };
                state.View = new ViewState { SelectedMonth = new DateTime(today.Year, today.Month, 1), AsOfDate = today, FollowToday = true };
                StateFile.Save(file, state);
                using (var window = new MainForm(false, file))
                {
                    window.Show();
                    Application.DoEvents();
                    var snapshot = new ChromeSnapshot { Month = today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), CompanyUrl = "https://example.bamboohr.com/", CapturedAtUtc = DateTime.UtcNow, PayMinutes = 6000 };
                    ChromeNativeHost.WriteSnapshot(folder, snapshot);
                    var start = DateTime.UtcNow;
                    while (!window.data.ChromeSnapshot && (DateTime.UtcNow - start).TotalSeconds < 3)
                    {
                        Application.DoEvents();
                        Thread.Sleep(10);
                    }
                    if (!window.data.ChromeSnapshot || window.data.PayMinutes != 6000 || window.values[2].Text != "Check leave")
                        throw new Exception("Desktop did not receive a Chrome snapshot safely");
                    if (today.Year == 2026 && today.Month == 10)
                    {
                        var daily = ChromeDayTextParser.Parse(ChromeTests.DailyFixture(), new DateTime(2026, 10, 1), 2536);
                        snapshot.Days = daily.Days;
                        snapshot.DailyVerified = true;
                        snapshot.PayMinutes = 2536;
                        snapshot.CapturedAtUtc = DateTime.UtcNow;
                        ChromeNativeHost.WriteSnapshot(folder, snapshot);
                        window.ReadChromeSnapshot();
                        if (window.chromeDailyGrid.Rows.Count != 31 || !window.data.ChromeDailyVerified || window.values[2].Text == "Check leave")
                            throw new Exception("Verified daily ledger was not displayed or used");
                        if(Convert.ToString(window.chromeDailyGrid.Rows[1].Cells["Sick"].Value)!="5h 00m"
                            || Convert.ToString(window.chromeDailyGrid.Rows[1].Cells["Worked"].Value)!="2h 23m")
                            throw new Exception("Main month view did not populate sick and worked hours");
                        window.followToday.Checked = false;
                        window.asof.Value = new DateTime(2026, 10, 2);
                        if (Convert.ToString(window.chromeDailyGrid.Rows[1].Cells["Difference"].Value) != "-0h 37m"
                            || Convert.ToString(window.chromeDailyGrid.Rows[4].Cells["Difference"].Value) != "After as-of")
                            throw new Exception("Daily differences ignored the comparison date");
                        if (!window.flexValues[2].Parent.Controls["MetricCaption"].Text.StartsWith("PROVISIONAL"))
                            throw new Exception("An incomplete month's flex was labelled final");
                        window.followToday.Checked = true;
                        window.dailyFilter.SelectedIndex = 1;
                        foreach (DataGridViewRow row in window.chromeDailyGrid.Rows)
                            if ((DateTime)row.Tag == new DateTime(2026, 10, 1)) throw new Exception("Leave filter included a work-only day");
                        if (window.chromeDailyGrid.Rows.Count == 0 || !window.chromeDailyTotals.Text.Contains("Paid total 42h 16m"))
                            throw new Exception("Filtering changed month totals or hid all leave");
                        window.dailyFilter.SelectedIndex = 2;
                        if (window.chromeDailyGrid.Rows.Count != 0) throw new Exception("Fully read month showed unread dates");
                        window.dailyFilter.SelectedIndex = 0;
                        var decemberDays=new System.Collections.Generic.List<ChromeDay>();
                        for(int day=1;day<=31;day++)decemberDays.Add(new ChromeDay{Date="2026-12-"+day.ToString("00"),Pto=day==14?480:0,Total=day==14?480:0});
                        var december=new ChromeSnapshot{Month="2026-12",CompanyUrl="https://example.bamboohr.com/",CapturedAtUtc=DateTime.UtcNow,PayMinutes=480,Days=decemberDays,DailyVerified=true};
                        ChromeNativeHost.WriteSnapshot(folder,december);
                        window.ReadChromeSnapshot();
                        bool foundDecember=false;
                        foreach(DataGridViewRow row in window.upcomingLeaveGrid.Rows)
                            if(Convert.ToString(row.Cells[0].Value)==new DateTime(2026,12,14).ToString("dd MMM yyyy")&&Convert.ToString(row.Cells[2].Value)=="PTO / vacation")foundDecember=true;
                        if(!foundDecember||window.data.PayMinutes!=2536)throw new Exception("December vacation was not listed separately from October");
                        window.contentTabs.SelectedTab = window.contentTabs.TabPages["Timesheet"];
                        Application.DoEvents();
                        using (var image = new System.Drawing.Bitmap(window.Width, window.Height))
                        {
                            window.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, window.Width, window.Height));
                            image.Save("chrome-daily-preview.png");
                        }
                        var partial=new ChromeSnapshot{Month="2026-10",CompanyUrl="https://example.bamboohr.com/",CapturedAtUtc=DateTime.UtcNow,PayMinutes=2536,
                            Days=new System.Collections.Generic.List<ChromeDay>{daily.Days[0]},DailyWarning="Some dates could not be read"};
                        ChromeNativeHost.WriteSnapshot(folder,partial);
                        window.ReadChromeSnapshot();
                        if(Convert.ToString(window.chromeDailyGrid.Rows[0].Cells["Status"].Value)!="Not reconciled"
                            ||Convert.ToString(window.chromeDailyGrid.Rows[1].Cells["Worked"].Value)!="-"
                            ||Convert.ToString(window.chromeDailyGrid.Rows[1].Cells["Status"].Value)!="Not read")
                            throw new Exception("Partial daily readings were hidden or missing hours displayed as zero");
                    }
                    window.manual.PerformClick();
                    decimal manualTotal = window.data.PayMinutes;
                    snapshot.PayMinutes = 7000;
                    snapshot.DailyVerified = false;
                    snapshot.Days = null;
                    snapshot.CapturedAtUtc = DateTime.UtcNow;
                    ChromeNativeHost.WriteSnapshot(folder, snapshot);
                    window.ReadChromeSnapshot();
                    if (window.store.ChromeSync.Enabled || window.data.PayMinutes != manualTotal || window.data.ChromeSnapshot)
                        throw new Exception("Manual mode did not pause automatic receiving");
                    window.Close();
                }
                using (var window = new MainForm(false, file))
                {
                    window.Show();
                    Application.DoEvents();
                    if (window.store.ChromeSync.Enabled) throw new Exception("Paused receiving was not persisted");
                    using (var dialog = new ChromeSyncForm(window.store.ChromeSync, false, today))
                    {
                        dialog.Show();
                        Application.DoEvents();
                        foreach (Control root in dialog.Controls)
                            foreach (Control child in root.Controls)
                                if (child.Bottom > root.Height - root.Padding.Bottom) throw new Exception("Chrome settings controls clipped");
                        using (var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height))
                        {
                            dialog.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, dialog.Width, dialog.Height));
                            image.Save("chrome-settings-preview.png");
                        }
                        dialog.Close();
                    }
                    window.Close();
                }
            }
            finally
            {
                foreach (string entry in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.Delete(entry);
                if (Directory.Exists(Path.Combine(folder, "Chrome"))) Directory.Delete(Path.Combine(folder, "Chrome"));
                Directory.Delete(folder);
            }
        }
    }
}
