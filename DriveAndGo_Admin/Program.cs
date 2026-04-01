using System;
using System.Windows.Forms;

namespace DriveAndGo_Admin
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            // ══ Dito natin papalitan, LoginForm na ang uunahin ══
            Application.Run(new LoginForm());
        }
    }
}