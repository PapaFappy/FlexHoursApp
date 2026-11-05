using System;
using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    public sealed class DayBreakdownLabel : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Text.Contains("\t")) { base.OnPaint(e); return; }
            int y = 0, height = Font.Height + 1;
            foreach (string line in Text.Split('\n'))
            {
                string[] parts = line.Split('\t');
                int labelWidth = parts.Length == 2 ? ClientSize.Width / 2 : ClientSize.Width;
                TextRenderer.DrawText(e.Graphics, parts[0], Font, new Rectangle(0, y, labelWidth, height), ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                if (parts.Length == 2) TextRenderer.DrawText(e.Graphics, parts[1], Font, new Rectangle(labelWidth, y, ClientSize.Width - labelWidth, height), ForeColor,
                    TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += height;
            }
        }
    }
}
