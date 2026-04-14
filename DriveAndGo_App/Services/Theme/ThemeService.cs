using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;

namespace DriveAndGo_App.Services.Theme;

public sealed class ThemeService : IThemeService
{
    private const string ThemePreferenceKey = "driveandgo.theme-mode";

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public string AutoModeLabel => "Auto mode is prepared for future sunrise or schedule-based theme switching.";

    public Task InitializeAsync()
    {
        var storedMode = Preferences.Default.Get(ThemePreferenceKey, nameof(ThemeMode.System));
        CurrentMode = Enum.TryParse<ThemeMode>(storedMode, true, out var themeMode)
            ? themeMode
            : ThemeMode.System;

        Apply(CurrentMode);
        return Task.CompletedTask;
    }

    public Task SetThemeAsync(ThemeMode mode)
    {
        CurrentMode = mode;
        Preferences.Default.Set(ThemePreferenceKey, mode.ToString());
        Apply(mode);
        return Task.CompletedTask;
    }

    public Task ToggleBetweenLightAndDarkAsync()
    {
        var nextMode = CurrentMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        return SetThemeAsync(nextMode);
    }

    private static void Apply(ThemeMode mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            ThemeMode.Auto => AppTheme.Unspecified,
            _ => AppTheme.Unspecified
        };
    }
}
