using System.Drawing;

namespace DriveAndGo_Admin
{
    public static class ThemeManager
    {
        public static bool IsDarkMode = true; // Default natin ay Dark Mode

        // ══ DARK THEME COLORS ══
        public static Color DarkBackground = Color.FromArgb(24, 24, 28);
        public static Color DarkSidebar = Color.FromArgb(32, 32, 36);
        public static Color DarkHeader = Color.FromArgb(32, 32, 36);
        public static Color DarkText = Color.White;
        public static Color DarkPrimary = Color.FromArgb(108, 92, 231); // Purple/Blue

        // ══ LIGHT THEME COLORS ══
        public static Color LightBackground = Color.FromArgb(245, 246, 250);
        public static Color LightSidebar = Color.White;
        public static Color LightHeader = Color.White;
        public static Color LightText = Color.FromArgb(45, 52, 54);
        public static Color LightPrimary = Color.FromArgb(108, 92, 231);

        // Getters para automatic ang palit kulay
        public static Color CurrentBackground => IsDarkMode ? DarkBackground : LightBackground;
        public static Color CurrentSidebar => IsDarkMode ? DarkSidebar : LightSidebar;
        public static Color CurrentText => IsDarkMode ? DarkText : LightText;
    }
}