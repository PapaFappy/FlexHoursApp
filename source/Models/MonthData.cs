using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FlexHours {
public class MonthData {
 public decimal PayMinutes { get; set; }
 public Dictionary<string,decimal> Leave { get; set; }
 public Dictionary<string,bool> HolidayIncluded { get; set; }
 public bool BrowserImported { get; set; }
 public DateTime BrowserAsOf { get; set; }
 public decimal? BrowserFuture { get; set; }
 public DateTime BrowserImportedAt { get; set; }
 public string BrowserSource { get; set; }
 public bool ChromeSnapshot { get; set; }
 public bool ChromeUseCalendar { get; set; }
 public DateTime? ChromeCapturedAtUtc { get; set; }
 public System.Collections.Generic.List<ChromeDay> ChromeDays { get; set; }
 public bool ChromeDailyVerified { get; set; }
 public bool ChromeFutureVerified { get; set; }
 public string ChromeDailyWarning { get; set; }
 public bool? HasRecordedHours {get;set;}
 public bool FlexComplete {get;set;}
 public decimal? MonthlyDifference {get;set;}
 public decimal? PreviousCarryOver {get;set;}
 public decimal? FinalFlexBalance {get;set;}
 public bool QuarterlyReset {get;set;}
 public string FlexCalculationStatus {get;set;}
 public MonthData() { Leave=new Dictionary<string,decimal>(); HolidayIncluded=new Dictionary<string,bool>(); }
}
}
