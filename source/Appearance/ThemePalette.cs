using System;
using System.Drawing;

namespace FlexHours
{
    public sealed class ThemePalette
    {
        public readonly Color Background, Surface, Text, Muted, Border;
        public readonly Color Accent, Positive, Negative, Warning, SpecialRow;

        public ThemePalette(AppearanceSettings settings)
        {
            settings = (settings ?? new AppearanceSettings()).Copy();
            settings.Normalize();
            bool dark = settings.DarkMode;
            Background = dark ? Color.FromArgb(20, 25, 34) : Color.FromArgb(242, 245, 249);
            Surface = dark ? Color.FromArgb(32, 39, 53) : Color.White;
            Text = dark ? Color.FromArgb(231, 237, 247) : Color.FromArgb(26, 43, 65);
            Muted = dark ? Color.FromArgb(171, 184, 204) : Color.FromArgb(82, 96, 113);
            Border = dark ? Color.FromArgb(66, 79, 99) : Color.FromArgb(203, 213, 225);
            Warning = dark ? Color.FromArgb(255, 201, 103) : Color.FromArgb(145, 95, 15);
            SpecialRow = dark ? Color.FromArgb(33, 57, 53) : Color.FromArgb(229, 244, 239);
            Accent = ColorTranslator.FromHtml(settings.AccentColor);
            Positive = ColorTranslator.FromHtml(settings.PositiveColor);
            Negative = ColorTranslator.FromHtml(settings.NegativeColor);
        }

        private static double Channel(byte value)
        {
            double x = value / 255.0;
            return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        private static double Luminance(Color color)
        {
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }

        public static double Contrast(Color first, Color second)
        {
            double a = Luminance(first), b = Luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        public static Color ReadableText(Color background)
        {
            return Contrast(background, Color.White) >= Contrast(background, Color.Black)
                ? Color.White : Color.Black;
        }
    }
}
