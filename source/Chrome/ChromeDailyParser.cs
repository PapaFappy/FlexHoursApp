using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlexHours
{
    public class ChromeRawTable
    {
        public string[] Headers { get; set; }
        public string[][] Rows { get; set; }
    }

    public class ChromeDay
    {
        public string Date { get; set; }
        public decimal Worked { get; set; }
        public decimal Pto { get; set; }
        public decimal Sick { get; set; }
        public decimal Holiday { get; set; }
        public decimal OtherPaidLeave { get; set; }
        public decimal Unpaid { get; set; }
        public decimal Total { get; set; }
    }

    public class ChromeDailyResult
    {
        public List<ChromeDay> Days = new List<ChromeDay>();
        public bool Verified;
        public string Warning;
    }

    /// <summary>Accepts an explicit date/paid-total table only after full-month reconciliation.</summary>
    public static class ChromeDailyParser
    {
        private static string Clean(string text) { return Regex.Replace(text ?? "", @"\s+", " ").Trim(); }
        private static string Cell(string[] row, int index) { return index < row.Length ? Clean(row[index]) : ""; }

        private static bool DateOf(string text, DateTime month, out DateTime result)
        {
            result = default(DateTime);
            string value = Clean(text);
            DateTime parsed;
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$"))
            {
                if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return false;
                result = parsed;
                return parsed.Year == month.Year && parsed.Month == month.Month;
            }
            string withoutWeekday = Regex.Replace(value, @"^(?:Mon(?:day)?|Tue(?:sday)?|Wed(?:nesday)?|Thu(?:rsday)?|Fri(?:day)?|Sat(?:urday)?|Sun(?:day)?)[,\s]+", "", RegexOptions.IgnoreCase);
            int day;
            if (int.TryParse(withoutWeekday, NumberStyles.None, CultureInfo.InvariantCulture, out day) && day >= 1 && day <= DateTime.DaysInMonth(month.Year, month.Month))
            {
                result = month.AddDays(day - 1);
                return true;
            }
            string[] formats = { "MMM d yyyy", "MMMM d yyyy", "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy" };
            if (!Regex.IsMatch(withoutWeekday, @"\b\d{4}\b")) withoutWeekday += " " + month.Year.ToString(CultureInfo.InvariantCulture);
            if (!DateTime.TryParseExact(withoutWeekday, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)) return false;
            result = parsed;
            return parsed.Year == month.Year && parsed.Month == month.Month;
        }

        private static decimal Minutes(string text)
        {
            text = Clean(text);
            if (text == "" || text == "-" || text == "\u2014" || text == "\u2013") return 0;
            var durations = Regex.Matches(text, @"(?<![\d.\-])(?<h>\d{1,3})\s*h(?:\s*(?<m>\d{1,2})\s*m)?", RegexOptions.IgnoreCase);
            if (durations.Count == 1)
            {
                int minute = durations[0].Groups["m"].Success ? int.Parse(durations[0].Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
                if (minute > 59 || text.Contains("-")) throw new FormatException("Invalid daily duration.");
                return int.Parse(durations[0].Groups["h"].Value, CultureInfo.InvariantCulture) * 60 + minute;
            }
            var colon = Regex.Match(text, @"^(?<h>\d{1,2}):(?<m>[0-5]\d)$");
            if (colon.Success) return int.Parse(colon.Groups["h"].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(colon.Groups["m"].Value, CultureInfo.InvariantCulture);
            decimal hours;
            if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out hours) && hours >= 0 && hours * 60 == Math.Truncate(hours * 60)) return hours * 60;
            throw new FormatException("Unsupported daily duration or mixed allocations.");
        }

        private static string Category(string header, string cell)
        {
            string value = (header + " " + cell).ToLowerInvariant();
            if (value.Contains("unpaid")) return "unpaid";
            if (Regex.IsMatch(value, @"\bsick\b")) return "sick";
            if (Regex.IsMatch(value, @"\bholiday\b")) return "holiday";
            if (Regex.IsMatch(value, @"\bpto\b|\bvacation\b|\bannual\b")) return "pto";
            return "other";
        }

        public static ChromeDailyResult Parse(List<ChromeRawTable> tables, DateTime month, decimal payTotal)
        {
            month = new DateTime(month.Year, month.Month, 1);
            var result = new ChromeDailyResult();
            try
            {
                var days = new Dictionary<string, ChromeDay>();
                foreach (var table in tables ?? new List<ChromeRawTable>())
                {
                    if (table == null || table.Headers == null || table.Rows == null) continue;
                    string[] headers = table.Headers.Select(Clean).ToArray();
                    int dateColumn = Array.FindIndex(headers, h => Regex.IsMatch(h, @"^(date|day)$", RegexOptions.IgnoreCase));
                    int totalColumn = Array.FindIndex(headers, h => Regex.IsMatch(h, @"^(total|total hours|daily total|paid total|paid hours)$", RegexOptions.IgnoreCase));
                    if (dateColumn < 0 || totalColumn < 0) continue;
                    foreach (var row in table.Rows)
                    {
                        if (row == null) continue;
                        DateTime date;
                        if (!DateOf(Cell(row, dateColumn), month, out date)) continue;
                        string rowText = string.Join(" ", row);
                        if (Regex.IsMatch(rowText, @"\bpending\b|\brequested\b|\bnot approved\b", RegexOptions.IgnoreCase))
                            throw new FormatException("Daily rows include unapproved leave.");
                        var day = new ChromeDay { Date = WorkHoursCalculator.Key(date), Total = Minutes(Cell(row, totalColumn)) };
                        bool hasAllocationColumn = false;
                        for (int i = 0; i < headers.Length; i++)
                        {
                            if (i == dateColumn || i == totalColumn) continue;
                            if (!Regex.IsMatch(headers[i], @"\bpto\b|\bsick\b|\bholiday\b|\bleave\b|\bvacation\b|\btime\s*off\b|\bunpaid\b", RegexOptions.IgnoreCase)) continue;
                            hasAllocationColumn = true;
                            string cell = Cell(row, i);
                            decimal minutes = Minutes(cell);
                            switch (Category(headers[i], cell))
                            {
                                case "pto": day.Pto += minutes; break;
                                case "sick": day.Sick += minutes; break;
                                case "holiday": day.Holiday += minutes; break;
                                case "unpaid": day.Unpaid += minutes; break;
                                default: day.OtherPaidLeave += minutes; break;
                            }
                        }
                        if (!hasAllocationColumn && Regex.IsMatch(rowText, @"\bpto\b|\bsick\b|\bholiday\b|\bleave\b|\bvacation\b", RegexOptions.IgnoreCase))
                            throw new FormatException("Leave appears outside supported allocation columns.");
                        day.Worked = day.Total - day.Pto - day.Sick - day.Holiday - day.OtherPaidLeave;
                        if (day.Worked < 0 || day.Total > 1440 || day.Unpaid > 1440) throw new FormatException("Daily paid allocations do not match the total.");
                        ChromeDay existing;
                        if (days.TryGetValue(day.Date, out existing))
                        {
                            if (existing.Total != day.Total || existing.Pto != day.Pto || existing.Sick != day.Sick || existing.Holiday != day.Holiday
                                || existing.OtherPaidLeave != day.OtherPaidLeave || existing.Unpaid != day.Unpaid)
                                throw new FormatException("More than one different allocation row was found for the same day.");
                        }
                        else days[day.Date] = day;
                    }
                }
                for (int i = 0; i < DateTime.DaysInMonth(month.Year, month.Month); i++)
                {
                    DateTime date = month.AddDays(i);
                    if (WorkHoursCalculator.Weekday(date) && !days.ContainsKey(WorkHoursCalculator.Key(date)))
                        throw new FormatException("The daily list does not include every weekday. Expand the full month before connecting.");
                }
                if (days.Values.Sum(day => day.Total) != payTotal)
                    throw new FormatException("Daily paid totals do not add up to the Pay Period total.");
                result.Days = days.Values.OrderBy(day => day.Date, StringComparer.Ordinal).ToList();
                result.Verified = true;
            }
            catch (FormatException error) { result.Warning = error.Message; }
            if (!result.Verified && string.IsNullOrEmpty(result.Warning)) result.Warning = "No supported full-month daily table was found.";
            return result;
        }

        public static decimal After(List<ChromeDay> days, DateTime asOf)
        {
            string cutoff = WorkHoursCalculator.Key(asOf);
            return (days ?? new List<ChromeDay>()).Where(day => string.CompareOrdinal(day.Date, cutoff) > 0).Sum(day => day.Total);
        }
    }
}
