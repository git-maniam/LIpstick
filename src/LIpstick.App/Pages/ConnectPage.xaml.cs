using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using LIpstick_App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

    private async void ChooseLinkedInPdf_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".pdf");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            await ViewModel.ImportLinkedInPdfAsync(file.Path);
    }
}
