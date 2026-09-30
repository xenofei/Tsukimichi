namespace Tsukimichi.Core.Query;

/// <summary>Display names the empty-result guard uses to name the filters that hid everything.</summary>
public static class FilterNames
{
    public const string HideCompleted = "Hide completed";
    public const string AvailableOnly = "Available now";
    public const string State = "State";
    public const string Expansion = "Expansion";
    public const string LevelRange = "Level range";
    public const string JobCategory = "Job category";
    public const string RewardKinds = "Reward kinds";
    public const string Repeatable = "Repeatable";
    public const string SeasonalActive = "Seasonal active";
    public const string IncludeUnlisted = "Include removed";
    public const string Pinned = "Pinned";
    public const string Abandoned = "Abandoned";
    public const string Search = "Search";

    // Quick views (presets)
    public const string FeatureQuests = "Unlocks";
    public const string LevelBand = "My level";
    public const string Stalled = "Stalled";
    public const string Sprout = "Sprout mode";
    public const string StorySidequests = "Story sidequests";

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
