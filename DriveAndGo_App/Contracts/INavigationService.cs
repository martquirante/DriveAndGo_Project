namespace DriveAndGo_App.Contracts;

public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);
    Task GoBackAsync();
    Task ResetToShellAsync();
    Task ResetToLoginAsync();
}
