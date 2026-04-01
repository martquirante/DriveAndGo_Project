using System;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Eto ang nagse-set ng modern UI graphics
            ApplicationConfiguration.Initialize();

            // Eto ang nag-uutos na LoginForm ang unang bubukas
            Application.Run(new LoginForm());
        }
    }
}