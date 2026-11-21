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
            ThemeRadioButtons.SelectedIndex = ViewModel.CurrentTheme switch
            {
                "Dark" => 1,
                "System" => 2,
                _ => 0
            };
        };
    }

    private async void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is RadioButtons rb && rb.SelectedItem is RadioButton { Tag: string theme })
        {
            if (theme != ViewModel.CurrentTheme)
            {
                await ViewModel.SetThemeAsync(theme);
            }
        }
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
