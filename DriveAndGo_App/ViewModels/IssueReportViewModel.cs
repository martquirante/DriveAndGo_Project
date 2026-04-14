using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class IssueReportViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private readonly IFileUploadService _fileUploadService;

    private RentalItem? _rental;
    private string _selectedIssueType = "General";
    private string _description = string.Empty;
    private string? _imageUrl;

    public IssueReportViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        IFileUploadService fileUploadService,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        _fileUploadService = fileUploadService;
        IssueTypes = new ObservableCollection<string>(new[] { "General", "Breakdown", "Accident", "Payment", "Behavior" });
        UploadImageCommand = new AsyncCommand(UploadImageAsync, () => !IsBusy);
        SubmitCommand = new AsyncCommand(SubmitAsync, () => !IsBusy);
    }

    public ObservableCollection<string> IssueTypes { get; }
    public AsyncCommand UploadImageCommand { get; }
    public AsyncCommand SubmitCommand { get; }

    public RentalItem? Rental
    {
        get => _rental;
        private set => SetProperty(ref _rental, value);
    }

    public string SelectedIssueType
    {
        get => _selectedIssueType;
        set => SetProperty(ref _selectedIssueType, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string? ImageUrl
    {
        get => _imageUrl;
        set => SetProperty(ref _imageUrl, value);
    }

    public Task LoadAsync(RentalItem? rental)
    {
        Rental = rental;
        return Task.CompletedTask;
    }

    private async Task UploadImageAsync()
    {
        ImageUrl = await _fileUploadService.PickAndUploadAsync(UploadCategory.IssueImage);
    }

    private async Task SubmitAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (Rental == null || _sessionState.CurrentUser == null)
            {
                throw new InvalidOperationException("Issue reporting is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                throw new InvalidOperationException("Describe the issue first.");
            }

            await _apiService.ReportIssueAsync(new CreateIssueRequestDto
            {
                RentalId = Rental.RentalId,
                ReporterId = _sessionState.CurrentUser.UserId,
                IssueType = SelectedIssueType,
                Description = Description.Trim(),
                ImageUrl = ImageUrl
            });

            await NavigationService.GoBackAsync();
        }, "Submitting issue report...");
    }
}
