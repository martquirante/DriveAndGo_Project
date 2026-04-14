using DriveAndGo_App.Configuration;
using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using DriveAndGo_App.Utilities;
using System.Collections.ObjectModel;

namespace DriveAndGo_App.ViewModels;

public sealed class CustomerHomeViewModel : ViewModelBase
{
    private readonly IDriveAndGoApiService _apiService;
    private readonly List<VehicleItem> _allVehicles = new();
    private string _searchText = string.Empty;
    private string _selectedCategory = "All";
    private string _selectedSort = "Recommended";
    private bool _isLoaded;

    public CustomerHomeViewModel(IDriveAndGoApiService apiService, INavigationService navigationService)
        : base(navigationService)
    {
        _apiService = apiService;
        Vehicles = new ObservableCollection<VehicleItem>();
        Categories = new ObservableCollection<string>(new[] { "All", "Bicycle", "Motorcycle", "Car", "Van", "Truck" });
        SortOptions = new ObservableCollection<string>(new[] { "Recommended", "Price: Low to High", "Capacity", "Newest" });
        RefreshCommand = new AsyncCommand(() => LoadAsync(forceRefresh: true), () => !IsBusy);
    }

    public ObservableCollection<VehicleItem> Vehicles { get; }
    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<string> SortOptions { get; }
    public AsyncCommand RefreshCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool HasVehicles => Vehicles.Count > 0;
    public VehicleItem? FeaturedVehicle => Vehicles.FirstOrDefault();

    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (_isLoaded && !forceRefresh)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            _allVehicles.Clear();
            _allVehicles.AddRange(await _apiService.GetAvailableVehiclesAsync());
            _isLoaded = true;
            ApplyFilters();
        }, "Loading vehicles...");
    }

    public async Task OpenVehicleAsync(VehicleItem? vehicle)
    {
        if (vehicle == null)
        {
            return;
        }

        await NavigationService.GoToAsync(AppRoutes.VehicleDetails, new Dictionary<string, object>
        {
            ["Vehicle"] = vehicle
        });
    }

    private void ApplyFilters()
    {
        IEnumerable<VehicleItem> filtered = _allVehicles;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(vehicle =>
                vehicle.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                vehicle.PlateNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                vehicle.CategoryLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(vehicle => string.Equals(vehicle.CategoryLabel, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedSort switch
        {
            "Price: Low to High" => filtered.OrderBy(vehicle => vehicle.RatePerDay),
            "Capacity" => filtered.OrderByDescending(vehicle => vehicle.SeatCapacity),
            "Newest" => filtered.OrderByDescending(vehicle => vehicle.CreatedAt ?? DateTime.MinValue),
            _ => filtered.OrderBy(vehicle => vehicle.Brand).ThenBy(vehicle => vehicle.Model)
        };

        Vehicles.Clear();
        foreach (var vehicle in filtered)
        {
            Vehicles.Add(vehicle);
        }

        OnPropertyChanged(nameof(HasVehicles));
        OnPropertyChanged(nameof(FeaturedVehicle));
    }
}
