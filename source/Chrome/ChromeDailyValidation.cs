using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FlexHours
{
    public static class ChromeDailyValidation
    {
        public static void ValidatePartial(List<ChromeDay> days, DateTime month)
        {
            if (days == null || days.Count > 31) throw new InvalidDataException("Invalid daily ledger.");
            var dates = new HashSet<string>();
            foreach (var day in days)
            {
                DateTime date;
                if (day == null || !DateTime.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                    || date.Year != month.Year || date.Month != month.Month || !dates.Add(day.Date))
                    throw new InvalidDataException("Invalid or duplicated daily date.");
                foreach (decimal value in new[] { day.Total, day.Worked, day.Pto, day.Sick, day.Holiday, day.OtherPaidLeave, day.Unpaid })
                    if (value < 0 || value > 1440 || value != Math.Truncate(value)) throw new InvalidDataException("Invalid daily allocation.");
                if (day.Total != day.Worked + day.Pto + day.Sick + day.Holiday + day.OtherPaidLeave)
                    throw new InvalidDataException("Daily paid allocations do not match their total.");
            }
        }

        public static void Validate(List<ChromeDay> days, DateTime month, decimal total)
        {
            ValidatePartial(days, month);
            var dates = new HashSet<string>(days.Select(day => day.Date));
            for (int i = 0; i < DateTime.DaysInMonth(month.Year, month.Month); i++)
                if (WorkHoursCalculator.Weekday(month.AddDays(i)) && !dates.Contains(WorkHoursCalculator.Key(month.AddDays(i))))
                    throw new InvalidDataException("The verified daily ledger is missing a weekday.");
            if (days.Sum(day => day.Total) != total) throw new InvalidDataException("Daily totals do not match the monthly total.");
        }

        public static void ValidateFuture(List<ChromeDay> days, DateTime month, DateTime asof, decimal total)
        {
            days = days ?? new List<ChromeDay>();
            ValidatePartial(days, month);
            var dates = new HashSet<string>(days.Select(day => day.Date));
            for (int i = 0; i < DateTime.DaysInMonth(month.Year, month.Month); i++)
            {
                DateTime date = month.AddDays(i);
                if (date > asof.Date && !dates.Contains(WorkHoursCalculator.Key(date)))
                    throw new InvalidDataException("A future date is missing from the timesheet reading.");
            }
            if (ChromeDailyParser.After(days, asof) > total)
                throw new InvalidDataException("Future dated hours exceed the Pay Period total.");
        }
    }
}
