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
    public partial class MainForm:Form {
        readonly string path;
        LaunchAnimation launchAnimation;
        Button backup, info, browser, manual, appearance, chrome, refresh;
        Action<string> launchChromeRefresh = url => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chrome.exe", url) { UseShellExecute = true });
        TabControl contentTabs;
        AnimatedProgress monthProgress = new AnimatedProgress();
        Label progressLabel = new Label();
        readonly Font historyBold = new Font("Segoe UI",10,FontStyle.Bold);
        Store store=new Store();
        bool loading;
        bool storageHealthy=true;
        bool recoveredFromBackup;
        bool lastSaveSucceeded=true;
        CheckBox followToday=new CheckBox();
        System.Windows.Forms.Timer dayTimer=new System.Windows.Forms.Timer();
        DateTimePicker month=new DateTimePicker(), asof=new DateTimePicker();
        NumericUpDown hours=new NumericUpDown(), minutes=new NumericUpDown();
        DataGridView grid=new DataGridView();
        Label status=new Label();
        Label instructions=new Label();
        Label[] values=new Label[5];
        MonthData data;
        DateTime selected;
        Label[] flexValues=new Label[3];
        Label flexNotice=new Label();
        CheckBox complete=new ReadableCheckBox();
        readonly ToolTip hints = new ToolTip { AutoPopDelay = 15000, ShowAlways = true };
        DataGridView history=new DataGridView();
        public MainForm(bool smoke=false,string statePath=null) {
            path=statePath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlexHours","months.json");
            InitializeLayout();
            hints.SetToolTip(values[0], "All Monday-Friday days in this month, at 8 hours each. Paid holidays and leave count toward this target.");
            hints.SetToolTip(values[1], "Expected hours through the selected As of date. Today counts as a full scheduled day.");
            hints.SetToolTip(values[2], "Paid hours through As of minus expected hours. Excludes later dated hours and carry-over.");
            hints.SetToolTip(values[3], "Full monthly target minus all credited hours in this month, before carry-over. A negative value means a surplus.");
            hints.SetToolTip(values[4], "Hours already included in the total but dated after As of. These are excluded from the balance through date.");
            hints.SetToolTip(flexValues[2], "Full monthly total minus full target plus carry-over. Provisional until the month is confirmed complete.");
            hints.SetToolTip(browser, "Fallback summary import. Applying an import pauses automatic receiving; the extension stays installed.");
            hints.SetToolTip(manual, "Switch to editable totals and leave. Pauses receiving for all months; re-enable it under Chrome sync.");
            hints.SetToolTip(complete, "Confirm only after the month has ended and BambooHR includes all its hours. The next month can then use its carry-over.");
            FormClosed += (s, e) => hints.Dispose();
            chrome.Click+=(s,e)=>ConfigureChrome();
            refresh.Click+=(s,e)=>RefreshFromChrome();
            hints.SetToolTip(refresh, "Opens the installed Chrome sync status page and requests a fresh capture for this month. Use while clocked in; stay signed in to BambooHR.");
            browser.Click+=(s,e)=>{
                if(!CommitPendingEdits())return;
                using(var dialog=new BrowserImportForm(selected,asof.Value.Date)){
                    ThemeManager.Apply(dialog,store.Appearance);
                    if(dialog.ShowDialog(this)==DialogResult.OK){
                        store.ChromeSync.Enabled=false;
                        StopChromeWatcher();
                        data.ChromeSnapshot=false;
                        data.BrowserImported=true;
                        data.HasRecordedHours=true;
                        data.BrowserAsOf=asof.Value.Date;
                        data.BrowserFuture=dialog.FutureCredits;
                        data.BrowserSource=dialog.Source;
                        data.BrowserImportedAt=DateTime.Now;
                        data.PayMinutes=dialog.Summary.Total;
                        LoadMonth();
                    }
                }
            };
            appearance.Click+=(s,e)=>{
                using(var dialog=new AppearanceForm(store.Appearance))if(dialog.ShowDialog(this)==DialogResult.OK){
                    store.Appearance=dialog.SelectedSettings;
                    Recalculate();
                }
            };
            manual.Click+=(s,e)=>{
                if(!CommitPendingEdits())return;
                store.ChromeSync.Enabled=false;
                StopChromeWatcher();
                data.ChromeSnapshot=false;
                data.BrowserImported=false;
                LoadMonth();
            };
            if(!smoke)try{
                store=StateFile.Load(path,out recoveredFromBackup);
            }
            catch(Exception ex){
                storageHealthy=false;
                MessageBox.Show("Saved data could not be read. It will not be overwritten.\n"+ex.Message,"FlexHours");
            }
            loading=true;
            if(store.View.SelectedMonth.HasValue&&store.View.SelectedMonth.Value>=month.MinDate&&store.View.SelectedMonth.Value<=month.MaxDate)month.Value=store.View.SelectedMonth.Value;
            followToday.Checked=store.View.FollowToday;
            DateTime restoredDate=followToday.Checked?DateTime.Today:(store.View.AsOfDate??DateTime.Today);
            if(restoredDate>=asof.MinDate&&restoredDate<=asof.MaxDate)asof.Value=restoredDate;
            asof.Enabled=!followToday.Checked;
            loading=false;
            month.ValueChanged+=(s,e)=>{
                if(loading)return;
                if(!CommitPendingEdits()){
                    loading=true;
                    month.Value=selected;
                    loading=false;
                    return;
                }
                LoadMonth();
            };
            asof.ValueChanged+=(s,e)=>Recalculate();
            hours.ValueChanged+=(s,e)=>{
                if(!loading)data.HasRecordedHours=true;
                Recalculate();
            };
            minutes.ValueChanged+=(s,e)=>{
                if(!loading)data.HasRecordedHours=true;
                Recalculate();
            };
            complete.CheckedChanged+=(s,e)=>{
                if(loading)return;
                data.FlexComplete=complete.Checked;
                if(complete.Checked)data.HasRecordedHours=true;
                Recalculate();
            };
            followToday.CheckedChanged+=(s,e)=>{
                asof.Enabled=!followToday.Checked;
                if(followToday.Checked)asof.Value=DateTime.Today;
                Recalculate();
            };
            dayTimer.Interval=60000;
            dayTimer.Tick+=(s,e)=>{
                if(followToday.Checked&&asof.Value.Date!=DateTime.Today)asof.Value=DateTime.Today;
                ReadChromeSnapshot();
            };
            dayTimer.Start();
            FormClosing+=(s,e)=>{
                if(!CommitPendingEdits()){
                    e.Cancel=true;
                    status.Text="Correct the leave entry before closing so it can be saved.";
                    return;
                }
                if(storageHealthy&&!lastSaveSucceeded)
                    e.Cancel=MessageBox.Show(this,"Your latest changes could not be saved. Choose No to stay and export a backup.\n\nClose anyway?","Unsaved changes",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes;
            };
            FormClosed+=(s,e)=>{
                dayTimer.Dispose();
                historyBold.Dispose();
                StopChromeWatcher();
            };
            grid.CurrentCellDirtyStateChanged+=(s,e)=>{
                if(grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValidating+=(s,e)=>{
                if(e.ColumnIndex==3){
                    decimal v;
                    if(!decimal.TryParse(Convert.ToString(e.FormattedValue),out v)||v<0||v>8||v*60!=Math.Truncate(v*60)){
                        e.Cancel=true;
                        grid.Rows[e.RowIndex].ErrorText="Enter 0 to 8 hours in whole minutes (e.g. 4 or 7.5).";
                    }
                    else grid.Rows[e.RowIndex].ErrorText="";
                }
            };
            grid.CellValueChanged+=(s,e)=>{
                if(!loading && e.RowIndex>=0)ReadRows();
            };
            backup.Click+=(s,e)=>{
                if(!CommitPendingEdits())return;
                using(var dlg=new SaveFileDialog{
                    Filter="JSON backup|*.json",FileName="FlexHours-backup.json"
                }
                )if(dlg.ShowDialog()==DialogResult.OK)try{
                    File.WriteAllText(dlg.FileName,new JavaScriptSerializer().Serialize(store));
                    MessageBox.Show("Backup exported.");
                }
                catch(Exception ex){
                    MessageBox.Show(ex.Message,"Export failed");
                }
            };
            info.Click+=(s,e)=>MessageBox.Show("Target: every Monday-Friday x 8 hours, including paid holidays and approved leave.\n\nThe balance through date excludes future credits and carry-over. Your total must contain worked hours only through that date; a monthly summary cannot reconstruct earlier daily work.\n\nRemaining hours use the full monthly total before carry-over. Final flex balance includes carry-over and is provisional until the month is complete. The full balance stays uncapped.\n\nA holiday and leave on the same day count once, up to 8 hours. Weekend holidays do not automatically credit worked hours here. Enter replacement leave when it is taken.\n\nHoliday dates follow Malta public holiday rules, with calculated Good Friday. Confirm future credits already in BambooHR before relying on the daily balance. Imported totals are snapshots: import again to refresh.","How FlexHours works");
            if(smoke) storageHealthy=false;
            LoadMonth();
            launchAnimation=new LaunchAnimation(this);
            Shown+=(s,e)=>launchAnimation.Start(store.Appearance.AnimationsEnabled && SystemInformation.IsMenuAnimationEnabled);
            Shown+=(s,e)=>{if(!smoke){StartChromeWatcher();ReadChromeSnapshot();}};
            FormClosed+=(s,e)=>launchAnimation.Dispose();
        }

        private bool CommitPendingEdits()
        {
            if(!ValidateChildren()||!grid.EndEdit()){
                status.Text="Correct the leave entry before continuing (0 to 8 hours in whole minutes).";
                return false;
            }
            if(data==null)return true;
            if(!data.BrowserImported)ReadRows();
            else Recalculate();
            return true;
        }
    }
}
