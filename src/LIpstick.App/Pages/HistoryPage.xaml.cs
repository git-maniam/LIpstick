using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LIpstick.Core.Models;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProfileHistoryItem item })
        {
            var dialog = new ContentDialog
            {
                Title = $"Restore Previous {item.SectionName}?",
                Content = $"This will revert your profile {item.SectionName} to:\n\n\"{item.PreviousText}\"",
                PrimaryButtonText = "Confirm Restore",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                await ViewModel.RollbackAsync(item);
            }
        }
    }
}
