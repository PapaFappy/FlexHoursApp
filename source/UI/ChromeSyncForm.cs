using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FlexHours
{
    public sealed class ChromeSyncForm : Form
    {
        public ChromeSyncSettings Settings { get; private set; }
        public bool UseCalendar { get; private set; }
        private readonly TextBox company = new TextBox();
        private readonly CheckBox enabled = new CheckBox(), calendar = new CheckBox();

        public ChromeSyncForm(ChromeSyncSettings current, bool useCalendar, DateTime month)
        {
            Text = "Chrome automatic sync";
            ClientSize = new Size(680, 580);
            MinimumSize = Size;
            Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 40, 150, 42, 50, 80, 44, 40 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Controls.Add(root);
            root.Controls.Add(new Label { Text = "Use your normal Chrome login", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Dock = DockStyle.Fill });
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "One-time setup:\n1. Load the chrome-extension folder at chrome://extensions.\n2. Run setup-chrome.ps1 with the extension ID shown by Chrome.\n3. Open your own current-month Timesheet in Chrome, then click\n   the extension and Connect & enable auto refresh.\n4. Enter the same company below and enable receiving updates." });
            company.Text = current.CompanyUrl ?? "";
            company.Dock = DockStyle.Top;
            company.AccessibleName = "BambooHR company subdomain or HTTPS address";
            root.Controls.Add(company);
            enabled.Text = "Receive automatic Chrome updates for this company";
            enabled.Checked = current.Enabled;
            enabled.Dock = DockStyle.Fill;
            root.Controls.Add(enabled);
            calendar.Text = "Fallback for " + month.ToString("MMMM yyyy") + ": my leave/holiday calendar is complete\nand includes only credits already counted by BambooHR.\nUse it only when the dated BambooHR rows cannot be verified.";
            calendar.Checked = useCalendar;
            calendar.Dock = DockStyle.Fill;
            root.Controls.Add(calendar);
            var help = new Button { Text = "Open setup folder", AutoSize = true };
            help.Click += (s, e) =>
            {
                string directory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directory + "\"") { UseShellExecute = true });
            };
            root.Controls.Add(help);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var save = new Button { Text = "Save", AutoSize = true, Tag = "Accent" };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.AddRange(new Control[] { save, cancel });
            root.Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;
            save.Click += (s, e) =>
            {
                try
                {
                    string origin = string.IsNullOrWhiteSpace(company.Text) && !enabled.Checked ? null : BrowserReader.CompanyUrl(company.Text);
                    Settings = new ChromeSyncSettings { Enabled = enabled.Checked, CompanyUrl = origin };
                    UseCalendar = calendar.Checked;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Company address"); }
            };
        }
    }

    public partial class MainForm
    {
        private FileSystemWatcher chromeWatcher;
        private void RefreshFromChrome()
        {
            if (!CommitPendingEdits()) return;
            if (!store.ChromeSync.Enabled) { status.Text = "Enable receiving updates under Chrome sync, then click Refresh now."; return; }
            try
            {
                string manifest = Path.Combine(ChromeDirectory, "Chrome", "native-host.json");
                string url = ChromeRefreshLink.FromManifest(File.ReadAllText(manifest), selected);
                launchChromeRefresh(url);
                status.Text = "Chrome refresh requested. The sync status page shows progress; the new balance appears when received.";
            }
            catch (Exception error) { status.Text = "Could not request refresh: " + error.Message + " Open the Chrome extension and click Refresh now."; }
        }
        private string ChromeDirectory { get { return Path.GetDirectoryName(Path.GetFullPath(path)); } }

        private void ConfigureChrome()
        {
            if (!CommitPendingEdits()) return;
            using (var dialog = new ChromeSyncForm(store.ChromeSync, data.ChromeUseCalendar, selected))
            {
                ThemeManager.Apply(dialog, store.Appearance);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                store.ChromeSync = dialog.Settings;
                data.ChromeUseCalendar = dialog.UseCalendar;
                if (!dialog.UseCalendar && data.ChromeSnapshot) data.BrowserFuture = null;
                Recalculate();
                StartChromeWatcher();
                ReadChromeSnapshot();
            }
        }

        private void StartChromeWatcher()
        {
            StopChromeWatcher();
            if (!store.ChromeSync.Enabled || !storageHealthy) return;
            string folder = Path.Combine(ChromeDirectory, "Chrome");
            Directory.CreateDirectory(folder);
            chromeWatcher = new FileSystemWatcher(folder, "latest*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                SynchronizingObject = this
            };
            chromeWatcher.Created += (s, e) => ReadChromeSnapshot();
            chromeWatcher.Changed += (s, e) => ReadChromeSnapshot();
            chromeWatcher.Renamed += (s, e) => ReadChromeSnapshot();
            chromeWatcher.EnableRaisingEvents = true;
        }

        private void StopChromeWatcher()
        {
            if (chromeWatcher != null) { chromeWatcher.Dispose(); chromeWatcher = null; }
        }

        private void ReadChromeSnapshot()
        {
            if (!store.ChromeSync.Enabled || !storageHealthy || !lastSaveSucceeded || loading || IsDisposed
                || grid.IsCurrentCellInEditMode || OwnedForms.Length != 0) return;
            try
            {
                bool changed=false, selectedChanged=false;
                var errors = new System.Collections.Generic.List<string>();
                foreach(var snapshot in ChromeNativeHost.ReadAllSnapshots(ChromeDirectory, errors))
                {
                    if(!ChromeSync.Apply(store,snapshot,DateTime.UtcNow))continue;
                    changed=true;
                    if(snapshot.Month==selected.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture))selectedChanged=true;
                }
                if(changed) { if(selectedChanged)LoadMonth();else Recalculate(); }
                if(errors.Count > 0) status.Text = "Some Chrome updates were skipped: " + string.Join(" ", errors);
            }
            catch (Exception error) { status.Text = "Chrome update not applied: " + error.Message; }
        }
    }
}
