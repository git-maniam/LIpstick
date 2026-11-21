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
    [NotifyPropertyChangedFor(nameof(HasHistory))]
    private List<ProfileHistoryItem> _filteredHistoryItems = new();

    public bool HasHistory => FilteredHistoryItems.Count > 0;

    [ObservableProperty]
    private string _sectionFilter = "All sections";

    [ObservableProperty]
    private string _dateFilter = "Last 30 days";

    [ObservableProperty]
    private string _statusFilter = "All statuses";

    public IReadOnlyList<string> Sections { get; } = new[] { "All sections", "Headline", "About", "Experience", "Featured" };
    public IReadOnlyList<string> DateRanges { get; } = new[] { "Last 30 days", "Last 90 days", "All time" };
    public IReadOnlyList<string> Statuses { get; } = new[] { "All statuses", "Applied", "Reverted" };

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
        ApplyFilters();
    }

    partial void OnSectionFilterChanged(string value) => ApplyFilters();
    partial void OnDateFilterChanged(string value) => ApplyFilters();
    partial void OnStatusFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    public void ResetFilters()
    {
        SectionFilter = "All sections";
        DateFilter = "Last 30 days";
        StatusFilter = "All statuses";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<ProfileHistoryItem> query = HistoryItems;
        if (SectionFilter != "All sections")
            query = query.Where(item => item.SectionName.Equals(SectionFilter, StringComparison.OrdinalIgnoreCase));
        if (StatusFilter != "All statuses")
            query = query.Where(item => item.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        DateTime cutoff = DateFilter switch
        {
            "Last 30 days" => DateTime.UtcNow.AddDays(-30),
            "Last 90 days" => DateTime.UtcNow.AddDays(-90),
            _ => DateTime.MinValue
        };
        if (cutoff > DateTime.MinValue)
            query = query.Where(item => item.Timestamp >= cutoff);
        FilteredHistoryItems = query.OrderByDescending(item => item.Timestamp).ToList();
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
