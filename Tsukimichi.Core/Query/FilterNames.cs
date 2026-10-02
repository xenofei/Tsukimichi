using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Query;

/// <summary>
/// The names the empty-result guard uses for the filters that hid everything. The constants are identities (English,
/// never translated: the UI switches on them); <see cref="Display"/> is what a chip prints, in the UI language.
/// </summary>
public static class FilterNames
{
    public const string HideCompleted = "Hide completed";
    public const string AvailableOnly = "Available now";
    public const string State = "State";
    public const string Expansion = "Expansion";
    public const string AddedIn = "Added in";
    public const string LevelRange = "Level range";
    public const string JobCategory = "Job category";
    public const string RewardKinds = "Reward kinds";
    public const string Repeatable = "Repeatable";
    public const string SeasonalActive = "Seasonal active";
    public const string IncludeUnlisted = "Include removed";
    public const string IncludeOtherPaths = "Include other paths";
    public const string Pinned = "Pinned";
    public const string Abandoned = "Abandoned";
    public const string OnceOnlyStory = "Once-only story";
    public const string Search = "Search";

    // Quick views (presets)
    public const string FeatureQuests = "Unlocks";
    public const string LevelBand = "My level";
    public const string Stalled = "Stalled";
    public const string Sprout = "Sprout mode";
    public const string StorySidequests = "Story sidequests";

    /// <summary>The label a chip prints for a filter identity, in the UI language (keys <c>Core.Filter.*</c>).</summary>
    public static string Display(string name) => name switch
    {
        HideCompleted => CoreText.T("Core.Filter.HideCompleted", "Hide completed"),
        AvailableOnly => CoreText.T("Core.Filter.AvailableOnly", "Available now"),
        State => CoreText.T("Core.Filter.State", "State"),
        Expansion => CoreText.T("Core.Filter.Expansion", "Expansion"),
        AddedIn => CoreText.T("Core.Filter.AddedIn", "Added in"),
        LevelRange => CoreText.T("Core.Filter.LevelRange", "Level range"),
        JobCategory => CoreText.T("Core.Filter.JobCategory", "Job category"),
        RewardKinds => CoreText.T("Core.Filter.RewardKinds", "Reward kinds"),
        Repeatable => CoreText.T("Core.Filter.Repeatable", "Repeatable"),
        SeasonalActive => CoreText.T("Core.Filter.SeasonalActive", "Seasonal active"),
        IncludeUnlisted => CoreText.T("Core.Filter.IncludeUnlisted", "Include removed"),
        IncludeOtherPaths => CoreText.T("Core.Filter.IncludeOtherPaths", "Include other paths"),
        Pinned => CoreText.T("Core.Filter.Pinned", "Pinned"),
        Abandoned => CoreText.T("Core.Filter.Abandoned", "Abandoned"),
        OnceOnlyStory => CoreText.T("Core.Filter.OnceOnlyStory", "Once-only story"),
        Search => CoreText.T("Core.Filter.Search", "Search"),
        FeatureQuests => CoreText.T("Core.Filter.FeatureQuests", "Unlocks"),
        LevelBand => CoreText.T("Core.Filter.LevelBand", "My level"),
        Stalled => CoreText.T("Core.Filter.Stalled", "Stalled"),
        Sprout => CoreText.T("Core.Filter.Sprout", "Sprout mode"),
        StorySidequests => CoreText.T("Core.Filter.StorySidequests", "Story sidequests"),
        _ => name,
    };

    /// <summary>Chip and empty-guard label of a preset; empty for <see cref="Preset.None"/>.</summary>
    public static string PresetName(Preset preset) => preset switch
    {
        Preset.FeatureQuests => FeatureQuests,
        Preset.LevelBand => LevelBand,
        Preset.Stalled => Stalled,
        Preset.Sprout => Sprout,
        Preset.StorySidequests => StorySidequests,
        _ => string.Empty,
    };
}
