using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Query;

/// <summary>What the spoiler shield hides; the plugin resolves it per character from its settings.</summary>
/// <param name="HideNames">Mask the names of main scenario quests more than <paramref name="Ahead"/> quests past the character's position.</param>
/// <param name="Ahead">Main scenario quests past the position whose names stay visible; clamped to 0–<see cref="MaxAhead"/>.</param>
/// <param name="HideArtwork">Show journal artwork only for quests in the journal or completed.</param>
/// <param name="HideRelated">
/// With <paramref name="HideNames"/>, also mask the names of places, duties, rewards and people from the story past
/// the character's (plan v7, 1.20.0 N6, "Also hide places, duties, rewards and people"; <see cref="SpoilerNames"/>).
/// </param>
public sealed record SpoilerOptions(bool HideNames = true, int Ahead = SpoilerOptions.DefaultAhead, bool HideArtwork = true, bool HideRelated = true)
{
    public const int DefaultAhead = 3;
    public const int MaxAhead = 10;

    /// <summary>The shield as a fresh install has it: names (and what the story ahead introduces) and artwork hidden, three quests ahead revealed.</summary>
    public static readonly SpoilerOptions Default = new();

    /// <summary>Shield off: every name and every banner shown.</summary>
    public static readonly SpoilerOptions Off = new(false, DefaultAhead, false, false);

    /// <summary><see cref="Ahead"/> within 0–<see cref="MaxAhead"/>.</summary>
    public int AheadClamped => Math.Clamp(Ahead, 0, MaxAhead);

    /// <summary>
    /// "Also hide places, duties, rewards and people" as a configuration loads (spec-1.20 N6): its saved value, or, in a
    /// configuration saved before the switch existed (null), the value of "Hide story names ahead", so a player who
    /// turned the shield off keeps everything shown and everyone else gets the wider shield.
    /// </summary>
    public static bool HideRelatedOnLoad(bool? saved, bool hideNames) => saved ?? hideNames;
}

/// <summary>
/// The spoiler shield for one character: which main scenario quest names are masked and whether a quest's journal
/// artwork may show. Built once per session version from the catalog, the character's states and its main scenario
/// position (walked as <see cref="MsqProgress"/> walks it: sections 0 then 1 in journal order, removed rows left out,
/// the journal's hide state ignored). A main scenario quest is masked when it lies more than
/// <see cref="SpoilerOptions.Ahead"/> quests past the position and is neither completed, in the journal nor once
/// abandoned from it (the game has shown all three names), nor on a path the character did not take (another city's
/// or class's start, another Grand Company's quest: it tells nothing of the character's own story, and the
/// Locked-out row names it for what it is). Inside a routed branch region (<see cref="MsqGraph"/>)
/// every route is a position of its own: a route quest is "N ahead" along its route from that route's next quest,
/// the reconvergence quest lies as far ahead as the quests still needed before it (every open route's for an All
/// join, the shortest route's for an Any join), and the story after it counts on from there. A
/// character without states (browse mode, a stored view without data) has no position: every main scenario quest
/// after the first is masked.
/// <para>
/// The wider shield (plan v7, 1.20.0 N6, <see cref="SpoilerOptions.HideRelated"/>): a place, aetheryte, duty, reward
/// or person from the story past the character's (<see cref="SpoilerNames"/> holds the rule and the data) prints as a
/// kind word with a safe locator (<see cref="Name"/>): "Dawntrail area 6", "Dawntrail aetheryte · area 6", "Dungeon
/// (Lv 97)", "A mount", "Dawntrail character". A name revealed for the session (<c>revealedNames</c>) shows.
/// </para>
/// <para>
/// Every placeholder, the quests' and the names', is registered as it is first formatted, so a surface can tell a
/// placeholder (and a composed string that holds one) from a name without knowing where it came from
/// (<see cref="IsPlaceholder"/>, <see cref="HoldsPlaceholder"/>): such a string is drawn in Secondary as a whole.
/// </para>
/// Immutable; <see cref="DisplayName(QuestRecord)"/> and <see cref="Name"/> allocate nothing.
/// </summary>
public sealed class SpoilerMask
{
    /// <summary>
    /// "Main scenario quest (Lv 83)": what a masked quest is called everywhere its name would print, in the UI language.
    /// The locator keeps a non-breaking space, so it never wraps apart (spec-1.20).
    /// </summary>
    public static string PlaceholderFormat => CoreText.T("Core.Spoiler.Placeholder", "Main scenario quest (Lv {0})");

    /// <summary>Masks nothing and shows every banner: no catalog yet, or the shield turned off.</summary>
    public static readonly SpoilerMask None = new(SpoilerOptions.Off, FrozenDictionary<uint, byte>.Empty, FrozenSet<uint>.Empty, byte.MaxValue, 0, SpoilerNames.Empty, FrozenSet<(SpoilerKind Kind, string Name)>.Empty);

    // One placeholder (and its lowercased search form) per display level and language, shared by every mask: a
    // rebuild per session version allocates no strings. Written racily at worst with equal values.
    private static readonly TextCache<string?[]> PlaceholderByLevel = new(static () => new string?[byte.MaxValue + 1]);
    private static readonly TextCache<string?[]> SearchNameByLevel = new(static () => new string?[byte.MaxValue + 1]);

    // Every placeholder formatted so far, in any language: what IsPlaceholder and HoldsPlaceholder look for.
    private static readonly ConcurrentDictionary<string, byte> Registered = new(StringComparer.Ordinal);

    // Bumped as a placeholder is registered: HoldsPlaceholder rebuilds its matcher when it moved.
    private static int registeredVersion;
    private static PlaceholderMatcher? matcher;
    private static readonly Lock MatcherLock = new();

    private static readonly SpoilerKind[] AllKinds = [SpoilerKind.Area, SpoilerKind.Aetheryte, SpoilerKind.Duty, SpoilerKind.Reward, SpoilerKind.Npc];

    /// <summary>Masked row id to the display level its placeholder prints.</summary>
    private readonly IReadOnlyDictionary<uint, byte> masked;

    /// <summary>
    /// The main scenario quests the story has not reached: <see cref="masked"/> before a quest's name is revealed or
    /// found abandoned. The wider shield places names against it, so revealing a quest's name is no story progress:
    /// the names it introduces stay hidden but for those revealed with it (<see cref="reveals"/>).
    /// </summary>
    private readonly IReadOnlySet<uint> ahead;

    /// <summary>Where each zone, duty, reward and NPC name sits in the story; <see cref="SpoilerNames.Empty"/> when the wider shield is off.</summary>
    private readonly SpoilerNames names;

    /// <summary>The names revealed for the session ("Reveal this name", "Reveal names in this quest").</summary>
    private readonly FrozenSet<(SpoilerKind Kind, string Name)> reveals;

    private int maskedNameCount = -1;

    private SpoilerMask(SpoilerOptions options, IReadOnlyDictionary<uint, byte> masked, IReadOnlySet<uint> ahead, byte reachExpansion, int fingerprint, SpoilerNames names, FrozenSet<(SpoilerKind Kind, string Name)> reveals)
    {
        Options = options;
        this.masked = masked;
        this.ahead = ahead;
        ReachExpansion = reachExpansion;
        Fingerprint = fingerprint;
        this.names = names;
        this.reveals = reveals;
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

    /// <summary>Whether the wider shield can mask anything: it is on and the names are placed.</summary>
    public bool MasksNames => names.Count > 0 && ahead.Count > 0;

    /// <summary>
    /// How many places, aetherytes, duties, rewards and people the wider shield hides ("486 other names" on Settings ›
    /// Spoilers); counted once per mask, on first ask.
    /// </summary>
    public int MaskedNameCount
    {
        get
        {
            if (maskedNameCount >= 0)
            {
                return maskedNameCount;
            }

            var count = 0;
            if (MasksNames)
            {
                foreach (var kind in AllKinds)
                {
                    foreach (var (name, _) in names.All(kind))
                    {
                        count += PlacedMasked(kind, name, out _) ? 1 : 0;
                    }
                }
            }

            maskedNameCount = count;
            return count;
        }
    }

    /// <summary>
    /// Whether the wider shield masks a name of <paramref name="kind"/> (<see cref="SpoilerNames"/> holds the rule):
    /// <see cref="SpoilerOptions.HideRelated"/> is on, the name was not revealed for the session, and the story has not
    /// reached it. False for a name the data does not place. Allocates nothing.
    /// </summary>
    public bool IsNameMasked(SpoilerKind kind, string? name) => name is not null && PlacedMasked(kind, name, out _);

    /// <summary>
    /// The name to print: its placeholder ("Dawntrail area 6", "Dungeon (Lv 97)") when <see cref="IsNameMasked"/>, the
    /// name itself otherwise (an empty string for null). Allocates nothing.
    /// </summary>
    public string Name(SpoilerKind kind, string? name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        return PlacedMasked(kind, name, out var placed) ? names.Placeholder(placed) : name;
    }

    /// <summary>
    /// The short form of a place in a slot that also holds a name (spec-1.20: the place is cut to its locator first):
    /// "area 6" for a masked area or aetheryte, the whole placeholder for another masked name, the name itself when
    /// shown. Allocates nothing.
    /// </summary>
    public string Locator(SpoilerKind kind, string? name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        return PlacedMasked(kind, name, out var placed) ? names.Locator(placed) : name;
    }

    /// <summary>
    /// The lowercased placeholder a masked name is searched by ("dawntrail area 6"), or null when the name is shown and
    /// searched itself: a hidden name matches only its placeholder, as a masked quest does.
    /// </summary>
    public string? SearchName(SpoilerKind kind, string? name) =>
        name is not null && PlacedMasked(kind, name, out var placed) ? names.SearchText(placed) : null;

    /// <summary>
    /// A journal node's name through the wider shield (spec-1.20 N6, "Journal tree"): a node named after an area the
    /// story has not reached takes the area's placeholder, the whole name ("Living Memory") or its leading place
    /// ("Tuliyollal Sidequests" reads "Dawntrail area 1 Sidequests"). The name itself otherwise. Allocates only to
    /// compose a shielded name.
    /// </summary>
    public string NodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!MasksNames || name.Length == 0)
        {
            return name;
        }

        if (PlacedMasked(SpoilerKind.Area, name, out var whole))
        {
            return names.Placeholder(whole);
        }

        // The longest leading run of words that is a hidden area.
        for (var end = name.LastIndexOf(' '); end > 0; end = name.LastIndexOf(' ', end - 1))
        {
            if (PlacedMasked(SpoilerKind.Area, name[..end], out var lead))
            {
                return names.Placeholder(lead) + name[end..];
            }
        }

        return name;
    }

    /// <summary>
    /// A place with its region through the wider shield, in <paramref name="pathFormat"/> ("{0} › {1}": "Hingashi ›
    /// Kugane"): the place's placeholder alone when the place is masked (its region would say where), the place alone
    /// under a masked region, the place alone when the region is empty or the same. Allocates only to compose the path.
    /// </summary>
    public string Place(string? region, string? place, string pathFormat)
    {
        ArgumentNullException.ThrowIfNull(pathFormat);
        if (string.IsNullOrEmpty(place))
        {
            return string.Empty;
        }

        if (PlacedMasked(SpoilerKind.Area, place, out var placed))
        {
            return names.Placeholder(placed);
        }

        return string.IsNullOrEmpty(region) || string.Equals(region, place, StringComparison.Ordinal) || IsNameMasked(SpoilerKind.Area, region)
            ? place
            : string.Format(CultureInfo.CurrentCulture, pathFormat, region, place);
    }

    /// <summary>
    /// Where the wider shield places a Moonlit reward's name (<paramref name="display"/>, <see cref="Unique.RewardNames.Display"/>),
    /// as the unlock index files it (<see cref="SpoilerNames"/>): a duty unlock or instance as a duty ("the Palace of
    /// the Dead (Floors 1-10)" as "the Palace of the Dead"), an aether current as flying in its zone, an area ("Aether
    /// Current (Yanxia)" as "Yanxia"), a curated unlock that opens flying or a field operation as that, anything else
    /// (an item, a mount, a job, a feature) as a reward. A name the data does not place under that kind is a reward.
    /// </summary>
    public (SpoilerKind Kind, string Name) RewardName(UniqueRewardEntry entry, string? display)
    {
        ArgumentNullException.ThrowIfNull(entry);
        display ??= string.Empty;
        var (kind, name) = entry.Kind switch
        {
            RewardKind.DutyUnlock or RewardKind.Instance => (SpoilerKind.Duty, UnlockTags.ContentName(display)),
            RewardKind.AetherCurrent => (SpoilerKind.Area, UnlockRewards.CurrentZone(display)),
            RewardKind.SystemUnlock or RewardKind.Other when UnlockTags.FlyingLabels.Contains(display) => (SpoilerKind.Area, display),
            RewardKind.SystemUnlock or RewardKind.Other when UnlockTags.FieldOperationLabels.Contains(display) => (SpoilerKind.Duty, display),
            _ => (SpoilerKind.Reward, display),
        };

        return kind == SpoilerKind.Reward || names.TryGet(kind, name, out _) ? (kind, name) : (SpoilerKind.Reward, display);
    }

    /// <summary>
    /// Whether the wider shield hides a Moonlit reward (<see cref="RewardName"/> says where its name is placed). A reward
    /// whose name the data does not place (a curated unlock, named by its note) is hidden while the quest that gives it
    /// lies in the story ahead. Not revealed for the session either way.
    /// </summary>
    public bool IsRewardMasked(UniqueRewardEntry entry, string? display)
    {
        var (kind, name) = RewardName(entry, display);
        if (names.TryGet(kind, name, out _))
        {
            return PlacedMasked(kind, name, out _);
        }

        return QuestAhead(entry.QuestRowId) && !(reveals.Count > 0 && reveals.Contains((kind, name)));
    }

    /// <summary>
    /// What a Moonlit reward prints through the wider shield: <paramref name="display"/> itself when shown; its
    /// placeholder when hidden ("Trial (Lv 60)", "A mount"), an aether current keeping its own words around its zone's
    /// ("Aether Current (Stormblood area 3)"). Allocates only to compose an aether current's.
    /// </summary>
    public string RewardDisplay(UniqueRewardEntry entry, string? display)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrEmpty(display) || !MasksNames)
        {
            return display ?? string.Empty;
        }

        var (kind, name) = RewardName(entry, display);
        if (PlacedMasked(kind, name, out var placed))
        {
            var placeholder = names.Placeholder(placed);
            var open = display.IndexOf('(', StringComparison.Ordinal);
            var close = display.LastIndexOf(')');
            return entry.Kind == RewardKind.AetherCurrent && open >= 0 && close > open && !ReferenceEquals(name, display)
                ? string.Concat(display.AsSpan(0, open + 1), placeholder, display.AsSpan(close))
                : placeholder;
        }

        return !names.TryGet(kind, name, out _) && IsRewardMasked(entry, display) ? SpoilerNames.RewardPlaceholder(entry.Kind) : display;
    }

    /// <summary>
    /// The kind a name is placed under: <paramref name="first"/> when the data places it there, else a reward. For a
    /// name whose kind is not known for sure (a plan's Other unlock: a duty the plan could not class, or a feature).
    /// </summary>
    public SpoilerKind PlacedKind(SpoilerKind first, string? name) =>
        first == SpoilerKind.Reward || names.TryGet(first, name, out _) ? first : SpoilerKind.Reward;

    /// <summary>
    /// A duty the story has not reached as the wider shield prints it, "Dungeon (Lv 90)": its own placeholder when the
    /// names place it, else one from its Duty Finder category and level (registered, so it reads and answers as one).
    /// For the duties hidden because every quest that shows them is (<see cref="HidesDuty"/>). Allocates nothing once formatted.
    /// </summary>
    public string DutyPlaceholder(DutyRunInfo duty)
    {
        ArgumentNullException.ThrowIfNull(duty);
        if (names.TryGet(SpoilerKind.Duty, duty.Name, out var placed))
        {
            return names.Placeholder(placed);
        }

        var target = duty.ContentTypeId switch
        {
            DutyRunInfo.Dungeons => UnlockTarget.Dungeon,
            DutyRunInfo.Trials => UnlockTarget.Trial,
            DutyRunInfo.Raids or DutyRunInfo.UltimateRaids => UnlockTarget.NormalRaid,
            DutyRunInfo.ChaoticAllianceRaid => UnlockTarget.AllianceRaid,
            _ => UnlockTarget.OtherDuty,
        };

        return SpoilerNames.DutyPlaceholder(target, duty.LevelRequired);
    }

    /// <summary>Whether the quest is anchored at a main scenario quest the story has not reached (the wider shield on).</summary>
    private bool QuestAhead(uint rowId) => names.Count > 0 && names.AnchorOf(rowId) is var anchor and not 0 && ahead.Contains(anchor);

    private bool PlacedMasked(SpoilerKind kind, string name, out SpoilerNames.Placed placed)
    {
        placed = default;
        if (ahead.Count == 0 || !names.TryGet(kind, name, out placed))
        {
            return false;
        }

        if (reveals.Count > 0 && reveals.Contains((kind, name)))
        {
            return false;
        }

        // An aetheryte is hidden while its area is, and prints its own placeholder.
        if (placed.Zone is { } zone && names.TryGet(SpoilerKind.Area, zone, out _))
        {
            return PlacedMasked(SpoilerKind.Area, zone, out _);
        }

        // Shown once the story reaches any quest that introduces it, "names ahead" included (a quest whose name was
        // revealed is not reached: the reveal is no story progress).
        var reached = false;
        var reach = ReachExpansion;
        foreach (var anchor in placed.Anchors)
        {
            if (!ahead.Contains(anchor))
            {
                reached = true;
                // The furthest shown anchor counts as reached, so "names ahead" past an expansion's last quest shows
                // the next expansion's places and duties.
                var expansion = names.AnchorExpansion(anchor);
                if (expansion != byte.MaxValue && expansion > reach)
                {
                    reach = expansion;
                }
            }
        }

        // A place or duty of an expansion past the story's own is hidden whatever introduces it, however early.
        if (kind is SpoilerKind.Area or SpoilerKind.Aetheryte or SpoilerKind.Duty && placed.Expansion != byte.MaxValue && placed.Expansion > reach)
        {
            return true;
        }

        return !reached && !placed.Unanchored && placed.Anchors.Length > 0;
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a placeholder exactly ("Main scenario quest (Lv 97)", "Dawntrail area 6"), one
    /// any mask has formatted in this session. Allocates nothing.
    /// </summary>
    public static bool IsPlaceholder(string? text) => !string.IsNullOrEmpty(text) && Volatile.Read(ref registeredVersion) != 0 && Registered.ContainsKey(text);

    /// <summary>
    /// Whether <paramref name="text"/> is or holds a placeholder: "Flying in Dawntrail area 6", "Opens Dungeon (Lv 97)",
    /// "Job: A job". Such a string is drawn in Secondary as a whole (spec-1.20). A placeholder counts inside a longer
    /// string only as a whole: on word boundaries ("Dawntrail area 1" is not in "Dawntrail area 10", "A trait" not in
    /// "A traitor"), and never as a kind label ("Dungeon: Sastasha" names a kind, not a hidden duty). A placeholder of one
    /// word ("Dungeon", a duty without a level) counts only as the whole string or after a kind label ("Dungeon:
    /// Dungeon"), so "Duty Finder" or "The Dungeon of …" is never one. Allocates nothing once the placeholders in use
    /// are formatted.
    /// </summary>
    public static bool HoldsPlaceholder(string? text)
    {
        if (string.IsNullOrEmpty(text) || Volatile.Read(ref registeredVersion) == 0)
        {
            return false;
        }

        if (Registered.ContainsKey(text))
        {
            return true;
        }

        var current = MatcherNow();
        var span = text.AsSpan();
        for (var from = 0; from < span.Length;)
        {
            var at = span[from..].IndexOfAny(current.Starts);
            if (at < 0)
            {
                break;
            }

            at += from;
            foreach (var placeholder in current.Inline)
            {
                if (PlaceholderAt(text, at, placeholder))
                {
                    return true;
                }
            }

            from = at + 1;
        }

        // "Dungeon: Dungeon": a one-word placeholder after a kind label.
        var label = text.LastIndexOf(": ", StringComparison.Ordinal);
        if (label >= 0 && current.Words.Length > 0)
        {
            var tail = span[(label + 2)..];
            foreach (var word in current.Words)
            {
                if (tail.SequenceEqual(word))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="placeholder"/> stands at <paramref name="index"/> as a whole: on word boundaries, and not a kind label (followed by a colon).</summary>
    private static bool PlaceholderAt(string text, int index, string placeholder)
    {
        if (!MatchesAt(text, index, placeholder))
        {
            return false;
        }

        var end = index + placeholder.Length;
        return end >= text.Length || text[end] != ':';
    }

    /// <summary>The matcher over the placeholders registered so far; rebuilt (allocating) only after one is registered.</summary>
    private static PlaceholderMatcher MatcherNow()
    {
        var version = Volatile.Read(ref registeredVersion);
        var current = Volatile.Read(ref matcher);
        if (current is not null && current.Version == version)
        {
            return current;
        }

        lock (MatcherLock)
        {
            current = matcher;
            if (current is null || current.Version != version)
            {
                current = new PlaceholderMatcher(version, Registered.Keys);
                Volatile.Write(ref matcher, current);
            }

            return current;
        }
    }

    /// <summary>
    /// The registered placeholders as <see cref="HoldsPlaceholder"/> looks for them: those of several words or with a
    /// locator (found anywhere, on word boundaries; longest first), and those of one word (only after a kind label).
    /// </summary>
    private sealed class PlaceholderMatcher
    {
        public PlaceholderMatcher(int version, IEnumerable<string> placeholders)
        {
            Version = version;
            var inline = new List<string>();
            var words = new List<string>();
            foreach (var placeholder in placeholders)
            {
                (IsOneWord(placeholder) ? words : inline).Add(placeholder);
            }

            inline.Sort(static (a, b) => b.Length != a.Length ? b.Length.CompareTo(a.Length) : string.CompareOrdinal(a, b));
            Inline = [.. inline];
            Words = [.. words];
            Starts = SearchValues.Create(Inline, StringComparison.Ordinal);
        }

        public int Version { get; }

        public string[] Inline { get; }

        public string[] Words { get; }

        public SearchValues<string> Starts { get; }

        private static bool IsOneWord(string placeholder)
        {
            foreach (var c in placeholder)
            {
                if (!char.IsLetter(c) && c != '-' && c != '\'')
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Notes a placeholder as formatted (<see cref="IsPlaceholder"/>) and returns it.</summary>
    internal static string Register(string placeholder)
    {
        if (placeholder.Length > 0 && Registered.TryAdd(placeholder, 0))
        {
            Interlocked.Increment(ref registeredVersion);
        }

        return placeholder;
    }

    /// <summary>A placeholder as search matches it: lowercased, its non-breaking spaces plain, so "lv 97" finds "Lv 97".</summary>
    internal static string SearchForm(string placeholder) => placeholder.ToLowerInvariant().Replace(' ', ' ');

    /// <summary>The placeholder a masked quest prints: "Main scenario quest (Lv 83)", with the journal's display level.</summary>
    public static string Placeholder(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return PlaceholderFor(quest.DisplayLevel);
    }

    private static string PlaceholderFor(byte level) =>
        PlaceholderByLevel.Value[level] ??= Register(string.Format(CultureInfo.InvariantCulture, PlaceholderFormat, level));

    private static string SearchNameFor(byte level) =>
        SearchNameByLevel.Value[level] ??= SearchForm(PlaceholderFor(level));

    /// <summary>Whether the quest's name is hidden.</summary>
    public bool IsMasked(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return masked.ContainsKey(quest.RowId);
    }

    /// <summary>Whether the quest with this row id has its name hidden.</summary>
    public bool IsMasked(uint rowId) => masked.ContainsKey(rowId);

    /// <summary>
    /// Whether a duty's name is hidden behind "A duty further along the story": every quest it is shown through (the
    /// quests that unlock it on the Duties board, the quest on view in the detail pane) is masked. A duty shown through
    /// no quest keeps its name.
    /// </summary>
    public bool HidesDuty(IReadOnlyCollection<QuestRecord> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (quests.Count == 0)
        {
            return false;
        }

        foreach (var quest in quests)
        {
            if (!masked.ContainsKey(quest.RowId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The name to print: <see cref="Placeholder"/> for a masked quest, the quest's own name otherwise.</summary>
    public string DisplayName(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return masked.TryGetValue(quest.RowId, out var level) ? PlaceholderFor(level) : quest.Name;
    }

    /// <summary>
    /// Compares two quests by the name they print, case-insensitively: the order of the table's Name column. Two
    /// masked quests compare by their placeholders' display level as a number, so "Main scenario quest (Lv 90)"
    /// comes before "(Lv 100)"; placeholders of one level keep their journal order (0 here, the caller's tiebreak).
    /// </summary>
    public int CompareDisplayNames(QuestRecord a, QuestRecord b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (masked.TryGetValue(a.RowId, out var levelA) && masked.TryGetValue(b.RowId, out var levelB))
        {
            return levelA.CompareTo(levelB);
        }

        return string.Compare(DisplayName(a), DisplayName(b), StringComparison.OrdinalIgnoreCase);
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
    /// otherwise only for a quest in the journal or completed, a repeatable handed in this cycle included (the art
    /// summarises the quest).
    /// </summary>
    public bool ShowArtwork(QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return !Options.HideArtwork || state is QuestState.Accepted or QuestState.Completed or QuestState.DoneThisCycle;
    }

    /// <summary>
    /// Replaces every masked quest name among <paramref name="rowIds"/> inside <paramref name="text"/> with its
    /// placeholder: for text built before the mask (a requirement's detail line naming its prerequisite). Returns the
    /// input when nothing is masked.
    /// <para>
    /// The text is scanned once, left to right, trying the listed names longest first and only where a name stands
    /// on word boundaries. So a masked name inside a longer listed name ("Endwalker" in "Endwalker - The Final
    /// Days", masked or not) or inside a word is left alone, and one replacement never feeds the next.
    /// </para>
    /// </summary>
    public string MaskNamesIn(string text, QuestCatalog catalog, IReadOnlyList<uint> rowIds)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rowIds);
        if (masked.Count == 0 || text.Length == 0)
        {
            return text;
        }

        // Every listed name, masked (with its placeholder) or not (kept as is, so a masked name inside it is not
        // touched), longest first.
        var names = new List<(string Name, string? Placeholder)>(rowIds.Count);
        var anyMasked = false;
        foreach (var rowId in rowIds)
        {
            if (catalog.GetByRowId(rowId) is not { Name.Length: > 0 } quest)
            {
                continue;
            }

            string? placeholder = masked.TryGetValue(rowId, out var level) ? PlaceholderFor(level) : null;
            anyMasked |= placeholder is not null;
            names.Add((quest.Name, placeholder));
        }

        if (!anyMasked)
        {
            return text;
        }

        names.Sort(static (a, b) => b.Name.Length != a.Name.Length ? b.Name.Length.CompareTo(a.Name.Length) : string.CompareOrdinal(a.Name, b.Name));

        StringBuilder? sb = null;
        var copied = 0;
        var i = 0;
        while (i < text.Length)
        {
            var matched = false;
            foreach (var (name, placeholder) in names)
            {
                if (!MatchesAt(text, i, name))
                {
                    continue;
                }

                if (placeholder is not null)
                {
                    sb ??= new StringBuilder(text.Length);
                    sb.Append(text, copied, i - copied).Append(placeholder);
                    copied = i + name.Length;
                }

                i += name.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                i++;
            }
        }

        return sb is null ? text : sb.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>
    /// Whether <paramref name="name"/> occurs at <paramref name="index"/> as a whole: a name that starts (ends) with a
    /// letter or digit must not be preceded (followed) by one.
    /// </summary>
    private static bool MatchesAt(string text, int index, string name)
    {
        if (index + name.Length > text.Length || string.CompareOrdinal(text, index, name, 0, name.Length) != 0)
        {
            return false;
        }

        if (char.IsLetterOrDigit(name[0]) && index > 0 && char.IsLetterOrDigit(text[index - 1]))
        {
            return false;
        }

        var end = index + name.Length;
        return !(char.IsLetterOrDigit(name[^1]) && end < text.Length && char.IsLetterOrDigit(text[end]));
    }

    /// <summary>The mask over evaluator output.</summary>
    /// <param name="revealed">Row ids the player revealed for the session ("Reveal this name"); never masked.</param>
    /// <param name="abandoned">
    /// The character's abandoned ledger (runtime quest id to entry): a quest that was once in the journal has had its
    /// name shown by the game, so it is never masked.
    /// </param>
    /// <param name="names">Where zone, duty, reward and NPC names sit in the story (the wider shield); null masks quest names only.</param>
    /// <param name="revealedNames">Names the player revealed for the session ("Reveal this name", "Reveal names in this quest"); never masked.</param>
    public static SpoilerMask Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> evaluations,
        SpoilerOptions options,
        IReadOnlySet<uint>? revealed = null,
        IReadOnlyDictionary<ushort, AbandonedEntry>? abandoned = null,
        SpoilerNames? names = null,
        IEnumerable<(SpoilerKind Kind, string Name)>? revealedNames = null)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        return Build(catalog, new EvaluationSource(evaluations), evaluations.Count == 0, options, revealed, abandoned, names, revealedNames);
    }

    /// <summary>The mask over a plain state map; missing rows read as <see cref="QuestState.Unknown"/>.</summary>
    /// <param name="revealed">Row ids the player revealed for the session ("Reveal this name"); never masked.</param>
    /// <param name="abandoned">The character's abandoned ledger; an abandoned quest is never masked.</param>
    /// <param name="names">Where zone, duty, reward and NPC names sit in the story (the wider shield); null masks quest names only.</param>
    /// <param name="revealedNames">Names the player revealed for the session; never masked.</param>
    public static SpoilerMask Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestState> states,
        SpoilerOptions options,
        IReadOnlySet<uint>? revealed = null,
        IReadOnlyDictionary<ushort, AbandonedEntry>? abandoned = null,
        SpoilerNames? names = null,
        IEnumerable<(SpoilerKind Kind, string Name)>? revealedNames = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        return Build(catalog, new StateMapSource(states), states.Count == 0, options, revealed, abandoned, names, revealedNames);
    }

    private static SpoilerMask Build<TSource>(
        QuestCatalog catalog,
        TSource source,
        bool noStates,
        SpoilerOptions options,
        IReadOnlySet<uint>? revealed,
        IReadOnlyDictionary<ushort, AbandonedEntry>? abandoned,
        SpoilerNames? names,
        IEnumerable<(SpoilerKind Kind, string Name)>? revealedNames)
        where TSource : struct, IStateSource
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        // Without states there is no position: only the very first quest of the story keeps its name.
        var namesAhead = noStates ? 0 : options.AheadClamped;
        var masked = new Dictionary<uint, byte>();
        var ahead = new HashSet<uint>();
        var hash = new HashCode();
        hash.Add(options.HideNames);
        // The wider shield: off, or on over this catalog's names (a new unlock index can mask other names).
        var related = options.HideNames && options.HideRelated && names is not null ? names : SpoilerNames.Empty;
        hash.Add(related.Count);
        var reveals = related.Count > 0 && revealedNames is not null
            ? revealedNames.ToFrozenSet(SpoilerNames.NameComparer)
            : FrozenSet<(SpoilerKind Kind, string Name)>.Empty;
        // Summed, so the set's own order does not count.
        var revealHash = 0;
        foreach (var reveal in reveals)
        {
            revealHash = unchecked(revealHash + SpoilerNames.NameComparer.GetHashCode(reveal));
        }

        hash.Add(reveals.Count);
        hash.Add(revealHash);
        var graph = MsqGraph.For(catalog);
        var msq = graph.Position(source);
        var positionRowId = msq?.Next?.RowId;
        var routes = msq is { IsBranched: true } ? new RouteDistances(msq, source) : null;
        var ordinal = -1;
        int? position = null;
        int? joinOrdinal = null;
        foreach (var quest in graph.Story)
        {
            var state = source.StateOf(quest.RowId);
            // A branch the character did not take holds no place in the story's order (MsqProgress skips it too).
            if (!source.LeavesTotals(quest.RowId))
            {
                ordinal++;
                if (position is null && quest.RowId == positionRowId)
                {
                    position = ordinal;
                }
            }

            int distance;
            if (routes is null)
            {
                // A linear stretch: quests at or before the position are never masked.
                distance = position is { } at ? ordinal - at : int.MinValue;
            }
            else if (routes.TryRoute(quest.RowId, out var onRoute))
            {
                // Inside a branch region "N ahead" counts along each route from its own next quest.
                distance = onRoute;
            }
            else
            {
                // The reconvergence quest lies as many quests ahead as must be done before it; the story after it
                // follows on from there.
                if (joinOrdinal is null && quest.RowId == routes.JoinRowId)
                {
                    joinOrdinal = ordinal;
                }

                distance = joinOrdinal is { } join ? routes.ToJoin + ordinal - join : int.MinValue;
            }

            if (!options.HideNames
                || distance <= namesAhead
                || state is QuestState.Completed or QuestState.Accepted
                || source.OtherPathKind(quest.RowId) is not null)
            {
                continue;
            }

            // The story has not reached it; its name shows when revealed, or once the game has shown it (abandoned).
            ahead.Add(quest.RowId);
            hash.Add(quest.RowId);
            if ((revealed is not null && revealed.Contains(quest.RowId))
                || (abandoned is not null && abandoned.ContainsKey(quest.QuestId)))
            {
                hash.Add(0);
                continue;
            }

            masked[quest.RowId] = quest.DisplayLevel;
        }

        var reachExpansion = msq is null ? byte.MaxValue : noStates ? (byte)0 : msq.Next?.Expansion ?? byte.MaxValue;
        hash.Add(reachExpansion);
        return new SpoilerMask(options, masked, ahead, reachExpansion, hash.ToHashCode(), related, reveals);
    }

    /// <summary>
    /// Inside a branch region: how far ahead each route quest lies along its own route (the route's next quest at
    /// 0, the quests before it negative, a locked-out quest or one completed out of order sharing the distance of the
    /// one before it), and how far ahead the reconvergence quest lies (the quests not completed on every open route
    /// for an All join, on the shortest for an Any join).
    /// </summary>
    private sealed class RouteDistances
    {
        private readonly Dictionary<uint, int> byRowId = [];

        public RouteDistances(MsqPosition position, IStateSource source)
        {
            var branch = position.Branch!;
            JoinRowId = branch.Join.RowId;
            var sum = 0;
            var shortest = int.MaxValue;
            foreach (var progress in position.Routes)
            {
                var quests = progress.Route.Quests;
                if (progress.Status == MsqRouteStatus.LockedOut)
                {
                    // A route the character cannot take reveals nothing.
                    foreach (var quest in quests)
                    {
                        byRowId[quest.RowId] = int.MaxValue;
                    }

                    continue;
                }

                var nextAt = progress.Next is { } next ? IndexOf(quests, next.RowId) : quests.Count;
                var left = 0;
                for (var i = 0; i < quests.Count; i++)
                {
                    if (i < nextAt)
                    {
                        byRowId[quests[i].RowId] = i - nextAt;
                    }
                    else if (source.LeavesTotals(quests[i].RowId) || source.StateOf(quests[i].RowId) == QuestState.Completed)
                    {
                        // Locked out, or already done out of order: not a quest left before the join.
                        byRowId[quests[i].RowId] = Math.Max(left - 1, 0);
                    }
                    else
                    {
                        byRowId[quests[i].RowId] = left++;
                    }
                }

                if (progress.Status is MsqRouteStatus.NotStarted or MsqRouteStatus.InProgress)
                {
                    sum += left;
                    shortest = Math.Min(shortest, left);
                }
            }

            ToJoin = branch.JoinKind == JoinKind.Any ? (shortest == int.MaxValue ? 0 : shortest) : sum;
        }

        public uint JoinRowId { get; }

        /// <summary>How many quests ahead the reconvergence quest lies.</summary>
        public int ToJoin { get; }

        public bool TryRoute(uint rowId, out int distance) => byRowId.TryGetValue(rowId, out distance);

        private static int IndexOf(IReadOnlyList<QuestRecord> quests, uint rowId)
        {
            for (var i = 0; i < quests.Count; i++)
            {
                if (quests[i].RowId == rowId)
                {
                    return i;
                }
            }

            return quests.Count;
        }
    }
}
