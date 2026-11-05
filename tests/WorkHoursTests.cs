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
public static class WorkHoursTests {
static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
public static void Run() {
  var m=new MonthData{PayMinutes=128*60};var r=WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,22),m);
  Check(r.Target==176*60 && r.Expected==128*60 && r.Balance==0,"October weekday targets");
  m.Leave["2026-10-26"]=480;m.PayMinutes=136*60;r=WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,22),m);Check(r.Future==480 && r.Balance==0,"Future leave must not inflate balance");
  r=WorkHoursCalculator.Calculate(new DateTime(2026,11,1),new DateTime(2026,10,22),m);Check(r.Future==0 && r.Expected==0,"Do not include other months' leave");
  m=new MonthData();r=WorkHoursCalculator.Calculate(new DateTime(2026,12,1),new DateTime(2026,12,1),m);Check(r.Future==960,"Two future weekday holidays; Sunday excluded");
  m.Leave["2026-12-08"]=480;r=WorkHoursCalculator.Calculate(new DateTime(2026,12,1),new DateTime(2026,12,1),m);Check(r.Future==960,"No double holiday and leave");
  m.HolidayIncluded["2026-12-25"]=false;r=WorkHoursCalculator.Calculate(new DateTime(2026,12,1),new DateTime(2026,12,1),m);Check(r.Future==480,"Uncredited holiday excluded");
  Check(WorkHoursCalculator.Holidays(2026).ContainsKey(new DateTime(2026,4,3)),"Good Friday 2026");Check(WorkHoursCalculator.Holidays(2027).ContainsKey(new DateTime(2027,3,26)),"Good Friday 2027");
  m=new MonthData();m.Leave["2026-10-23"]=210;m.PayMinutes=128*60+210+90;r=WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,22),m);Check(r.Balance==90,"Partial leave and positive balance");
  r=WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,31),m);Check(r.Future==0 && r.Expected==r.Target,"Month end");
  Check(WorkHoursCalculator.Calculate(new DateTime(2028,2,1),new DateTime(2028,2,29),new MonthData()).Target==21*480,"Leap February");
  var json=new JavaScriptSerializer();var restored=json.Deserialize<MonthData>(json.Serialize(m));Check(restored.Leave["2026-10-23"]==210,"Persistence round trip");



  var imported=new MonthData{PayMinutes=8014,BrowserImported=true,BrowserFuture=300,BrowserAsOf=new DateTime(2026,10,22)};var importedResult=WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,22),imported);Check(importedResult.Balance==34&&importedResult.Remaining==2546,"Imported future credit reconciliation");
  imported.BrowserFuture=0;Check(WorkHoursCalculator.Calculate(new DateTime(2026,10,1),new DateTime(2026,10,22),imported).Balance==334,"Imported past credits retained");
  var importedRoundTrip=json.Deserialize<MonthData>(json.Serialize(imported));Check(importedRoundTrip.BrowserFuture==0&&importedRoundTrip.BrowserImported,"Browser mode saved");
 }
}
}
