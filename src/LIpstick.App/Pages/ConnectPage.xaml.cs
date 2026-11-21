using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class ConnectPage : Page
{
    public ConnectViewModel ViewModel { get; }

    public ConnectPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ConnectViewModel>();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }
}
