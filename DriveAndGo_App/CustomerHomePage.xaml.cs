using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;

namespace DriveAndGo_App
{
    public partial class CustomerHomePage : ContentPage
    {
        // Ito ang magha-handle ng listahan na lalabas sa UI
        public ObservableCollection<AppVehicle> AvailableVehicles { get; set; } = new ObservableCollection<AppVehicle>();

        public CustomerHomePage()
        {
            InitializeComponent();

            // I-connect ang UI (cvVehicles) sa listahan natin (AvailableVehicles)
            cvVehicles.ItemsSource = AvailableVehicles;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadSampleVehicles();
        }

        private void LoadSampleVehicles()
        {
            // Kapag ready na ang API mo, dito natin ilalagay ang pag-fetch ng data from MySQL
            if (AvailableVehicles.Count == 0)
            {
                AvailableVehicles.Add(new AppVehicle
                {
                    Id = 1,
                    Name = "BMW 5 Series",
                    PlateNo = "SAD-4458",
                    PricePerDay = 3500,
                    PhotoUrl = "car_logo.png"
                });

                AvailableVehicles.Add(new AppVehicle
                {
                    Id = 2,
                    Name = "Nissan Altima",
                    PlateNo = "ALT-2022",
                    PricePerDay = 2500,
                    PhotoUrl = "car_logo.png"
                });

                AvailableVehicles.Add(new AppVehicle
                {
                    Id = 3,
                    Name = "Toyota Hiace Van",
                    PlateNo = "VAN-777",
                    PricePerDay = 4500,
                    PhotoUrl = "car_logo.png"
                });
            }
        }

        private async void OnVehicleSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is AppVehicle selectedVehicle)
            {
                // TODO: Pupunta sa Booking Summary / Details Page
                await DisplayAlert("Vehicle Selected", $"You selected {selectedVehicle.Name}.\nRedirecting to Booking Page...", "OK");

                // Para mawala yung gray highlight kapag na-click
                ((CollectionView)sender).SelectedItem = null;
            }
        }
    }

    // Model para sa Sasakyan
    public class AppVehicle
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PlateNo { get; set; }
        public decimal PricePerDay { get; set; }
        public string PhotoUrl { get; set; }

        // C# Trick: Formatting agad ng string para madaling i-bind sa XAML
        public string PriceString => $"₱{PricePerDay:N0} / Day";
    }
}