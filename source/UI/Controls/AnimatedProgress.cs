using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlexHours
{
    /// <summary>A short transition, never a permanent animation/render loop.</summary>
    public sealed class AnimatedProgress : Control
    {
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch clock = new Stopwatch();
        private double shown, start, target;
        public Color TrackColor { get; set; }
        public Color FillColor { get; set; }
        public bool IsAnimating { get { return timer.Enabled; } }
        public double TargetValue { get { return target; } }

        public AnimatedProgress()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Height = 10;
            timer.Tick += (s, e) =>
            {
                double time = Math.Min(1, clock.Elapsed.TotalMilliseconds / 260);
                shown = start + (target - start) * (1 - Math.Pow(1 - time, 3));
                if (time >= 1 || !Visible)
                {
                    shown = target;
                    timer.Stop();
                    clock.Stop();
                }
                Invalidate();
            };
            VisibleChanged += (s, e) => { if (!Visible) Finish(); };
        }

        public void SetValue(double value, bool animate)
        {
            value = Math.Max(0, Math.Min(1, value));
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            if (Math.Abs(value - target) < 0.0001)
            {
                if (!animate) Finish();
                return;
            }
            target = value;
            if (!animate || !Visible || !IsHandleCreated)
            {
                Finish();
                return;
            }
            start = shown;
            clock.Restart();
            timer.Start();
        }

        private void Finish()
        {
            timer.Stop();
            clock.Stop();
            shown = target;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Width < 10 || Height < 4) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var track = SurfaceCard.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 4))
            using (var brush = new SolidBrush(TrackColor))
            {
                e.Graphics.FillPath(brush, track);
            }
            int width = (int)((Width - 1) * shown);
            if (width < 9) return;
            using (var fill = SurfaceCard.Rounded(new Rectangle(0, 0, width, Height - 1), 4))
            using (var brush = new SolidBrush(FillColor))
            {
                e.Graphics.FillPath(brush, fill);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
