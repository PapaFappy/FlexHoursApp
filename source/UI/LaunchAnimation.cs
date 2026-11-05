using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace FlexHours
{
    /// <summary>Owns one brief fade; no timer remains active afterward.</summary>
    public sealed class LaunchAnimation : IDisposable
    {
        private readonly Form form;
        private readonly Timer timer = new Timer { Interval = 25 };
        private readonly Stopwatch clock = new Stopwatch();
        public bool IsRunning { get { return timer.Enabled; } }

        public LaunchAnimation(Form window)
        {
            form = window;
            timer.Tick += (s, e) =>
            {
                if (form.IsDisposed || !form.Visible || form.WindowState == FormWindowState.Minimized)
                {
                    Finish();
                    return;
                }
                double time = Math.Min(1, clock.Elapsed.TotalMilliseconds / 320);
                form.Opacity = 1 - Math.Pow(1 - time, 3);
                if (time >= 1) Finish();
            };
        }

        public void Start(bool enabled)
        {
            if (!enabled) { Finish(); return; }
            form.Opacity = 0.05;
            clock.Restart();
            timer.Start();
        }

        private void Finish()
        {
            timer.Stop();
            clock.Stop();
            if (!form.IsDisposed) form.Opacity = 1;
        }

        public void Dispose()
        {
            Finish();
            timer.Dispose();
        }
    }
}
