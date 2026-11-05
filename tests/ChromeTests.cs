using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FlexHours
{
    public static class ChromeTests
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        public static string DailyFixture()
        {
            var text = new StringBuilder("42h 16m Pay Period Oct 1-31, 2026 Paid Hours 20h 16m REG 22h Time Off\nMon\nSep 28\n9h 48m\n7:00 AM - 5:10 PM\n");
            for (int day = 1; day <= 31; day++)
            {
                text.AppendLine(new DateTime(2026, 10, day).ToString("ddd", CultureInfo.InvariantCulture));
                text.AppendLine("Oct " + day);
                text.AppendLine(day == 1 ? "9h 57m" : day == 2 ? "2h 23m" : day == 5 ? "7h 56m" : "0h 00m");
                if (day == 1) text.AppendLine("7:00 AM - 5:11 PM");
                if (day == 2) text.AppendLine("7:00 AM - 9:23 AM\n5 hours Malta Sick Leave");
                if (day == 26) text.AppendLine("5 hours Malta PTO");
                if (day == 27) text.AppendLine("4 hours Malta Sick Leave");
                if (day == 28) text.AppendLine("8 hours Malta Public Holiday");
            }
            return text.ToString().Replace("\r", "");
        }

        public static void TestDailyRows()
        {
            var month = new DateTime(2026, 10, 1);
            string fixture = DailyFixture();
            var parsed = ChromeDayTextParser.Parse(fixture, month, 2536);
            Check(parsed.Verified && parsed.Days.Count == 31, "Full daily-card fixture did not reconcile: " + parsed.Warning);
            var sick = parsed.Days.Single(day => day.Date == "2026-10-02");
            Check(sick.Worked == 143 && sick.Sick == 300 && sick.Total == 443, "Screenshot's 2h23 work plus 5h sick must equal 7h23");
            Check(parsed.Days.Single(day=>day.Date=="2026-10-05").Worked==476&&parsed.Days.Single(day=>day.Date=="2026-10-06").Worked==0,
                "An adjacent day's worked total shifted to the wrong date");
            Check(!parsed.Days.Any(day => day.Date.StartsWith("2026-09")), "Previous-month grey rows were included");
            Check(ChromeDailyParser.After(parsed.Days, new DateTime(2026, 10, 22)) == 1020, "Future PTO/sick/holiday credits not dated correctly");
            foreach (string invalid in new[] { fixture.Replace("5 hours Malta Sick Leave", "5 hours Malta Sick Leave Pending"), fixture.Replace("Oct 8\n0h 00m", "missing date\n0h 00m"), fixture.Replace("9h 57m", "9h 99m") })
                Check(!ChromeDayTextParser.Parse(invalid, month, 2536).Verified, "Invalid or incomplete daily cards accepted");
            Check(!ChromeDayTextParser.Parse(fixture, month, 2537).Verified, "Unreconciled paid total accepted");
            Check(!ChromeDayTextParser.Parse(fixture.Replace("5 hours Malta PTO", "300 minutes Malta PTO"), month, 2536).Verified, "Unknown leave display silently guessed");

            var rows = new List<string[]>();
            foreach (var day in parsed.Days)
                rows.Add(new[] { day.Date, WorkHoursCalculator.Hours(day.Total), WorkHoursCalculator.Hours(day.Pto), WorkHoursCalculator.Hours(day.Sick), WorkHoursCalculator.Hours(day.Holiday) });
            var table = new ChromeRawTable { Headers = new[] { "Date", "Paid total", "PTO", "Sick", "Holiday" }, Rows = rows.ToArray() };
            var grid = ChromeDailyParser.Parse(new List<ChromeRawTable> { table }, month, 2536);
            Check(grid.Verified && grid.Days[1].Total == 443, "Explicit daily table failed: " + grid.Warning);

            var data = new MonthData { PayMinutes = 2536, BrowserImported = true, ChromeSnapshot = true, ChromeDailyVerified = true, ChromeDays = parsed.Days };
            var result = WorkHoursCalculator.Calculate(month, month, data);
            Check(result.Balance == 117 && result.Future == 1939, "Dated ledger did not reconstruct the earlier day correctly");
        }

        public static void TestNativeProtocol()
        {
            string folder = Path.Combine(Environment.CurrentDirectory, "chrome-protocol-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                DateTime month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var request = new ChromeCaptureRequest { Command = "capture", CompanyUrl = "https://example.bamboohr.com", PageUrl = "https://example.bamboohr.com/employees/timesheet", SummaryText = "100h Pay Period " + month.ToString("MMM", CultureInfo.InvariantCulture) + " 1-" + DateTime.DaysInMonth(month.Year, month.Month) + ", " + month.Year };
                var serializer = new JavaScriptSerializer();
                byte[] body = Encoding.UTF8.GetBytes(serializer.Serialize(request));
                using (var input = new MemoryStream())
                using (var output = new MemoryStream())
                {
                    input.Write(BitConverter.GetBytes(body.Length), 0, 4);
                    input.Write(body, 0, body.Length);
                    input.Position = 0;
                    Check(ChromeNativeHost.Run(input, output, folder) == 0, "Native capture failed");
                    byte[] response = output.ToArray();
                    Check(BitConverter.ToInt32(response, 0) == response.Length - 4, "Native UTF-8 frame length invalid");
                    var reply = serializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(response, 4, response.Length - 4));
                    Check(Convert.ToBoolean(reply["ok"]), "Native success response absent");
                }
                var captured = ChromeNativeHost.ReadSnapshot(folder);
                Check(captured.PayMinutes == 6000 && captured.CompanyUrl == "https://example.bamboohr.com/" && !captured.DailyVerified, "Snapshot content invalid");
                Check(!File.ReadAllText(ChromeNativeHost.InboxPath(folder,captured.Month)).Contains("SummaryText"), "Raw page text persisted");
                request.PageUrl = "https://foreign.example/";
                bool rejected = false;
                try { ChromeNativeHost.Capture(request, DateTime.UtcNow); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Foreign capture page accepted");
                foreach (byte[] invalid in new[] { new byte[0], BitConverter.GetBytes(1024 * 1024 + 1), new byte[] { 4, 0, 0, 0, 123 } })
                using (var input = new MemoryStream(invalid))
                using (var output = new MemoryStream())
                    Check(ChromeNativeHost.Run(input, output, folder) == 1 && output.Length > 4, "Malformed native frame was not rejected");
            }
            finally { foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.Delete(file); Directory.Delete(Path.Combine(folder, "Chrome")); Directory.Delete(folder); }
        }

        public static void TestApply()
        {
            var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            var daily = ChromeDayTextParser.Parse(DailyFixture(), new DateTime(2026, 10, 1), 2536);
            var snapshot = new ChromeSnapshot { Month = "2026-10", CompanyUrl = "https://example.bamboohr.com/", CapturedAtUtc = now, PayMinutes = 2536, Days = daily.Days, DailyVerified = true };
            var store = new Store();
            Check(!ChromeSync.Apply(store, snapshot, now), "Disabled receiving imported data");
            store.ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" };
            Check(ChromeSync.Apply(store, snapshot, now), "Enabled receiving did not import data");
            var data = store.Months["2026-10"];
            Check(data.BrowserFuture == 1020 && data.ChromeDays.Count == 31, "Verified dates did not replace manual future credits");
            Check(!ChromeSync.Apply(store, snapshot, now), "Duplicate snapshot reapplied");
            Check(!ChromeSync.Apply(store, snapshot, now.AddDays(1)), "Yesterday's snapshot was treated as current");
            snapshot.CapturedAtUtc = now.AddSeconds(1);
            snapshot.CompanyUrl = "https://other.bamboohr.com/";
            Check(!ChromeSync.Apply(store, snapshot, now), "Another company's data imported");
            snapshot.CompanyUrl = "https://example.bamboohr.com/";
            snapshot.DailyVerified = false;
            snapshot.Days = null;
            Check(ChromeSync.Apply(store, snapshot, now) && data.BrowserFuture == null, "Summary-only update retained an old aggregate confirmation");
            data.ChromeUseCalendar = true;
            data.Leave["2026-10-26"] = 300;
            ChromeSync.RefreshCalendar(data, new DateTime(2026, 10, 1));
            Check(data.BrowserFuture == 300, "Reviewed-calendar fallback failed");
        }
    }
}
