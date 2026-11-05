using System;

namespace FlexHours
{
    public class ChromeSyncSettings
    {
        public bool Enabled { get; set; }
        public string CompanyUrl { get; set; }
    }

    public class ChromeSnapshot
    {
        public string Month { get; set; }
        public string CompanyUrl { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public decimal PayMinutes { get; set; }
        public decimal? TimeOffMinutes { get; set; }
        public System.Collections.Generic.List<ChromeDay> Days { get; set; }
        public bool DailyVerified { get; set; }
        public bool FutureVerified { get; set; }
        public string DailyWarning { get; set; }
    }

    public class ChromeCaptureRequest
    {
        public string Command { get; set; }
        public string CompanyUrl { get; set; }
        public string PageUrl { get; set; }
        public string SummaryText { get; set; }
        public System.Collections.Generic.List<ChromeRawTable> Tables { get; set; }
        public System.Collections.Generic.List<ChromeRawDayCard> Cards { get; set; }
        public string Month { get; set; }
    }
}
