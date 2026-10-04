namespace Tsukimichi.Core.Ipc;

/// <summary>
/// What Tsukimichi says about tonight in a few numbers and lines (plan v8 M1 and M2; spec-1.22 H1's quick card, M1's
/// tooltip, A1's popup): the logged-in character, Up next, the Ready count, the journal, the events ending soon and the
/// story meter. One immutable capture, built on the framework thread when its inputs change, read by the server info
/// bar's tooltip and the summary IPC gates from any thread. Every quest name in it already went through the
/// character's spoiler shield, so a consumer never receives a hidden name.
/// </summary>
/// <param name="Ready">Whether a character is logged in and evaluated; false for <see cref="Empty"/>.</param>
/// <param name="Character">The character's name.</param>
/// <param name="Job">The current job's abbreviation ("WHM").</param>
/// <param name="Level">The current job's level.</param>
/// <param name="UpNextRowId">Up next's Quest row id; 0 for none.</param>
/// <param name="UpNextName">Up next's name through the shield; empty for none.</param>
/// <param name="UpNextStep">Up next's place line ("Step 3: Speak with Erenville. · Shaaloani", "Talk to … · place"); empty for none.</param>
/// <param name="ReadyCount">Quests Ready on the current job.</param>
/// <param name="HereCount">Quests that can start in the current zone.</param>
/// <param name="JournalUsed">Journal slots holding a quest; -1 when not read.</param>
/// <param name="JournalCap">Journal slots in all.</param>
/// <param name="EndingSoon">Events ending soon, soonest first: name and whole days left (0 ends today).</param>
/// <param name="StoryPart">The main scenario part the character is in ("Dawntrail"); the latest part when caught up.</param>
/// <param name="StoryLeft">Main scenario quests left to the latest story; 0 when caught up.</param>
/// <param name="CaughtUp">The main scenario is done to the latest story.</param>
public sealed record TsukimichiSummary(
    bool Ready,
    string Character,
    string Job,
    int Level,
    uint UpNextRowId,
    string UpNextName,
    string UpNextStep,
    int ReadyCount,
    int HereCount,
    int JournalUsed,
    int JournalCap,
    IReadOnlyList<(string Name, int DaysLeft)> EndingSoon,
    string StoryPart,
    int StoryLeft,
    bool CaughtUp)
{
    /// <summary>Nobody logged in, or the catalog not built: every gate's not-ready answer.</summary>
    public static readonly TsukimichiSummary Empty = new(false, string.Empty, string.Empty, 0, 0, string.Empty, string.Empty, 0, 0, -1, 0, [], string.Empty, 0, false);

    /// <summary><see cref="IpcChannels.GetCharacterGate"/>'s answer.</summary>
    public (string Name, string Job, int Level) CharacterAnswer() => (Character, Job, Level);

    /// <summary><see cref="IpcChannels.GetUpNextGate"/>'s answer.</summary>
    public (uint RowId, string Name, string Step) UpNextAnswer() => (UpNextRowId, UpNextName, UpNextStep);

    /// <summary><see cref="IpcChannels.GetReadyCountGate"/>'s answer.</summary>
    public (int Ready, int Here, string Job) ReadyAnswer() => (ReadyCount, HereCount, Job);

    /// <summary><see cref="IpcChannels.GetJournalRoomGate"/>'s answer: (-1, 0) when the journal was not read.</summary>
    public (int Used, int Cap) JournalAnswer() => JournalUsed < 0 ? (-1, 0) : (JournalUsed, JournalCap);

    /// <summary><see cref="IpcChannels.GetEndingSoonGate"/>'s answer, a fresh array per call.</summary>
    public (string Name, int DaysLeft)[] EndingSoonAnswer() => [.. EndingSoon];

    /// <summary><see cref="IpcChannels.GetStoryMeterGate"/>'s answer.</summary>
    public (string Part, int LeftToLatest, bool CaughtUp) StoryAnswer() => (StoryPart, StoryLeft, CaughtUp);

    /// <summary>The most Ready quests a capture keeps for <see cref="IpcChannels.GetReadyTonightGate"/>.</summary>
    public const int MaxReadyTonight = 20;

    /// <summary>
    /// The first Ready quests in Tonight's order (<c>Core.Todo.UpNextPicker.ReadyInOrder</c>), at most
    /// <see cref="MaxReadyTonight"/>: Quest row id, name and the giver's place, both through the logged-in character's shield.
    /// </summary>
    public IReadOnlyList<(uint RowId, string Name, string Place)> ReadyTonight { get; init; } = [];

    /// <summary><see cref="IpcChannels.GetReadyTonightGate"/>'s answer: the first <paramref name="max"/>, a fresh array per call.</summary>
    public (uint RowId, string Name, string Place)[] ReadyTonightAnswer(int max) =>
        max <= 0 ? [] : [.. ReadyTonight.Take(max)];

    /// <summary>Whether two captures say the same (the <see cref="IpcChannels.SummaryChangedGate"/> message's test).</summary>
    public bool Same(TsukimichiSummary? other) =>
        other is not null
        && this with { EndingSoon = [], ReadyTonight = [] } == other with { EndingSoon = [], ReadyTonight = [] }
        && EndingSoon.SequenceEqual(other.EndingSoon)
        && ReadyTonight.SequenceEqual(other.ReadyTonight);
}

/// <summary>
/// The places <see cref="IpcChannels.OpenAtGate"/> opens Tsukimichi at (plan v8 M2). The words are stable within
/// <see cref="IpcChannels.SummaryVersion"/>; a new one is appended. Opening never starts travel, a route or a run: anything
/// that does still needs the player's click inside Tsukimichi.
/// </summary>
public static class IpcPlaces
{
    /// <summary>The main window as the player left it.</summary>
    public const string Main = "main";

    /// <summary>The main window on Tonight.</summary>
    public const string Tonight = "tonight";

    /// <summary>The main window on Tonight's Up next, its quest selected (A1's Go: it never travels by itself).</summary>
    public const string UpNext = "upnext";

    /// <summary>The followed route's window (nothing when no route is followed).</summary>
    public const string Route = "route";

    /// <summary>The Settings window.</summary>
    public const string Settings = "settings";

    /// <summary>The main window with the Make room popover open (C9, 1.19.0): the journal's quests by what finishing each takes.</summary>
    public const string MakeRoom = "makeroom";

    /// <summary>Every place, in the order docs/ipc.md lists them (a new one is appended).</summary>
    public static IReadOnlyList<string> All { get; } = [Main, Tonight, UpNext, Route, Settings, MakeRoom];

    /// <summary>The place a caller's word names (case and spaces ignored); null for a word this build does not know.</summary>
    public static string? Parse(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return null;
        }

        var trimmed = word.Trim();
        foreach (var place in All)
        {
            if (string.Equals(place, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return place;
            }
        }

        return null;
    }
}
