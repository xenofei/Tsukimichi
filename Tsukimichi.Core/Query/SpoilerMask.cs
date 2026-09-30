using System.Collections.Frozen;
using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>What the spoiler shield hides; the plugin resolves it per character from its settings.</summary>
/// <param name="HideNames">Mask the names of main scenario quests more than <paramref name="Ahead"/> quests past the character's position.</param>
/// <param name="Ahead">Main scenario quests past the position whose names stay visible; clamped to 0–<see cref="MaxAhead"/>.</param>
/// <param name="HideArtwork">Show journal artwork only for quests in the journal or completed.</param>
public sealed record SpoilerOptions(bool HideNames = true, int Ahead = SpoilerOptions.DefaultAhead, bool HideArtwork = true)
{
    public const int DefaultAhead = 3;
    public const int MaxAhead = 10;

    /// <summary>The shield as a fresh install has it: names and artwork hidden, three quests ahead revealed.</summary>
    public static readonly SpoilerOptions Default = new();

    /// <summary>Shield off: every name and every banner shown.</summary>
    public static readonly SpoilerOptions Off = new(false, DefaultAhead, false);

    /// <summary><see cref="Ahead"/> within 0–<see cref="MaxAhead"/>.</summary>
    public int AheadClamped => Math.Clamp(Ahead, 0, MaxAhead);
}

/// <summary>
/// The spoiler shield for one character: which main scenario quest names are masked and whether a quest's journal
/// artwork may show. Built once per session version from the catalog, the character's states and its main scenario
/// position (walked as <see cref="MsqProgress"/> walks it: sections 0 then 1 in journal order, removed rows left out,
/// the journal's hide state ignored). A main scenario quest is masked when it lies more than
/// <see cref="SpoilerOptions.Ahead"/> quests past the position and is neither completed nor in the journal. A
/// character without states (browse mode, a stored view without data) has no position: every main scenario quest
/// after the first is masked. Immutable; <see cref="DisplayName"/> allocates nothing.
/// </summary>
public sealed class SpoilerMask
{
    /// <summary>"Main scenario quest (Lv 83)": what a masked quest is called everywhere its name would print.</summary>
    public const string PlaceholderFormat = "Main scenario quest (Lv {0})";

    /// <summary>Masks nothing and shows every banner: no catalog yet, or the shield turned off.</summary>
    public static readonly SpoilerMask None = new(SpoilerOptions.Off, FrozenDictionary<uint, byte>.Empty, byte.MaxValue, 0);

    // One placeholder (and its lowercased search form) per display level, shared by every mask: a rebuild per session
    // version allocates no strings. Written racily at worst with equal values.
    private static readonly string?[] PlaceholderByLevel = new string?[byte.MaxValue + 1];
    private static readonly string?[] SearchNameByLevel = new string?[byte.MaxValue + 1];

    /// <summary>Masked row id to the display level its placeholder prints.</summary>
    private readonly IReadOnlyDictionary<uint, byte> masked;

    private SpoilerMask(SpoilerOptions options, IReadOnlyDictionary<uint, byte> masked, byte reachExpansion, int fingerprint)
    {
        Options = options;
        this.masked = masked;
        ReachExpansion = reachExpansion;
        Fingerprint = fingerprint;
    }

    /// <summary>The options the mask was built with.</summary>
    public SpoilerOptions Options { get; }

    /// <summary>How many quest names are masked.</summary>
    public int MaskedCount => masked.Count;

    /// <summary>
    /// The expansion of the character's next main scenario quest: the furthest expansion the story has reached. 0 (A
    /// Realm Reborn) for a character without states; <see cref="byte.MaxValue"/> once the main scenario is complete
    /// or the catalog has none. Sprout mode lists quests at or below it.
    /// </summary>
    public byte ReachExpansion { get; }

    /// <summary>
    /// Changes whenever the set of masked names changes (a quest completed, the position moved, a setting flipped, a
    /// name revealed); equal masks share it. The integrations that register names re-register when it changes.
    /// </summary>
    public int Fingerprint { get; }

    /// <summary>The placeholder a masked quest prints: "Main scenario quest (Lv 83)", with the journal's display level.</summary>
    public static string Placeholder(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return PlaceholderFor(quest.DisplayLevel);
    }

    private static string PlaceholderFor(byte level) =>
        PlaceholderByLevel[level] ??= string.Format(CultureInfo.InvariantCulture, PlaceholderFormat, level);

    private static string SearchNameFor(byte level) =>
        SearchNameByLevel[level] ??= PlaceholderFor(level).ToLowerInvariant();

    /// <summary>Whether the quest's name is hidden.</summary>
    public bool IsMasked(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return masked.ContainsKey(quest.RowId);
    }

    /// <summary>Whether the quest with this row id has its name hidden.</summary>
    public bool IsMasked(uint rowId) => masked.ContainsKey(rowId);

    /// <summary>The name to print: <see cref="Placeholder"/> for a masked quest, the quest's own name otherwise.</summary>
    public string DisplayName(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return masked.TryGetValue(quest.RowId, out var level) ? PlaceholderFor(level) : quest.Name;
    }

    /// <summary>
    /// The lowercased placeholder a masked quest is searched by, or null when the quest is not masked and its own
    /// name is searched (<see cref="SearchIndex.Matches(uint, string, SpoilerMask?)"/>).
    /// </summary>
    public string? SearchName(uint rowId) => masked.TryGetValue(rowId, out var level) ? SearchNameFor(level) : null;

    /// <summary>The name to print for a row id; <paramref name="fallback"/> when the catalog does not know it.</summary>
    public string DisplayName(QuestCatalog catalog, uint rowId, string fallback)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.GetByRowId(rowId) is { } quest ? DisplayName(quest) : fallback;
    }

    /// <summary>
    /// Whether the quest's journal artwork may show: always with <see cref="SpoilerOptions.HideArtwork"/> off,
    /// otherwise only for a quest in the journal or completed (the art summarises the quest).
    /// </summary>
    public bool ShowArtwork(QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return !Options.HideArtwork || state is QuestState.Accepted or QuestState.Completed;
    }

    /// <summary>
    /// Replaces every masked quest name among <paramref name="rowIds"/> inside <paramref name="text"/> with its
    /// placeholder: for text built before the mask (a requirement's detail line naming its prerequisite). Returns the
    /// input when nothing is masked.
    /// </summary>
    public string MaskNamesIn(string text, QuestCatalog catalog, IReadOnlyList<uint> rowIds)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rowIds);
        if (masked.Count == 0)
        {
            return text;
        }

        foreach (var rowId in rowIds)
        {
            if (masked.TryGetValue(rowId, out var level) && catalog.GetByRowId(rowId) is { Name.Length: > 0 } quest)
            {
                text = text.Replace(quest.Name, PlaceholderFor(level), StringComparison.Ordinal);
            }
        }

        return text;
    }

    /// <summary>The mask over evaluator output.</summary>
    /// <param name="revealed">Row ids the player revealed for the session ("Reveal this name"); never masked.</param>
    public static SpoilerMask Build(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> evaluations, SpoilerOptions options, IReadOnlySet<uint>? revealed = null)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return Build(catalog, new EvaluationSource(evaluations), evaluations.Count == 0, options, revealed);
    }

    /// <summary>The mask over a plain state map; missing rows read as <see cref="QuestState.Unknown"/>.</summary>
    /// <param name="revealed">Row ids the player revealed for the session ("Reveal this name"); never masked.</param>
    public static SpoilerMask Build(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states, SpoilerOptions options, IReadOnlySet<uint>? revealed = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        return Build(catalog, new StateMapSource(states), states.Count == 0, options, revealed);
    }

    private static SpoilerMask Build<TSource>(QuestCatalog catalog, TSource source, bool noStates, SpoilerOptions options, IReadOnlySet<uint>? revealed)
        where TSource : struct, IStateSource
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        // Without states there is no position: only the very first quest of the story keeps its name.
        var ahead = noStates ? 0 : options.AheadClamped;
        var masked = new Dictionary<uint, byte>();
        var hash = new HashCode();
        hash.Add(options.HideNames);
        byte? reach = null;
        var ordinal = -1;
        int? position = null;
        var any = false;
        foreach (var section in MsqProgress.MainScenarioSections)
        {
            if (catalog.BySection.GetValueOrDefault(section) is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (quest.IsRemoved)
                {
                    continue;
                }

                any = true;
                var state = source.StateOf(quest.RowId);
                // A branch the character did not take holds no place in the story's order (MsqProgress skips it too).
                if (!source.LeavesTotals(quest.RowId))
                {
                    ordinal++;
                    if (position is null && state != QuestState.Completed)
                    {
                        position = ordinal;
                        reach = quest.Expansion;
                    }
                }

                if (!options.HideNames
                    || position is not { } at
                    || ordinal - at <= ahead
                    || state is QuestState.Completed or QuestState.Accepted
                    || (revealed is not null && revealed.Contains(quest.RowId)))
                {
                    continue;
                }

                masked[quest.RowId] = quest.DisplayLevel;
                hash.Add(quest.RowId);
            }
        }

        var reachExpansion = !any ? byte.MaxValue : noStates ? (byte)0 : reach ?? byte.MaxValue;
        return new SpoilerMask(options, masked, reachExpansion, hash.ToHashCode());
    }
}
