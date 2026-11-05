using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace FlexHours
{
    /// <summary>Portable user preferences; colours are stored as #RRGGBB.</summary>
    public class AppearanceSettings
    {
        public bool DarkMode { get; set; }
        public bool AnimationsEnabled { get; set; }
        public string AccentColor { get; set; }
        public string PositiveColor { get; set; }
        public string NegativeColor { get; set; }

        public AppearanceSettings()
        {
            AnimationsEnabled = true;
            AccentColor = "#2563EB";
            PositiveColor = "#188064";
            NegativeColor = "#B93E39";
        }

        public static AppearanceSettings Defaults(bool dark)
        {
            return new AppearanceSettings
            {
                DarkMode = dark,
                AccentColor = dark ? "#729FFF" : "#2563EB",
                PositiveColor = dark ? "#55D6A0" : "#188064",
                NegativeColor = dark ? "#FF858A" : "#B93E39"
            };
        }

        public AppearanceSettings Copy()
        {
            return (AppearanceSettings)MemberwiseClone();
        }

        public AppearanceSettings WithDarkMode(bool dark)
        {
            var previousDefaults = Defaults(DarkMode);
            var nextDefaults = Defaults(dark);
            var next = Copy();
            next.DarkMode = dark;
            if (string.Equals(AccentColor, previousDefaults.AccentColor, StringComparison.OrdinalIgnoreCase)) next.AccentColor = nextDefaults.AccentColor;
            if (string.Equals(PositiveColor, previousDefaults.PositiveColor, StringComparison.OrdinalIgnoreCase)) next.PositiveColor = nextDefaults.PositiveColor;
            if (string.Equals(NegativeColor, previousDefaults.NegativeColor, StringComparison.OrdinalIgnoreCase)) next.NegativeColor = nextDefaults.NegativeColor;
            next.Normalize();
            return next;
        }

        public static bool IsColor(string value)
        {
            return value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
        }

        public void Normalize()
        {
            var defaults = Defaults(DarkMode);
            // Restore the earlier stock palette while retaining custom colours.
            if (string.Equals(AccentColor, DarkMode ? "#A78BFA" : "#7055CF", StringComparison.OrdinalIgnoreCase))
                AccentColor = defaults.AccentColor;
            if (!IsColor(AccentColor)) AccentColor = defaults.AccentColor;
            if (!IsColor(PositiveColor)) PositiveColor = defaults.PositiveColor;
            if (!IsColor(NegativeColor)) NegativeColor = defaults.NegativeColor;
            AccentColor = AccentColor.ToUpperInvariant();
            PositiveColor = PositiveColor.ToUpperInvariant();
            NegativeColor = NegativeColor.ToUpperInvariant();

            // A damaged preference should never make monthly data unreadable.
            Color surface = DarkMode ? Color.FromArgb(32, 39, 53) : Color.White;
            if (ThemePalette.Contrast(ColorTranslator.FromHtml(PositiveColor), surface) < 3)
                PositiveColor = defaults.PositiveColor;
            if (ThemePalette.Contrast(ColorTranslator.FromHtml(NegativeColor), surface) < 3)
                NegativeColor = defaults.NegativeColor;
        }
    }
}
