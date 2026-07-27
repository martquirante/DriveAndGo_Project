using System;
using System.Threading;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the DriveAndGo Admin WinForms application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Global Exception Handling to prevent silent crashes
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (sender, e) =>
            {
                MessageBox.Show(
                    $"An unhandled UI thread exception occurred:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
                    "DriveAndGo Admin — UI Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"A fatal domain exception occurred:\n\n{ex?.Message ?? "Unknown Error"}\n\nStack Trace:\n{ex?.StackTrace}",
                    "DriveAndGo Admin — Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            // Bulletproof, classic WinForms initialization
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Run the login form entry point
            Application.Run(new LoginForm());
        }
    }
}