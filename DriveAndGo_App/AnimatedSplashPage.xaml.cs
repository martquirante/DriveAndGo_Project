using DriveAndGo_App.Configuration;
using DriveAndGo_App.ViewModels;

namespace DriveAndGo_App;

public partial class AnimatedSplashPage : ContentPage
{
    private readonly SplashViewModel _viewModel;
    private bool _played;

    public AnimatedSplashPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = AppServices.GetRequiredService<SplashViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_played)
        {
            return;
        }

        _played = true;
        LogoFrame.Scale = 0.85;
        TitleLabel.TranslationY = 18;
        SubtitleLabel.TranslationY = 18;

        await Task.WhenAll(
            LogoImage.FadeToAsync(1, 450, Easing.CubicOut),
            LogoFrame.ScaleToAsync(1.0, 450, Easing.CubicOut));

        await Task.WhenAll(
            TitleLabel.FadeToAsync(1, 350, Easing.CubicOut),
            TitleLabel.TranslateToAsync(0, 0, 350, Easing.CubicOut));

        await Task.WhenAll(
            SubtitleLabel.FadeToAsync(1, 350, Easing.CubicOut),
            SubtitleLabel.TranslateToAsync(0, 0, 350, Easing.CubicOut));

        await _viewModel.InitializeAsync();
    }
}
