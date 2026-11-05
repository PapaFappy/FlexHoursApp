using System;
using System.Threading;
using System.Windows.Forms;

namespace FlexHours
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool created;
            using (var singleInstance = new Mutex(true, "Local\\FlexHours-Browser-" + Environment.UserName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("FlexHours is already running. Use the open window to avoid conflicting saves.", "FlexHours");
                    return 0;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (sender, args) =>
                    MessageBox.Show("The action could not be completed.\n" + args.Exception.Message, "FlexHours");
                try
                {
                    Application.Run(new MainForm());
                    return 0;
                }
                catch (Exception error)
                {
                    MessageBox.Show("FlexHours could not start.\n" + error.Message, "FlexHours");
                    return 1;
                }
            }
        }
    }
}
