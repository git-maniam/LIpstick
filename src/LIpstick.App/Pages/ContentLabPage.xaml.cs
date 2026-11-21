using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class ContentLabPage : Page
{
    public ContentLabViewModel ViewModel { get; }

    public ContentLabPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ContentLabViewModel>();
        Loaded += (s, e) => ViewModel.Initialize();
    }
}
