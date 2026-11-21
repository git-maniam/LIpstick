using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.AI;
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
        AppContext.SetSwitch("OpenAI.DisableTelemetry", true);
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
        services.AddSingleton<LinkedInOAuthClient>();
        services.AddSingleton<ILinkedInClient>(sp => sp.GetRequiredService<LinkedInOAuthClient>());
        services.AddSingleton<ILinkedInSyncWorker, LinkedInSyncWorker>();

        // AI Providers & Hub
        services.AddSingleton<GeminiProvider>();
        services.AddSingleton<OpenAIProvider>();
        services.AddSingleton<MockAIProvider>();
        services.AddSingleton<IAIProvider>(sp => sp.GetRequiredService<GeminiProvider>());
        services.AddSingleton<IAIProvider>(sp => sp.GetRequiredService<OpenAIProvider>());
        services.AddSingleton<IAIHubService, AIHubService>();

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
