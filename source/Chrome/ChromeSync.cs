using System;
using System.Globalization;

namespace FlexHours
{
    public static class ChromeSync
    {
        public static bool Apply(Store store, ChromeSnapshot snapshot, DateTime nowUtc)
        {
            if (store.ChromeSync == null || !store.ChromeSync.Enabled || snapshot == null) return false;
            ChromeNativeHost.Validate(snapshot, nowUtc);
            if (BrowserReader.CompanyUrl(store.ChromeSync.CompanyUrl) != snapshot.CompanyUrl) return false;
            DateTime local = snapshot.CapturedAtUtc.ToLocalTime();
            DateTime today = nowUtc.ToLocalTime().Date;
            if (local.Date != today) return false; // Never revive an old day's balance on startup.
            MonthData month;
            if (!store.Months.TryGetValue(snapshot.Month, out month))
            {
                month = new MonthData();
                store.Months[snapshot.Month] = month;
            }
            if (month.ChromeCapturedAtUtc.HasValue && month.ChromeCapturedAtUtc.Value >= snapshot.CapturedAtUtc) return false;
            month.PayMinutes = snapshot.PayMinutes;
            month.HasRecordedHours = true;
            month.BrowserImported = true;
            month.ChromeSnapshot = true;
            month.ChromeCapturedAtUtc = snapshot.CapturedAtUtc;
            month.ChromeDays = snapshot.Days;
            month.ChromeDailyVerified = snapshot.DailyVerified;
            month.ChromeFutureVerified = snapshot.FutureVerified;
            month.ChromeDailyWarning = snapshot.DailyWarning;
            month.BrowserAsOf = local.Date;
            month.BrowserImportedAt = local;
            month.BrowserSource = "Chrome: " + snapshot.CompanyUrl;
            // Summary totals have no leave dates. Never carry an old aggregate confirmation forward.
            month.BrowserFuture = null;
            RefreshCalendar(month, DateTime.ParseExact(snapshot.Month, "yyyy-MM", CultureInfo.InvariantCulture));
            FlexLedger.RecalculateAll(store);
            return true;
        }

        public static void RefreshCalendar(MonthData data, DateTime month)
        {
            if (data.ChromeSnapshot && data.BrowserImported && (data.ChromeDailyVerified || data.ChromeFutureVerified))
            {
                data.BrowserFuture = ChromeDailyParser.After(data.ChromeDays, data.BrowserAsOf);
                return;
            }
            if (!data.ChromeSnapshot || !data.BrowserImported || !data.ChromeUseCalendar) return;
            var calendar = new MonthData { Leave = data.Leave, HolidayIncluded = data.HolidayIncluded };
            decimal credit = WorkHoursCalculator.Calculate(month, data.BrowserAsOf, calendar).Future;
            data.BrowserFuture = credit <= data.PayMinutes ? (decimal?)credit : null;
        }
    }
}
