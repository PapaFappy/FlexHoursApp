using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace FlexHours
{
    public static class TestProgram
    {
        private static readonly List<string> report = new List<string>();
        private static int failed;
        private static int total;

        private static void Run(string name, Action test)
        {
            total++;
            try { test(); report.Add("PASS  " + name); }
            catch (Exception error) { failed++; report.Add("FAIL  " + name + Environment.NewLine + error); }
        }

        [STAThread]
        public static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Run("Work hours, holidays, leave credits and month boundaries", WorkHoursTests.Run);
            Run("Browser summary parsing, periods, subtotals and allowed company", SummaryParserTests.Run);
            Run("Signed carry caps, quarterly resets, year boundary and dependent months", FlexLedgerTests.Run);
            Run("Legacy migration, atomic saves, backup and corruption recovery", StateFileTests.Run);
            Run("Appearance validation, contrast and saved preferences", AppearanceTests.Run);
            Run("Malformed state, invalid totals/dates and unsafe browser input", RobustnessTests.Run);
            Run("Actual desktop save, close and reopen", MainForm.TestSavedWindow);
            Run("Actual desktop carry-over display and recalculation", MainForm.TestFlexWindow);
            Run("Light/dark windows, custom colours and bounded animations", MainForm.TestAppearanceWindow);
            Run("21-year weekday oracle, official 2026 holidays, 144-month ledger and culture independence", CalculationAuditTests.Run);
            Run("Import confirmation, failed reads, stale-result prevention and close cancellation", BrowserImportForm.TestImportWorkflow);
            Run("Dashboard missing totals, pending edits, navigation, stale imports and manual mode", MainForm.TestDashboardAudit);
            Run("Dated card/table PTO, sick, holidays, month boundaries and full reconciliation", ChromeTests.TestDailyRows);
            Run("Chrome native message framing, origin validation and minimal snapshot persistence", ChromeTests.TestNativeProtocol);
            Run("Chrome opt-in, duplicates, stale snapshots and dated-calendar precedence", ChromeTests.TestApply);
            Run("Actual desktop Chrome file watcher, dated details, manual pause and saved configuration", MainForm.TestChromeDesktop);
            Run("Reordered/blank daily cards, partial readings and future-month vacation persistence", ChromeMonthViewTests.Run);
            Run("Calendar selection, hidden-tab refresh, six-week months, leap days and unknown readings", MainForm.TestCalendarDashboard);
            Run("Live monthly summary, active-day mismatch, independent future credits and visible ahead balance", MainForm.TestLiveSummaryBalance);
            report.Add("");
            report.Add((total - failed) + "/" + total + " suites passed.");
            report.Add("Live BambooHR pages/network access are not exercised by these tests.");
            File.WriteAllLines("test-results.txt", report);
            foreach (string line in report) Console.WriteLine(line);
            return failed == 0 ? 0 : 1;
        }
    }
}
