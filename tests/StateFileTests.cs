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
public static class StateFileTests {
public static void Run() {
  string folder=Path.Combine(Environment.CurrentDirectory,"FlexHours-state-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"months.json");
  try {
   bool recovered;var state=StateFile.Load(path,out recovered);if(state.Months.Count!=0||recovered)throw new Exception("New state load failed");
   var legacyMonths=new System.Collections.Generic.Dictionary<string,MonthData>();legacyMonths["2026-10"]=new MonthData{PayMinutes=8014,BrowserImported=true,BrowserAsOf=new DateTime(2026,10,22),BrowserFuture=300};
   File.WriteAllText(path,new JavaScriptSerializer().Serialize(new{Months=legacyMonths}));
   state=StateFile.Load(path,out recovered);if(state.Months["2026-10"].PayMinutes!=8014||state.View==null)throw new Exception("Earlier-version data migration failed");
   state.View=new ViewState{SelectedMonth=new DateTime(2026,10,1),AsOfDate=new DateTime(2026,10,22),FollowToday=false};StateFile.Save(path,state);
   var reopened=StateFile.Load(path,out recovered);if(reopened.View.SelectedMonth!=state.View.SelectedMonth||reopened.View.AsOfDate!=state.View.AsOfDate||reopened.View.FollowToday||reopened.Months["2026-10"].BrowserFuture!=300||!reopened.SavedAtUtc.HasValue)throw new Exception("Full state restore failed: "+new JavaScriptSerializer().Serialize(reopened)+" expected "+new JavaScriptSerializer().Serialize(state));
   string previous=File.ReadAllText(path);StateFile.Save(path,reopened);if(File.ReadAllText(path)!=previous)throw new Exception("Unchanged state was rewritten");
   reopened.Months["2026-10"].PayMinutes=8100;StateFile.Save(path,reopened);if(File.ReadAllText(path+".bak")!=previous)throw new Exception("Previous save backup failed");
   File.WriteAllText(path,"broken JSON");var backup=StateFile.Load(path,out recovered);if(!recovered||backup.Months["2026-10"].PayMinutes!=8014||Directory.GetFiles(folder,"*.unreadable-*").Length!=1)throw new Exception("Safe backup recovery failed");
   File.WriteAllText(path,"broken");File.WriteAllText(path+".bak","broken too");bool rejected=false;try{StateFile.Load(path,out recovered);}catch{rejected=true;}if(!rejected||File.ReadAllText(path)!="broken")throw new Exception("Unreadable files were not protected");
  }finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
 }
}
}
