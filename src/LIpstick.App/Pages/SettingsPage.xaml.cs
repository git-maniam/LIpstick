using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };
    }

    private async void ClearDataButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear All Local Data?",
            Content = "This will delete your local config.json, history.json, and encrypted credentials. You will need to re-connect your providers on next launch.",
            PrimaryButtonText = "Confirm Clear",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ClearLocalDataAsync();
        }
    }
}
