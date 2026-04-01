using System.Drawing;

namespace DriveAndGo_Admin.Helpers
{
    public static class ThemeManager
    {
        // Default to Dark Mode
        public static bool IsDarkMode { get; set; } = true;

        // Base Colors
        public static Color CurrentBackground => IsDarkMode ? Color.FromArgb(27, 27, 41) : Color.FromArgb(245, 246, 250);
        public static Color CurrentSidebar => IsDarkMode ? Color.FromArgb(21, 21, 33) : Color.White;
        public static Color CurrentText => IsDarkMode ? Color.White : Color.Black;

        // MGA BAGONG DAGDAG NA KULAY NA HINAHANAP NI MAINFORM
        public static Color CurrentPrimary => Color.FromArgb(230, 81, 0);       // Orange (Drive&Go Logo color)
        public static Color CurrentSubText => IsDarkMode ? Color.Gray : Color.DarkGray; // Gray para sa mga sub-labels
        public static Color CurrentBorder => IsDarkMode ? Color.FromArgb(50, 50, 65) : Color.LightGray; // Lines at dividers
        public static Color CurrentAccent => Color.FromArgb(46, 204, 113);      // Green (para sa Online status indicator)
        public static Color CurrentCard => IsDarkMode ? Color.FromArgb(39, 41, 61) : Color.White;       // Background ng mga summary cards
    }
}