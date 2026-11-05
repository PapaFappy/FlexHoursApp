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
public static class FlexLedgerTests {
static void Require(bool condition,string message){if(!condition)throw new Exception("Flex ledger: "+message);}
 static void Put(Store store,int year,int month,decimal difference,bool complete=true) {
  var date=new DateTime(year,month,1);var data=new MonthData{HasRecordedHours=true,FlexComplete=complete};data.PayMinutes=WorkHoursCalculator.Calculate(date,date.AddMonths(1).AddDays(-1),data).Target+difference;store.Months[date.ToString("yyyy-MM")]=data;
 }
 public static void Run() {
  foreach(decimal[] pair in new[]{new decimal[]{420,240},new decimal[]{180,180},new decimal[]{-120,-120},new decimal[]{-360,-240},new decimal[]{0,0},new decimal[]{240,240},new decimal[]{-240,-240},new decimal[]{241,240},new decimal[]{-241,-240}})Require(FlexLedger.Cap(pair[0])==pair[1],"signed carry cap");
  var store=new Store();Put(store,2026,10,360);Put(store,2026,11,-420);Put(store,2026,12,300);Put(store,2027,1,-120);
  var october=FlexLedger.Evaluate(new DateTime(2026,10,1),store);var november=FlexLedger.Evaluate(new DateTime(2026,11,1),store);var december=FlexLedger.Evaluate(new DateTime(2026,12,1),store);var january=FlexLedger.Evaluate(new DateTime(2027,1,1),store);
  Require(october.Carry==0&&october.Final==360,"October full surplus retained");Require(november.Carry==240&&november.Final==-180,"November example");Require(december.Carry==-180&&december.Final==120,"December example");Require(january.Carry==0&&january.Final==-120,"January year-boundary reset");
  Put(store,2026,10,-360);FlexLedger.RecalculateAll(store);Require(store.Months["2026-10"].FinalFlexBalance==-360&&store.Months["2026-11"].PreviousCarryOver==-240&&store.Months["2026-11"].FinalFlexBalance==-660&&store.Months["2026-12"].FinalFlexBalance==60,"earlier edit recalculates dependent balances without capping displayed deficits");Require(store.Months["2027-01"].FinalFlexBalance==-120,"prior-quarter changes do not affect January");
  foreach(int month in new[]{1,4,7,10}){var date=new DateTime(2026,month,1);Put(store,2026,month,61);Require(FlexLedger.Evaluate(date,store).Carry==0&&FlexLedger.Evaluate(date,store).Final==61,"all reset months");}
  var missing=new Store();Put(missing,2026,11,120);Require(!FlexLedger.Evaluate(new DateTime(2026,11,1),missing).Carry.HasValue,"missing prior data is not silently zero");Put(missing,2026,10,360,false);Require(!FlexLedger.Evaluate(new DateTime(2026,11,1),missing).Final.HasValue,"in-progress previous month is not finalized automatically");missing.Months["2026-10"].FlexComplete=true;Require(FlexLedger.Evaluate(new DateTime(2026,11,1),missing).Final==360,"completion resolves next month");
  var empty=new Store();empty.Months["2026-10"]=new MonthData();Require(!FlexLedger.Evaluate(new DateTime(2026,10,1),empty).Difference.HasValue,"visiting a month does not record zero hours");empty.Months["2026-10"].HasRecordedHours=true;Require(FlexLedger.Evaluate(new DateTime(2026,10,1),empty).Difference==-10560,"explicit zero hours is recorded");
  var legacy=new MonthData{PayMinutes=60};Require(FlexLedger.HasHours(legacy),"legacy input recognized");
  FlexLedger.RecalculateAll(store);var serialized=new System.Web.Script.Serialization.JavaScriptSerializer();var restored=serialized.Deserialize<Store>(serialized.Serialize(store));Require(restored.Months["2026-11"].FinalFlexBalance==store.Months["2026-11"].FinalFlexBalance&&restored.Months["2026-10"].FlexComplete,"calculated balance persistence");
 }

}
}
