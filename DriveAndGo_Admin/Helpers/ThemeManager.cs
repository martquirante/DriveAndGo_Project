using System;
using System.Drawing;

namespace DriveAndGo_Admin.Helpers
{
    public static class ThemeManager
    {
        // Backing field for theme mode
        private static bool _isDarkMode = true;

        // Event raised when the theme changes so UI can update immediately
        public static event EventHandler ThemeChanged;

        // Default to Dark Mode
        public static bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode == value) return;
                _isDarkMode = value;
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        // ── Base Colors (Upgraded Deep-Space Palette) ──────────────────────────────
        // Dark:  deep space navy / obsidian  |  Light: clean off-white
        public static Color CurrentBackground   => IsDarkMode ? Color.FromArgb(11,  11,  22)  : Color.FromArgb(242, 244, 255);
        public static Color CurrentSidebar      => IsDarkMode ? Color.FromArgb(8,   8,   18)  : Color.FromArgb(255, 255, 255);
        public static Color CurrentCard         => IsDarkMode ? Color.FromArgb(18,  18,  34)  : Color.FromArgb(255, 255, 255);
        public static Color CurrentCardHover    => IsDarkMode ? Color.FromArgb(24,  24,  44)  : Color.FromArgb(248, 249, 255);
        public static Color CurrentText         => IsDarkMode ? Color.FromArgb(225, 228, 248) : Color.FromArgb(15,  15,  30);
        public static Color CurrentSubText      => IsDarkMode ? Color.FromArgb(105, 110, 148) : Color.FromArgb(110, 115, 145);
        public static Color CurrentBorder       => IsDarkMode ? Color.FromArgb(32,  33,  58)  : Color.FromArgb(218, 220, 240);
        public static Color CurrentInputBg      => IsDarkMode ? Color.FromArgb(18,  18,  34)  : Color.FromArgb(235, 238, 250);

        // ── Accent / Brand ──────────────────────────────────────────────────────────
        // Electric neon-orange — premium, more vibrant than the old #E65100
        public static Color CurrentPrimary      => Color.FromArgb(255, 90,  31);
        public static Color CurrentPrimaryGlow  => Color.FromArgb(255, 130, 65);
        public static Color CurrentPrimaryDark  => Color.FromArgb(200, 60,  0);

        // ── Status accent palette ────────────────────────────────────────────────────
        public static Color CurrentAccentGreen  => Color.FromArgb(34,  211, 100);
        public static Color CurrentAccentBlue   => Color.FromArgb(59,  130, 246);
        public static Color CurrentAccentPurple => Color.FromArgb(168, 85,  247);
        public static Color CurrentAccentRed    => Color.FromArgb(239, 68,  68);
        public static Color CurrentAccentYellow => Color.FromArgb(234, 179, 8);

        // ── Glass / Glow Tokens ──────────────────────────────────────────────────────
        // Alpha values for glassmorphism layers
        public static int GlassAlpha        => IsDarkMode ? 16 : 90;
        public static int GlassBorderAlpha  => IsDarkMode ? 38 : 60;
        public static int SidebarGlowAlpha  => IsDarkMode ? 22 : 0;

        // Sidebar gradient (top→bottom)
        public static Color SidebarGradientTop => IsDarkMode ? Color.FromArgb(14, 14, 28) : Color.FromArgb(255, 255, 255);
        public static Color SidebarGradientBot => IsDarkMode ? Color.FromArgb(6,   6, 14) : Color.FromArgb(248, 249, 255);

        // Mouse-tracking radial glow cast on the content panel
        public static Color RadialGlowColor    => Color.FromArgb(10, 255, 90, 31); // subtle orange

        // Drop-shadow color for cards
        public static Color ShadowColor        => IsDarkMode ? Color.FromArgb(0, 0, 0) : Color.FromArgb(100, 120, 180);

        // ── Sidebar active-item pill background ─────────────────────────────────────
        public static Color NavActiveBg        => IsDarkMode
            ? Color.FromArgb(28, 255, 90, 31)
            : Color.FromArgb(22, 255, 90, 31);
    }
}
