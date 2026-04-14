using DriveAndGo_App.Contracts;

namespace DriveAndGo_App.Utilities;

public abstract class ViewModelBase : ObservableObject
{
    protected readonly INavigationService NavigationService;

    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _infoMessage = string.Empty;

    protected ViewModelBase(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string InfoMessage
    {
        get => _infoMessage;
        set => SetProperty(ref _infoMessage, value);
    }

    protected async Task RunBusyAsync(Func<Task> operation, string? infoMessage = null)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            InfoMessage = infoMessage ?? string.Empty;
            await operation();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
