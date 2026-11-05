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
public static class SummaryParserTests {
public static void Run() {
  string sample="8h 44m Today\nClocked Out: Today at 5:57 PM\nThis Week\n34h 27m\nOct 19 - 25 (3 days left)\n133h 34m\nPay Period\nOct 1-31 (9 days left)\nPaid Hours\n128h 34m REG\n5h Time Off";
  var s=SummaryParser.Parse(sample,new DateTime(2026,10,1));if(s.Total!=8014||s.Regular!=7714||s.TimeOff!=300)throw new Exception("Screenshot summary parsing failed");
  s=SummaryParser.Parse(sample.Replace("Oct 1-31","October 1 – October 31, 2026"),new DateTime(2026,10,1));if(s.Total!=8014)throw new Exception("Long date parsing failed");
  foreach(string separator in new[]{"\u2013","\u2014","\u2212"}) {
   s=SummaryParser.Parse(sample.Replace("Oct 1-31","Oct 1"+separator+"31"),new DateTime(2026,10,1));if(s.Total!=8014)throw new Exception("Unicode date separator failed");
  }
  string[] invalid={sample.Replace("Oct 1-31","Nov 1-30"),sample.Replace("133h 34m","133h 99m"),sample.Replace("5h Time Off","6h Time Off"),sample.Replace("Oct 1-31","Oct 1-15"),sample+" 140h Pay Period Oct 1-31",sample.Replace("Oct 1-31","Oct 1-31, 2025"),sample+" 3h Time Off",sample+" 100h REG"};
  foreach(var bad in invalid){bool rejected=false;try{SummaryParser.Parse(bad,new DateTime(2026,10,1));}catch{rejected=true;}if(!rejected)throw new Exception("Invalid summary accepted");}
  s=SummaryParser.Parse("Pay Period 12h 05m Feb 1-29, 2028",new DateTime(2028,2,1));if(s.Total!=725)throw new Exception("Leap-month summary failed");
  bool rejectedUrl=false;try{BrowserReader.CompanyUrl("https://evil.example/test");}catch{rejectedUrl=true;}if(!rejectedUrl)throw new Exception("Foreign host accepted");
  if(BrowserReader.CompanyUrl("mycompany")!="https://mycompany.bamboohr.com/")throw new Exception("Company normalization failed");
 }
}
}
