using System;
using System.Collections.Generic;
using System.IO;

namespace FlexHours
{
    public static class RobustnessTests
    {
        private static void Reject(Action action, string description)
        {
            try { action(); }
            catch { return; }
            throw new Exception("Accepted invalid input: " + description);
        }

        public static void Run()
        {
            Reject(() => SummaryParser.Parse(null, new DateTime(2026, 10, 1)), "null clipboard");
            Reject(() => SummaryParser.Parse(new string(' ', 250001), new DateTime(2026, 10, 1)), "oversized whitespace clipboard");
            Reject(() => SummaryParser.Parse("-133h Pay Period Oct 1-31", new DateTime(2026, 10, 1)), "negative total");
            Reject(() => SummaryParser.Parse("1.5h Pay Period Oct 1-31", new DateTime(2026, 10, 1)), "unsupported decimal display");
            foreach (string url in new[] { "http://example.bamboohr.com", "https://example.bamboohr.com.evil.test", "https://user:password@example.bamboohr.com", "https://example.bamboohr.com:444", "javascript:alert(1)" })
                Reject(() => BrowserReader.CompanyUrl(url), "unsafe company address");

            var store = new Store();
            store.Months["2026-10"] = new MonthData { PayMinutes = -1 };
            Reject(() => StateFile.Validate(store), "negative pay minutes");
            store.Months["2026-10"].PayMinutes = 0.5m;
            Reject(() => StateFile.Validate(store), "fractional minute");
            store.Months["2026-10"].PayMinutes = 60;
            store.Months["2026-10"].Leave["2026-11-01"] = 60;
            Reject(() => StateFile.Validate(store), "leave in another month");
            store.Months["2026-10"].Leave.Clear();
            store.Months["2026-10"].BrowserFuture = 120;
            store.Months["2026-10"].BrowserImported = true;
            Reject(() => StateFile.Validate(store), "future credit greater than total");
            store.Months.Clear();
            store.Months["9999-12"] = new MonthData();
            Reject(() => StateFile.Validate(store), "unsupported picker date");
        }
    }
}
