using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>A quest that unlocks a system feature, from <c>curated/system_unlocks.json</c>.</summary>
public sealed record SystemUnlock(string Label, string Kind, string? Note);

/// <summary>
/// The quest (or quests, one per path) that opens an aetheryte the first-visit rule cannot place, from
/// <c>curated/aetheryte_unlocks.json</c> (feature plan v6 K1): it replaces whatever the rule inferred for that aetheryte.
/// </summary>
/// <param name="Name">The aetheryte's name, for the file's reader (rows print the game's name).</param>
/// <param name="Quests">Quest row ids that open it; several for one reached on different paths.</param>
public sealed record AetheryteUnlock(string Name, IReadOnlyList<uint> Quests, string Note, string Evidence);

/// <summary>
/// Side quests a main scenario quest needs that neither the sheets' previous quests nor its required duties record,
/// from <c>curated/story_required.json</c> (feature plan v7 N3): the Shadowbringers role quests The Light of
/// Inspiration asks for, say. The story meter and the catch-up count them with the quests the sheets give
/// (<c>Query.StoryRequirements</c>).
/// </summary>
/// <param name="Quests">The quests named: each brings the side quests before it on its line.</param>
/// <param name="Join"><see cref="JoinKind.All"/> (<c>allOf</c>): every one; <see cref="JoinKind.Any"/> (<c>anyOf</c>): one line will do.</param>
public sealed record StoryRequiredEntry(IReadOnlyList<uint> Quests, JoinKind Join, string Note, string Evidence);

/// <summary>A quest that unlocks duties, from <c>curated/duty_unlocks.json</c>.</summary>
public sealed record DutyUnlock(IReadOnlyList<uint> ContentFinderConditionIds, string? Note);

/// <summary>
/// An expansion's launch, from <c>curated/expansion_launches.json</c> (1.20.0 N7): the day its early access opens (UTC,
/// the start of that day), whether that day is still an estimate, and the page it was read from. The Before Evercold
/// card names the day and retires on it (<see cref="Plan.EvercoldPrep"/>).
/// </summary>
/// <param name="Expansion">The ExVersion row the launch brings (6 is Evercold).</param>
/// <param name="Name">The expansion's name ("Evercold").</param>
/// <param name="Expected">An estimate, not an announcement: the card says "(expected)".</param>
public sealed record ExpansionLaunch(byte Expansion, string Name, DateTime EarlyAccessUtc, bool Expected, string Evidence, string Note);

/// <summary>
/// A seasonal event window, from <c>curated/festivals.json</c>. Null dates mean "unknown; use the live flag only".
/// <paramref name="Evidence"/> is the announcement the dates were read from (the Lodestone page); the plugin shows an
/// end date only when it has one, so a date is never shown that cannot be attributed.
/// </summary>
public sealed record FestivalInfo(string Name, DateTime? Start, DateTime? End, bool MogStation, string? Evidence = null, string? Note = null)
{
    /// <summary>
    /// A collaboration's dated runs (1.19.0, C10): the game reruns a collaboration under the same Festival id, so its
    /// entry keeps <see cref="End"/> null (it never reads as past between runs) and lists each run with its own
    /// announcement here, oldest first. The run under way gives the running event its end date
    /// (<see cref="Seasonal.SeasonalNow.AnnouncedEnd"/>). Empty for every other entry.
    /// </summary>
    public IReadOnlyList<FestivalRun> Runs { get; init; } = [];

    /// <summary>"Moonfire Faire (2014)": the edition year closing a curated name.</summary>
    private static readonly Regex EditionYear = new(@"\(\d{4}\)\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// An undated entry whose name carries no edition year: a collaboration event the game reruns under the same
    /// Festival id (A Nocturne for Heroes, Blunderville), which never reads as past. An undated edition keeps its year
    /// in the name ("The Rising (2024)") and is not a rerun.
    /// </summary>
    public bool IsRerun => End is null && Name.Length > 0 && !EditionYear.IsMatch(Name);
}

/// <summary>
/// One dated run of a collaboration event (<see cref="FestivalInfo.Runs"/>): its window (UTC; the close of the last day
/// when the announcement gives dates only) and the announcement or event page it was read from (https).
/// </summary>
public sealed record FestivalRun(DateTime Start, DateTime End, string Evidence);

/// <summary>
/// A named quest chain from <c>curated/chains.json</c>, in one of three forms: journal genres concatenated in the listed
/// order (<see cref="GenreIds"/>), a list of quests (<see cref="QuestIds"/>), or a first quest the line grows from
/// (<see cref="StartQuest"/>: it and every side quest of its journal section that requires a quest of the line, so a
/// later patch's chapter joins it by itself). Exactly one form is set.
/// </summary>
public sealed record CuratedChain(string Name, IReadOnlyList<uint> GenreIds, string? Note)
{
    /// <summary>Quest row ids in play order (feature plan v7 P5); empty unless the chain is listed by quest.</summary>
    public IReadOnlyList<uint> QuestIds { get; init; } = [];

    /// <summary>The quest a line grows from (feature plan v7 P5); 0 unless the chain is given by its first quest.</summary>
    public uint StartQuest { get; init; }

    /// <summary>
    /// A series the game still adds to patch by patch (Hildibrand, Cosmic Exploration, the Occult Crescent): with every
    /// released quest done it reads "Caught up · continues in a later patch" rather than finished (feature plan v7 P5).
    /// </summary>
    public bool Ongoing { get; init; }
}

/// <summary>
/// A quest reward that the FFXIV Online Store also sells, from <c>curated/online_store.json</c>. The file is keyed by
/// the store item's row id; <paramref name="Kind"/> and <paramref name="RewardId"/> name the collectible that item
/// unlocks, so a reward a quest grants directly (an emote with no item) still matches.
/// </summary>
public sealed record OnlineStoreItem(string Name, RewardKind Kind, uint RewardId, string Evidence, string? Note);

/// <summary>
/// A quest reward that can also be had somewhere other than the quest and the Online Store, from
/// <c>curated/other_sources.json</c>, keyed by the reward item's row id: today the Darklight and Hero's accessories that
/// also drop in A Realm Reborn dungeons (<see cref="OtherSource.DungeonDrop"/>).
/// </summary>
/// <param name="Source">An <see cref="OtherSource"/> name the file may use (<see cref="CuratedData.OtherSourcesFileSources"/>).</param>
/// <param name="Where">Where it drops, as players read it ("Snowcloak, Sastasha (Hard) and The Sunken Temple of Qarn (Hard)").</param>
public sealed record OtherSourceItem(string Name, string Source, string Where, string Evidence, string Note);

/// <summary>
/// A quest pinned to a journal genre after the refiling rules ran, from <c>curated/refile_overrides.json</c>: a
/// quest whose sheet signals point at the wrong genre (the Eureka entry quasi-quests land in Kugane Sidequests by
/// their issuer's zone; they belong with The Forbidden Land, Eureka) or at none.
/// </summary>
/// <param name="GenreId">JournalGenre row id the quest is filed under.</param>
public sealed record RefileOverride(uint GenreId, string Note, string Evidence);

/// <summary>
/// A quest the game removed that the sheets do not mark, from <c>curated/retired_quests.json</c>: sidequests deleted
/// without a placeholder issuer or the hidden flag, and listed rows the game retired but never re-filed.
/// </summary>
/// <param name="Patch">The patch that removed it ("6.3"), when known; empty otherwise.</param>
public sealed record RetiredQuest(string Note, string Evidence, string Patch);

/// <summary>
/// A known quirk of one quest, from <c>curated/quirks.json</c>: the game behaves differently from what its data
/// says (a prerequisite it waives, such as Up in Arms once the Zenith is in hand) or a name changed and older guides
/// mislead ("Bloodsworn" is "Allied" since 7.0; a prerequisite re-pointed in 7.5). The detail pane shows the note
/// under the requirements, <c>/tsuki why</c> prints it and the diagnostic block carries it.
/// </summary>
public sealed record QuestQuirk(string Note, string Evidence);

/// <summary>
/// Quests the game wants completed before it offers a quest that neither the sheet's previous quests nor its accept
/// conditions record, from <c>curated/extra_prerequisites.json</c> (feature plan v5, 1.5.0 Gates): most are a main
/// scenario milestone the quest's own text names ("you must first complete the main scenario quest …"). The catalog
/// adds them to <c>QuestCatalog.PrerequisitesOf</c> as an "all" list, so the evaluator, the blocker text, the reverse
/// index and every route see them.
/// </summary>
/// <param name="Requires">Quest row ids, each needed; never empty.</param>
/// <param name="Sources">
/// At least two of <see cref="CuratedData.ExtraPrerequisiteSources"/> that each name every id of
/// <paramref name="Requires"/>: the wiki's infobox, Questionable's hand-added links (the committed snapshot,
/// <c>docs/data/questionable-prerequisites.json</c>) and the game's own quest text.
/// </param>
/// <param name="GameTextKey">The quest text row that names the requirement (<c>TEXT_LUCKBA131_03246_SYSTEM_100_001</c>) when <paramref name="Sources"/> holds <c>gameText</c>; the text itself is never committed.</param>
public sealed record ExtraPrerequisite(IReadOnlyList<uint> Requires, IReadOnlyList<string> Sources, string Evidence, string Note, string? GameTextKey);

/// <summary>
/// A gate the game checks before it offers a quest that no quest of the sheets records, from <c>curated/game_gates.json</c>:
/// a relic weapon or tool at some stage equipped or held, a mount collection, unlock links (Occult Record entries, a
/// blue magic spell, the chocobo companion), Eureka story and elemental level, a deep-dungeon floor, a Resistance rank,
/// Doman Enclave reconstruction progress. The catalog lists it as a requirement (<c>QuestCatalog.GameGateOf</c>): a
/// gate with weapons, mounts or unlock links is judged from what the capture read, one met by a <paramref name="MetBy"/>
/// quest is met, and any other is never judged, so the quest reads Not checked where it would otherwise read Ready,
/// never Blocked by it; it adds <paramref name="After"/> to <c>QuestCatalog.PrerequisitesOf</c>, the quests before
/// which the gate cannot be passed at all.
/// </summary>
/// <param name="Gate">What the game wants, in English, as a phrase after "needs" ("a relic weapon nexus equipped").</param>
/// <param name="After">Quest row ids the gate cannot be passed before (the step that makes a nexus possible); may be empty.</param>
/// <param name="GameTextKey">The gated quest's own text row that states the gate, when the game states it (<c>TEXT_JOBREL015_00361_SYSTEM_000_000</c>); the text is never committed.</param>
/// <param name="AfterTextKey">The text row of an <paramref name="After"/> quest that says what it opens (the soulglazing, Eureka Pagos); required with <paramref name="After"/>.</param>
/// <param name="Items">For a gear gate (a relic weapon at a stage equipped, or held), the weapons that pass it, which make the gate one Tsukimichi can judge from the captured gear; null for any other gate.</param>
/// <param name="Mounts">For a mount-collection gate (the seven Lanners before the Firebird), the mounts that must all be owned, judged from the owned mounts a capture reads; null for any other gate.</param>
/// <param name="UnlockLinks">For a gate the game keeps as unlock links (feature plan v7 C3: Occult Record entries, a blue magic spell learned, the chocobo companion), the links that must all be set, judged from the links a capture reads; null for any other gate.</param>
/// <param name="MetBy">Quest row ids the game gives only once the gate is passed (What Lies Beneath after floor 50 of the Palace of the Dead): one of them completed meets the gate; without one the gate is judged as it otherwise would be. Empty for most gates.</param>
/// <param name="AcceptConditions">The sheet's accept conditions (<c>QuestAcceptAdditionCondition</c>) this gate stands for, values that are no quest: listed under the gate rather than as an accept condition not checked. Empty for most gates.</param>
/// <param name="Lodestone">The quest's Lodestone Eorzea Database page, when its requirement line states the gate ("Players must first progress through … before accepting this quest"); the <see cref="QuestGate.LodestoneSource"/> source.</param>
/// <param name="Questionable">Questionable holds the quest back on the same check (<c>docs/data/questionable-locks.json</c> lists the quest); the <see cref="QuestGate.QuestionableSource"/> source.</param>
/// <param name="PlayerConfirmed">Why a gate the wiki alone states may stand (the <see cref="QuestGate.PlayerSource"/> source): only for a gate Tsukimichi never judges (<see cref="NeverJudged"/>), whose evidence is the wiki page and which no other source states; the player confirms it with "I've done this". Null for every other gate.</param>
/// <param name="RequiredTextKey">The text row of a quest the sheet already requires (a previous quest or accept condition) that says the gated quest waits for more (A Spellbinding Read: the advanced dungeon opens through Memolivia once more of the tale is explored); the text is never committed.</param>
/// <param name="Duties">The duties the gate counts, by their ContentFinderCondition name (Abridged Too Far: The Merchant's Tale, whose final bosses it counts); the verifier's duties fact reads a wiki duty named here as this gate (<see cref="DutiesItCounts"/>). Null for most gates; it changes nothing the plugin judges.</param>
public sealed record GameGate(
    string Gate,
    IReadOnlyList<uint> After,
    string? GameTextKey,
    string? AfterTextKey,
    string Evidence,
    string Note,
    GateItemSet? Items = null,
    GateMountSet? Mounts = null,
    GateUnlockLinkSet? UnlockLinks = null,
    IReadOnlyList<uint>? MetBy = null,
    IReadOnlyList<uint>? AcceptConditions = null,
    string? Lodestone = null,
    bool Questionable = false,
    string? PlayerConfirmed = null,
    string? RequiredTextKey = null,
    IReadOnlyList<string>? Duties = null)
{
    /// <summary><see cref="MetBy"/>, never null.</summary>
    public IReadOnlyList<uint> MetByIds => MetBy ?? [];

    /// <summary><see cref="Duties"/>, never null.</summary>
    public IReadOnlyList<string> DutyNames => Duties ?? [];

    /// <summary>
    /// The verifier's duties rule (1.22.0, owner ruling 3): the duties a source names beyond <paramref name="sheetDuties"/>
    /// when the gate names every one of them explicitly (<see cref="Duties"/>) and the source names every duty the sheet
    /// requires. The source then describes this gate, which the catalog carries, not a duty the sheet leaves out. Null
    /// when there is no such duty, when the gate does not name one of them (its phrase never counts), or when the source
    /// leaves out a duty the sheet requires. Names compare by <paramref name="canon"/>.
    /// </summary>
    public IReadOnlyList<string>? DutiesItCounts(IReadOnlyList<string> sheetDuties, IReadOnlyList<string> sourceDuties, Func<string, string> canon)
    {
        ArgumentNullException.ThrowIfNull(sheetDuties);
        ArgumentNullException.ThrowIfNull(sourceDuties);
        ArgumentNullException.ThrowIfNull(canon);
        var sheet = sheetDuties.Select(canon).ToHashSet(StringComparer.Ordinal);
        var source = sourceDuties.Select(canon).ToHashSet(StringComparer.Ordinal);
        var named = DutyNames.Select(canon).ToHashSet(StringComparer.Ordinal);
        var extra = sourceDuties.Where(d => !sheet.Contains(canon(d))).ToList();
        return extra.Count > 0 && sheet.All(source.Contains) && extra.All(d => named.Contains(canon(d))) ? extra : null;
    }

    /// <summary><see cref="AcceptConditions"/>, never null.</summary>
    public IReadOnlyList<uint> AcceptConditionIds => AcceptConditions ?? [];

    /// <summary>Tsukimichi never judges the gate: it has no weapons, mounts, unlock links, met-by quests, accept conditions or after quests.</summary>
    public bool NeverJudged => Items is null && Mounts is null && UnlockLinks is null && MetByIds.Count == 0 && AcceptConditionIds.Count == 0 && After.Count == 0;

    /// <summary>
    /// Where the gate is confirmed (<see cref="QuestGate.Sources"/>): the game's text when <see cref="GameTextKey"/>,
    /// <see cref="AfterTextKey"/> or <see cref="RequiredTextKey"/> names a row, the sheets when weapons, mounts, unlock
    /// links or accept conditions are derived from them, the wiki when <see cref="Evidence"/> is a Console Games Wiki
    /// page, the Lodestone when <see cref="Lodestone"/> names the quest's page, Questionable when
    /// <see cref="Questionable"/> is set, and the player when <see cref="PlayerConfirmed"/> gives the reason.
    /// </summary>
    public IReadOnlyList<string> SourceKinds
    {
        get
        {
            var kinds = new List<string>(6);
            if (GameTextKey is not null || AfterTextKey is not null || RequiredTextKey is not null)
            {
                kinds.Add(QuestGate.GameTextSource);
            }

            if (Items is not null || Mounts is not null || UnlockLinks is not null || AcceptConditionIds.Count > 0)
            {
                kinds.Add(QuestGate.SheetSource);
            }

            if (Evidence.StartsWith("https://ffxiv.consolegameswiki.com/", StringComparison.Ordinal))
            {
                kinds.Add(QuestGate.WikiSource);
            }

            if (Lodestone is not null)
            {
                kinds.Add(QuestGate.LodestoneSource);
            }

            if (Questionable)
            {
                kinds.Add(QuestGate.QuestionableSource);
            }

            if (PlayerConfirmed is not null)
            {
                kinds.Add(QuestGate.PlayerSource);
            }

            return kinds;
        }
    }
}

/// <summary>
/// An unlock-link gate's links in <c>game_gates.json</c> (<c>"unlockLinks"</c>, feature plan v7 C3): every one must be
/// set, and where they were derived from, which the game-data tests derive again and compare. An unlock link is the
/// game's own flag for a thing a character has opened (the value space of <c>Quest.SystemReward</c>, of an accept
/// condition that is no quest, and of <c>Action.UnlockLink</c>): <c>UIState.IsUnlockLinkUnlocked</c> reads it.
/// </summary>
/// <param name="Sources">
/// The sheet rows the links come from, as <c>Sheet#row</c>: <c>QuestAcceptAdditionCondition#70852</c> (the values of
/// the gated quest's own accept row that are no quest), <c>Quest#67093</c> (the link the gated quest's own row says it
/// waits for, its <c>Header</c> column: the floor-50 clear of the Palace of the Dead), <c>Action#11395</c> (the
/// <c>UnlockLink</c> of an action, a blue magic spell learned).
/// </param>
/// <param name="All">Unlock link ids, each needed. Ascending, distinct, never empty, each below 65536.</param>
public sealed record GateUnlockLinkSet(IReadOnlyList<string> Sources, uint[] All);

/// <summary>
/// A mount-collection gate's mounts in <c>game_gates.json</c> (<c>"mounts"</c>, 1.11.0): every one must be owned, and
/// where they were derived from, which the game-data tests derive again and compare.
/// </summary>
/// <param name="Sources">
/// The sheet rows the mounts come from, as <c>Sheet#row</c>: <c>Mount#105</c> is the collection the reward mount 105
/// (the Firebird) crowns, the mounts whose <c>UIPriority</c> sits in its hundred with a ones digit (30100 to 30109 for
/// the Firebird's 30150), so a later addition to the family (the Wondrous Lanner, 30110) is not one of the set.
/// </param>
/// <param name="All">Mount row ids, each needed. Ascending, distinct, never empty.</param>
public sealed record GateMountSet(IReadOnlyList<string> Sources, uint[] All);

/// <summary>
/// A gear gate's weapons in <c>game_gates.json</c> (<c>"equipped"</c> or <c>"held"</c>): the groups that pass it and
/// where they were derived from, which the game-data tests derive again and compare.
/// </summary>
/// <param name="Hold">Equipped (<c>"equipped"</c>) or equipped, in the Armoury Chest or in the inventory (<c>"held"</c>).</param>
/// <param name="Sources">The sheet rows the items come from, as <c>Sheet#row</c>: <c>RelicItem#5</c> (a Zodiac stage), <c>AnimaWeaponItem#7</c> (an anima stage), <c>QuestClassJobReward#8</c> (the RequiredItem of the quest's own reward row), <c>Quest#67823</c> (the items a quest's script parameters name as <c>REPLICA_</c>).</param>
/// <param name="Shield">For a <c>RelicItem</c>, <c>AnimaWeaponItem</c> or <c>Quest</c> source, how a paladin's shield counts: <c>both</c> (sword and shield together), <c>either</c> (each alone), <c>none</c> (the sword alone); null for <c>QuestClassJobReward</c>, whose rows group them already.</param>
/// <param name="Groups">The weapons: any one group passes when every item of it is there. Ascending.</param>
public sealed record GateItemSet(GateHold Hold, IReadOnlyList<string> Sources, string? Shield, uint[][] Groups);

/// <summary>
/// A "Before you continue" payoff gate (P5), from <c>curated/payoff_gates.json</c>: optional content whose completion
/// changes a scene of the milestone quest's story. The plugin shows <see cref="Instruction"/> only while the milestone
/// is Ready or in the journal and the content is not done (<c>Core.Payoff.PayoffGates</c>); <see cref="Why"/> is the
/// spoiler behind a closed "why?".
/// </summary>
/// <param name="Id">The entry's key ("eden"): stable, spoiler-free, what per-character notice and disclosure state is stored by.</param>
/// <param name="MilestoneRowId">Quest row id at which the gate speaks.</param>
/// <param name="BeforeRowIds">Quest row ids that must all be completed; empty when <paramref name="BeforeChain"/> names the content.</param>
/// <param name="BeforeChain">A <c>chains.json</c> chain name whose every quest must be completed; null when the ids are listed.</param>
/// <param name="Instruction">Spoiler-free: names the optional content, never the payoff ("Finish the Eden raid series first.").</param>
/// <param name="Why">The reason, a spoiler, shown only when opened.</param>
/// <param name="Evidence">https URLs the pairing was read from.</param>
/// <param name="Note">Why the milestone and the content were chosen.</param>
public sealed record PayoffGate(
    string Id,
    uint MilestoneRowId,
    IReadOnlyList<uint> BeforeRowIds,
    string? BeforeChain,
    string Instruction,
    string Why,
    IReadOnlyList<string> Evidence,
    string Note);

/// <summary>
/// Hand-maintained overlays shipped in the plugin's <c>curated/</c> directory. Every file is optional and every entry
/// is validated on its own, so one bad line never hides the rest. Shapes (object keys are row ids as strings; keys
/// starting with <c>$</c>, such as <c>$schema_note</c>, are comments and ignored everywhere):
/// <code>
/// system_unlocks.json  { "66038": { "label": "Glamour Dresser", "kind": "system", "note": "..." } }
/// duty_unlocks.json    { "66038": [ 4, 5 ] }  or  { "66038": { "contentFinderConditionIds": [ 4, 5 ], "note": "..." } }
/// feature_quests.json  [ 66038, 66039 ]  or  { "questRowIds": [ 66038, 66039 ], "note": "..." }   (written by DataGen, not by hand)
/// festivals.json       { "1": { "name": "Starlight Celebration", "start": "2025-12-15T08:00:00Z", "end": "...", "mogStation": false, "evidence": "https://...", "note": "..." } }
///                      or  { "entries": { "1": { ... } } }
/// chains.json          { "chains": [ { "name": "Hildibrand", "genreIds": [ 93, 94 ], "note": "..." } ] }
/// online_store.json    { "schema": 1, "note": "...", "entries": { "22437": { "name": "Starlight Bear", "kind": "Mount", "rewardId": 99, "evidence": "https://...", "note": "..." } } }
/// other_sources.json   { "schema": 1, "note": "...", "entries": { "4520": { "name": "Darklight Band of Striking", "source": "DungeonDrop", "where": "...", "evidence": "https://...", "note": "..." } } }
/// refile_overrides.json { "schema": 1, "entries": { "68478": { "genre": 90, "note": "...", "evidence": "https://..." } } }
/// retired_quests.json  { "schema": 1, "entries": { "66033": { "note": "...", "evidence": "https://...", "patch": "6.3" } } }   (patch optional)
/// quirks.json          { "schema": 1, "entries": { "66971": { "note": "...", "evidence": "https://..." } } }
/// extra_prerequisites.json { "schema": 1, "entries": { "68782": { "requires": [ 68850 ], "sources": [ "gameText", "questionable", "wiki" ], "gameTextKey": "TEXT_...", "evidence": "https://...", "note": "..." } } }   (gameTextKey only with gameText)
/// game_gates.json      { "schema": 1, "entries": { "65897": { "gate": "a relic weapon nexus equipped", "after": [ 65742 ], "gameTextKey": "TEXT_...", "afterTextKey": "TEXT_...", "evidence": "https://...", "note": "..." } } }   (after, gameTextKey optional; afterTextKey with after; a gear gate adds "equipped" or "held": { "sources": [ "RelicItem#5" ], "shield": "both", "items": [ [ 8649, 8658 ], ... ] }; a mount-collection gate adds "mounts": { "sources": [ "Mount#105" ], "all": [ 75, 76, ... ] })
/// payoff_gates.json    { "schema": 1, "review": "...", "entries": { "eden": { "milestone": 70286, "before": "Eden" or [ 69515 ], "instruction": "...", "why": "...", "evidence": [ "https://..." ], "note": "..." } } }
/// path_choices.json    { "schema": 1, "cities": { "65575": { "label": "Gridania", "note": "..." } },
///                        "classes": { "1": { "label": "Gladiator", "closeToHome": 66104, "starter": 65789, "note": "..." } },
///                        "grandCompanies": { "66216": { "grandCompany": 2, "note": "..." } } }   (classes keyed by ClassJob row id)
/// expansion_launches.json { "schema": 1, "entries": { "6": { "name": "Evercold", "earlyAccess": "2027-01-22", "expected": true, "evidence": "https://...", "note": "..." } } }   (keyed by ExVersion row)
/// story_required.json { "schema": 1, "entries": { "69186": { "anyOf": [ 68784, 68808 ], "evidence": "https://...", "note": "..." } } }   ("allOf" for every one; keyed by the main scenario quest)
/// aetheryte_unlocks.json { "schema": 1, "entries": { "75": { "name": "Idyllshire", "quests": [ 67116 ], "evidence": "https://...", "note": "..." } } }   (keyed by Aetheryte row id)
/// giver_portraits.json { "schema": 1, "crops": { .. }, "iconCrops": { .. }, "faces": { .. }, "aliases": { .. }, "blocks": { .. }, "pins": { .. } }   (see <see cref="Portraits.PortraitCuration"/>)
/// VERSION.json        { "curatedRevision": "573d225" }   (written by tools/regen.ps1; absent in a checkout that never ran it)
/// </code>
/// Every file must be strict JSON (no comments, no trailing commas), as the curated README requires.
/// </summary>
public sealed class CuratedData
{
    public const string SystemUnlocksFileName = "system_unlocks.json";
    public const string DutyUnlocksFileName = "duty_unlocks.json";
    public const string FeatureQuestsFileName = "feature_quests.json";
    public const string FestivalsFileName = "festivals.json";
    public const string ChainsFileName = "chains.json";
    public const string OnlineStoreFileName = "online_store.json";
    public const string OtherSourcesFileName = "other_sources.json";
    public const string RefileOverridesFileName = "refile_overrides.json";
    public const string RetiredQuestsFileName = "retired_quests.json";
    public const string QuirksFileName = "quirks.json";
    public const string PayoffGatesFileName = "payoff_gates.json";
    public const string PathChoicesFileName = "path_choices.json";
    public const string ExtraPrerequisitesFileName = "extra_prerequisites.json";
    public const string GameGatesFileName = "game_gates.json";
    public const string AetheryteUnlocksFileName = "aetheryte_unlocks.json";
    public const string GiverPortraitsFileName = "giver_portraits.json";
    public const string StoryRequiredFileName = "story_required.json";
    public const string ExpansionLaunchesFileName = "expansion_launches.json";
    public const string StoryCastFileName = "story_cast.json";

    /// <summary>The sources an <see cref="ExtraPrerequisitesFileName"/> entry may cite; each entry needs two of them.</summary>
    public static readonly IReadOnlyList<string> ExtraPrerequisiteSources = [GameTextSource, QuestionableSource, WikiSource];

    /// <summary>The game's own quest text names the requirement; the entry carries the row's key.</summary>
    public const string GameTextSource = "gameText";

    /// <summary>Questionable hand-adds the link (the snapshot under <c>docs/data/questionable-prerequisites.json</c>).</summary>
    public const string QuestionableSource = "questionable";

    /// <summary>The Console Games Wiki's quest infobox names the requirement.</summary>
    public const string WikiSource = "wiki";

    /// <summary>The <see cref="OtherSource"/> names <see cref="OtherSourcesFileName"/> may use; any other is a skipped entry.</summary>
    public static readonly IReadOnlyList<string> OtherSourcesFileSources = [OtherSource.DungeonDrop];

    /// <summary>Written by <c>tools/regen.ps1</c>: the overlay's revision for the About stamp and the diagnostic block.</summary>
    public const string VersionFileName = "VERSION.json";

    /// <summary>The key in <see cref="VersionFileName"/> holding the short git hash of the last commit touching the overlay.</summary>
    public const string CuratedRevisionKey = "curatedRevision";

    private const string DefaultSystemKind = "system";

    /// <summary>Object files may wrap their entries under this key (festivals.json does); the wrapper's other keys are ignored.</summary>
    private const string EntriesKey = "entries";

    /// <summary>feature_quests.json may wrap its ids under this key.</summary>
    private const string QuestRowIdsKey = "questRowIds";

    /// <summary>Object keys starting with this are comments (<c>$schema_note</c>) and never entries.</summary>
    private const char CommentKeyPrefix = '$';

    /// <summary>The parse options every curated file must satisfy: no comments, no trailing commas.</summary>
    public static readonly JsonDocumentOptions StrictOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    private CuratedData(
        IReadOnlyDictionary<uint, SystemUnlock> systemUnlocks,
        IReadOnlyDictionary<uint, DutyUnlock> dutyUnlocks,
        IReadOnlySet<uint> featureQuests,
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        IReadOnlyList<CuratedChain> chains,
        IReadOnlyDictionary<uint, OnlineStoreItem> onlineStore,
        IReadOnlyDictionary<uint, OtherSourceItem> otherSources,
        IReadOnlyDictionary<uint, RefileOverride> refileOverrides,
        IReadOnlyDictionary<uint, RetiredQuest> retiredQuests,
        IReadOnlyDictionary<uint, QuestQuirk> quirks,
        string curatedRevision,
        IReadOnlyList<string> warnings)
    {
        SystemUnlocks = systemUnlocks;
        DutyUnlocks = dutyUnlocks;
        FeatureQuests = featureQuests;
        Festivals = festivals;
        Chains = chains;
        OnlineStore = onlineStore;
        OtherSources = otherSources;
        RefileOverrides = refileOverrides;
        RetiredQuests = retiredQuests;
        Quirks = quirks;
        CuratedRevision = curatedRevision;
        Warnings = warnings;
    }

    public static readonly CuratedData Empty = new(
        new Dictionary<uint, SystemUnlock>(),
        new Dictionary<uint, DutyUnlock>(),
        new HashSet<uint>(),
        new Dictionary<ushort, FestivalInfo>(),
        [],
        new Dictionary<uint, OnlineStoreItem>(),
        new Dictionary<uint, OtherSourceItem>(),
        new Dictionary<uint, RefileOverride>(),
        new Dictionary<uint, RetiredQuest>(),
        new Dictionary<uint, QuestQuirk>(),
        string.Empty,
        []);

    public IReadOnlyDictionary<uint, SystemUnlock> SystemUnlocks { get; }
    public IReadOnlyDictionary<uint, DutyUnlock> DutyUnlocks { get; }
    public IReadOnlySet<uint> FeatureQuests { get; }
    public IReadOnlyDictionary<ushort, FestivalInfo> Festivals { get; }

    /// <summary>Named chains in file order; genre ids are not checked against the catalog here.</summary>
    public IReadOnlyList<CuratedChain> Chains { get; }

    /// <summary>Rewards the Online Store also sells, by store item row id.</summary>
    public IReadOnlyDictionary<uint, OnlineStoreItem> OnlineStore { get; }

    /// <summary>Rewards that also come from somewhere other than the quest (a dungeon drop), by reward item row id.</summary>
    public IReadOnlyDictionary<uint, OtherSourceItem> OtherSources { get; }

    /// <summary>Quests pinned to a genre after the refiling rules, by quest row id; read by <c>JournalRefiler</c>.</summary>
    public IReadOnlyDictionary<uint, RefileOverride> RefileOverrides { get; }

    /// <summary>Quests the game removed that the sheets do not mark, by quest row id; read by <c>JournalRefiler</c>.</summary>
    public IReadOnlyDictionary<uint, RetiredQuest> RetiredQuests { get; }

    /// <summary>Known quirks by quest row id: a note the detail pane, <c>/tsuki why</c> and the diagnostic block show.</summary>
    public IReadOnlyDictionary<uint, QuestQuirk> Quirks { get; }

    /// <summary>"Before you continue" payoff gates in file order (P5); milestones and content are not checked against the catalog here.</summary>
    public IReadOnlyList<PayoffGate> PayoffGates { get; private init; } = [];

    /// <summary>City and class labels, the city pin and the Grand Company tags the choice groups read (<c>Evaluation.PathIndex</c>).</summary>
    public PathChoices PathChoices { get; private init; } = PathChoices.Empty;

    /// <summary>Prerequisites neither the sheet nor its accept conditions record, by quest row id; ids not checked against the catalog here.</summary>
    public IReadOnlyDictionary<uint, ExtraPrerequisite> ExtraPrerequisites { get; private init; } = new Dictionary<uint, ExtraPrerequisite>();

    /// <summary><see cref="ExtraPrerequisites"/> as the catalog builders take them (<c>QuestCatalog.Build</c>): quest row id to required row ids.</summary>
    public IReadOnlyDictionary<uint, uint[]> ExtraPrerequisiteIds => ExtraPrerequisites.ToDictionary(kv => kv.Key, kv => kv.Value.Requires.ToArray());

    /// <summary>Gates the game checks that Tsukimichi cannot read, by quest row id; ids not checked against the catalog here.</summary>
    public IReadOnlyDictionary<uint, GameGate> GameGates { get; private init; } = new Dictionary<uint, GameGate>();

    /// <summary>
    /// Aetherytes the first-visit rule cannot place, by Aetheryte row id, with the quests that open them
    /// (<see cref="AetheryteUnlock"/>); ids not checked against the sheets here.
    /// </summary>
    public IReadOnlyDictionary<uint, AetheryteUnlock> AetheryteUnlocks { get; private init; } = new Dictionary<uint, AetheryteUnlock>();

    /// <summary>
    /// Side quests main scenario quests need that the sheets do not record, by the main scenario quest's row id
    /// (<see cref="StoryRequiredEntry"/>); ids not checked against the catalog here.
    /// </summary>
    public IReadOnlyDictionary<uint, StoryRequiredEntry> StoryRequired { get; private init; } = new Dictionary<uint, StoryRequiredEntry>();

    /// <summary>
    /// The story cast overlay (feature plan v7 N10, <c>story_cast.json</c>): names joined under one character and names
    /// kept out, which <c>QuestCastReader</c> applies when it builds the <see cref="global::Tsukimichi.Core.Chains.StoryCast"/>.
    /// </summary>
    public global::Tsukimichi.Core.Chains.StoryCastCuration StoryCast { get; private init; } = global::Tsukimichi.Core.Chains.StoryCastCuration.Empty;

    /// <summary>
    /// The giver portrait overlay (feature plan v7 F3): crops, names for unnamed faces, aliases, blocked matches and
    /// pins, which <c>GiverPortraitSources</c> applies when it builds the <see cref="Portraits.PortraitIndex"/>.
    /// </summary>
    public Portraits.PortraitCuration GiverPortraits { get; private init; } = Portraits.PortraitCuration.Empty;

    /// <summary>Expansion launches by ExVersion row (<see cref="ExpansionLaunchesFileName"/>): the Before Evercold card's day.</summary>
    public IReadOnlyDictionary<byte, ExpansionLaunch> ExpansionLaunches { get; private init; } = new Dictionary<byte, ExpansionLaunch>();

    /// <summary><see cref="GameGates"/> as the catalog builders take them (<c>QuestCatalog.Build</c>).</summary>
    public IReadOnlyDictionary<uint, QuestGate> GameGateIds => GameGates.ToDictionary(
        kv => kv.Key,
        kv => new QuestGate(kv.Value.Gate, kv.Value.After.ToArray(), kv.Value.Items is { } items ? new GateItems(items.Hold, items.Groups) : null, kv.Value.Mounts?.All)
        {
            UnlockLinks = kv.Value.UnlockLinks?.All,
            MetBy = [.. kv.Value.MetByIds],
            AcceptConditions = [.. kv.Value.AcceptConditionIds],
            Sources = kv.Value.SourceKinds,
        });

    /// <summary>
    /// Short git hash of the last commit touching the overlay, from <see cref="VersionFileName"/> ("573d225", or
    /// "573d225-dirty" when regenerated with uncommitted changes); empty when the file is absent or has no value.
    /// </summary>
    public string CuratedRevision { get; }

    /// <summary>One line per skipped entry or unreadable file, for the caller to log once.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// The same data with <see cref="FeatureQuests"/> empty: what DataGen derives <c>feature_quests.json</c> from and
    /// what the invariants test compares the shipped file against, so the file never feeds its own derivation.
    /// </summary>
    public CuratedData WithoutFeatureQuests() =>
        FeatureQuests.Count == 0 ? this : new CuratedData(SystemUnlocks, DutyUnlocks, new HashSet<uint>(), Festivals, Chains, OnlineStore, OtherSources, RefileOverrides, RetiredQuests, Quirks, CuratedRevision, Warnings) { PayoffGates = PayoffGates, PathChoices = PathChoices, ExtraPrerequisites = ExtraPrerequisites, GameGates = GameGates, AetheryteUnlocks = AetheryteUnlocks, GiverPortraits = GiverPortraits, StoryRequired = StoryRequired, ExpansionLaunches = ExpansionLaunches, StoryCast = StoryCast };

    /// <summary>Loads every curated file under <paramref name="dir"/>. A missing directory or file yields empty collections.</summary>
    public static CuratedData Load(string dir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        if (!Directory.Exists(dir))
        {
            return Empty;
        }

        var warnings = new List<string>();
        var systemUnlocks = new Dictionary<uint, SystemUnlock>();
        var dutyUnlocks = new Dictionary<uint, DutyUnlock>();
        var featureQuests = new HashSet<uint>();
        var festivals = new Dictionary<ushort, FestivalInfo>();
        var chains = new List<CuratedChain>();
        var onlineStore = new Dictionary<uint, OnlineStoreItem>();
        var otherSources = new Dictionary<uint, OtherSourceItem>();
        var refileOverrides = new Dictionary<uint, RefileOverride>();
        var retiredQuests = new Dictionary<uint, RetiredQuest>();
        var quirks = new Dictionary<uint, QuestQuirk>();

        ForEachEntry(Path.Combine(dir, SystemUnlocksFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId))
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var label = StorageJson.ReadString(obj, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                warn("label is missing");
                return;
            }

            var kind = StorageJson.ReadString(obj, "kind");
            systemUnlocks[rowId] = new SystemUnlock(
                label,
                string.IsNullOrWhiteSpace(kind) ? DefaultSystemKind : kind,
                StorageJson.ReadString(obj, "note"));
        });

        ForEachEntry(Path.Combine(dir, DutyUnlocksFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId))
            {
                warn("key is not a quest row id");
                return;
            }

            JsonArray? ids;
            string? note = null;
            switch (node)
            {
                case JsonArray bare:
                    ids = bare;
                    break;
                case JsonObject obj:
                    ids = obj.TryGetPropertyValue("contentFinderConditionIds", out var idsNode) ? idsNode as JsonArray : null;
                    note = StorageJson.ReadString(obj, "note");
                    break;
                default:
                    ids = null;
                    break;
            }

            if (ids is null)
            {
                warn("value must be an array of content finder condition ids or an object with contentFinderConditionIds");
                return;
            }

            var parsed = new List<uint>(ids.Count);
            foreach (var element in ids)
            {
                if (!StorageJson.TryReadId(element, out var cfc))
                {
                    warn($"content finder condition id '{element}' is not a non-negative integer");
                    return;
                }

                parsed.Add(cfc);
            }

            dutyUnlocks[rowId] = new DutyUnlock(parsed, note);
        });

        ForEachElement(Path.Combine(dir, FeatureQuestsFileName), QuestRowIdsKey, warnings, (index, node, warn) =>
        {
            if (!StorageJson.TryReadId(node, out var rowId))
            {
                warn("not a quest row id");
                return;
            }

            featureQuests.Add(rowId);
        });

        ForEachEntry(Path.Combine(dir, FestivalsFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out ushort festivalId))
            {
                warn("key is not a festival id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warn("name is missing");
                return;
            }

            if (!StorageJson.TryReadUtc(obj, "start", out var start))
            {
                warn("start is not a valid timestamp");
                return;
            }

            if (!StorageJson.TryReadUtc(obj, "end", out var end))
            {
                warn("end is not a valid timestamp");
                return;
            }

            var mogStation = false;
            if (obj.TryGetPropertyValue("mogStation", out var mogNode) && mogNode is not null)
            {
                if (mogNode is not JsonValue mogValue || !mogValue.TryGetValue<bool>(out mogStation))
                {
                    warn("mogStation is not a boolean");
                    return;
                }
            }

            var evidence = StorageJson.ReadString(obj, "evidence");
            var note = StorageJson.ReadString(obj, "note");
            if (!TryReadRuns(obj, out var runs, out var runError))
            {
                warn(runError);
                return;
            }

            festivals[festivalId] = new FestivalInfo(
                name,
                start,
                end,
                mogStation,
                string.IsNullOrWhiteSpace(evidence) ? null : evidence,
                string.IsNullOrWhiteSpace(note) ? null : note)
            {
                Runs = runs,
            };
        });

        LoadChains(Path.Combine(dir, ChainsFileName), chains, warnings);

        ForEachEntry(Path.Combine(dir, OnlineStoreFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint itemId) || itemId == 0)
            {
                warn("key is not an item row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warn("name is missing");
                return;
            }

            var kindText = StorageJson.ReadString(obj, "kind");
            if (kindText is null || !Enum.TryParse<RewardKind>(kindText, ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
            {
                warn($"kind '{kindText}' is not a RewardKind");
                return;
            }

            if (!obj.TryGetPropertyValue("rewardId", out var rewardNode) || !StorageJson.TryReadId(rewardNode, out var rewardId) || rewardId == 0)
            {
                warn("rewardId is not a positive integer");
                return;
            }

            var evidence = StorageJson.ReadString(obj, "evidence");
            if (string.IsNullOrWhiteSpace(evidence))
            {
                warn("evidence is missing");
                return;
            }

            onlineStore[itemId] = new OnlineStoreItem(name.Trim(), kind, rewardId, evidence.Trim(), StorageJson.ReadString(obj, "note"));
        });

        ForEachEntry(Path.Combine(dir, OtherSourcesFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint itemId) || itemId == 0)
            {
                warn("key is not an item row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warn("name is missing");
                return;
            }

            var source = StorageJson.ReadString(obj, "source")?.Trim();
            if (source is null || !OtherSourcesFileSources.Contains(source, StringComparer.Ordinal))
            {
                warn($"source '{source}' is not one of {string.Join(", ", OtherSourcesFileSources)}");
                return;
            }

            var where = StorageJson.ReadString(obj, "where")?.Trim();
            if (string.IsNullOrEmpty(where))
            {
                warn("where is missing");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            otherSources[itemId] = new OtherSourceItem(name.Trim(), source, where, evidence, note);
        });

        ForEachEntry(Path.Combine(dir, RefileOverridesFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!obj.TryGetPropertyValue("genre", out var genreNode) || !StorageJson.TryReadId(genreNode, out var genreId) || genreId == 0)
            {
                warn("genre is not a positive JournalGenre row id");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            refileOverrides[rowId] = new RefileOverride(genreId, note, evidence);
        });

        ForEachEntry(Path.Combine(dir, RetiredQuestsFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            retiredQuests[rowId] = new RetiredQuest(note, evidence, StorageJson.ReadString(obj, "patch")?.Trim() ?? string.Empty);
        });

        ForEachEntry(Path.Combine(dir, QuirksFileName), warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            quirks[rowId] = new QuestQuirk(note, evidence);
        });

        var payoffGates = LoadPayoffGates(Path.Combine(dir, PayoffGatesFileName), warnings);
        var pathChoices = LoadPathChoices(Path.Combine(dir, PathChoicesFileName), warnings);
        var extraPrerequisites = LoadExtraPrerequisites(Path.Combine(dir, ExtraPrerequisitesFileName), warnings);
        var gameGates = LoadGameGates(Path.Combine(dir, GameGatesFileName), warnings);
        var aetheryteUnlocks = LoadAetheryteUnlocks(Path.Combine(dir, AetheryteUnlocksFileName), warnings);
        var giverPortraits = Portraits.PortraitCuration.Load(Path.Combine(dir, GiverPortraitsFileName), warnings);
        var storyRequired = LoadStoryRequired(Path.Combine(dir, StoryRequiredFileName), warnings);
        var expansionLaunches = LoadExpansionLaunches(Path.Combine(dir, ExpansionLaunchesFileName), warnings);
        var storyCast = LoadStoryCast(Path.Combine(dir, StoryCastFileName), warnings);

        var curatedRevision = LoadRevision(Path.Combine(dir, VersionFileName), warnings);

        return new CuratedData(systemUnlocks, dutyUnlocks, featureQuests, festivals, chains, onlineStore, otherSources, refileOverrides, retiredQuests, quirks, curatedRevision, warnings)
        {
            PayoffGates = payoffGates,
            PathChoices = pathChoices,
            ExtraPrerequisites = extraPrerequisites,
            GameGates = gameGates,
            AetheryteUnlocks = aetheryteUnlocks,
            GiverPortraits = giverPortraits,
            StoryRequired = storyRequired,
            ExpansionLaunches = expansionLaunches,
            StoryCast = storyCast,
        };
    }

    /// <summary>
    /// expansion_launches.json: entries keyed by ExVersion row, each with a <c>name</c>, an <c>earlyAccess</c> date
    /// (read as the start of that day, UTC), an <c>expected</c> flag (true while the day is an estimate), an https
    /// <c>evidence</c> URL and a <c>note</c>. An entry missing any of them, or with a malformed one, is skipped with a
    /// warning.
    /// </summary>
    private static Dictionary<byte, ExpansionLaunch> LoadExpansionLaunches(string path, List<string> warnings)
    {
        var entries = new Dictionary<byte, ExpansionLaunch>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint expansion) || expansion == 0 || expansion > byte.MaxValue)
            {
                warn("key is not an expansion (ExVersion row)");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name")?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                warn("name is missing");
                return;
            }

            if (!StorageJson.TryReadUtc(obj, "earlyAccess", out var day) || day is not { } earlyAccess)
            {
                warn("earlyAccess is missing or not a date");
                return;
            }

            if (!obj.TryGetPropertyValue("expected", out var expectedNode) || expectedNode is not JsonValue expectedValue
                || !expectedValue.TryGetValue<bool>(out var expected))
            {
                warn("expected is missing or not true or false");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            if (!evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                warn("evidence is not an https URL");
                return;
            }

            entries[(byte)expansion] = new ExpansionLaunch((byte)expansion, name, earlyAccess.Date, expected, evidence, note);
        });

        return entries;
    }

    /// <summary>
    /// story_cast.json: an object with <c>aliases</c> (a name as the game writes it to an object with the character's
    /// <c>name</c> and a <c>note</c>) and <c>blocks</c> (a name to an object with a <c>note</c>). An entry without its note
    /// or name is skipped with a warning; an alias onto itself or onto another alias is skipped too.
    /// </summary>
    private static global::Tsukimichi.Core.Chains.StoryCastCuration LoadStoryCast(string path, List<string> warnings)
    {
        if (ParseRoot(path, warnings) is not JsonObject root)
        {
            return global::Tsukimichi.Core.Chains.StoryCastCuration.Empty;
        }

        var fileName = Path.GetFileName(path);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["aliases"] is JsonObject aliasNode)
        {
            foreach (var (name, value) in aliasNode)
            {
                if (IsCommentKey(name))
                {
                    continue;
                }

                if (value is not JsonObject entry
                    || StorageJson.ReadString(entry, "name") is not { Length: > 0 } target
                    || string.IsNullOrWhiteSpace(StorageJson.ReadString(entry, "note")))
                {
                    warnings.Add($"{fileName}: alias \"{name}\" skipped: it needs a name and a note");
                    continue;
                }

                if (string.Equals(name, target, StringComparison.Ordinal) || aliasNode.ContainsKey(target))
                {
                    warnings.Add($"{fileName}: alias \"{name}\" skipped: it must join onto a character's own name");
                    continue;
                }

                aliases[name] = target;
            }
        }

        var blocks = new HashSet<string>(StringComparer.Ordinal);
        if (root["blocks"] is JsonObject blockNode)
        {
            foreach (var (name, value) in blockNode)
            {
                if (IsCommentKey(name))
                {
                    continue;
                }

                if (value is not JsonObject entry || string.IsNullOrWhiteSpace(StorageJson.ReadString(entry, "note")))
                {
                    warnings.Add($"{fileName}: block \"{name}\" skipped: it needs a note");
                    continue;
                }

                blocks.Add(name);
            }
        }

        return new global::Tsukimichi.Core.Chains.StoryCastCuration(aliases, blocks);
    }

    /// <summary>
    /// story_required.json: entries keyed by a main scenario quest's row id, each with exactly one of <c>allOf</c> and
    /// <c>anyOf</c> (a non-empty array of quest row ids without repeats), an https <c>evidence</c> URL and a
    /// <c>note</c>. An entry missing any of them, or with a malformed one, is skipped with a warning.
    /// </summary>
    private static Dictionary<uint, StoryRequiredEntry> LoadStoryRequired(string path, List<string> warnings)
    {
        var entries = new Dictionary<uint, StoryRequiredEntry>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var hasAll = obj.TryGetPropertyValue("allOf", out var allNode);
            var hasAny = obj.TryGetPropertyValue("anyOf", out var anyNode);
            if (hasAll == hasAny || (hasAll ? allNode : anyNode) is not JsonArray array || array.Count == 0)
            {
                warn("needs exactly one of allOf and anyOf, a non-empty array of quest row ids");
                return;
            }

            var quests = new List<uint>(array.Count);
            foreach (var element in array)
            {
                if (!StorageJson.TryReadId(element, out var id) || id == 0 || id == rowId || quests.Contains(id))
                {
                    warn($"quest '{element}' is not a quest row id, repeats or names the entry's own quest");
                    return;
                }

                quests.Add(id);
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            if (!evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                warn("evidence is not an https URL");
                return;
            }

            entries[rowId] = new StoryRequiredEntry(quests, hasAll ? JoinKind.All : JoinKind.Any, note, evidence);
        });

        return entries;
    }

    /// <summary>
    /// aetheryte_unlocks.json: entries keyed by Aetheryte row id, each with a <c>name</c>, a non-empty <c>quests</c>
    /// array of quest row ids without repeats, an https <c>evidence</c> URL and a <c>note</c>. An entry missing any of
    /// them, or with a malformed one, is skipped with a warning.
    /// </summary>
    /// <summary>
    /// A festival entry's "runs" (1.19.0, C10): an array of { start, end, evidence }, each window UTC with its start
    /// before its end and an https evidence URL. A missing key reads as no runs; anything malformed rejects the entry
    /// with <paramref name="error"/> saying why. The runs come back oldest first.
    /// </summary>
    private static bool TryReadRuns(JsonObject obj, out IReadOnlyList<FestivalRun> runs, out string error)
    {
        runs = [];
        error = string.Empty;
        if (!obj.TryGetPropertyValue("runs", out var node) || node is null)
        {
            return true;
        }

        if (node is not JsonArray array)
        {
            error = "runs is not an array";
            return false;
        }

        var list = new List<FestivalRun>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonObject run)
            {
                error = "a run is not an object";
                return false;
            }

            if (!StorageJson.TryReadUtc(run, "start", out var start) || start is not { } from
                || !StorageJson.TryReadUtc(run, "end", out var end) || end is not { } to || to <= from)
            {
                error = "a run needs a start before its end";
                return false;
            }

            var evidence = StorageJson.ReadString(run, "evidence");
            if (!Uri.TryCreate(evidence, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "a run's evidence is not an https URL";
                return false;
            }

            list.Add(new FestivalRun(from, to, evidence));
        }

        list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        runs = list;
        return true;
    }

    private static Dictionary<uint, AetheryteUnlock> LoadAetheryteUnlocks(string path, List<string> warnings)
    {
        var entries = new Dictionary<uint, AetheryteUnlock>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint aetheryteId) || aetheryteId == 0)
            {
                warn("key is not an Aetheryte row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                warn("name is missing");
                return;
            }

            if (!obj.TryGetPropertyValue("quests", out var questsNode) || questsNode is not JsonArray array || array.Count == 0)
            {
                warn("quests must be a non-empty array of quest row ids");
                return;
            }

            var quests = new List<uint>(array.Count);
            foreach (var element in array)
            {
                if (!StorageJson.TryReadId(element, out var rowId) || rowId == 0 || quests.Contains(rowId))
                {
                    warn($"quest '{element}' is not a quest row id, or repeats");
                    return;
                }

                quests.Add(rowId);
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            if (!evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                warn("evidence is not an https URL");
                return;
            }

            entries[aetheryteId] = new AetheryteUnlock(name, quests, note, evidence);
        });

        return entries;
    }

    /// <summary>The start of a Lodestone Eorzea Database quest page, which a gate's <c>lodestone</c> source must be.</summary>
    public const string LodestoneQuestPrefix = "https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/";

    /// <summary>A Lodestone quest page: <see cref="LodestoneQuestPrefix"/>, a hex id, a closing slash.</summary>
    public static bool IsLodestoneQuestPage(string url)
        => url.StartsWith(LodestoneQuestPrefix, StringComparison.Ordinal)
           && url.EndsWith('/')
           && url.Length > LodestoneQuestPrefix.Length + 1
           && url[LodestoneQuestPrefix.Length..^1].All(Uri.IsHexDigit);

    /// <summary>
    /// game_gates.json: entries keyed by quest row id, each with <c>gate</c> (what the game wants, non-empty), optional
    /// <c>after</c> (quest row ids, none the key itself, no repeats) with <c>afterTextKey</c> (a <c>TEXT_</c> key,
    /// required with and only with <c>after</c>), an optional <c>gameTextKey</c> (a <c>TEXT_</c> key), for a gear gate
    /// either <c>equipped</c> or <c>held</c> (<see cref="ReadGateItems"/>), for a mount-collection gate <c>mounts</c>
    /// (<see cref="ReadGateMounts"/>), for an unlock-link gate <c>unlockLinks</c> (<see cref="ReadGateUnlockLinks"/>; one
    /// of the three at most), optional <c>metBy</c> (quest row ids, none the key itself) and <c>acceptConditions</c>
    /// (values below 65536), an https <c>evidence</c> URL and a <c>note</c>; since 1.22.0 optional <c>requiredTextKey</c>
    /// (a <c>TEXT_</c> key), <c>lodestone</c> (a Lodestone quest page), <c>questionable</c> (<c>true</c>),
    /// <c>playerConfirmed</c> (a reason, only on a never-judged gate the wiki alone states) and <c>duties</c> (the duty
    /// names the gate counts, non-empty, none twice). An entry missing any of them, or with a malformed one, is skipped
    /// with a warning.
    /// </summary>
    private static Dictionary<uint, GameGate> LoadGameGates(string path, List<string> warnings)
    {
        var entries = new Dictionary<uint, GameGate>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var gate = StorageJson.ReadString(obj, "gate")?.Trim();
            if (string.IsNullOrEmpty(gate))
            {
                warn("gate must say what the game wants");
                return;
            }

            var after = new List<uint>();
            if (obj.TryGetPropertyValue("after", out var afterNode))
            {
                if (afterNode is not JsonArray afterArray)
                {
                    warn("after must be an array of quest row ids");
                    return;
                }

                foreach (var element in afterArray)
                {
                    if (!StorageJson.TryReadId(element, out var id) || id == 0 || id == rowId || after.Contains(id))
                    {
                        warn($"after id '{element}' is not a quest row id, names the quest itself or repeats");
                        return;
                    }

                    after.Add(id);
                }
            }

            var gameTextKey = StorageJson.ReadString(obj, "gameTextKey")?.Trim();
            if (gameTextKey is not null && !gameTextKey.StartsWith("TEXT_", StringComparison.Ordinal))
            {
                warn("gameTextKey must be a TEXT_ key of the quest's own text");
                return;
            }

            var afterTextKey = StorageJson.ReadString(obj, "afterTextKey")?.Trim();
            if ((after.Count > 0) != (afterTextKey is not null) || (afterTextKey is not null && !afterTextKey.StartsWith("TEXT_", StringComparison.Ordinal)))
            {
                warn("afterTextKey must be the TEXT_ key of an after quest's text, set with and only with after");
                return;
            }

            GateItemSet? items = null;
            var equippedNode = obj["equipped"];
            var heldNode = obj["held"];
            if (equippedNode is not null && heldNode is not null)
            {
                warn("a gate lists either equipped or held weapons, not both");
                return;
            }

            if (equippedNode is not null || heldNode is not null)
            {
                var hold = equippedNode is not null ? GateHold.Equipped : GateHold.Held;
                if (ReadGateItems(equippedNode ?? heldNode, hold) is not { } read)
                {
                    warn($"{(hold == GateHold.Equipped ? "equipped" : "held")} must be {{ \"sources\": [\"Sheet#row\"], \"shield\"?: \"both\" | \"either\" | \"none\", \"items\": [[item ids], ...] }} with no item twice");
                    return;
                }

                items = read;
            }

            GateMountSet? mounts = null;
            if (obj["mounts"] is { } mountsNode)
            {
                if (items is not null)
                {
                    warn("a gate lists either weapons or mounts, not both");
                    return;
                }

                if (ReadGateMounts(mountsNode) is not { } read)
                {
                    warn("mounts must be { \"sources\": [\"Mount#row\"], \"all\": [mount ids] } with no id twice");
                    return;
                }

                mounts = read;
            }

            GateUnlockLinkSet? links = null;
            if (obj["unlockLinks"] is { } linksNode)
            {
                if (items is not null || mounts is not null)
                {
                    warn("a gate lists one of weapons, mounts or unlock links");
                    return;
                }

                if (ReadGateUnlockLinks(linksNode) is not { } read)
                {
                    warn("unlockLinks must be { \"sources\": [\"Sheet#row\"], \"all\": [unlock link ids below 65536] } with no id twice");
                    return;
                }

                links = read;
            }

            if (!TryReadIdList(obj, "metBy", id => id != rowId && id >= 65536, out var metBy) || (metBy.Count > 0 && mounts is not null))
            {
                warn("metBy must be an array of quest row ids, none the quest itself, none twice, and no part of a mount-collection gate");
                return;
            }

            if (!TryReadIdList(obj, "acceptConditions", id => id is > 0 and < 65536, out var acceptConditions))
            {
                warn("acceptConditions must be an array of accept-condition values that are no quest (below 65536), none twice");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            if (!Uri.TryCreate(evidence, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                warn($"evidence '{evidence}' is not an https URL");
                return;
            }

            var requiredTextKey = StorageJson.ReadString(obj, "requiredTextKey")?.Trim();
            if (requiredTextKey is not null && !requiredTextKey.StartsWith("TEXT_", StringComparison.Ordinal))
            {
                warn("requiredTextKey must be the TEXT_ key of a required quest's text");
                return;
            }

            var lodestone = StorageJson.ReadString(obj, "lodestone")?.Trim();
            if (lodestone is not null && !IsLodestoneQuestPage(lodestone))
            {
                warn($"lodestone '{lodestone}' is not a Lodestone Eorzea Database quest page ({LodestoneQuestPrefix}<id>/)");
                return;
            }

            var questionable = false;
            if (obj["questionable"] is { } questionableNode
                && (questionableNode is not JsonValue questionableValue || !questionableValue.TryGetValue(out questionable) || !questionable))
            {
                warn("questionable must be true when present");
                return;
            }

            List<string>? duties = null;
            if (obj.TryGetPropertyValue("duties", out var dutiesNode))
            {
                duties = [];
                if (dutiesNode is not JsonArray { Count: > 0 } dutyArray)
                {
                    warn("duties must be a non-empty array of the duty names the gate counts");
                    return;
                }

                foreach (var element in dutyArray)
                {
                    if (element is not JsonValue dutyValue || !dutyValue.TryGetValue<string>(out var duty) || duty.Trim().Length == 0
                        || duties.Contains(duty.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        warn("duties must be a non-empty array of the duty names the gate counts, none twice");
                        return;
                    }

                    duties.Add(duty.Trim());
                }
            }

            var playerConfirmed = StorageJson.ReadString(obj, "playerConfirmed")?.Trim();
            var entry = new GameGate(gate, after, gameTextKey, afterTextKey, evidence, note, items, mounts, links, metBy, acceptConditions, lodestone, questionable, playerConfirmed, requiredTextKey, duties);
            if (playerConfirmed is not null
                && (playerConfirmed.Length == 0 || !entry.NeverJudged || !entry.SourceKinds.SequenceEqual([QuestGate.WikiSource, QuestGate.PlayerSource])))
            {
                warn("playerConfirmed (a reason) is only for a gate never judged that the wiki alone states, with the wiki page as its evidence");
                return;
            }

            entries[rowId] = entry;
        });

        return entries;
    }

    /// <summary>
    /// An optional array of ids under <paramref name="name"/>: absent reads empty; each must pass <paramref name="valid"/>
    /// and none repeat. False when the value is no array or an element fails.
    /// </summary>
    private static bool TryReadIdList(JsonObject obj, string name, Func<uint, bool> valid, out IReadOnlyList<uint> ids)
    {
        var list = new List<uint>();
        ids = list;
        if (!obj.TryGetPropertyValue(name, out var node))
        {
            return true;
        }

        if (node is not JsonArray array)
        {
            return false;
        }

        foreach (var element in array)
        {
            if (!StorageJson.TryReadId(element, out var id) || !valid(id) || list.Contains(id))
            {
                return false;
            }

            list.Add(id);
        }

        return true;
    }

    /// <summary>
    /// An unlock-link gate's <c>"unlockLinks"</c> object: a non-empty <c>sources</c> array of <c>Sheet#row</c> strings and
    /// a non-empty <c>all</c> array of unlock link ids (1 to 65535), none twice; the ids sorted. Null when anything is off.
    /// </summary>
    private static GateUnlockLinkSet? ReadGateUnlockLinks(JsonNode node)
    {
        if (node is not JsonObject obj || obj["sources"] is not JsonArray sourcesArray || sourcesArray.Count == 0 || obj["all"] is not JsonArray allArray || allArray.Count == 0)
        {
            return null;
        }

        var sources = new List<string>();
        foreach (var element in sourcesArray)
        {
            var source = element is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : null;
            var hash = source?.IndexOf('#', StringComparison.Ordinal) ?? -1;
            if (source is null || hash < 1 || !uint.TryParse(source.AsSpan(hash + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var row) || row == 0)
            {
                return null;
            }

            sources.Add(source);
        }

        var ids = new HashSet<uint>();
        foreach (var element in allArray)
        {
            if (!StorageJson.TryReadId(element, out var id) || id is 0 or > ushort.MaxValue || !ids.Add(id))
            {
                return null;
            }
        }

        return new GateUnlockLinkSet(sources, [.. ids.Order()]);
    }

    /// <summary>
    /// A mount-collection gate's <c>"mounts"</c> object: a non-empty <c>sources</c> array of <c>Mount#row</c> strings and
    /// a non-empty <c>all</c> array of mount row ids, none twice; the ids sorted. Null when anything is off.
    /// </summary>
    private static GateMountSet? ReadGateMounts(JsonNode node)
    {
        if (node is not JsonObject obj || obj["sources"] is not JsonArray sourcesArray || sourcesArray.Count == 0 || obj["all"] is not JsonArray allArray || allArray.Count == 0)
        {
            return null;
        }

        var sources = new List<string>();
        foreach (var element in sourcesArray)
        {
            var source = element is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : null;
            if (source is null || !source.StartsWith("Mount#", StringComparison.Ordinal)
                || !uint.TryParse(source.AsSpan("Mount#".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var row) || row == 0)
            {
                return null;
            }

            sources.Add(source);
        }

        var ids = new HashSet<uint>();
        foreach (var element in allArray)
        {
            if (!StorageJson.TryReadId(element, out var id) || id == 0 || !ids.Add(id))
            {
                return null;
            }
        }

        return new GateMountSet(sources, [.. ids.Order()]);
    }

    /// <summary>The shield words a <see cref="GateItemSet.Shield"/> may hold.</summary>
    public static readonly IReadOnlyList<string> GateShieldRules = ["both", "either", "none"];

    /// <summary>
    /// A gear gate's <c>"equipped"</c> or <c>"held"</c> object: a non-empty <c>sources</c> array of <c>Sheet#row</c>
    /// strings, an optional <c>shield</c> (<see cref="GateShieldRules"/>) and a non-empty <c>items</c> array of
    /// non-empty arrays of item ids, no id twice. Groups and the ids in each are sorted. Null when anything is off.
    /// </summary>
    private static GateItemSet? ReadGateItems(JsonNode? node, GateHold hold)
    {
        if (node is not JsonObject obj || obj["sources"] is not JsonArray sourcesArray || sourcesArray.Count == 0 || obj["items"] is not JsonArray itemsArray || itemsArray.Count == 0)
        {
            return null;
        }

        var sources = new List<string>();
        foreach (var element in sourcesArray)
        {
            var source = element is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : null;
            var hash = source?.IndexOf('#', StringComparison.Ordinal) ?? -1;
            if (source is null || hash < 1 || !uint.TryParse(source.AsSpan(hash + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return null;
            }

            sources.Add(source);
        }

        string? shield = null;
        if (obj.TryGetPropertyValue("shield", out var shieldNode))
        {
            shield = shieldNode is JsonValue sv && sv.TryGetValue(out string? text) ? text : null;
            if (shield is null || !GateShieldRules.Contains(shield))
            {
                return null;
            }
        }

        var seen = new HashSet<uint>();
        var groups = new List<uint[]>();
        foreach (var groupNode in itemsArray)
        {
            if (groupNode is not JsonArray groupArray || groupArray.Count == 0)
            {
                return null;
            }

            var group = new uint[groupArray.Count];
            for (var i = 0; i < group.Length; i++)
            {
                if (!StorageJson.TryReadId(groupArray[i], out var id) || id == 0 || !seen.Add(id))
                {
                    return null;
                }

                group[i] = id;
            }

            Array.Sort(group);
            groups.Add(group);
        }

        groups.Sort((a, b) => a[0].CompareTo(b[0]));
        return new GateItemSet(hold, sources, shield, [.. groups]);
    }

    /// <summary>
    /// extra_prerequisites.json: entries keyed by quest row id, each with <c>requires</c> (a non-empty array of quest
    /// row ids, none the key itself, no repeats), <c>sources</c> (at least two distinct names of
    /// <see cref="ExtraPrerequisiteSources"/>), <c>gameTextKey</c> (a <c>TEXT_</c> key, required with and only with the
    /// <c>gameText</c> source), an https <c>evidence</c> URL and a <c>note</c>. An entry missing any of them is skipped
    /// with a warning, so a bad line never adds a gate.
    /// </summary>
    private static Dictionary<uint, ExtraPrerequisite> LoadExtraPrerequisites(string path, List<string> warnings)
    {
        var entries = new Dictionary<uint, ExtraPrerequisite>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!obj.TryGetPropertyValue("requires", out var requiresNode) || requiresNode is not JsonArray requiresArray || requiresArray.Count == 0)
            {
                warn("requires must be a non-empty array of quest row ids");
                return;
            }

            var requires = new List<uint>(requiresArray.Count);
            foreach (var element in requiresArray)
            {
                if (!StorageJson.TryReadId(element, out var id) || id == 0)
                {
                    warn($"requires id '{element}' is not a quest row id");
                    return;
                }

                if (id == rowId)
                {
                    warn("requires names the quest itself");
                    return;
                }

                if (requires.Contains(id))
                {
                    warn($"requires lists {id} twice");
                    return;
                }

                requires.Add(id);
            }

            var sources = new List<string>();
            if (obj.TryGetPropertyValue("sources", out var sourcesNode) && sourcesNode is JsonArray sourceArray)
            {
                foreach (var element in sourceArray)
                {
                    if (element is not JsonValue value || !value.TryGetValue<string>(out var source) || !ExtraPrerequisiteSources.Contains(source, StringComparer.Ordinal))
                    {
                        warn($"source '{element}' is not one of {string.Join(", ", ExtraPrerequisiteSources)}");
                        return;
                    }

                    if (!sources.Contains(source, StringComparer.Ordinal))
                    {
                        sources.Add(source);
                    }
                }
            }

            if (sources.Count < 2)
            {
                warn($"sources must name at least two of {string.Join(", ", ExtraPrerequisiteSources)}");
                return;
            }

            var gameTextKey = StorageJson.ReadString(obj, "gameTextKey")?.Trim();
            var citesGameText = sources.Contains(GameTextSource, StringComparer.Ordinal);
            if (citesGameText && (gameTextKey is null || !gameTextKey.StartsWith("TEXT_", StringComparison.Ordinal)))
            {
                warn("gameTextKey must be the TEXT_ key of the quest text row the gameText source names");
                return;
            }

            if (!citesGameText && gameTextKey is not null)
            {
                warn("gameTextKey is set but sources does not cite gameText");
                return;
            }

            if (!TryReadNoteAndEvidence(obj, warn, out var note, out var evidence))
            {
                return;
            }

            if (!Uri.TryCreate(evidence, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                warn($"evidence '{evidence}' is not an https URL");
                return;
            }

            entries[rowId] = new ExtraPrerequisite(requires, sources, evidence, note, gameTextKey);
        });

        return entries;
    }

    /// <summary>
    /// path_choices.json: an object with "cities" (keyed by the city's first quest row id: label, note), "classes"
    /// (keyed by ClassJob row id: label, closeToHome, starter, note) and "grandCompanies" (keyed by quest row id:
    /// grandCompany 1 to 3, note). Every entry needs its note; one without it, or with a bad id, is skipped with a
    /// warning. Cities and classes keep the file's order.
    /// </summary>
    private static PathChoices LoadPathChoices(string path, List<string> warnings)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return PathChoices.Empty;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonObject obj)
        {
            warnings.Add($"{fileName}: root is not a JSON object; file ignored.");
            return PathChoices.Empty;
        }

        var cities = new List<CityPin>();
        var classes = new List<ClassPin>();
        var grandCompanies = new Dictionary<uint, GrandCompanyTag>();

        void Each(string section, Action<string, JsonObject, Action<string>> handle)
        {
            if (!obj.TryGetPropertyValue(section, out var node) || node is null)
            {
                return;
            }

            if (node is not JsonObject entries)
            {
                warnings.Add($"{fileName}: \"{section}\" is not an object; section ignored.");
                return;
            }

            foreach (var pair in entries)
            {
                if (IsCommentKey(pair.Key))
                {
                    continue;
                }

                void Warn(string reason) => warnings.Add($"{fileName}: {section} entry \"{pair.Key}\" skipped: {reason}");
                if (pair.Value is not JsonObject entry)
                {
                    Warn("value is not an object");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(StorageJson.ReadString(entry, "note")))
                {
                    Warn("note is missing");
                    continue;
                }

                handle(pair.Key, entry, Warn);
            }
        }

        Each("cities", (key, entry, warn) =>
        {
            var label = StorageJson.ReadString(entry, "label")?.Trim();
            if (!StorageJson.TryParseKey(key, out uint root) || root == 0)
            {
                warn("key is not a quest row id");
            }
            else if (string.IsNullOrEmpty(label))
            {
                warn("label is missing");
            }
            else
            {
                cities.Add(new CityPin(root, label, StorageJson.ReadString(entry, "note")!.Trim()));
            }
        });

        Each("classes", (key, entry, warn) =>
        {
            var label = StorageJson.ReadString(entry, "label")?.Trim();
            if (!StorageJson.TryParseKey(key, out uint classJob) || classJob is 0 or > byte.MaxValue)
            {
                warn("key is not a ClassJob row id");
            }
            else if (string.IsNullOrEmpty(label))
            {
                warn("label is missing");
            }
            else if (!entry.TryGetPropertyValue("closeToHome", out var homeNode) || !StorageJson.TryReadId(homeNode, out var home) || home == 0)
            {
                warn("closeToHome is not a quest row id");
            }
            else if (!entry.TryGetPropertyValue("starter", out var starterNode) || !StorageJson.TryReadId(starterNode, out var starter) || starter == 0)
            {
                warn("starter is not a quest row id");
            }
            else
            {
                classes.Add(new ClassPin((byte)classJob, label, home, starter, StorageJson.ReadString(entry, "note")!.Trim()));
            }
        });

        Each("grandCompanies", (key, entry, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint rowId) || rowId == 0)
            {
                warn("key is not a quest row id");
            }
            else if (!entry.TryGetPropertyValue("grandCompany", out var gcNode) || !StorageJson.TryReadId(gcNode, out var gc) || gc is 0 or > 3)
            {
                warn("grandCompany is not 1, 2 or 3");
            }
            else
            {
                grandCompanies[rowId] = new GrandCompanyTag((byte)gc, StorageJson.ReadString(entry, "note")!.Trim());
            }
        });

        return new PathChoices(cities, classes, grandCompanies);
    }

    /// <summary>
    /// payoff_gates.json: entries keyed by a gate id, each with a milestone quest row id, <c>before</c> (an array of
    /// quest row ids or a chain name), a spoiler-free instruction, the spoiler reason, at least one https evidence URL
    /// and a note. An entry missing any of them is skipped with a warning.
    /// </summary>
    private static List<PayoffGate> LoadPayoffGates(string path, List<string> warnings)
    {
        var gates = new List<PayoffGate>();
        ForEachEntry(path, warnings, (key, node, warn) =>
        {
            var id = key.Trim();
            if (id.Length == 0)
            {
                warn("key is empty");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!obj.TryGetPropertyValue("milestone", out var milestoneNode) || !StorageJson.TryReadId(milestoneNode, out var milestone) || milestone == 0)
            {
                warn("milestone is not a quest row id");
                return;
            }

            var beforeIds = new List<uint>();
            string? beforeChain = null;
            obj.TryGetPropertyValue("before", out var beforeNode);
            switch (beforeNode)
            {
                case JsonArray ids:
                    foreach (var element in ids)
                    {
                        if (!StorageJson.TryReadId(element, out var rowId) || rowId == 0)
                        {
                            warn($"before id '{element}' is not a quest row id");
                            return;
                        }

                        if (!beforeIds.Contains(rowId))
                        {
                            beforeIds.Add(rowId);
                        }
                    }

                    break;
                case JsonValue value when value.TryGetValue<string>(out var chain) && !string.IsNullOrWhiteSpace(chain):
                    beforeChain = chain.Trim();
                    break;
            }

            if (beforeIds.Count == 0 && beforeChain is null)
            {
                warn("before must be a non-empty array of quest row ids or a chain name");
                return;
            }

            var instruction = StorageJson.ReadString(obj, "instruction")?.Trim() ?? string.Empty;
            var why = StorageJson.ReadString(obj, "why")?.Trim() ?? string.Empty;
            var note = StorageJson.ReadString(obj, "note")?.Trim() ?? string.Empty;
            if (instruction.Length == 0 || why.Length == 0 || note.Length == 0)
            {
                warn("instruction, why and note are all required");
                return;
            }

            var evidence = new List<string>();
            if (obj.TryGetPropertyValue("evidence", out var evidenceNode) && evidenceNode is JsonArray urls)
            {
                foreach (var element in urls)
                {
                    if (element is not JsonValue urlValue
                        || !urlValue.TryGetValue<string>(out var url)
                        || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                        || uri.Scheme != Uri.UriSchemeHttps)
                    {
                        warn($"evidence '{element}' is not an https URL");
                        return;
                    }

                    evidence.Add(url.Trim());
                }
            }

            if (evidence.Count == 0)
            {
                warn("evidence is missing");
                return;
            }

            gates.Add(new PayoffGate(id, milestone, beforeIds, beforeChain, instruction, why, evidence, note));
        });

        return gates;
    }

    /// <summary>The note and evidence URL every refiling entry must carry (the curated README's rule for hand-filed quests).</summary>
    private static bool TryReadNoteAndEvidence(JsonObject obj, Action<string> warn, out string note, out string evidence)
    {
        note = StorageJson.ReadString(obj, "note")?.Trim() ?? string.Empty;
        evidence = StorageJson.ReadString(obj, "evidence")?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            warn("note is missing");
            return false;
        }

        if (evidence.Length == 0)
        {
            warn("evidence is missing");
            return false;
        }

        return true;
    }

    /// <summary>
    /// VERSION.json: an object with a <see cref="CuratedRevisionKey"/> string. A missing file is the normal state of a
    /// checkout that never ran <c>tools/regen.ps1</c> and reads as an empty revision without a warning; a file without
    /// the key, or with a blank one, is warned about.
    /// </summary>
    private static string LoadRevision(string path, List<string> warnings)
    {
        if (!File.Exists(path) || ParseRoot(path, warnings) is not { } root)
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonObject obj)
        {
            warnings.Add($"{fileName}: root is not an object; no curated revision.");
            return string.Empty;
        }

        var revision = StorageJson.ReadString(obj, CuratedRevisionKey);
        if (string.IsNullOrWhiteSpace(revision))
        {
            warnings.Add($"{fileName}: {CuratedRevisionKey} is missing; no curated revision.");
            return string.Empty;
        }

        return revision.Trim();
    }

    /// <summary>
    /// chains.json: an object with a "chains" array (a bare array is accepted too). Each chain needs a name and at
    /// least one genre id; ids may be numbers or digit strings. Duplicate ids within a chain are dropped.
    /// </summary>
    private static void LoadChains(string path, List<CuratedChain> chains, List<string> warnings)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        var array = root switch
        {
            JsonArray bare => bare,
            JsonObject obj when obj.TryGetPropertyValue("chains", out var node) && node is JsonArray inner => inner,
            _ => null,
        };

        if (array is null)
        {
            warnings.Add($"{fileName}: root must be an object with a \"chains\" array; file ignored.");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var label = $"{fileName}: chains[{i}] skipped: ";
            if (array[i] is not JsonObject chain)
            {
                warnings.Add(label + "not an object");
                continue;
            }

            var name = StorageJson.ReadString(chain, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                warnings.Add(label + "name is missing");
                continue;
            }

            var hasGenres = chain.ContainsKey("genreIds");
            var hasQuests = chain.ContainsKey("questIds");
            var hasStart = chain.ContainsKey("startQuest");
            if ((hasGenres ? 1 : 0) + (hasQuests ? 1 : 0) + (hasStart ? 1 : 0) != 1)
            {
                warnings.Add(label + "needs exactly one of genreIds, questIds and startQuest");
                continue;
            }

            var ongoing = chain.TryGetPropertyValue("ongoing", out var ongoingNode) && ongoingNode is JsonValue flag && flag.TryGetValue<bool>(out var isOngoing) && isOngoing;
            var note = StorageJson.ReadString(chain, "note");
            if (hasStart)
            {
                if (!StorageJson.TryReadId(chain["startQuest"], out var start) || start == 0)
                {
                    warnings.Add(label + "startQuest is not a quest row id");
                    continue;
                }

                chains.Add(new CuratedChain(name.Trim(), [], note) { StartQuest = start, Ongoing = ongoing });
                continue;
            }

            var key = hasGenres ? "genreIds" : "questIds";
            if (chain[key] is not JsonArray ids)
            {
                warnings.Add(label + key + " is not an array");
                continue;
            }

            var listed = new List<uint>(ids.Count);
            var valid = true;
            foreach (var element in ids)
            {
                if (!StorageJson.TryReadId(element, out var id) || id == 0)
                {
                    warnings.Add(label + $"{(hasGenres ? "genre" : "quest")} id '{element}' is not a positive integer");
                    valid = false;
                    break;
                }

                if (!listed.Contains(id))
                {
                    listed.Add(id);
                }
            }

            if (!valid)
            {
                continue;
            }

            if (listed.Count == 0)
            {
                warnings.Add(label + key + " is empty");
                continue;
            }

            chains.Add(hasGenres
                ? new CuratedChain(name.Trim(), listed, note) { Ongoing = ongoing }
                : new CuratedChain(name.Trim(), [], note) { QuestIds = listed, Ongoing = ongoing });
        }
    }

    private delegate void EntryHandler(string key, JsonNode? value, Action<string> warn);

    private delegate void ElementHandler(int index, JsonNode? value, Action<string> warn);

    /// <summary>
    /// Visits each property of a JSON object file. The root may instead wrap the entries under
    /// <see cref="EntriesKey"/>; keys starting with <see cref="CommentKeyPrefix"/> are skipped silently. Missing file →
    /// nothing; unparseable → one warning.
    /// </summary>
    private static void ForEachEntry(string path, List<string> warnings, EntryHandler handle)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        if (root is not JsonObject obj)
        {
            warnings.Add($"{fileName}: root is not a JSON object; file ignored.");
            return;
        }

        if (obj.TryGetPropertyValue(EntriesKey, out var entriesNode) && entriesNode is JsonObject entries)
        {
            obj = entries;
        }

        foreach (var pair in obj)
        {
            if (IsCommentKey(pair.Key))
            {
                continue;
            }

            handle(pair.Key, pair.Value, reason => warnings.Add($"{fileName}: entry \"{pair.Key}\" skipped: {reason}"));
        }
    }

    /// <summary>
    /// Visits each element of a JSON array file. The root may instead be an object holding the array under
    /// <paramref name="wrapperKey"/> (its other keys, such as a note, are ignored). Missing file → nothing;
    /// unparseable or neither shape → one warning.
    /// </summary>
    private static void ForEachElement(string path, string wrapperKey, List<string> warnings, ElementHandler handle)
    {
        if (ParseRoot(path, warnings) is not { } root)
        {
            return;
        }

        var fileName = Path.GetFileName(path);
        var array = root switch
        {
            JsonArray bare => bare,
            JsonObject obj when obj.TryGetPropertyValue(wrapperKey, out var node) && node is JsonArray inner => inner,
            _ => null,
        };

        if (array is null)
        {
            warnings.Add($"{fileName}: root must be a JSON array or an object with a \"{wrapperKey}\" array; file ignored.");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var index = i;
            handle(index, array[index], reason => warnings.Add($"{fileName}: element [{index}] skipped: {reason}"));
        }
    }

    private static bool IsCommentKey(string key) => key.Length > 0 && key[0] == CommentKeyPrefix;

    /// <summary>Missing file → null silently; locked or inaccessible → null with one warning (the file is never moved).</summary>
    private static JsonNode? ParseRoot(string path, List<string> warnings)
    {
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings.Add($"{Path.GetFileName(path)} could not be read: {ioError}");
            }

            return null;
        }

        try
        {
            // Strict, as the curated README promises: a comment or a trailing comma is a parse error, not a tolerance.
            return JsonNode.Parse(text, documentOptions: StrictOptions) ?? throw new JsonException("file is empty");
        }
        catch (JsonException ex)
        {
            warnings.Add($"{Path.GetFileName(path)} could not be parsed: {ex.Message}");
            return null;
        }
    }
}
