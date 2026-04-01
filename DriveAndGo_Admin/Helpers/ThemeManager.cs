using System.Drawing;

namespace DriveAndGo_Admin.Helpers
{
    public static class ThemeManager
    {
        public static bool IsDarkMode { get; set; } = true;

        // ── Dark theme colors ──
        public static Color DarkBackground = Color.FromArgb(18, 18, 24);
        public static Color DarkSidebar = Color.FromArgb(26, 26, 36);
        public static Color DarkCard = Color.FromArgb(30, 30, 42);
        public static Color DarkPrimary = Color.FromArgb(99, 102, 241);
        public static Color DarkText = Color.FromArgb(240, 240, 255);
        public static Color DarkSubText = Color.FromArgb(140, 140, 170);
        public static Color DarkBorder = Color.FromArgb(50, 50, 70);
        public static Color DarkAccent = Color.FromArgb(34, 197, 94);

        // ── Light theme colors ──
        public static Color LightBackground = Color.FromArgb(245, 247, 255);
        public static Color LightSidebar = Color.FromArgb(255, 255, 255);
        public static Color LightCard = Color.FromArgb(255, 255, 255);
        public static Color LightPrimary = Color.FromArgb(99, 102, 241);
        public static Color LightText = Color.FromArgb(20, 20, 40);
        public static Color LightSubText = Color.FromArgb(100, 100, 130);
        public static Color LightBorder = Color.FromArgb(220, 220, 235);
        public static Color LightAccent = Color.FromArgb(22, 163, 74);

        // ── Dynamic getters ──
        public static Color CurrentBackground =>
            IsDarkMode ? DarkBackground : LightBackground;
        public static Color CurrentSidebar =>
            IsDarkMode ? DarkSidebar : LightSidebar;
        public static Color CurrentCard =>
            IsDarkMode ? DarkCard : LightCard;
        public static Color CurrentPrimary =>
            IsDarkMode ? DarkPrimary : LightPrimary;
        public static Color CurrentText =>
            IsDarkMode ? DarkText : LightText;
        public static Color CurrentSubText =>
            IsDarkMode ? DarkSubText : LightSubText;
        public static Color CurrentBorder =>
            IsDarkMode ? DarkBorder : LightBorder;
        public static Color CurrentAccent =>
            IsDarkMode ? DarkAccent : LightAccent;
    }
}