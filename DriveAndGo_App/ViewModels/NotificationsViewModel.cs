using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class NotificationsViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;

    public NotificationsViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        Notifications = new ObservableCollection<AppNotificationModel>();
        RefreshCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
    }

    public ObservableCollection<AppNotificationModel> Notifications { get; }
    public AsyncCommand RefreshCommand { get; }
    public bool HasNotifications => Notifications.Count > 0;

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            Notifications.Clear();

            if (_sessionState.CurrentUser == null)
            {
                return;
            }

            foreach (var notification in await _apiService.GetNotificationsAsync(_sessionState.CurrentUser.UserId))
            {
                Notifications.Add(notification);
            }

            OnPropertyChanged(nameof(HasNotifications));
        }, "Loading notifications...");
    }

    public async Task OpenNotificationAsync(AppNotificationModel? notification)
    {
        if (notification == null || notification.IsRead)
        {
            return;
        }

        await _apiService.MarkNotificationReadAsync(notification.NotifId);
        notification.IsRead = true;
        OnPropertyChanged(nameof(HasNotifications));
    }
}
