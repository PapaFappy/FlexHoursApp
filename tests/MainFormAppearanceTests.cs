using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace FlexHours
{
    public partial class MainForm
    {
        public static void TestAppearanceWindow()
        {
            using (var window = new MainForm(true))
            {
                window.Show();
                Application.DoEvents();
                using (var launch = new LaunchAnimation(window))
                {
                    launch.Start(true);
                    var launched = DateTime.UtcNow;
                    while ((DateTime.UtcNow - launched).TotalMilliseconds < 500)
                    {
                        Application.DoEvents();
                        Thread.Sleep(5);
                    }
                    if (launch.IsRunning || window.Opacity != 1)
                        throw new Exception("Launch animation did not finish and stop its timer");
                    launch.Start(false);
                    if (launch.IsRunning || window.Opacity != 1)
                        throw new Exception("Disabled launch animation changed visibility");
                }
                window.store.Appearance = AppearanceSettings.Defaults(true);
                window.store.Appearance.AccentColor = "#AA77FF";
                window.Recalculate();
                if (window.BackColor != new ThemePalette(window.store.Appearance).Background)
                    throw new Exception("Dark theme did not apply");
                if (window.refresh.BackColor != ColorTranslator.FromHtml("#AA77FF"))
                    throw new Exception("Accent did not apply to primary action");
                window.monthProgress.SetValue(0.65, true);
                var start = DateTime.UtcNow;
                while ((DateTime.UtcNow - start).TotalMilliseconds < 450)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                }
                if (window.monthProgress.IsAnimating) throw new Exception("Animation timer did not stop");
                window.monthProgress.SetValue(0.8, false);
                if (window.monthProgress.IsAnimating) throw new Exception("Reduced motion started a timer");
                window.monthProgress.SetValue(0, false);
                Draw(window, "dark-preview.png");
                window.store.Appearance = AppearanceSettings.Defaults(false);
                window.Recalculate();
                Draw(window, "light-preview.png");
                window.Size = window.MinimumSize;
                Application.DoEvents();
                if (window.chromeDailyGrid.Height < 150)
                    throw new Exception("Compact layout squeezed the timesheet out of view instead of scrolling");
                var viewport = window.Controls[0] as Panel;
                if (viewport.ClientSize.Height < 900 && !viewport.VerticalScroll.Visible)
                    throw new Exception("Compact layout did not expose its vertical scrollbar");
                foreach (Button action in new[] { window.refresh, window.chrome, window.browser, window.manual, window.appearance, window.backup, window.info })
                    if (action.Bottom > action.Parent.ClientSize.Height || action.Right > action.Parent.ClientSize.Width)
                        throw new Exception("An action was clipped at the minimum window size");
                foreach (Label metric in window.values)
                    if (metric.Bottom > metric.Parent.ClientSize.Height - metric.Parent.Padding.Bottom)
                        throw new Exception("A metric value was clipped at the minimum window size");
                Draw(window, "compact-preview.png");
                using (var appearance = new AppearanceForm(AppearanceSettings.Defaults(true)))
                {
                    appearance.Show();
                    Application.DoEvents();
                    foreach (Control root in appearance.Controls)
                        foreach (Control child in root.Controls)
                            if (child.Bottom > root.ClientSize.Height - root.Padding.Bottom)
                                throw new Exception("Appearance controls clipped at bottom");
                    Draw(appearance, "appearance-preview.png");
                    appearance.Close();
                    if (appearance.SelectedSettings != null) throw new Exception("Cancel changed appearance");
                }
                window.Close();
            }
        }

        private static void Draw(Form form, string name)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(name);
            }
        }
    }
}
