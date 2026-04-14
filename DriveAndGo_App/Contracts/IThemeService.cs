using DriveAndGo_App.Models;

namespace DriveAndGo_App.Contracts;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }
    string AutoModeLabel { get; }
    Task InitializeAsync();
    Task SetThemeAsync(ThemeMode mode);
    Task ToggleBetweenLightAndDarkAsync();
}
