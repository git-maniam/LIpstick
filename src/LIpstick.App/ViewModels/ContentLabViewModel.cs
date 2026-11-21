using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick_App.ViewModels;

public partial class ContentLabViewModel : ObservableObject
{
    private readonly IContentLabService _contentLab;
    private readonly IHumanizerEngine _humanizer;

    [ObservableProperty]
    private List<SkillToolDefinition> _skills = new();

    [ObservableProperty]
    private SkillToolDefinition? _selectedSkill;

    // Post authoring fields
    [ObservableProperty]
    private string _postTopic = "Automating manual spreadsheets";

    [ObservableProperty]
    private string _postAudience = "B2B Operations Leaders";

    [ObservableProperty]
    private string _postMetric = "12 hours/week";

    [ObservableProperty]
    private string _generatedPost = "";

    [ObservableProperty]
    private string _postStatus = "";

    // DM fields
    [ObservableProperty]
    private string _dmProspect = "Sarah Connor";

    [ObservableProperty]
    private string _dmRole = "Head of Engineering";

    [ObservableProperty]
    private string _dmContext = "your talk on real-time event streaming";

    [ObservableProperty]
    private string _generatedDm = "";

    [ObservableProperty]
    private int _dmLength = 0;

    public ContentLabViewModel(IContentLabService contentLab, IHumanizerEngine humanizer)
    {
        _contentLab = contentLab;
        _humanizer = humanizer;
    }

    public void Initialize()
    {
        Skills = _contentLab.GetSkillDefinitions().ToList();
        if (Skills.Count > 0)
        {
            SelectedSkill = Skills[0];
        }
    }

    [RelayCommand]
    public void GeneratePost()
    {
        var req = new PostDraftRequest(PostTopic, PostAudience, PostMetric, "Hook-01");
        var result = _contentLab.DraftPost(req, _humanizer);
        GeneratedPost = result.FullCopyReadyPost;
        PostStatus = $"✓ Drafted with 0 AI tells. Score: {result.HumanizerScore.OverallScore:F0} {result.HumanizerScore.Verdict}";
    }

    [RelayCommand]
    public void GenerateDmInvite()
    {
        var req = new DmDraftRequest(DmProspect, DmRole, DmContext, true);
        var result = _contentLab.DraftDm(req, _humanizer);
        GeneratedDm = result.DraftMessage;
        DmLength = result.CharacterCount;
    }
}
