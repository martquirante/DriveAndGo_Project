using DriveAndGo_App.Contracts;
using DriveAndGo_App.Dtos;
using DriveAndGo_App.Models;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class PaymentViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly IFileUploadService _fileUploadService;

    private RentalItem? _rental;
    private string _selectedMethod = "cash";
    private string? _proofUrl;

    public PaymentViewModel(
        IDriveAndGoApiService apiService,
        IFileUploadService fileUploadService,
        INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        _fileUploadService = fileUploadService;
        PaymentMethods = new ObservableCollection<string>(new[] { "cash", "gcash", "maya", "bank" });
        UploadProofCommand = new AsyncCommand(UploadProofAsync, () => !IsBusy);
        SubmitPaymentCommand = new AsyncCommand(SubmitPaymentAsync, () => !IsBusy);
    }

    public RentalItem? Rental
    {
        get => _rental;
        private set
        {
            if (SetProperty(ref _rental, value))
            {
                OnPropertyChanged(nameof(AmountLabel));
            }
        }
    }

    public ObservableCollection<string> PaymentMethods { get; }
    public AsyncCommand UploadProofCommand { get; }
    public AsyncCommand SubmitPaymentCommand { get; }

    public string SelectedMethod
    {
        get => _selectedMethod;
        set
        {
            if (SetProperty(ref _selectedMethod, value))
            {
                OnPropertyChanged(nameof(ProofIsRequired));
            }
        }
    }

    public string? ProofUrl
    {
        get => _proofUrl;
        set => SetProperty(ref _proofUrl, value);
    }

    public bool ProofIsRequired => !string.Equals(SelectedMethod, "cash", StringComparison.OrdinalIgnoreCase);
    public string AmountLabel => Rental == null ? "PHP 0" : Rental.TotalAmountLabel;

    public Task LoadAsync(RentalItem? rental)
    {
        Rental = rental;
        return Task.CompletedTask;
    }

    private async Task UploadProofAsync()
    {
        ProofUrl = await _fileUploadService.PickAndUploadAsync(UploadCategory.PaymentProof);
    }

    private async Task SubmitPaymentAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (Rental == null)
            {
                throw new InvalidOperationException("Rental details are missing.");
            }

            if (ProofIsRequired && string.IsNullOrWhiteSpace(ProofUrl))
            {
                throw new InvalidOperationException("Upload a payment proof for the selected method.");
            }

            await _apiService.SubmitPaymentAsync(new CreateTransactionRequestDto
            {
                RentalId = Rental.RentalId,
                Amount = Rental.TotalAmount,
                Method = SelectedMethod,
                ProofUrl = ProofUrl
            });

            await NavigationService.GoBackAsync();
        }, "Submitting payment...");
    }
}
