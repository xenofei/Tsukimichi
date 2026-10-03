using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The saved appearance (feature plan v7 T1; theme-system §5.3): a theme, and the user's overrides of any of its axes.
/// Every field is a string key from the registries (<see cref="ThemePresets"/>, <see cref="GlyphSets"/>,
/// <see cref="PaletteChoices"/>, <see cref="FrameKits"/>), so the JSON stays readable and survives enum reordering, and a
/// key this build does not know (one a newer build wrote) is kept as written and resolves to the theme's own choice
/// (<see cref="AppearanceResolver"/>). Null means "from the theme". <see cref="AppearanceMigration"/> builds the first one
/// from the settings it replaces (Moon style, Moon colours, Follow Dalamud colours) and writes those back for a downgrade.
/// </summary>
public sealed class AppearanceConfig
{
    /// <summary>The format version: 1 since 1.16.0. A later format migrates from here.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>The theme's key (<see cref="ThemePreset.Key"/>); Menphina's Medallion by default.</summary>
    public string Theme { get; set; } = ThemePresets.Default.Key;

    /// <summary>
    /// The per-state mix (1.17 T10): a state's key (<see cref="AppearanceStates.Key"/>, "ready" …) to a glyph set's key.
    /// A state that is absent draws from the theme's set. Null when nothing is mixed.
    /// </summary>
    public Dictionary<string, string>? Glyphs { get; set; }

    /// <summary>The palette's key (<see cref="PaletteInfo.Key"/>; "dalamud" follows the Dalamud style); null for the theme's own.</summary>
    public string? Palette { get; set; }

    /// <summary>The frame kit's key (1.17 T11); null for the theme's own.</summary>
    public string? Frames { get; set; }

    /// <summary>
    /// High contrast (the old Moon colours › High contrast): one shared low-vision set of moons whatever the theme or mix,
    /// and the palette's high-contrast form.
    /// </summary>
    public bool HighContrast { get; set; }

    /// <summary>A deep copy (the Undo snapshot and the live preview's scratch copy).</summary>
    public AppearanceConfig Clone() => new()
    {
        Version = Version,
        Theme = Theme,
        Glyphs = Glyphs is null ? null : new Dictionary<string, string>(Glyphs, StringComparer.Ordinal),
        Palette = Palette,
        Frames = Frames,
        HighContrast = HighContrast,
    };

    /// <summary>Whether <paramref name="other"/> saves the same appearance (field by field; the mix order does not matter).</summary>
    public bool SameAs(AppearanceConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Version != other.Version || HighContrast != other.HighContrast
            || !string.Equals(Theme, other.Theme, StringComparison.Ordinal)
            || !string.Equals(Palette, other.Palette, StringComparison.Ordinal)
            || !string.Equals(Frames, other.Frames, StringComparison.Ordinal))
        {
            return false;
        }

        var mine = Glyphs?.Count ?? 0;
        var theirs = other.Glyphs?.Count ?? 0;
        if (mine != theirs)
        {
            return false;
        }

        if (mine == 0)
        {
            return true;
        }

        foreach (var (state, set) in Glyphs!)
        {
            if (!other.Glyphs!.TryGetValue(state, out var otherSet) || !string.Equals(set, otherSet, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>The eight quest states an appearance assigns glyphs to, and their stable keys (the atlas sprite stems).</summary>
public static class AppearanceStates
{
    /// <summary>The number of states (every <see cref="QuestState"/>).</summary>
    public const int Count = 8;

    /// <summary>The states in <see cref="QuestState"/> order, so a state's index is its value.</summary>
    public static readonly IReadOnlyList<QuestState> All =
    [
        QuestState.Ready,
        QuestState.ReadyOnOtherJob,
        QuestState.Accepted,
        QuestState.Blocked,
        QuestState.DoneThisCycle,
        QuestState.Completed,
        QuestState.Foreclosed,
        QuestState.Unknown,
    ];

    /// <summary>The state's index, 0–7; an out-of-range value counts as Not checked, as the medals treat it.</summary>
    public static int Index(QuestState state) => (uint)state < Count ? (int)state : (int)QuestState.Unknown;

    /// <summary>The state's stable key: the player-facing name in kebab case, as the atlas sprites are named.</summary>
    public static string Key(QuestState state) => state switch
    {
        QuestState.Ready => "ready",
        QuestState.ReadyOnOtherJob => "ready-on-another-job",
        QuestState.Accepted => "in-journal",
        QuestState.Blocked => "blocked",
        QuestState.DoneThisCycle => "done-this-cycle",
        QuestState.Completed => "completed",
        QuestState.Foreclosed => "locked-out",
        _ => "not-checked",
    };

    /// <summary>The state with <paramref name="key"/> (case-insensitive); false for anything else.</summary>
    public static bool TryParse(string? key, out QuestState state)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var trimmed = key.AsSpan().Trim();
            foreach (var candidate in All)
            {
                if (trimmed.Equals(Key(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    state = candidate;
                    return true;
                }
            }
        }

        state = QuestState.Unknown;
        return false;
    }
}
