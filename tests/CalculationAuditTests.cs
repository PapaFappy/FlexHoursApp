using System;
using System.Globalization;
using System.Threading;

namespace FlexHours
{
    public static class CalculationAuditTests
    {
        private static void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
        }
        private static void Reject(Action action, string description)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new Exception(description);
        }

        public static void Run()
        {
            // Independent daily oracle: 21 years, every month and comparison day.
            for (int year = 2020; year <= 2040; year++)
                for (int number = 1; number <= 12; number++)
                {
                    var month = new DateTime(year, number, 1);
                    int target = 0, expected = 0;
                    int days = DateTime.DaysInMonth(year, number);
                    for (int day = 1; day <= days; day++)
                    {
                        int weekday = (int)new DateTime(year, number, day).DayOfWeek;
                        if (weekday >= 1 && weekday <= 5) target += 480;
                    }
                    for (int day = 1; day <= days; day++)
                    {
                        var date = new DateTime(year, number, day);
                        int weekday = (int)date.DayOfWeek;
                        if (weekday >= 1 && weekday <= 5) expected += 480;
                        var result = WorkHoursCalculator.Calculate(month, date, new MonthData());
                        Check(result.Target == target && result.Expected == expected, "Weekday oracle failed on " + date.ToString("yyyy-MM-dd"));
                    }
                }

            string[] governmentDates = { "01-01", "02-10", "03-19", "03-31", "04-03", "05-01", "06-07", "06-29", "08-15", "09-08", "09-21", "12-08", "12-13", "12-25" };
            var holidays = WorkHoursCalculator.Holidays(2026);
            Check(holidays.Count == governmentDates.Length, "2026 government holiday count");
            foreach (string value in governmentDates)
                Check(holidays.ContainsKey(DateTime.ParseExact("2026-" + value, "yyyy-MM-dd", CultureInfo.InvariantCulture)), "2026 government holiday " + value);

            var october = new DateTime(2026, 10, 1);
            var cutoff = new DateTime(2026, 10, 22);
            FutureCreditValidator.Validate(october, cutoff, 8014, 300);
            Reject(() => FutureCreditValidator.Validate(october, cutoff, 8014, -1), "Negative future credit accepted");
            Reject(() => FutureCreditValidator.Validate(october, cutoff, 8014, 0.5m), "Fractional future minute accepted");
            Reject(() => FutureCreditValidator.Validate(october, cutoff, 100, 101), "Credit exceeding total accepted");
            Reject(() => FutureCreditValidator.Validate(october, new DateTime(2026, 10, 30), 10000, 1), "Credit after last weekday accepted");
            Check(FutureCreditValidator.Maximum(october, cutoff, 8014) == 6 * 480, "Remaining weekdays credit bound");

            var state = new Store();
            var random = new Random(37);
            decimal previous = 0;
            for (int i = 0; i < 144; i++)
            {
                var month = new DateTime(2020, 1, 1).AddMonths(i);
                decimal difference = random.Next(-600, 601);
                var item = new MonthData { HasRecordedHours = true, FlexComplete = true };
                item.PayMinutes = WorkHoursCalculator.Calculate(month, month.AddMonths(1).AddDays(-1), item).Target + difference;
                state.Months[month.ToString("yyyy-MM", CultureInfo.InvariantCulture)] = item;
                decimal carry = month.Month == 1 || month.Month == 4 || month.Month == 7 || month.Month == 10 ? 0 : Math.Max(-240, Math.Min(240, previous));
                var evaluated = FlexLedger.Evaluate(month, state);
                Check(evaluated.Carry == carry && evaluated.Final == difference + carry, "Independent ledger oracle " + month);
                previous = difference + carry;
            }

            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
                Check(WorkHoursCalculator.Key(cutoff) == "2026-10-22", "Saved day keys must use Gregorian dates");
                Check(FlexLedger.Evaluate(new DateTime(2020, 1, 1), state).HasHours, "Ledger lookup depends on Windows calendar");
            }
            finally { Thread.CurrentThread.CurrentCulture = original; }
        }
    }
}
