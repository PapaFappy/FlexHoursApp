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
public partial class MainForm {
public static void TestFlexWindow() {
  string folder=Path.Combine(Environment.CurrentDirectory,"FlexHours-ledger-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string statePath=Path.Combine(folder,"months.json");
  try {
   var state=new Store();state.View=new ViewState{SelectedMonth=new DateTime(2026,11,1),AsOfDate=new DateTime(2026,11,30),FollowToday=false};
   int[] months={10,11,12,1};decimal[] differences={360,-420,300,-120};
   for(int i=0;i<months.Length;i++){var date=new DateTime(i==3?2027:2026,months[i],1);var item=new MonthData{HasRecordedHours=true,FlexComplete=true};item.PayMinutes=WorkHoursCalculator.Calculate(date,date.AddMonths(1).AddDays(-1),item).Target+differences[i];state.Months[date.ToString("yyyy-MM")]=item;}StateFile.Save(statePath,state);
   using(var f=new MainForm(false,statePath)) {
    f.Show();Application.DoEvents();if(f.flexValues[0].Text!="-7h 00m"||f.flexValues[1].Text!="+4h 00m"||f.flexValues[2].Text!="-3h 00m"||f.history.Rows.Count!=12)throw new Exception("Flex example display failed");
    f.contentTabs.SelectedTab=f.contentTabs.TabPages["FlexHistory"];Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));bitmap.Save("flex-preview.png");}
    f.month.Value=new DateTime(2026,10,1);f.hours.Value=177;f.minutes.Value=0;
    if(f.store.Months["2026-11"].PreviousCarryOver!=60||f.store.Months["2026-11"].FinalFlexBalance!=-360||f.store.Months["2026-12"].FinalFlexBalance!=60||f.store.Months["2027-01"].FinalFlexBalance!=-120)throw new Exception("Dependent month UI update failed");f.Close();
   }
   using(var f=new MainForm(false,statePath)){f.Show();Application.DoEvents();f.month.Value=new DateTime(2026,11,1);if(f.flexValues[2].Text!="-6h 00m")throw new Exception("Flex calculation not restored");f.month.Value=new DateTime(2027,1,1);if(f.flexValues[1].Text!="0h 00m"||f.flexValues[2].Text!="-2h 00m")throw new Exception("Year boundary reset not restored");f.Close();}
  }finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
 }
public static void TestSavedWindow() {
  string folder=Path.Combine(Environment.CurrentDirectory,"FlexHours-window-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string statePath=Path.Combine(folder,"months.json");
  try {
   var state=new Store();state.View=new ViewState{SelectedMonth=new DateTime(2026,9,1),AsOfDate=new DateTime(2026,9,21),FollowToday=false};state.Months["2026-09"]=new MonthData{PayMinutes=8014,BrowserImported=true,BrowserAsOf=new DateTime(2026,9,21),BrowserFuture=0,BrowserImportedAt=DateTime.Now};StateFile.Save(statePath,state);
   using(var f=new MainForm(false,statePath)) {
    f.Show();Application.DoEvents();if(f.selected!=new DateTime(2026,9,1)||f.asof.Value.Date!=new DateTime(2026,9,21)||f.followToday.Checked||!f.data.BrowserImported||f.data.PayMinutes!=8014||f.values[2].Text=="Check leave")throw new Exception("Saved window did not restore");
    f.data.BrowserImported=false;f.LoadMonth();f.hours.Value=77;f.minutes.Value=30;f.grid.Rows[0].Cells[3].Value=4.5m;f.Close();
   }
   using(var f=new MainForm(false,statePath)) {f.Show();Application.DoEvents();if(f.data.PayMinutes!=4650||f.data.Leave["2026-09-01"]!=270||f.month.Value.Month!=9||f.asof.Value.Day!=21)throw new Exception("Edited values did not recover on reopen");f.followToday.Checked=true;f.Close();}
   using(var f=new MainForm(false,statePath)) {f.Show();Application.DoEvents();if(!f.followToday.Checked||f.asof.Value.Date!=DateTime.Today)throw new Exception("Today preference did not restore");f.Close();}
  }finally{foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
 }
}
}
