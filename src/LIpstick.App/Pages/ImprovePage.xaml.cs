using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using LIpstick_App.ViewModels;

namespace LIpstick_App.Pages;

public sealed partial class ImprovePage : Page
{
    public ImproveViewModel ViewModel { get; }

    public ImprovePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ImproveViewModel>();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }

    private void ToBeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            ViewModel.ToBeText = tb.Text;
        }
    }
}
