using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.State;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class MessagesViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly AppSessionState _sessionState;
    private readonly IFileUploadService _fileUploadService;

    private RentalItem? _rental;
    private string _draftMessage = string.Empty;
    private string? _attachmentUrl;

    public MessagesViewModel(
        IDriveAndGoApiService apiService,
        AppSessionState sessionState,
        IFileUploadService fileUploadService,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        _fileUploadService = fileUploadService;
        Messages = new ObservableCollection<MessageItem>();
        SendCommand = new AsyncCommand(SendAsync, () => !IsBusy);
        AttachFileCommand = new AsyncCommand(AttachAsync, () => !IsBusy);
    }

    public ObservableCollection<MessageItem> Messages { get; }
    public AsyncCommand SendCommand { get; }
    public AsyncCommand AttachFileCommand { get; }

    public RentalItem? Rental
    {
        get => _rental;
        private set => SetProperty(ref _rental, value);
    }

    public string DraftMessage
    {
        get => _draftMessage;
        set => SetProperty(ref _draftMessage, value);
    }

    public string? AttachmentUrl
    {
        get => _attachmentUrl;
        set => SetProperty(ref _attachmentUrl, value);
    }

    public async Task LoadAsync(RentalItem? rental)
    {
        if (rental == null)
        {
            return;
        }

        Rental = rental;
        await RefreshMessagesAsync();
    }

    private async Task RefreshMessagesAsync()
    {
        await RunBusyAsync(async () =>
        {
            Messages.Clear();

            if (Rental == null || _sessionState.CurrentUser == null)
            {
                return;
            }

            foreach (var message in await _apiService.GetMessagesAsync(Rental.RentalId))
            {
                message.IsMine = message.SenderId == _sessionState.CurrentUser.UserId;
                Messages.Add(message);
            }
        }, "Loading chat...");
    }

    private async Task AttachAsync()
    {
        AttachmentUrl = await _fileUploadService.PickAndUploadAsync(UploadCategory.MessageAttachment);
    }

    private async Task SendAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (Rental == null || _sessionState.CurrentUser == null)
            {
                throw new InvalidOperationException("Messaging is unavailable for this booking.");
            }

            if (string.IsNullOrWhiteSpace(DraftMessage) && string.IsNullOrWhiteSpace(AttachmentUrl))
            {
                throw new InvalidOperationException("Write a message or attach a file first.");
            }

            await _apiService.SendMessageAsync(new SendMessageRequestDto
            {
                RentalId = Rental.RentalId,
                SenderId = _sessionState.CurrentUser.UserId,
                Content = DraftMessage.Trim(),
                AttachmentUrl = AttachmentUrl
            });

            DraftMessage = string.Empty;
            AttachmentUrl = null;
            await RefreshMessagesAsync();
        }, "Sending message...");
    }
}
