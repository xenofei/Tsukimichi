using System.Globalization;

namespace Tsukimichi.Core.Runtime;

/// <summary>What the <see cref="HookGate"/> decided, and why.</summary>
public enum HookGateVerdict
{
    /// <summary>The running game version is the one the hooks were play-tested on.</summary>
    Tested,

    /// <summary>The game carries a newer patch date than the tested version: the hooks are paused until an update is tested.</summary>
    Paused,

    /// <summary>The game carries a newer patch date and the player turned on "Enable game hooks on this untested version" for that patch.</summary>
    Overridden,

    /// <summary>The game carries an older patch date than the tested version (a stale client or a test server): allowed, noted in the log.</summary>
    Older,

    /// <summary>Either version is missing or unreadable: allowed, noted in the log; the kill switch only acts on evidence.</summary>
    Unknown,

    /// <summary>
    /// Same patch date as the tested version with other build numbers (a hotfix): allowed, noted in the log. A hotfix
    /// does not move the game's interface the hooks sit beside, so it does not pause them (feature plan v5, decision 6).
    /// </summary>
    Hotfix,
}

/// <summary>A <see cref="HookGate"/> decision: the verdict and the two versions it was made from.</summary>
public readonly record struct HookGateDecision(HookGateVerdict Verdict, string TestedVersion, string RunningVersion)
{
    /// <summary>Whether the game hooks may register.</summary>
    public bool Allowed => Verdict != HookGateVerdict.Paused;

    /// <summary>
    /// One log line for the verdicts worth a note (paused, overridden, older, unknown); null when the versions match.
    /// Carries only the two game versions, nothing about the character.
    /// </summary>
    public string? LogNote => Verdict switch
    {
        HookGateVerdict.Paused => $"Game hooks paused: game {Show(RunningVersion)} is newer than the tested {Show(TestedVersion)}",
        HookGateVerdict.Overridden => $"Game hooks enabled on untested game {Show(RunningVersion)} (tested {Show(TestedVersion)}) by the player's setting",
        HookGateVerdict.Older => $"Game {Show(RunningVersion)} is older than the tested {Show(TestedVersion)}; game hooks left on",
        HookGateVerdict.Unknown => $"Game version not comparable (running {Show(RunningVersion)}, tested {Show(TestedVersion)}); game hooks left on",
        HookGateVerdict.Hotfix => $"Game {Show(RunningVersion)} is a hotfix of the tested {Show(TestedVersion)} (same patch date); game hooks left on",
        _ => null,
    };

    private static string Show(string version) => string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();
}

/// <summary>
/// A game version as <c>ffxivgame.ver</c> writes it: "2026.09.15.0000.0000", a release date then two build numbers.
/// Ordered by date, then build, then revision.
/// </summary>
public readonly record struct GameVersion(int Year, int Month, int Day, int Build, int Revision) : IComparable<GameVersion>
{
    /// <summary>
    /// Parses "yyyy.MM.dd.bbbb.rrrr"; the two build parts may be missing (read as 0). False for anything else: fewer
    /// than three parts, more than five, a non-numeric part, or a date that is not a calendar date.
    /// </summary>
    public static bool TryParse(string? text, out GameVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split('.');
        if (parts.Length is < 3 or > 5)
        {
            return false;
        }

        Span<int> values = stackalloc int[5];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        if (values[1] is < 1 or > 12 || values[2] < 1 || values[0] < 1 || values[2] > DateTime.DaysInMonth(Math.Min(values[0], 9999), values[1]))
        {
            return false;
        }

        version = new GameVersion(values[0], values[1], values[2], values[3], values[4]);
        return true;
    }

    /// <summary>The patch date part ("2026.09.15") as one comparable number, yyyymmdd.</summary>
    public int PatchDate => (Year * 100 + Month) * 100 + Day;

    /// <summary>Orders by the patch date alone: a hotfix (same date, other build numbers) compares equal.</summary>
    public int ComparePatchDate(GameVersion other) => PatchDate.CompareTo(other.PatchDate);

    public int CompareTo(GameVersion other)
    {
        var c = Year.CompareTo(other.Year);
        if (c == 0) { c = Month.CompareTo(other.Month); }
        if (c == 0) { c = Day.CompareTo(other.Day); }
        if (c == 0) { c = Build.CompareTo(other.Build); }
        if (c == 0) { c = Revision.CompareTo(other.Revision); }
        return c;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:D4}.{Month:D2}.{Day:D2}.{Build:D4}.{Revision:D4}");
}

/// <summary>
/// The addon kill switch (feature plan v3 T20): whether the plugin's game hooks (the item tooltip panel, the item
/// and NPC context-menu entries, the server info bar entry) may run on the game version the client reports. The
/// hooks were play-tested on <see cref="TestedVersion"/> (the csproj's <c>TsukimichiTestedGameVersion</c>, stamped
/// into the assembly). Only the patch date counts (feature plan v5, decision 6): a game with a newer date
/// ("2026.10.28" against "2026.09.15") pauses them until an update ships with the tested version bumped, unless the
/// player turned on "Enable game hooks on this untested version" for that patch; a hotfix, which keeps the date and
/// bumps the build numbers, leaves them on. The override is stored as the game version it was ticked on
/// (<see cref="EnableAnywayVersion"/>) and covers every build of that patch date, so the next patch pauses the hooks
/// again. An older or unreadable version leaves them on with a log note: the switch acts only when the client is
/// known to carry a newer patch.
/// <para>
/// One instance is shared by every hook consumer, which reads <see cref="HooksAllowed"/> and re-applies itself on
/// <see cref="Changed"/>. The Duty Finder unlock hint (P13, 0.9.0) draws beside a game addon too and is to take the
/// same instance. Not thread-safe: set and read on the framework thread.
/// </para>
/// </summary>
public sealed class HookGate
{
    private string runningVersion = string.Empty;
    private string enableAnywayVersion = string.Empty;

    /// <param name="enableAnywayVersion">The game version the player enabled the hooks on anyway; empty for none.</param>
    public HookGate(string? testedVersion, string? runningVersion = null, string? enableAnywayVersion = null)
    {
        TestedVersion = testedVersion?.Trim() ?? string.Empty;
        this.runningVersion = runningVersion?.Trim() ?? string.Empty;
        this.enableAnywayVersion = enableAnywayVersion?.Trim() ?? string.Empty;
        Decision = Decide(TestedVersion, this.runningVersion, this.enableAnywayVersion);
    }

    /// <summary>Raised after <see cref="Decision"/> changed (a new running version or a flipped override).</summary>
    public event Action? Changed;

    /// <summary>The game version the hooks were tested on; empty in a build that carries none.</summary>
    public string TestedVersion { get; }

    /// <summary>The client's game version; empty until known.</summary>
    public string RunningVersion => runningVersion;

    /// <summary>The game version the player's "enable anyway" was ticked on; empty when it is off.</summary>
    public string EnableAnywayVersion => enableAnywayVersion;

    /// <summary>Whether the stored override names the running game's patch date (what the Settings checkbox shows).</summary>
    public bool EnableAnywayApplies => OverrideApplies(runningVersion, enableAnywayVersion);

    public HookGateDecision Decision { get; private set; }

    /// <summary>Whether the game hooks may register now.</summary>
    public bool HooksAllowed => Decision.Allowed;

    /// <summary>Whether the hooks are paused on an untested game version (the case the notice speaks about).</summary>
    public bool IsPaused => Decision.Verdict == HookGateVerdict.Paused;

    /// <summary>Records the client's game version once it is known; raises <see cref="Changed"/> when the decision moves.</summary>
    public void SetRunningVersion(string? version)
    {
        runningVersion = version?.Trim() ?? string.Empty;
        Update();
    }

    /// <summary>
    /// Applies the player's override: the game version it was ticked on (the running one), or empty to clear it.
    /// Raises <see cref="Changed"/> when the decision moves.
    /// </summary>
    public void SetEnableAnyway(string? version)
    {
        enableAnywayVersion = version?.Trim() ?? string.Empty;
        Update();
    }

    /// <summary>
    /// The rule, on the patch date alone (year.month.day): same date → allowed (<see cref="HookGateVerdict.Hotfix"/>,
    /// noted, when the build numbers differ); running date newer → paused unless <paramref name="enableAnywayVersion"/>
    /// carries that same date (an override from an earlier patch does not carry over; one from an earlier hotfix of
    /// this patch does); running date older, or either version missing or unparseable → allowed (the decision's
    /// <see cref="HookGateDecision.LogNote"/> says so).
    /// </summary>
    public static HookGateDecision Decide(string? tested, string? running, string? enableAnywayVersion)
    {
        var testedText = tested?.Trim() ?? string.Empty;
        var runningText = running?.Trim() ?? string.Empty;
        if (!GameVersion.TryParse(testedText, out var testedVersion) || !GameVersion.TryParse(runningText, out var runningVersion))
        {
            return new HookGateDecision(HookGateVerdict.Unknown, testedText, runningText);
        }

        var order = runningVersion.ComparePatchDate(testedVersion);
        var verdict = order switch
        {
            0 => runningVersion.CompareTo(testedVersion) == 0 ? HookGateVerdict.Tested : HookGateVerdict.Hotfix,
            < 0 => HookGateVerdict.Older,
            _ => OverrideApplies(runningText, enableAnywayVersion) ? HookGateVerdict.Overridden : HookGateVerdict.Paused,
        };
        return new HookGateDecision(verdict, testedText, runningText);
    }

    /// <summary>Whether a stored override names the running patch: both parse and carry the same patch date.</summary>
    public static bool OverrideApplies(string? running, string? enableAnywayVersion) =>
        GameVersion.TryParse(running, out var runningVersion)
        && GameVersion.TryParse(enableAnywayVersion, out var overrideVersion)
        && runningVersion.ComparePatchDate(overrideVersion) == 0;

    private void Update()
    {
        var next = Decide(TestedVersion, runningVersion, enableAnywayVersion);
        if (next == Decision)
        {
            return;
        }

        Decision = next;
        Changed?.Invoke();
    }
}
