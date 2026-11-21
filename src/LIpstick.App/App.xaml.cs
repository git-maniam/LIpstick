using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;
using LIpstick_App.ViewModels;

namespace LIpstick_App;

public partial class App : Application
{
    private Window? _window;
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core
        services.AddSingleton<IHumanizerEngine, HumanizerEngine>();
        services.AddSingleton<IRubricEvaluator, RubricEvaluator>();
        services.AddSingleton<IContentLabService, ContentLabService>();

        // Infrastructure
        services.AddSingleton<ISecureStorageService, DpapiSecureStorageService>();
        services.AddSingleton<ILocalDataService, LocalDataService>();
        services.AddSingleton<ILinkedInClient, MockLinkedInClient>();
        services.AddSingleton<ILinkedInSyncWorker, LinkedInSyncWorker>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<ConnectViewModel>();
        services.AddTransient<ScannerViewModel>();
        services.AddTransient<ImproveViewModel>();
        services.AddTransient<ContentLabViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
