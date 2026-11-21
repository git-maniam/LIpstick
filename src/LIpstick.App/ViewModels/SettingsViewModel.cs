using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;

namespace LIpstick_App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalDataService _dataService;
    private readonly ShellViewModel _shellViewModel;

    [ObservableProperty]
    private string _currentTheme = "Light";

    [ObservableProperty]
    private string _dataDirectory = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public SettingsViewModel(ILocalDataService dataService, ShellViewModel shellViewModel)
    {
        _dataService = dataService;
        _shellViewModel = shellViewModel;
        DataDirectory = _dataService.DataDirectory;
    }

    public async Task InitializeAsync()
    {
        var config = await _dataService.LoadConfigAsync();
        CurrentTheme = config.Theme;
    }

    [RelayCommand]
    public async Task SetThemeAsync(string theme)
    {
        CurrentTheme = theme;
        await _shellViewModel.ChangeThemeAsync(theme);
    }

    [RelayCommand]
    public async Task ClearLocalDataAsync()
    {
        var deleted = await _dataService.ClearLocalDataAsync();
        StatusMessage = $"✓ Removed {deleted.Count} local artifacts: {string.Join(", ", deleted)}";
        await _shellViewModel.InitializeAsync();
    }
}
