using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace FlexHours
{
    /// <summary>Validates source data and atomically replaces the previous save.</summary>
    public static class StateFile
    {
        private const int MaximumJsonLength = 10000000;
        private const decimal MaximumPayMinutes = 60059;

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = MaximumJsonLength };
        }

        private static DateTime LocalDate(DateTime value)
        {
            return (value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value).Date;
        }

        private static bool SupportedDate(DateTime value)
        {
            return value.Year >= 1753 && value.Year <= 9998;
        }

        public static void Validate(Store state)
        {
            if (state == null || state.Months == null)
                throw new InvalidDataException("The saved file does not contain monthly data.");

            foreach (var entry in state.Months)
            {
                DateTime month;
                if (!DateTime.TryParseExact(entry.Key, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
                    || !SupportedDate(month) || entry.Value == null)
                    throw new InvalidDataException("The saved file contains an invalid month.");

                var data = entry.Value;
                if (data.BrowserImported && data.ChromeSnapshot && data.ChromeDailyVerified)
                    ChromeDailyValidation.Validate(data.ChromeDays, month, data.PayMinutes);
                else if (data.ChromeDays != null) ChromeDailyValidation.ValidatePartial(data.ChromeDays, month);
                if (data.ChromeSnapshot && data.ChromeFutureVerified)
                    ChromeDailyValidation.ValidateFuture(data.ChromeDays, month, LocalDate(data.BrowserAsOf), data.PayMinutes);
                if (data.PayMinutes < 0 || data.PayMinutes > MaximumPayMinutes || data.PayMinutes != Math.Truncate(data.PayMinutes))
                    throw new InvalidDataException("Hours must be nonnegative whole minutes within the supported range.");
                if (data.Leave == null) data.Leave = new Dictionary<string, decimal>();
                if (data.HolidayIncluded == null) data.HolidayIncluded = new Dictionary<string, bool>();

                foreach (var leave in data.Leave)
                {
                    ValidateDayKey(leave.Key, month);
                    if (leave.Value < 0 || leave.Value > 480 || leave.Value != Math.Truncate(leave.Value))
                        throw new InvalidDataException("Leave must be between 0 and 8 hours in whole minutes.");
                }
                foreach (string key in data.HolidayIncluded.Keys) ValidateDayKey(key, month);

                if (data.BrowserFuture.HasValue)
                {
                    decimal future = data.BrowserFuture.Value;
                    if (future < 0 || future > MaximumPayMinutes || future != Math.Truncate(future)
                        || (data.BrowserImported && future > data.PayMinutes))
                        throw new InvalidDataException("The saved future credits are invalid.");
                }
                data.BrowserAsOf = LocalDate(data.BrowserAsOf);
                if (data.BrowserImported && !SupportedDate(data.BrowserAsOf))
                    throw new InvalidDataException("The import comparison date is invalid.");
                if (data.BrowserImportedAt.Kind == DateTimeKind.Utc)
                    data.BrowserImportedAt = data.BrowserImportedAt.ToLocalTime();
            }

            if (state.View == null) state.View = new ViewState();
            if (state.View.SelectedMonth.HasValue)
            {
                state.View.SelectedMonth = LocalDate(state.View.SelectedMonth.Value);
                if (!SupportedDate(state.View.SelectedMonth.Value)) throw new InvalidDataException("The selected month is invalid.");
            }
            if (state.View.AsOfDate.HasValue)
            {
                state.View.AsOfDate = LocalDate(state.View.AsOfDate.Value);
                if (!SupportedDate(state.View.AsOfDate.Value)) throw new InvalidDataException("The comparison date is invalid.");
            }
            if (state.Appearance == null) state.Appearance = AppearanceSettings.Defaults(true);
            state.Appearance.Normalize();
            if (state.ChromeSync == null) state.ChromeSync = new ChromeSyncSettings();
            if (!string.IsNullOrWhiteSpace(state.ChromeSync.CompanyUrl))
                state.ChromeSync.CompanyUrl = BrowserReader.CompanyUrl(state.ChromeSync.CompanyUrl);
            if (state.ChromeSync.Enabled && string.IsNullOrWhiteSpace(state.ChromeSync.CompanyUrl))
                throw new InvalidDataException("A company address is required for Chrome sync.");
        }

        private static void ValidateDayKey(string key, DateTime month)
        {
            DateTime date;
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || date.Year != month.Year || date.Month != month.Month)
                throw new InvalidDataException("A saved leave or holiday date belongs to a different month.");
        }

        private static Store Read(string path)
        {
            if (new FileInfo(path).Length > MaximumJsonLength)
                throw new InvalidDataException("The saved file is too large.");
            var state = Serializer().Deserialize<Store>(File.ReadAllText(path));
            Validate(state);
            FlexLedger.RecalculateAll(state);
            return state;
        }

        public static Store Load(string path, out bool recovered)
        {
            recovered = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new Store();
            try { return Read(path); }
            catch (Exception original)
            {
                if (!File.Exists(path + ".bak"))
                    throw new InvalidDataException("Saved data could not be read. No valid backup was available. " + original.Message);
                Store backup;
                try { backup = Read(path + ".bak"); }
                catch { throw new InvalidDataException("Saved data and its backup could not be read. Both files have been preserved."); }

                if (File.Exists(path)) File.Copy(path, path + ".unreadable-" + Guid.NewGuid().ToString("N"));
                File.Copy(path + ".bak", path, true);
                recovered = true;
                return backup;
            }
        }

        public static void Save(string path, Store state)
        {
            Validate(state);
            FlexLedger.RecalculateAll(state);
            var serializer = Serializer();
            string json = serializer.Serialize(state);
            if (state.SavedAtUtc.HasValue && File.Exists(path) && File.ReadAllText(path) == json) return;

            // Do not overwrite an externally damaged file, or rotate it over a good backup.
            if (File.Exists(path)) Read(path);
            state.SavedAtUtc = DateTime.UtcNow;
            json = serializer.Serialize(state);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                output.Write(bytes, 0, bytes.Length);
                output.Flush(true);
            }

            if (File.Exists(path))
            {
                string backupTemporary = path + ".bak.tmp";
                File.Copy(path, backupTemporary, true);
                if (File.Exists(path + ".bak")) File.Replace(backupTemporary, path + ".bak", null);
                else File.Move(backupTemporary, path + ".bak");
                File.Replace(temporary, path, null);
            }
            else File.Move(temporary, path);
        }
    }
}
