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
    public const string IncludeUnlisted = "Include Unlisted";
    public const string Pinned = "Pinned";
    public const string Search = "Search";

    // Presets
    public const string FeatureQuests = "Feature quests";
    public const string LevelBand = "Around my level";
    public const string Stalled = "Stalled";

    /// <summary>Chip and empty-guard label of a preset; empty for <see cref="Preset.None"/>.</summary>
    public static string PresetName(Preset preset) => preset switch
    {
        Preset.FeatureQuests => FeatureQuests,
        Preset.LevelBand => LevelBand,
        Preset.Stalled => Stalled,
        _ => string.Empty,
    };
}
