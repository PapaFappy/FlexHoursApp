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
public static class FlexLedger {
 public static bool IsReset(DateTime month){return month.Month%3==1;}
 public static decimal Cap(decimal minutes){return Math.Max(-240,Math.Min(240,minutes));}
 public static bool HasHours(MonthData data){return data.HasRecordedHours??(data.PayMinutes!=0||data.BrowserImported);}
 public static FlexMonth Evaluate(DateTime month,Store store) {
  month=new DateTime(month.Year,month.Month,1);MonthData data;store.Months.TryGetValue(month.ToString("yyyy-MM",CultureInfo.InvariantCulture),out data);
  var result=new FlexMonth{Reset=IsReset(month),HasHours=data!=null&&HasHours(data),Complete=data!=null&&data.FlexComplete};
  if(result.HasHours)result.Difference=data.PayMinutes-WorkHoursCalculator.Calculate(month,month.AddMonths(1).AddDays(-1),data).Target;
  string reason="";
  if(result.Reset)result.Carry=0;
  else {
   var previous=Evaluate(month.AddMonths(-1),store);
   if(!previous.HasHours)reason="Previous month missing";
   else if(!previous.Complete)reason="Previous month not complete";
   else if(!previous.Final.HasValue)reason="Earlier carry unresolved";
   else result.Carry=Cap(previous.Final.Value);
  }
  if(result.Difference.HasValue&&result.Carry.HasValue)result.Final=result.Difference.Value+result.Carry.Value;
  result.Status=!result.HasHours?"No hours recorded":reason!=""?reason:result.Complete?"Complete":"Provisional - month in progress";
  if(result.Reset)result.Status+=" | Quarter reset";
  return result;
 }
 public static void RecalculateAll(Store store) {
  foreach(var entry in store.Months) {
   DateTime month;if(!DateTime.TryParseExact(entry.Key,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out month))continue;
   var calculated=Evaluate(month,store);entry.Value.MonthlyDifference=calculated.Difference;entry.Value.PreviousCarryOver=calculated.Carry;entry.Value.FinalFlexBalance=calculated.Final;entry.Value.QuarterlyReset=calculated.Reset;entry.Value.FlexCalculationStatus=calculated.Status;
  }
 }
 public static string Display(decimal? value){return value.HasValue?(value.Value>0?"+":"")+WorkHoursCalculator.Hours(value.Value):"Pending";}
 
}
}
