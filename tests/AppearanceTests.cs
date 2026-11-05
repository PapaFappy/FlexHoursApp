using System;
using System.Drawing;
using System.IO;

namespace FlexHours
{
    public static class AppearanceTests
    {
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
        }

        public static void Run()
        {
            Check(AppearanceSettings.IsColor("#123aBc"), "Valid custom colour");
            foreach (string bad in new[] { "red", "#123", "#GG0000", "#11223344", "", null })
                Check(!AppearanceSettings.IsColor(bad), "Reject malformed colour");
            Check(Math.Abs(ThemePalette.Contrast(Color.Black, Color.White) - 21) < 0.01, "Contrast calculation");
            foreach (bool dark in new[] { false, true })
            {
                var settings = AppearanceSettings.Defaults(dark);
                var palette = new ThemePalette(settings);
                Check(ThemePalette.Contrast(palette.Text, palette.Background) > 7, "Readable primary text");
                Check(ThemePalette.Contrast(palette.Positive, palette.Surface) >= 3, "Readable positive balance");
                Check(ThemePalette.Contrast(palette.Negative, palette.Surface) >= 3, "Readable negative balance");
                Check(ThemePalette.Contrast(palette.Accent, ThemePalette.ReadableText(palette.Accent)) >= 4.5, "Readable accent button");
            }
            var repaired = AppearanceSettings.Defaults(true);
            repaired.AccentColor = "bad";
            repaired.PositiveColor = "#202735";
            repaired.Normalize();
            Check(AppearanceSettings.IsColor(repaired.AccentColor), "Bad stored colour repaired");
            Check(repaired.PositiveColor != "#202735", "Invisible stored balance colour repaired");
            var custom = AppearanceSettings.Defaults(false);
            var previousStock = AppearanceSettings.Defaults(true);
            previousStock.AccentColor = "#A78BFA";
            previousStock.Normalize();
            Check(previousStock.AccentColor == "#729FFF", "Earlier blue stock colour was not restored");
            custom.AccentColor = "#AA77FF";
            custom.AnimationsEnabled = false;
            var switched = custom.WithDarkMode(true);
            Check(switched.DarkMode && switched.AccentColor == "#AA77FF" && !switched.AnimationsEnabled, "Theme switch discarded custom preferences");
            Check(switched.PositiveColor == AppearanceSettings.Defaults(true).PositiveColor, "Default balance colours did not adapt to theme");

            string folder = Path.Combine(Environment.CurrentDirectory, "appearance-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "months.json");
            try
            {
                var store = new Store();
                store.Appearance = AppearanceSettings.Defaults(true);
                store.Appearance.AccentColor = "#AA77FF";
                store.Appearance.AnimationsEnabled = false;
                StateFile.Save(path, store);
                bool recovered;
                var restored = StateFile.Load(path, out recovered);
                Check(restored.Appearance.DarkMode && !restored.Appearance.AnimationsEnabled && restored.Appearance.AccentColor == "#AA77FF", "Theme settings survive restart");
            }
            finally
            {
                foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
}
