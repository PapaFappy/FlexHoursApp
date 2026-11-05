using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlexHours
{
    public static class ChromeMonthViewTests
    {
        public static void Run()
        {
            var october = new DateTime(2026, 10, 1);
            string reordered = Regex.Replace(ChromeTests.DailyFixture(), @"(?m)^(Mon|Tue|Wed|Thu|Fri|Sat|Sun)\n(Oct \d{1,2})\n(\d+h \d+m)$", "$1\n$3\n$2");
            var parsed = ChromeDayTextParser.Parse(reordered, october, 2536);
            if (!parsed.Verified || parsed.Days.Single(day => day.Date == "2026-10-02").Sick != 300||parsed.Days.Single(day=>day.Date=="2026-10-05").Worked!=476)
                throw new Exception("Worked totals before date badges were attached to the wrong date: " + parsed.Warning);
            var empty = ChromeDayTextParser.Parse(ChromeTests.DailyFixture().Replace("\n0h 00m", ""), october, 2536);
            if (!empty.Verified) throw new Exception("Blank/leave-only rows failed monthly reconciliation: " + empty.Warning);
            var repeated = ChromeDayTextParser.Parse(ChromeTests.DailyFixture().Replace("2h 23m", "2h 23m\n2h 23m"), october, 2536);
            if (!repeated.Verified || repeated.Days[1].Worked != 143) throw new Exception("Repeated display double-counted a daily total");
            var partial = ChromeDayTextParser.Parse(ChromeTests.DailyFixture().Replace("5 hours Malta Sick Leave", "unsupported sick leave display"), october, 2536);
            if (partial.Verified || partial.Days.Count == 0 || partial.Days.Any(day => day.Date == "2026-10-02") || !partial.Days.Any(day => day.Date == "2026-10-26"))
                throw new Exception("Readable days were discarded or unread sick hours were guessed");

            var cards = new List<ChromeRawDayCard>();
            for (int i = 1; i <= 31; i++) cards.Add(new ChromeRawDayCard
            {
                DateLabel = "Dec " + i,
                Text = i == 14 ? "8 hours Malta Vacation\nMon\nDec 14" : new DateTime(2026, 12, i).ToString("ddd", CultureInfo.InvariantCulture) + "\nDec " + i
            });
            DateTime now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            var snapshot = ChromeNativeHost.Capture(new ChromeCaptureRequest
            {
                Command = "capture", CompanyUrl = "example", PageUrl = "https://example.bamboohr.com/timesheet",
                Month = "2026-12", SummaryText = "8h Pay Period Dec 1-31, 2026 Paid Hours 0h REG 8h Time Off", Cards = cards
            }, now);
            if (!snapshot.DailyVerified || snapshot.Month != "2026-12" || snapshot.Days.Single(day => day.Date == "2026-12-14").Pto != 480)
                throw new Exception("December vacation could not be captured in October");
            var store = new Store { ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" } };
            store.Months["2026-10"] = new MonthData { HasRecordedHours = true, PayMinutes = 8014 };
            if (!ChromeSync.Apply(store, snapshot, now) || store.Months["2026-10"].PayMinutes != 8014 || store.Months["2026-12"].PayMinutes != 480)
                throw new Exception("Future month's leave altered current-month hours");
            var fallbackStore = new Store { ChromeSync = new ChromeSyncSettings { Enabled = true, CompanyUrl = "example" } };
            fallbackStore.Months["2026-12"] = new MonthData { ChromeUseCalendar = true };
            var summaryOnly = new ChromeSnapshot { Month = "2026-12", CompanyUrl = "https://example.bamboohr.com/", CapturedAtUtc = now, PayMinutes = 960 };
            if (!ChromeSync.Apply(fallbackStore, summaryOnly, now) || fallbackStore.Months["2026-12"].BrowserFuture != 960)
                throw new Exception("Future-month calendar fallback used the capture month's holidays");
            string folder = Path.Combine(Environment.CurrentDirectory, "multi-month-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                ChromeNativeHost.WriteSnapshot(folder, snapshot);
                var current = new ChromeSnapshot { Month = "2026-10", CompanyUrl = "https://example.bamboohr.com/", CapturedAtUtc = now, PayMinutes = 8014 };
                ChromeNativeHost.WriteSnapshot(folder, current);
                if (ChromeNativeHost.ReadAllSnapshots(folder).Count != 2) throw new Exception("One month's snapshot overwrote another");
                string damaged = Path.Combine(folder, "Chrome", "latest-2026-11.json");
                File.WriteAllText(damaged, "{broken");
                var errors = new List<string>();
                var healthy = ChromeNativeHost.ReadAllSnapshots(folder, errors);
                if (healthy.Count != 2 || errors.Count != 1 || !errors[0].Contains("latest-2026-11.json"))
                    throw new Exception("One damaged month blocked healthy Chrome updates or was silently ignored");
                File.Delete(damaged);
                string stateFile = Path.Combine(folder, "months.json");
                StateFile.Save(stateFile, store);
                bool recovered;
                var restored = StateFile.Load(stateFile, out recovered);
                if (!restored.ChromeSync.Enabled || restored.Months["2026-12"].ChromeDays[13].Pto != 480)
                    throw new Exception("Connection settings or future vacation did not survive reopening");
            }
            finally
            {
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) File.Delete(path);
                Directory.Delete(Path.Combine(folder, "Chrome"));
                Directory.Delete(folder);
            }
        }
    }
}
