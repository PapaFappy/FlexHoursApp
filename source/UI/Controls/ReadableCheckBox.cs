using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    // Windows' disabled checkbox text can become black on a dark custom background.
    public sealed class ReadableCheckBox : CheckBox
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(BackColor);
            var glyph = new Rectangle(0, (ClientSize.Height - 13) / 2, 13, 13);
            ControlPaint.DrawCheckBox(e.Graphics, glyph, ButtonState.Inactive | (Checked ? ButtonState.Checked : ButtonState.Normal));
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(18, 0, System.Math.Max(0, ClientSize.Width - 18), ClientSize.Height),
                ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }
}
