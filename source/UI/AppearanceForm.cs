using System;
using System.Drawing;
using System.Windows.Forms;

namespace FlexHours
{
    public sealed class AppearanceForm : Form
    {
        private AppearanceSettings draft;
        private readonly CheckBox darkMode = new CheckBox();
        private readonly CheckBox animations = new CheckBox();
        private readonly Button accent = new Button(), positive = new Button(), negative = new Button();
        private readonly Label example = new Label();
        public AppearanceSettings SelectedSettings { get; private set; }

        public AppearanceForm(AppearanceSettings current)
        {
            draft = (current ?? new AppearanceSettings()).Copy();
            draft.Normalize();
            Text = "Appearance";
            ClientSize = new Size(600, 510);
            MinimumSize = Size;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8
            };
            foreach (int height in new[] { 44, 42, 42, 48, 48, 48, 64, 55 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "Make FlexHours your own", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) });
            darkMode.Text = "Dark mode";
            darkMode.Dock = DockStyle.Fill;
            darkMode.Checked = draft.DarkMode;
            layout.Controls.Add(darkMode);
            animations.Text = "Animate launch and progress (brief transitions; no idle animation)";
            animations.Checked = draft.AnimationsEnabled;
            animations.Dock = DockStyle.Fill;
            layout.Controls.Add(animations);
            AddColorButton(layout, accent, "Accent and selection", () => draft.AccentColor, value => draft.AccentColor = value);
            AddColorButton(layout, positive, "Positive balance", () => draft.PositiveColor, value => draft.PositiveColor = value);
            AddColorButton(layout, negative, "Negative balance", () => draft.NegativeColor, value => draft.NegativeColor = value);
            example.Text = "Colours are saved with your preferences.\nCancel leaves the current appearance unchanged.";
            example.Dock = DockStyle.Fill;
            layout.Controls.Add(example);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var save = new Button { Text = "Save appearance", AutoSize = true, Tag = "Accent" };
            var reset = new Button { Text = "Reset colours", AutoSize = true };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            actions.Controls.AddRange(new Control[] { save, reset, cancel });
            layout.Controls.Add(actions);
            AcceptButton = save;
            CancelButton = cancel;

            darkMode.CheckedChanged += (s, e) =>
            {
                draft = draft.WithDarkMode(darkMode.Checked);
                RefreshPreview();
            };
            animations.CheckedChanged += (s, e) => draft.AnimationsEnabled = animations.Checked;
            reset.Click += (s, e) => { draft = AppearanceSettings.Defaults(darkMode.Checked); draft.AnimationsEnabled = animations.Checked; RefreshPreview(); };
            save.Click += (s, e) =>
            {
                SelectedSettings = draft.Copy();
                SelectedSettings.Normalize();
                DialogResult = DialogResult.OK;
                Close();
            };
            RefreshPreview();
        }

        private void AddColorButton(TableLayoutPanel layout, Button button, string label, Func<string> get, Action<string> set)
        {
            button.Text = label;
            button.Dock = DockStyle.Fill;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Margin = new Padding(0, 4, 0, 4);
            layout.Controls.Add(button);
            button.Click += (s, e) =>
            {
                using (var picker = new ColorDialog { Color = ColorTranslator.FromHtml(get()), FullOpen = true })
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    var palette = new ThemePalette(draft);
                    if (button != accent && ThemePalette.Contrast(picker.Color, palette.Surface) < 3)
                    {
                        MessageBox.Show(this, "Choose a balance colour with more contrast against this theme.", "Colour contrast");
                        return;
                    }
                    set("#" + picker.Color.R.ToString("X2") + picker.Color.G.ToString("X2") + picker.Color.B.ToString("X2"));
                    RefreshPreview();
                }
            };
        }

        private void RefreshPreview()
        {
            ThemeManager.Apply(this, draft);
            var palette = new ThemePalette(draft);
            accent.Text = "Accent and selection  " + draft.AccentColor;
            positive.Text = "Positive balance  +3h 30m  " + draft.PositiveColor;
            negative.Text = "Negative balance  -2h 15m  " + draft.NegativeColor;
            accent.BackColor = palette.Accent;
            accent.ForeColor = ThemePalette.ReadableText(palette.Accent);
            positive.ForeColor = palette.Positive;
            negative.ForeColor = palette.Negative;
        }
    }
}
