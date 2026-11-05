using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlexHours
{
    public class ChromeRawDayCard
    {
        public string DateLabel { get; set; }
        public string Text { get; set; }
    }

    public static class ChromeDayTextParser
    {
        private static readonly Regex DateLine = new Regex(@"^(?:(?:Mon(?:day)?|Tue(?:sday)?|Wed(?:nesday)?|Thu(?:rsday)?|Fri(?:day)?|Sat(?:urday)?|Sun(?:day)?)[,\s]+)?(?<mon>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s*(?<day>\d{1,2})(?:,?\s+(?<year>\d{4}))?$", RegexOptions.IgnoreCase);
        private static readonly Regex WeekdayLine = new Regex(@"^(Mon(day)?|Tue(sday)?|Wed(nesday)?|Thu(rsday)?|Fri(day)?|Sat(urday)?|Sun(day)?)$", RegexOptions.IgnoreCase);
        private static readonly Regex WorkedLine = new Regex(@"^(?<h>\d{1,2})\s*h(?:\s*(?<m>\d{1,2})\s*m)?$", RegexOptions.IgnoreCase);
        private static readonly Regex LeaveLine = new Regex(@"^(?<hours>\d{1,2}(?:\.\d+)?)\s*(?:hours?|hrs?)\s+(?<name>.+)$", RegexOptions.IgnoreCase);

        private static string[] Lines(string text)
        {
            text = Regex.Replace(text ?? "", @"(?im)^(\d{1,2}\s*h)\s*\r?\n\s*(\d{1,2}\s*m)\s*$", "$1 $2");
            return text.Replace("\r", "").Split('\n').Select(line => Regex.Replace(line, @"\s+", " ").Trim()).Where(line => line.Length != 0).ToArray();
        }

        private static bool DateOf(string label, DateTime month, out DateTime date)
        {
            date = default(DateTime);
            var match = DateLine.Match(label ?? "");
            if (!match.Success) return false;
            int number = DateTime.ParseExact(match.Groups["mon"].Value.Substring(0, 3), "MMM", CultureInfo.InvariantCulture).Month;
            int year = match.Groups["year"].Success ? int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture) : month.Year;
            int day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
            if (day < 1 || day > DateTime.DaysInMonth(year, number)) throw new FormatException("Invalid date in the daily list.");
            date = new DateTime(year, number, day);
            return true;
        }

        private static ChromeDay ReadDay(DateTime date, string[] lines)
        {
            var day = new ChromeDay { Date = WorkHoursCalculator.Key(date) };
            var workedValues = new List<decimal>();
            foreach (string line in lines)
                if (Regex.IsMatch(line, @"\bpending\b|\brequested\b|\bnot approved\b", RegexOptions.IgnoreCase))
                    throw new FormatException(day.Date + ": leave is not confirmed as credited.");
            for (int j = 0; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j], @"^(Paid Hours|Pay Period|This Week|Summary|Submit Timesheet)$", RegexOptions.IgnoreCase)) break;
                var worked = WorkedLine.Match(lines[j]);
                if (worked.Success)
                {
                    if (j + 1 < lines.Length && Regex.IsMatch(lines[j + 1], @"^(Pay Period|REG|Time Off|Today|This Week)$", RegexOptions.IgnoreCase)) break;
                    int minutes = worked.Groups["m"].Success ? int.Parse(worked.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
                    if (minutes > 59) throw new FormatException(day.Date + ": invalid worked minutes.");
                    workedValues.Add(int.Parse(worked.Groups["h"].Value, CultureInfo.InvariantCulture) * 60 + minutes);
                    continue;
                }
                string leaveText = lines[j];
                if (Regex.IsMatch(leaveText, @"^\d{1,2}(?:\.\d+)?\s*(hours?|hrs?)$", RegexOptions.IgnoreCase) && j + 1 < lines.Length)
                    leaveText += " " + lines[++j];
                var leave = LeaveLine.Match(leaveText);
                if (!leave.Success)
                {
                    if (Regex.IsMatch(leaveText, @"\b(sick|pto|leave|holiday|vacation)\b", RegexOptions.IgnoreCase))
                        throw new FormatException(day.Date + ": unsupported leave/holiday allocation format.");
                    continue;
                }
                string name = leave.Groups["name"].Value.ToLowerInvariant();
                decimal amount = decimal.Parse(leave.Groups["hours"].Value, CultureInfo.InvariantCulture) * 60;
                if (amount != Math.Truncate(amount)) throw new FormatException(day.Date + ": leave must be whole minutes.");
                if (name.Contains("unpaid")) day.Unpaid += amount;
                else if (name.Contains("sick")) day.Sick += amount;
                else if (name.Contains("holiday")) day.Holiday += amount;
                else if (Regex.IsMatch(name, @"\bpto\b|\bannual\b|\bvacation\b")) day.Pto += amount;
                else if (name.Contains("leave") || name.Contains("time off")) day.OtherPaidLeave += amount;
                else throw new FormatException(day.Date + ": unknown allocation type.");
            }
            var unique = workedValues.Distinct().ToList();
            if (unique.Count == 1) day.Worked = unique[0]; // Repeated display of the same daily total counts once.
            else if (unique.Count > 1)
            {
                decimal sum = workedValues.Sum();
                var aggregate = workedValues.Where(value => value * 2 == sum).Distinct().ToList();
                if (aggregate.Count != 1) throw new FormatException(day.Date + ": multiple ambiguous worked totals; expand this day and refresh.");
                day.Worked = aggregate[0];
            }
            else if (lines.Any(line => Regex.IsMatch(line, @"\b\d{1,2}:\d{2}\s*(AM|PM)\b", RegexOptions.IgnoreCase)))
                throw new FormatException(day.Date + ": clock entries found without a readable worked total.");
            // A visibly empty day or leave-only day has zero worked hours. Monthly reconciliation still must pass.
            day.Total = day.Worked + day.Pto + day.Sick + day.Holiday + day.OtherPaidLeave;
            ChromeDailyValidation.ValidatePartial(new List<ChromeDay> { day }, date);
            return day;
        }

        public static ChromeDailyResult ParseCards(List<ChromeRawDayCard> cards, DateTime month, decimal payTotal)
        {
            var result = new ChromeDailyResult();
            var found = new Dictionary<string, ChromeDay>();
            var errors = new List<string>();
            var conflicting = new HashSet<string>();
            try
            {
                foreach (var card in cards ?? new List<ChromeRawDayCard>())
                {
                    try
                    {
                    DateTime date;
                    if (card == null || !DateOf(card.DateLabel, month, out date) || date.Year != month.Year || date.Month != month.Month) continue;
                    var day = ReadDay(date, Lines(card.Text));
                    if(conflicting.Contains(day.Date))continue;
                    ChromeDay previous;
                    if (found.TryGetValue(day.Date, out previous))
                    {
                        if (previous.Total != day.Total || previous.Worked != day.Worked || previous.Sick != day.Sick || previous.Pto != day.Pto
                            || previous.Holiday != day.Holiday || previous.OtherPaidLeave != day.OtherPaidLeave || previous.Unpaid != day.Unpaid)
                        {
                            found.Remove(day.Date);
                            conflicting.Add(day.Date);
                            throw new FormatException(day.Date + ": conflicting daily cards.");
                        }
                        continue;
                    }
                    found[day.Date] = day;
                    result.Days = found.Values.OrderBy(value => value.Date, StringComparer.Ordinal).ToList();
                    }
                    catch(Exception error)
                    {
                        if (!(error is FormatException) && !(error is System.IO.InvalidDataException) && !(error is ArgumentOutOfRangeException)) throw;
                        errors.Add(error.Message);
                    }
                }
                result.Days = found.Values.OrderBy(value => value.Date, StringComparer.Ordinal).ToList();
                if(errors.Count>0){result.Warning=string.Join(" ",errors.Take(3));return result;}
                ChromeDailyValidation.Validate(result.Days, month, payTotal);
                result.Verified = true;
            }
            catch (Exception error)
            {
                if (!(error is FormatException) && !(error is System.IO.InvalidDataException) && !(error is ArgumentOutOfRangeException)) throw;
                result.Warning = error.Message;
            }
            return result;
        }

        public static ChromeDailyResult Parse(string text, DateTime month, decimal payTotal)
        {
            string[] lines = Lines(text);
            var markers = new List<Tuple<int, int, string>>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!DateLine.IsMatch(lines[i])) continue;
                int start = i;
                if (i >= 1 && WeekdayLine.IsMatch(lines[i - 1])) start = i - 1;
                if (i >= 2 && WeekdayLine.IsMatch(lines[i - 2]) && WorkedLine.IsMatch(lines[i - 1])) start = i - 2;
                markers.Add(Tuple.Create(i, start, lines[i]));
            }
            var cards = new List<ChromeRawDayCard>();
            for (int i = 0; i < markers.Count; i++)
            {
                int end = i + 1 < markers.Count ? markers[i + 1].Item2 : lines.Length;
                cards.Add(new ChromeRawDayCard { DateLabel = markers[i].Item3, Text = string.Join("\n", lines.Skip(markers[i].Item2).Take(end - markers[i].Item2)) });
            }
            return ParseCards(cards, month, payTotal);
        }
    }
}
