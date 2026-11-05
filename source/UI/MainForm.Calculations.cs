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
        void LoadMonth() {
            loading=true;
            selected=new DateTime(month.Value.Year,month.Value.Month,1);
            string key=selected.ToString("yyyy-MM",CultureInfo.InvariantCulture);
            if(!store.Months.TryGetValue(key,out data)) {
                data=new MonthData();
                store.Months[key]=data;
            }
            if(data.Leave==null)data.Leave=new Dictionary<string,decimal>();
            if(data.HolidayIncluded==null)data.HolidayIncluded=new Dictionary<string,bool>();
            hours.Value=Math.Min(1000,Math.Max(0,Math.Floor(data.PayMinutes/60)));
            minutes.Value=Math.Max(0,data.PayMinutes%60);
            complete.Checked=data.FlexComplete;
            grid.Rows.Clear();
            var holidays=WorkHoursCalculator.Holidays(selected.Year);
            for(int d=1;d<=DateTime.DaysInMonth(selected.Year,selected.Month);d++) {
                var day=selected.AddDays(d-1);
                string name;
                holidays.TryGetValue(day,out name);
                decimal leave;
                data.Leave.TryGetValue(WorkHoursCalculator.Key(day),out leave);
                bool inc;
                inc=!data.HolidayIncluded.TryGetValue(WorkHoursCalculator.Key(day),out inc)||inc;
                int idx=grid.Rows.Add(day.ToString("dd MMM yyyy"),day.ToString("ddd"),name??"",leave/60,inc,"");
                var row=grid.Rows[idx];
                row.Tag=day;
                row.Cells[4].ReadOnly=name==null||!WorkHoursCalculator.Weekday(day);
                if(row.Cells[4].ReadOnly)row.Cells[4].Value=false;
                row.Cells[3].ReadOnly=!WorkHoursCalculator.Weekday(day)||name!=null;
                if(!WorkHoursCalculator.Weekday(day))row.DefaultCellStyle.BackColor=Color.FromArgb(240,242,245);
                else if(name!=null)row.DefaultCellStyle.BackColor=Color.FromArgb(229,244,239);
            }
            loading=false;
            Recalculate();
        }
        void ReadRows() {
            foreach(DataGridViewRow row in grid.Rows){
                var day=(DateTime)row.Tag;
                decimal leave;
                if(decimal.TryParse(Convert.ToString(row.Cells[3].Value),out leave))data.Leave[WorkHoursCalculator.Key(day)]=leave*60;
                if(!row.Cells[4].ReadOnly)data.HolidayIncluded[WorkHoursCalculator.Key(day)]=Convert.ToBoolean(row.Cells[4].Value);
            }
            Recalculate();
        }
        void Recalculate() {
            if(loading||data==null)return;
            data.PayMinutes=hours.Value*60+minutes.Value;
            ChromeSync.RefreshCalendar(data,selected);
            var r=WorkHoursCalculator.Calculate(selected,asof.Value.Date,data);
            FlexLedger.RecalculateAll(store);
            var flex=FlexLedger.Evaluate(selected,store);
            flexValues[0].Text=FlexLedger.Display(flex.Difference);
            flexValues[1].Text=FlexLedger.Display(flex.Carry);
            flexValues[2].Text=FlexLedger.Display(flex.Final);
            flexValues[2].Parent.Controls["MetricCaption"].Text=flex.Complete?"FINAL FLEX BALANCE / UNCAPPED":"PROVISIONAL FLEX / FULL MONTH TARGET";
            flexValues[2].ForeColor=flex.Final.HasValue&&flex.Final.Value<0?Color.FromArgb(185,62,57):Color.FromArgb(24,128,100);
            complete.Enabled=selected.AddMonths(1).AddDays(-1)<=DateTime.Today;
            flexNotice.Text=(flex.Reset?"QUARTER RESET: carry-over is 0h. ":"")+flex.Status.Replace(" | Quarter reset","")+"\nMonthly difference uses the full month's target; The balance through date above excludes carry-over.";
            history.Rows.Clear();
            for(int i=1;i<=12;i++){
                var date=new DateTime(selected.Year,i,1);
                var item=FlexLedger.Evaluate(date,store);
                int row=history.Rows.Add(date.ToString("MMM yyyy"),FlexLedger.Display(item.Difference),FlexLedger.Display(item.Carry),FlexLedger.Display(item.Final),item.Status);
                history.Rows[row].Tag=date;
                if(item.Reset)history.Rows[row].DefaultCellStyle.BackColor=Color.FromArgb(229,244,239);
                if(date==selected)history.Rows[row].DefaultCellStyle.Font=historyBold;
            }
            history.ClearSelection();
            int currentRow=selected.Month-1;
            history.CurrentCell=history.Rows[currentRow].Cells[0];
            history.Rows[currentRow].Selected=true;
            if(history.DisplayedRowCount(false)>0)history.FirstDisplayedScrollingRowIndex=Math.Max(0,currentRow-7);
            values[0].Text=WorkHoursCalculator.Hours(r.Target);
            values[1].Text=WorkHoursCalculator.Hours(r.Expected);
            values[2].Text=(r.Balance>0?"+":"")+WorkHoursCalculator.Hours(r.Balance);
            values[2].ForeColor=r.Balance<0?Color.FromArgb(185,62,57):Color.FromArgb(24,128,100);
            values[3].Text=WorkHoursCalculator.Hours(r.Remaining);
            values[4].Text=WorkHoursCalculator.Hours(r.Future);
            bool browserBalanceKnown=data.BrowserImported&&data.BrowserFuture.HasValue&&data.BrowserAsOf.Date==asof.Value.Date;
            if(data.BrowserImported&&data.ChromeSnapshot&&data.ChromeDailyVerified&&asof.Value.Date<=data.BrowserAsOf.Date)browserBalanceKnown=true;
            if(data.BrowserImported&&!browserBalanceKnown){
                values[2].Text="Check leave";
                values[2].ForeColor=Color.FromArgb(155,105,20);
                values[4].Text="Unconfirmed";
            }
            bool hasHours=FlexLedger.HasHours(data);
            bool manualCreditsInvalid=!data.BrowserImported&&r.Future>data.PayMinutes;
            if(!hasHours){
                values[2].Text="Pending";
                values[3].Text="Pending";
                values[4].Text="-";
            }
            else if(manualCreditsInvalid){
                values[2].Text="Check leave";
            }
            hours.Enabled=minutes.Enabled=!data.BrowserImported;
            grid.Enabled=!data.BrowserImported||(data.ChromeSnapshot&&data.ChromeUseCalendar&&!data.ChromeDailyVerified);
            instructions.Text=data.BrowserImported?"Browser summary imported "+data.BrowserImportedAt.ToString("dd MMM HH:mm")+". Reimport to refresh the total. Manual leave entries below are inactive.\n"+(browserBalanceKnown?"Future credits confirmed for "+asof.Value.ToString("dd MMM yyyy")+". Today counts as a full 8-hour day.":"Daily balance is unconfirmed: reimport and verify credits dated after the selected date."):"Enter the Pay Period total exactly as BambooHR shows it. Add only approved leave already credited in that total.\nPublic holiday ticks mean those hours are included in BambooHR. Untick any that are not included. Today counts as a full 8-hour day.";
            loading=true;
            var holidays=WorkHoursCalculator.Holidays(selected.Year);
            foreach(DataGridViewRow row in grid.Rows){
                var day=(DateTime)row.Tag;
                row.Cells[5].Value=data.BrowserImported?"Summary mode":day>asof.Value.Date?WorkHoursCalculator.Hours(WorkHoursCalculator.Credit(day,data,holidays)):"—";
            }
            loading=false;
            if(!hasHours)instructions.Text="Enter or import the BambooHR Pay Period total to calculate your balance. No total is recorded for this month yet.";
            if(data.ChromeSnapshot)instructions.Text="Chrome snapshot captured "+data.BrowserImportedAt.ToString("dd MMM HH:mm")+". Automatic updates require Chrome and enabled sync.\n"+(data.ChromeUseCalendar?"Future credits use your reviewed leave/holiday calendar below. Keep it current when leave changes.":"For a daily balance, open Chrome sync and confirm the leave/holiday calendar. Monthly remainder works without it.");
            if(data.ChromeSnapshot&&data.ChromeDailyVerified)instructions.Text="Chrome snapshot "+data.BrowserImportedAt.ToString("dd MMM HH:mm")+": dated paid hours reconcile with the monthly total.\nSee Timesheet days for worked hours, PTO, sick leave and holidays. Hours after the comparison date are excluded from the daily balance.";
            else if(data.ChromeSnapshot&&!string.IsNullOrEmpty(data.ChromeDailyWarning))instructions.Text="Chrome summary updated; daily table needs attention: "+data.ChromeDailyWarning+"\nUse Chrome sync's reviewed-calendar fallback if needed; daily totals have not been inferred.";
            RefreshChromeDailyGrid();
            if(data.ChromeSnapshot && data.ChromeFutureVerified && !data.ChromeDailyVerified)
                instructions.Text = "Pay Period total synced from BambooHR. Future dated hours verified for " + data.BrowserAsOf.ToString("dd MMM yyyy") + ".\nDaily details still need review: " + data.ChromeDailyWarning;
            RefreshCalendarView();
            values[2].Parent.Controls["MetricCaption"].Text = asof.Value.Date == DateTime.Today ? "TODAY'S BALANCE / AHEAD OR BEHIND" : "BALANCE THROUGH " + asof.Value.ToString("dd MMM yyyy").ToUpperInvariant();
            bool dailyBalanceKnown = hasHours && !manualCreditsInvalid && (!data.BrowserImported || browserBalanceKnown);
            balanceNote.Text = !dailyBalanceKnown ? "Waiting for verified hours / leave credits"
                : (r.Balance > 0 ? "Ahead of expected hours" : r.Balance < 0 ? "Behind expected hours" : "On target")
                    + " | carry-over excluded";
            balanceNote.Tag = !dailyBalanceKnown ? "Warning" : r.Balance < 0 ? "Negative" : "Positive";
            if(manualCreditsInvalid)instructions.Text="Future leave and holiday credits exceed the entered total. Check the total and untick holidays not yet credited. Monthly remainder is still shown.";
            values[2].Tag=(!hasHours||manualCreditsInvalid||(!browserBalanceKnown&&data.BrowserImported))?"Warning":r.Balance<0?"Negative":"Positive";
            flexValues[0].Tag=flex.Difference.HasValue?(flex.Difference<0?"Negative":"Positive"):"Warning";
            flexValues[2].Tag=flex.Final.HasValue?(flex.Final<0?"Negative":"Positive"):"Warning";
            ThemeManager.Apply(this,store.Appearance);
            double progress=r.Target>0?(double)(data.PayMinutes/r.Target):0;
            monthProgress.SetValue(progress,store.Appearance.AnimationsEnabled&&SystemInformation.IsMenuAnimationEnabled);
            progressLabel.Text=hasHours?Math.Round(Math.Max(0,progress)*100)+"% of monthly target credited":"Enter or import a total to begin";
            if(!storageHealthy){
                status.Text="Preview mode or unreadable saved data: automatic saving disabled.";
                return;
            }
            try{
                store.View.SelectedMonth=selected;
                store.View.AsOfDate=asof.Value.Date;
                store.View.FollowToday=followToday.Checked;
                StateFile.Save(path,store);
                lastSaveSucceeded=true;
                status.Text="Saved locally at "+store.SavedAtUtc.Value.ToLocalTime().ToString("HH:mm:ss")+" | Restored on next launch";
                if(recoveredFromBackup)status.Text+=" | Recovered previous backup";
            }
            catch(Exception ex){
                lastSaveSucceeded=false;
                status.Text="Not saved: "+ex.Message;
            }
        }
    }
}
