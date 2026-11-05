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
public static class SummaryParser {
 const string Duration=@"(?<![\d.,+\-])(?<h>\d{1,4})\s*h(?:\s*(?<m>\d{1,2})\s*m)?";
 static decimal Amount(Match m) {int h=int.Parse(m.Groups["h"].Value);int min=m.Groups["m"].Success?int.Parse(m.Groups["m"].Value):0;if(min>59)throw new Exception("Unrecognized minutes in summary.");return h*60+min;}
 static Match Before(string text,int at) {return Regex.Match(text.Substring(Math.Max(0,at-45),Math.Min(at,45)),Duration+@"\s*$",RegexOptions.IgnoreCase);}
 public static BrowserSummary Parse(string text,DateTime month,DateTime? captureDate=null) {
  if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("Copy the timesheet summary first.");
  if(text.Length>250000)throw new ArgumentException("Copy only the summary; this text is too long.");
  text=Regex.Replace(text.Replace('\u00a0',' ').Replace('\u2013','-').Replace('\u2014','-').Replace('\u2212','-'),@"[\t\r\n ]+"," ").Trim();
  if(text.Length>250000)throw new Exception("Page text is too long. Copy just the timesheet summary.");
  var labels=Regex.Matches(text,@"\bPay\s*Period\b",RegexOptions.IgnoreCase);var found=new List<BrowserSummary>();
  foreach(Match label in labels) {
   var amount=Before(text,label.Index);int after=label.Index+label.Length;string tail=text.Substring(after,Math.Min(180,text.Length-after));
   if(!amount.Success)amount=Regex.Match(tail,@"^\s*"+Duration,RegexOptions.IgnoreCase);
   if(!amount.Success)continue;
   var period=Regex.Match(tail,@"\b(?<mon>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(?<first>\d{1,2})\s*[-–—]\s*(?:(?<endmon>[A-Za-z]+)\s+)?(?<last>\d{1,2})(?:\s*,?\s+(?<year>20\d{2}))?",RegexOptions.IgnoreCase);
   if(!period.Success)continue;
   DateTime mon;if(!DateTime.TryParseExact(period.Groups["mon"].Value.Substring(0,3),"MMM",CultureInfo.InvariantCulture,DateTimeStyles.None,out mon))continue;
   if(mon.Month!=month.Month||int.Parse(period.Groups["first"].Value)!=1||int.Parse(period.Groups["last"].Value)!=DateTime.DaysInMonth(month.Year,month.Month))continue;
   if(period.Groups["endmon"].Success&&!period.Groups["endmon"].Value.StartsWith(period.Groups["mon"].Value.Substring(0,3),StringComparison.OrdinalIgnoreCase))continue;
   if(period.Groups["year"].Success&&int.Parse(period.Groups["year"].Value)!=month.Year)continue;
   found.Add(new BrowserSummary{Total=Amount(amount),Period=period.Value});
  }
  if(found.Count==0)throw new Exception("Could not find a full-month Pay Period total matching "+month.ToString("MMMM yyyy")+". Open that month's Timesheet summary, or paste only its summary text.");
  if(found.Select(s=>s.Total).Distinct().Count()!=1)throw new Exception("More than one different Pay Period total was found. Copy only the summary you want.");
  var result=found[0];
  var regular=Regex.Matches(text,@"\bREG\b",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>Before(text,m.Index)).Where(m=>m.Success).Select(Amount).Distinct().ToList();
  var off=Regex.Matches(text,@"\bTime\s*Off\b",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>Before(text,m.Index)).Where(m=>m.Success).Select(Amount).Distinct().ToList();
  if(regular.Count>1||off.Count>1)throw new Exception("Conflicting regular or time-off totals were found. Copy only the selected month's summary.");
  if(regular.Count==1){result.HasRegular=true;result.Regular=regular[0];}if(off.Count==1){result.HasTimeOff=true;result.TimeOff=off[0];}
  if(result.HasRegular&&result.HasTimeOff&&result.Regular+result.TimeOff!=result.Total) {
   DateTime captured=(captureDate??DateTime.Today).Date;
   decimal difference=result.Total-result.Regular-result.TimeOff;
   var running=Regex.Matches(text,Duration+@"(?:\s+edit)?\s+Clocked\s+In\s*:\s*Today\b",RegexOptions.IgnoreCase).Cast<Match>().Select(Amount).Distinct().ToList();
   if(month.Year!=captured.Year||month.Month!=captured.Month||difference<=0||running.Count!=1||running[0]!=difference)
    throw new Exception("Regular hours plus time off do not match the Pay Period total or its visible active clock. Copy just that summary so it can be checked.");
  }
  if(result.Total>60000)throw new Exception("The summary total is outside the app's supported range.");return result;
 }
 
}
}
