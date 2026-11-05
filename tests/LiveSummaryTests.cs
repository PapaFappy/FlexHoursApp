using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        public static void TestLiveSummaryBalance()
        {
            var now = new DateTime(2026, 10, 23, 8, 53, 0, DateTimeKind.Utc);
            var cards = new List<ChromeRawDayCard>();
            for (int i = 1; i <= 31; i++) cards.Add(new ChromeRawDayCard { DateLabel = "Oct " + i,
                Text = i == 23 ? "Oct 23\n3h 22m\n7:30 AM - Now\n3h 21m" : "Oct " + i + "\n0h 00m" });
            var request = new ChromeCaptureRequest { Command = "capture", CompanyUrl = "example", PageUrl = "https://example.bamboohr.com/timesheet",
                Month = "2026-10", SummaryText = "3h 22m edit Clocked In: Today at 7:30 AM Clock Out This Week 37h 49m Oct 19-25 136h 56m Pay Period Oct 1-31, 2026 Paid Hours 128h 34m REG 5h Time Off", Cards = cards };
            bool unexplainedRejected = false;
            try { SummaryParser.Parse(request.SummaryText.Replace("3h 22m edit", "3h 21m edit"), new DateTime(2026, 10, 1), now.ToLocalTime().Date); }
            catch (Exception) { unexplainedRejected = true; }
            if (!unexplainedRejected) throw new Exception("An unexplained summary mismatch was accepted as a running clock");
            var capture = ChromeNativeHost.Capture(request, now);
            if (capture.DailyVerified || !capture.FutureVerified || capture.PayMinutes != 8216)
                throw new Exception("Live Pay Period summary was blocked by an unreconciled active day");
            var state = new Store { ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" } };
            state.View = new ViewState { SelectedMonth = new DateTime(2026, 10, 1), AsOfDate = new DateTime(2026, 10, 23), FollowToday = false };
            if (!ChromeSync.Apply(state, capture, now) || state.Months["2026-10"].BrowserFuture != 0)
                throw new Exception("Verified empty future days did not establish a zero deduction");
            var result = WorkHoursCalculator.Calculate(new DateTime(2026, 10, 1), new DateTime(2026, 10, 23), state.Months["2026-10"]);
            if (result.Balance != 56 || result.Remaining != 2344) throw new Exception("136h56 minus 136h expected did not give +56m");
            string folder = Path.Combine(Environment.CurrentDirectory, "live-summary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "months.json");
            try
            {
                StateFile.Save(file, state);
                Directory.CreateDirectory(Path.Combine(folder, "Chrome"));
                string manifest = "{\"name\":\"com.flexhours.chrome_sync\",\"allowed_origins\":[\"chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/\"]}";
                File.WriteAllText(Path.Combine(folder, "Chrome", "native-host.json"), manifest);
                using (var window = new MainForm(false, file))
                {
                    window.Show(); Application.DoEvents();
                    if (window.values[2].Text != "+0h 56m" || !window.balanceNote.Text.StartsWith("Ahead") || window.data.ChromeDailyVerified)
                        throw new Exception("Summary-based balance was hidden, or partial daily readings were called verified");
                    string launched = null;
                    window.launchChromeRefresh = url => launched = url;
                    window.refresh.PerformClick();
                    if (launched != "chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/popup.html#refresh=2026-10")
                        throw new Exception("Desktop refresh did not target the installed extension and selected month");
                    window.Close();
                }
            }
            finally {
                foreach (string item in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.Delete(item);
                if (Directory.Exists(Path.Combine(folder, "Chrome"))) Directory.Delete(Path.Combine(folder, "Chrome"));
                Directory.Delete(folder);
            }
            cards.Single(card => card.DateLabel == "Oct 26").Text = "Oct 26\n8 hours Malta Vacation";
            capture = ChromeNativeHost.Capture(request, now);
            if (!capture.FutureVerified || ChromeDailyParser.After(capture.Days, now.ToLocalTime().Date) != 480)
                throw new Exception("Future vacation was omitted from a summary-based balance");
            var withLeave = new Store { ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" } };
            ChromeSync.Apply(withLeave, capture, now);
            if (WorkHoursCalculator.Calculate(new DateTime(2026, 10, 1), new DateTime(2026, 10, 23), withLeave.Months["2026-10"]).Balance != -424)
                throw new Exception("Future vacation was not deducted from the live monthly summary");
            cards.RemoveAll(card => card.DateLabel == "Oct 30");
            var missing = ChromeNativeHost.Capture(request, now);
            if (missing.FutureVerified) throw new Exception("Missing future date was inferred to have zero hours");
            missing.FutureVerified = true;
            bool rejected = false;
            try { ChromeNativeHost.Validate(missing, now); } catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception("A false future-verification flag bypassed validation");
            request.Cards = null;
            if (ChromeNativeHost.Capture(request, now).FutureVerified) throw new Exception("Summary-only data invented a future deduction");
        }
    }
}
