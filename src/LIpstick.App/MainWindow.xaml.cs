using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using LIpstick.Core.Models;
using LIpstick_App.Pages;
using LIpstick_App.ViewModels;

namespace LIpstick_App;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/LIpstick-Logo.png");
        const int initialWidth = 1280;
        const int initialHeight = 850;
        AppWindow.Resize(new SizeInt32(initialWidth, initialHeight));
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - initialWidth) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - initialHeight) / 2)));

        NavFrame.Navigated += NavFrame_Navigated;
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.CurrentTheme))
            {
                ApplyTheme(ViewModel.CurrentTheme);
            }
        };
    }

    private void NavFrame_Navigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = NavFrame.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
        }
        else
        {
            var tag = e.SourcePageType switch
            {
                var t when t == typeof(ConnectPage) => "connect",
                var t when t == typeof(ScannerPage) => "scan",
                var t when t == typeof(ImprovePage) => "diff",
                var t when t == typeof(ContentLabPage) => "content",
                var t when t == typeof(HistoryPage) => "history",
                _ => null
            };

            if (tag != null)
            {
                NavView.SelectedItem = NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == tag);
            }
        }
    }

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        ApplyTheme(ViewModel.CurrentTheme);
        NavFrame.Navigate(typeof(ConnectPage));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "connect":
                    NavFrame.Navigate(typeof(ConnectPage));
                    break;
                case "scan":
                    NavFrame.Navigate(typeof(ScannerPage));
                    break;
                case "diff":
                    NavFrame.Navigate(typeof(ImprovePage));
                    break;
                case "content":
                    NavFrame.Navigate(typeof(ContentLabPage));
                    break;
                case "history":
                    NavFrame.Navigate(typeof(HistoryPage));
                    break;
                default:
                    NavFrame.Navigate(typeof(ScannerPage));
                    break;
            }
        }
    }

    private async void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        string nextTheme = ViewModel.CurrentTheme switch
        {
            "Light" => "Dark",
            "Dark" => "System",
            _ => "Light"
        };
        await ViewModel.ChangeThemeAsync(nextTheme);
        ApplyTheme(nextTheme);
    }

    private void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Dark" => ElementTheme.Dark,
                "Light" => ElementTheme.Light,
                _ => ElementTheme.Default
            };
        }
    }

    private async void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is ModelInfo modelInfo)
        {
            await ViewModel.SelectModelAsync(modelInfo.Id);
        }
        else if (ModelSelector.SelectedItem is string model)
        {
            await ViewModel.SelectModelAsync(model);
        }
    }
}
