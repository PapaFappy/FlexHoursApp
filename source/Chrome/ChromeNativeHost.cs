using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FlexHours
{
    /// <summary>Short-lived Chrome native messaging host. It never writes months.json.</summary>
    public static class ChromeNativeHost
    {
        private const int MaximumMessageBytes = 1024 * 1024;
        public static string InboxPath(string dataDirectory, string month) { return Path.Combine(dataDirectory, "Chrome", "latest-" + month + ".json"); }

        private static byte[] ReadExactly(Stream input, int count)
        {
            var bytes = new byte[count];
            int read = 0;
            while (read < count)
            {
                int part = input.Read(bytes, read, count - read);
                if (part == 0) throw new EndOfStreamException("Incomplete Chrome message.");
                read += part;
            }
            return bytes;
        }

        public static ChromeSnapshot Capture(ChromeCaptureRequest request, DateTime nowUtc)
        {
            if (request == null || request.Command != "capture") throw new ArgumentException("Unknown Chrome request.");
            string company = BrowserReader.CompanyUrl(request.CompanyUrl);
            Uri page;
            if (!Uri.TryCreate(request.PageUrl, UriKind.Absolute, out page) || page.Scheme != "https"
                || page.Host != new Uri(company).Host || !page.IsDefaultPort || !string.IsNullOrEmpty(page.UserInfo))
                throw new ArgumentException("The captured page must belong to the configured BambooHR company.");
            DateTime local = nowUtc.ToLocalTime();
            var month = new DateTime(local.Year, local.Month, 1);
            if (!string.IsNullOrWhiteSpace(request.Month) && (!DateTime.TryParseExact(request.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
                || month.Year < 1753 || month.Year > 9998)) throw new ArgumentException("Invalid selected timesheet month.");
            var summary = SummaryParser.Parse(request.SummaryText, month, local.Date);
            var daily = ChromeDailyParser.Parse(request.Tables, month, summary.Total);
            if (!daily.Verified) daily = ChromeDayTextParser.Parse(request.SummaryText, month, summary.Total);
            if (request.Cards != null && request.Cards.Count > 0)
            {
                var cards = ChromeDayTextParser.ParseCards(request.Cards, month, summary.Total);
                if (cards.Verified || !daily.Verified) daily = cards;
            }
            bool futureVerified = false;
            try { ChromeDailyValidation.ValidateFuture(daily.Days, month, local.Date, summary.Total); futureVerified = true; }
            catch (InvalidDataException) { }
            return new ChromeSnapshot
            {
                Month = month.ToString("yyyy-MM", CultureInfo.InvariantCulture), CompanyUrl = company,
                CapturedAtUtc = nowUtc, PayMinutes = summary.Total,
                TimeOffMinutes = summary.HasTimeOff ? (decimal?)summary.TimeOff : null,
                Days = daily.Days, DailyVerified = daily.Verified, FutureVerified = futureVerified, DailyWarning = daily.Warning
            };
        }

        public static void Validate(ChromeSnapshot snapshot, DateTime nowUtc)
        {
            DateTime month;
            if (snapshot == null || !DateTime.TryParseExact(snapshot.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
                || snapshot.PayMinutes < 0 || snapshot.PayMinutes > 60059 || snapshot.PayMinutes != Math.Truncate(snapshot.PayMinutes))
                throw new InvalidDataException("Invalid Chrome snapshot.");
            if (snapshot.CapturedAtUtc.Kind != DateTimeKind.Utc) snapshot.CapturedAtUtc = snapshot.CapturedAtUtc.ToUniversalTime();
            if (snapshot.CapturedAtUtc > nowUtc.AddMinutes(2) || month.Year < 1753 || month.Year > 9998)
                throw new InvalidDataException("Invalid Chrome snapshot date.");
            if (snapshot.TimeOffMinutes.HasValue && (snapshot.TimeOffMinutes < 0 || snapshot.TimeOffMinutes > snapshot.PayMinutes
                || snapshot.TimeOffMinutes != Math.Truncate(snapshot.TimeOffMinutes.Value)))
                throw new InvalidDataException("Invalid Chrome time-off subtotal.");
            snapshot.CompanyUrl = BrowserReader.CompanyUrl(snapshot.CompanyUrl);
            if (snapshot.DailyVerified) ChromeDailyValidation.Validate(snapshot.Days, month, snapshot.PayMinutes);
            else if (snapshot.Days != null) ChromeDailyValidation.ValidatePartial(snapshot.Days, month);
            if (snapshot.FutureVerified) ChromeDailyValidation.ValidateFuture(snapshot.Days, month, snapshot.CapturedAtUtc.ToLocalTime().Date, snapshot.PayMinutes);
        }

        public static void WriteSnapshot(string dataDirectory, ChromeSnapshot snapshot)
        {
            Validate(snapshot, DateTime.UtcNow);
            string path = InboxPath(dataDirectory, snapshot.Month);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(snapshot), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public static ChromeSnapshot ReadSnapshot(string dataDirectory)
        {
            var snapshots = ReadAllSnapshots(dataDirectory);
            return System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OrderByDescending(snapshots, item => item.CapturedAtUtc));
        }

        public static System.Collections.Generic.List<ChromeSnapshot> ReadAllSnapshots(string dataDirectory)
        {
            var errors = new System.Collections.Generic.List<string>();
            var snapshots = ReadAllSnapshots(dataDirectory, errors);
            if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
            return snapshots;
        }

        public static System.Collections.Generic.List<ChromeSnapshot> ReadAllSnapshots(string dataDirectory, System.Collections.Generic.List<string> errors)
        {
            string directory = Path.Combine(dataDirectory, "Chrome");
            var result = new System.Collections.Generic.List<ChromeSnapshot>();
            if (!Directory.Exists(directory)) return result;
            foreach (string path in Directory.GetFiles(directory, "latest*.json"))
            {
                try { result.Add(ReadSnapshotFile(path)); }
                catch (Exception error)
                {
                    if (!(error is IOException) && !(error is UnauthorizedAccessException) && !(error is ArgumentException)
                        && !(error is InvalidOperationException) && !(error is FormatException)) throw;
                    errors.Add(Path.GetFileName(path) + ": " + error.Message);
                }
            }
            return result;
        }

        private static ChromeSnapshot ReadSnapshotFile(string path)
        {
            if (new FileInfo(path).Length > 64000) throw new InvalidDataException("Chrome snapshot is too large.");
            var snapshot = new JavaScriptSerializer().Deserialize<ChromeSnapshot>(File.ReadAllText(path));
            Validate(snapshot, DateTime.UtcNow);
            return snapshot;
        }

        public static int Run(Stream input, Stream output, string dataDirectory)
        {
            object response;
            int result = 0;
            try
            {
                byte[] header = ReadExactly(input, 4);
                int count = BitConverter.ToInt32(header, 0);
                if (count < 1 || count > MaximumMessageBytes) throw new InvalidDataException("Chrome message is too large.");
                string json = new UTF8Encoding(false, true).GetString(ReadExactly(input, count));
                var request = new JavaScriptSerializer().Deserialize<ChromeCaptureRequest>(json);
                var snapshot = Capture(request, DateTime.UtcNow);
                WriteSnapshot(dataDirectory, snapshot);
                response = new { ok = true, total = WorkHoursCalculator.Hours(snapshot.PayMinutes), month = snapshot.Month,
                    dailyVerified = snapshot.DailyVerified, futureVerified = snapshot.FutureVerified, dailyWarning = snapshot.DailyWarning };
            }
            catch (Exception error) { result = 1; response = new { ok = false, error = error.Message }; }
            byte[] body = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(response));
            output.Write(BitConverter.GetBytes(body.Length), 0, 4);
            output.Write(body, 0, body.Length);
            output.Flush();
            return result;
        }
    }

    internal static class ChromeNativeHostProgram
    {
        private static int Main()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexHours");
            return ChromeNativeHost.Run(Console.OpenStandardInput(), Console.OpenStandardOutput(), directory);
        }
    }
}
