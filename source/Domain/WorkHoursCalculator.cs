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
public static class WorkHoursCalculator {
 public static bool Weekday(DateTime d) { return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday; }
 public static string Key(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
 public static Dictionary<DateTime,string> Holidays(int y) {
  var h = new Dictionary<DateTime,string>();
  int[] months={1,2,3,3,5,6,6,8,9,9,12,12,12};
  int[] days={1,10,19,31,1,7,29,15,8,21,8,13,25};
  string[] names={"New Year's Day","St Paul's Shipwreck","St Joseph","Freedom Day","Workers' Day","Sette Giugno","St Peter and St Paul","Assumption","Victory Day","Independence Day","Immaculate Conception","Republic Day","Christmas Day"};
  for(int i=0;i<days.Length;i++) h[new DateTime(y,months[i],days[i])]=names[i];
  int a=y%19,b=y/100,c=y%100,d=b/4,e=b%4,f=(b+8)/25,g=(b-f+1)/3;
  int j=(19*a+b-d-g+15)%30,k=c/4,l=c%4,m=(32+2*e+2*k-j-l)%7,n=(a+11*j+22*m)/451;
  int q=j+m-7*n+114;
  h[new DateTime(y,q/31,q%31+1).AddDays(-2)]="Good Friday";
  return h;
 }
 public static decimal Credit(DateTime day, MonthData data, Dictionary<DateTime,string> holidays) {
  if(!Weekday(day)) return 0;
  decimal leave; data.Leave.TryGetValue(Key(day),out leave);
  bool included; bool holiday=holidays.ContainsKey(day) && (!data.HolidayIncluded.TryGetValue(Key(day),out included)||included);
  return holiday ? 480 : Math.Max(0,Math.Min(480,leave));
 }
 public static WorkHoursResult Calculate(DateTime month,DateTime asof,MonthData data) {
  var r=new WorkHoursResult(); var holidays=Holidays(month.Year);
  for(int i=1;i<=DateTime.DaysInMonth(month.Year,month.Month);i++) {
   DateTime day=new DateTime(month.Year,month.Month,i);
   if(Weekday(day)) {r.Target+=480; if(day<=asof.Date) r.Expected+=480;}
   if(day>asof.Date) r.Future+=Credit(day,data,holidays);
  }
  if(data.BrowserImported&&data.BrowserAsOf.Date==asof.Date&&data.BrowserFuture.HasValue)r.Future=data.BrowserFuture.Value;
  if(data.BrowserImported&&data.ChromeSnapshot&&data.ChromeDailyVerified)r.Future=ChromeDailyParser.After(data.ChromeDays,asof);
  r.Balance=data.PayMinutes-r.Future-r.Expected;
  r.Remaining=r.Target-data.PayMinutes;
  return r;
 }
 public static string Hours(decimal minutes) { var v=(int)Math.Round(Math.Abs(minutes)); return (minutes<0?"-":"")+(v/60)+"h "+(v%60).ToString("00")+"m"; }
}
}
