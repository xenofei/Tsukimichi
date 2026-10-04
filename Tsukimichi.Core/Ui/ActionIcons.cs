using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Rewards;
using Tsukimichi.Core.Route;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// How a game icon sits on a button (docs/design/v7/ui/spec-1.15.md B1): a map symbol is a transparent shape, drawn bare
/// with a 1 px shadow; an action tile is a framed square, drawn at a small radius with a faint inset ring.
/// </summary>
public enum IconStyle : byte
{
    /// <summary>A transparent map symbol (the aetheryte, the flag, a quest marker): drawn bare.</summary>
    MapSymbol,

    /// <summary>A framed action tile (Sprint, the Map menu, a job icon): rounded 3 with a 1 px inset ring.</summary>
    Tile,
}

/// <summary>A game icon a button or a line wears: its id and how it is drawn. <see cref="None"/> (id 0) draws nothing.</summary>
/// <param name="IconId">The icon id (060453 the aetheryte …); 0 for none.</param>
/// <param name="Style">Map symbol or action tile.</param>
public readonly record struct GameIconRef(uint IconId, IconStyle Style)
{
    /// <summary>No icon.</summary>
    public static readonly GameIconRef None = default;

    /// <summary>Whether there is an icon to draw.</summary>
    public bool HasIcon => IconId != 0;

    /// <summary>A map symbol (drawn bare).</summary>
    public static GameIconRef Symbol(uint iconId) => new(iconId, IconStyle.MapSymbol);

    /// <summary>An action tile (rounded, ringed); <see cref="None"/> for id 0.</summary>
    public static GameIconRef Tile(uint iconId) => new(iconId, IconStyle.Tile);
}

/// <summary>
/// The game's own icons for the plugin's actions and the lines that name a game thing (UI-5e, owner point 9: "add any
/// icons that are missing", and "if an icon would give it more emphasis, add it in"): every travel and route button, the
/// Route window's header and the requirement lines. Every id was read from the 2026.09.15 client (GeneralAction,
/// MainCommand, MapSymbol and ClassJob); the game-data tests check each one is in the installed game. Actions without a
/// game icon (Stop, Pin, Path, Link, Copy, Report, More) keep their FontAwesome glyph, and "Read the journal" wears the
/// approved book glyph, never the red "!" tile 000005 (red is Locked out's).
/// </summary>
public static class ActionIcons
{
    /// <summary>MapSymbol 1, the aetheryte: Teleport.</summary>
    public const uint Teleport = 60453;

    /// <summary>GeneralAction 8, Return.</summary>
    public const uint Return = 112;

    /// <summary>GeneralAction 4, Sprint: Walk (vnavmesh).</summary>
    public const uint Walk = 104;

    /// <summary>The map's flag marker: Flag on the map.</summary>
    public const uint Flag = 60561;

    /// <summary>MainCommand 16, Map: Open the map.</summary>
    public const uint Map = 7;

    /// <summary>MapSymbol 2, the aethernet shard: an aethernet hop (Lifestream).</summary>
    public const uint Aethernet = 60430;

    /// <summary>MapSymbol 15, the ferry docks: a ferry or an airship.</summary>
    public const uint Ferry = 60456;

    /// <summary>GeneralAction 9, Mount Roulette: Mount up, and a mount requirement.</summary>
    public const uint Mount = 118;

    /// <summary>GeneralAction 24, Flying Mount Roulette: Fly.</summary>
    public const uint Fly = 122;

    /// <summary>MainCommand 33, the Duty Finder: a duty without a category tile of its own.</summary>
    public const uint DutyFinder = 46;

    /// <summary>MainCommand's Crafting Log: an Artisan craft (the "Why it stopped" card's icon for it, spec-1.18).</summary>
    public const uint CraftingLog = 22;

    /// <summary>The job icons (062101 Gladiator …): 062100 plus the ClassJob row.</summary>
    public const uint JobIconBase = 62100;

    /// <summary>The highest ClassJob row the job icons are offset by.</summary>
    private const uint LastJobRow = 99;

    /// <summary>SeIconChar.Instance1 (U+E0B1) in the game font; Instance2 … Instance9 follow it.</summary>
    public const char FirstInstanceGlyph = '';

    /// <summary>Teleport: the aetheryte map symbol.</summary>
    public static GameIconRef TeleportIcon => GameIconRef.Symbol(Teleport);

    /// <summary>Walk to giver: Sprint's tile.</summary>
    public static GameIconRef WalkIcon => GameIconRef.Tile(Walk);

    /// <summary>Flag on the map: the flag marker.</summary>
    public static GameIconRef FlagIcon => GameIconRef.Symbol(Flag);

    /// <summary>Open the map: the Map menu tile.</summary>
    public static GameIconRef MapIcon => GameIconRef.Tile(Map);

    /// <summary>An aethernet hop: the shard's map symbol.</summary>
    public static GameIconRef AethernetIcon => GameIconRef.Symbol(Aethernet);

    /// <summary>A duty with nothing better: the Duty Finder tile.</summary>
    public static GameIconRef DutyFinderIcon => GameIconRef.Tile(DutyFinder);

    /// <summary>
    /// The map marker of a quest of journal icon family <paramref name="eventIconType"/> (<see cref="QuestRecord.EventIconType"/>):
    /// the main scenario's for 3, the blue "+" feature marker for the feature quest and the quasi-quest, the side
    /// quest's for every other.
    /// </summary>
    public static uint QuestMarker(byte eventIconType) => eventIconType switch
    {
        MsqEventIconType => NodeIcons.MsqMarker,
        FeaturePresets.FeatureEventIconType or FeaturePresets.QuasiQuestEventIconType => NodeIcons.FeatureMarker,
        _ => NodeIcons.SidequestMarker,
    };

    /// <summary><c>Quest.EventIconType</c> of the main scenario.</summary>
    public const byte MsqEventIconType = 3;

    /// <summary>Go to giver (Questionable): the quest's own map marker, a map symbol; the side quest's for no quest.</summary>
    public static GameIconRef GoTo(QuestRecord? quest) => GameIconRef.Symbol(QuestMarker(quest?.EventIconType ?? 0));

    /// <summary>A job's icon (062100 + the ClassJob row), an action tile; 0 for no job.</summary>
    public static uint Job(uint classJob) => classJob is 0 or > LastJobRow ? 0u : JobIconBase + classJob;

    /// <summary>
    /// The game font's glyph for instance <paramref name="instance"/> (SeIconChar.Instance1 … Instance9, U+E0B1 …
    /// U+E0B9); <c>'\0'</c> outside 1–9, where the caller writes "Instance 12" instead.
    /// </summary>
    public static char InstanceGlyph(int instance) => instance is >= 1 and <= 9 ? (char)(FirstInstanceGlyph + instance - 1) : '\0';

    /// <summary>
    /// The 22 px icon before the Route window's title (spec B4, I19): the target's own when the caller knew it (a duty's
    /// tile, a reward's icon, a job's), else a quest target's map marker; <see cref="GameIconRef.None"/> leaves the
    /// FontAwesome sign. <paramref name="quest"/> is the target's first quest, for its marker.
    /// </summary>
    public static GameIconRef RouteHeader(RouteTarget? target, QuestRecord? quest)
    {
        if (target is null)
        {
            return GameIconRef.None;
        }

        if (target.Icon != 0)
        {
            return target.Kind == RouteTargetKind.Quest ? GameIconRef.Symbol(target.Icon) : GameIconRef.Tile(target.Icon);
        }

        return target.Kind == RouteTargetKind.Quest && quest is not null ? GoTo(quest) : GameIconRef.None;
    }

    /// <summary>
    /// The 18 px icon on a requirement line (spec B4, I17), after its check or cross: Class or job wears the pinned job's
    /// icon (the Class &amp; Job emblem for a category of several), Level the job the check was made for
    /// (<paramref name="job"/>); the previous quest's marker; the Grand
    /// Company's insignia; the allied society's emblem; Mount Roulette for a mount; the achievement's icon; the duty's
    /// tile. <see cref="GameIconRef.None"/> for a requirement with no game thing behind it (a seasonal event, the level
    /// cap), so its line keeps the empty slot.
    /// </summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="job">The ClassJob the level check was made for (the Class or job line's job); 0 when unknown.</param>
    /// <param name="eventIconType">A quest's journal icon family (<see cref="QuestRecord.EventIconType"/>), by row id.</param>
    /// <param name="dutyIcon">An instance's icon (<c>DutyIcons.ForInstance</c>), by InstanceContent id; 0 when unknown.</param>
    /// <param name="sheets">The sheet icons.</param>
    public static GameIconRef Requirement(Requirement requirement, uint job, Func<uint, byte?> eventIconType, Func<uint, uint>? dutyIcon, IPaneIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(eventIconType);
        ArgumentNullException.ThrowIfNull(sheets);
        switch (requirement)
        {
            case ClassJobRequirement classJob:
            {
                // One pinned job wears its own icon; a category of several, the Class & Job emblem.
                var pinned = Job(classJob.RequiredJob);
                return GameIconRef.Tile(pinned != 0 ? pinned : NodeIcons.ClassJobEmblem);
            }

            case LevelRequirement:
                return GameIconRef.Tile(Job(job));
            case PreviousQuestsRequirement previous:
                foreach (var id in previous.QuestIds)
                {
                    if (eventIconType(id) is { } family)
                    {
                        return GameIconRef.Symbol(QuestMarker(family));
                    }
                }

                return GameIconRef.None;
            case GrandCompanyRequirement company:
                return GameIconRef.Tile(PaneIcons.GrandCompanyRank(company.GrandCompany, 0, sheets));
            case GrandCompanyRankRequirement rank:
                return GameIconRef.Tile(PaneIcons.GrandCompanyRank(rank.GrandCompany, rank.RequiredRank, sheets));
            case TribeRankRequirement tribe:
                return GameIconRef.Tile(PaneIcons.Tribe(tribe.Tribe, sheets));
            case TribeReputationRequirement reputation:
                return GameIconRef.Tile(PaneIcons.Tribe(reputation.Tribe, sheets));
            case MountRequirement mount:
            {
                var first = mount.Missing.Length > 0 ? mount.Missing[0] : mount.Mounts.Length > 0 ? mount.Mounts[0] : 0u;
                var own = first == 0 ? 0u : sheets.MountIcon(first);
                return GameIconRef.Tile(own != 0 ? own : Mount);
            }

            case AchievementRequirement achievement:
                return GameIconRef.Tile(sheets.AchievementIcon(achievement.RowId));
            case DutyCompletionRequirement duty:
            {
                var icon = duty.InstanceIds.Length > 0 && dutyIcon is not null ? dutyIcon(duty.InstanceIds[0]) : 0u;
                return GameIconRef.Tile(icon != 0 ? icon : DutyFinder);
            }

            default:
                return GameIconRef.None;
        }
    }

    /// <summary>Every icon id <see cref="ActionIcons"/> answers without a sheet, for the game-data test.</summary>
    public static IReadOnlyList<uint> FixedIcons { get; } =
    [
        Teleport, Return, Walk, Flag, Map, Aethernet, Ferry, Mount, Fly, DutyFinder,
        NodeIcons.MsqMarker, NodeIcons.SidequestMarker, NodeIcons.FeatureMarker, NodeIcons.ClassJobEmblem,
        RewardIcons.Exp, RewardIcons.Gil,
    ];
}
