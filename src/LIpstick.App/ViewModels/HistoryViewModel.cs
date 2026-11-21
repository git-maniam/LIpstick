using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick_App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ILocalDataService _dataService;
    private readonly ILinkedInSyncWorker _syncWorker;

    [ObservableProperty]
    private List<ProfileHistoryItem> _historyItems = new();

    [ObservableProperty]
    private string _statusMessage = "";

    public HistoryViewModel(ILocalDataService dataService, ILinkedInSyncWorker syncWorker)
    {
        _dataService = dataService;
        _syncWorker = syncWorker;
    }

    public async Task InitializeAsync()
    {
        var log = await _dataService.LoadHistoryAsync();
        HistoryItems = log.Items.OrderByDescending(i => i.Timestamp).ToList();
    }

    [RelayCommand]
    public async Task RollbackAsync(ProfileHistoryItem item)
    {
        StatusMessage = $"Restoring {item.SectionName} to previous state...";
        var result = item.SectionName.ToLowerInvariant() switch
        {
            "headline" => await _syncWorker.UpdateHeadlineAsync(item.PreviousText),
            "about" => await _syncWorker.UpdateAboutAsync(item.PreviousText),
            _ => await _syncWorker.UpdateHeadlineAsync(item.PreviousText)
        };

        if (result.Outcome == UpdateOutcome.Succeeded)
        {
            StatusMessage = $"✓ Restored {item.SectionName} successfully!";
            await InitializeAsync();
        }
        else
        {
            StatusMessage = $"Failed to restore: {result.Message}";
        }
    }
}
