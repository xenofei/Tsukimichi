namespace Tsukimichi.Core.Model;

/// <summary>
/// One quest as read from the game sheets. Immutable; built once per catalog load.
/// Init-only properties so loaders and tests can use object initializers, and JSON round-trips without a custom converter.
/// </summary>
public sealed record QuestRecord
{
    /// <summary>Row id in the Quest sheet (65536 + n).</summary>
    public uint RowId { get; init; }

    /// <summary>Low 16 bits of <see cref="RowId"/>; every runtime lookup uses this.</summary>
    public ushort QuestId { get; init; }

    /// <summary>Internal script id, e.g. ManSea001_00107.</summary>
    public string InternalId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public JournalRef Journal { get; init; } = JournalRef.None;

    public byte Expansion { get; init; }
    /// <summary>
    /// Acceptance level from <c>Quest.ClassJobLevel[0]</c>: the level a job must reach to take the quest. The
    /// evaluator gates on this; the interface shows <see cref="DisplayLevel"/>.
    /// </summary>
    public byte Level { get; init; }

    public byte LevelMax { get; init; }

    /// <summary><c>Quest.QuestLevelOffset</c>: what the journal adds to <see cref="Level"/> for the level it prints.</summary>
    public byte LevelOffset { get; init; }

    /// <summary>
    /// The level the game journal and the Lodestone print: <see cref="Level"/> plus <see cref="LevelOffset"/> (at most
    /// 117 in the current sheets, so the byte never wraps). Every level the interface shows, sorts or filters on is
    /// this one; requirements still use <see cref="Level"/>.
    /// </summary>
    public byte DisplayLevel => (byte)(Level + LevelOffset);

    /// <summary>
    /// How many steps the journal walks the quest through: the distinct non-zero <c>Quest.ToDoCompleteSeq</c> values
    /// (the sequence each objective is active in, 1, 2, … then 255 for the last). An accepted quest's
    /// <see cref="AcceptedQuest.Sequence"/> is its current step, 255 meaning the last one; 0 when the sheet lists no
    /// objectives, and then only the raw sequence can be shown.
    /// </summary>
    public byte StepCount { get; init; }

    public uint ClassJobCategory { get; init; }
    public uint ClassJobCategory1 { get; init; }
    public uint ClassJobRequired { get; init; }

    public Prereq PreviousQuests { get; init; } = Prereq.None;

    /// <summary>Quests that foreclose this one once completed.</summary>
    public uint[] QuestLocks { get; init; } = [];

    public uint[] InstanceContentRequired { get; init; } = [];
    public JoinKind InstanceJoin { get; init; } = JoinKind.All;

    public byte GrandCompany { get; init; }
    public byte GrandCompanyRank { get; init; }
    public byte BeastTribe { get; init; }
    public byte BeastRank { get; init; }
    public ushort BeastValue { get; init; }

    public bool IsRepeatable { get; init; }
    public byte RepeatInterval { get; init; }
    public byte DailyPool { get; init; }
    public ushort Festival { get; init; }

    /// <summary>
    /// <c>Quest.FestivalBegin</c> / <c>Quest.FestivalEnd</c>: the phase window of the running festival the quest is
    /// offered in (a phased event opens later chapters on later days; Hatching-tide 2014 ran phases 1 to 5). Both 0
    /// means the quest is offered for the whole run; a non-zero bound is inclusive and the other bound, when 0, is
    /// open. Only read when the client reports the festival's phase (<see cref="CharacterSnapshot.ActiveFestivalPhases"/>).
    /// </summary>
    public byte FestivalBegin { get; init; }

    /// <inheritdoc cref="FestivalBegin"/>
    public byte FestivalEnd { get; init; }

    /// <summary>
    /// <c>Quest.SatisfactionNpc</c>: the custom delivery client (SatisfactionNpc row id, 1-based; 0 when none) whose
    /// satisfaction rank <see cref="SatisfactionLevel"/> the quest needs.
    /// </summary>
    public byte SatisfactionNpc { get; init; }

    /// <summary><c>Quest.SatisfactionLevel</c>: the satisfaction rank needed with <see cref="SatisfactionNpc"/>; 0 when none.</summary>
    public byte SatisfactionLevel { get; init; }

    /// <summary>
    /// <c>Quest.DeliveryQuest</c>: the Delivery Moogle carrier level the quest needs. The sheet's row id is the
    /// carrier level itself on every row (docs/data/verification-report-2.md section 2 row 3); 0 when none.
    /// </summary>
    public byte CarrierLevel { get; init; }

    public bool MountRequired { get; init; }
    public bool HouseRequired { get; init; }
    public uint[] AcceptConditions { get; init; } = [];

    public Issuer? Issuer { get; init; }

    /// <summary>Journal banner artwork icon id; zero when the quest has none.</summary>
    public uint Icon { get; init; }

    /// <summary>Small icon the journal shows beside special quests (seasonal events, promotions); zero for ordinary quests.</summary>
    public uint IconSpecial { get; init; }

    /// <summary>
    /// Journal icon family from <c>Quest.EventIconType</c>: 3 is the ordinary side quest, 8 the blue "+" feature
    /// quest and 10 the quasi-quest that shares its icon (see <c>FeaturePresets.HasFeatureIcon</c>); zero when the
    /// sheet has none.
    /// </summary>
    public byte EventIconType { get; init; }

    /// <summary>
    /// The Quest sheet's unnamed bool between <c>HideOfferIcon</c> and <c>HideInScenarioGuide</c> (Lumina's
    /// <c>Unknown12</c>): set on the rows the game retired in 5.3, 5.5 and 6.3. One of the two signals of
    /// <see cref="IsRetired"/>; kept raw so the refiler can run on a frozen catalog.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Removed from the game: the row sits on the placeholder issuer or carries <see cref="IsHidden"/> (refiling rule
    /// 1), or the curated <c>retired_quests.json</c> names it. Never counted in a total, never listed under a journal
    /// node, never a feature quest; shown only under the "Removed from the game" node or when revealed directly, where
    /// a completed one still reads Completed. A retired quest keeps whatever genre the sheet gave it.
    /// </summary>
    public bool IsRetired { get; init; }

    /// <summary>
    /// Which refiling rule (docs/data/unlisted-report.md section 4) decided this quest's filing: 1 retired, 2 class or
    /// job intro, 3 Grand Company, 4 nearest listed prerequisite, 5 nearest listed successor or lock, 6 the issuer's
    /// zone, 7 no signal (still unlisted), or <c>JournalRefiler.CuratedRule</c> (8) when a curated file decided. Zero
    /// when the sheet's own genre stands (every listed quest, and every quest under <see cref="JournalFiling.Legacy"/>).
    /// </summary>
    public byte RefiledFrom { get; init; }

    /// <summary>
    /// Whether the quest enters its section, category and genre totals (and the overall count). False for the class
    /// intro quasi-quests the refiler files by rule 2 ("So You Want to Be a Gladiator", <c>Cls…001</c>/<c>999</c>):
    /// the receptionist offers the intro only to a character switching into the class, so the class a character
    /// started as never gets its intro flagged, and a genre that counted it would stay one short for good. They still
    /// sit under their genre in the table, in search, in Unlock quests and in reveals; only the counts skip them.
    /// True for every other quest, the job intros included (<c>Job…299</c>: nobody starts as a job); removed quests
    /// are kept out of the counts by <see cref="IsRemoved"/> before this is read.
    /// </summary>
    public bool CountsInTotals { get; init; } = true;

    /// <summary>
    /// The patch the quest was added in, as the game writes it ("2.0", "6.55", "7.5"; compare with
    /// <see cref="PatchVersion"/>), from <c>quest_patches.json</c> at catalog build (P8). Empty when unknown: the file
    /// has no patch for the quest, or the catalog was built without the file (the test fixture holds the sheet's own
    /// data; the patches are laid over it on read, as the curated overlay is).
    /// </summary>
    public string AddedIn { get; init; } = string.Empty;

    public IReadOnlyList<RewardRef> Rewards { get; init; } = [];
    public uint ExpFactor { get; init; }
    public uint Gil { get; init; }

    /// <summary>
    /// No journal genre after refiling: the retired rows (rule 1) and the rare quest no rule could place (rule 7).
    /// Under <see cref="JournalFiling.Legacy"/> every genre-0 row of the sheet.
    /// </summary>
    public bool IsUnlisted => Journal.GenreId == 0;

    /// <summary>
    /// Belongs to the "Removed from the game" bucket rather than a journal node: <see cref="IsRetired"/>, or
    /// <see cref="IsUnlisted"/> (a genre-0 quest is either retired or left without a home by every rule). Tree counts,
    /// queries, the main scenario line, ladders, chains, Compare, search and the integrations all test this one flag.
    /// </summary>
    public bool IsRemoved => IsRetired || IsUnlisted;

    /// <summary>Computes the runtime quest id from a Quest sheet row id.</summary>
    public static ushort ToQuestId(uint rowId) => (ushort)(rowId & 0xFFFF);
}
