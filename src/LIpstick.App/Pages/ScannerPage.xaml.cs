using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class ScannerPage : Page
{
    public ScannerViewModel ViewModel { get; }

    public ScannerPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ScannerViewModel>();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }
}
