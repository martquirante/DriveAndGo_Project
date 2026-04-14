using DriveAndGo_App.Configuration;
using DriveAndGo_App.Models;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App.Views.Shared;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _viewModel;

    public NotificationsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<NotificationsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnNotificationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AppNotificationModel notification)
        {
            await _viewModel.OpenNotificationAsync(notification);
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
}
