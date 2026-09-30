using System.Globalization;

namespace Tsukimichi.Core.Runtime;

/// <summary>What the <see cref="HookGate"/> decided, and why.</summary>
public enum HookGateVerdict
{
    /// <summary>The running game version is the one the hooks were play-tested on.</summary>
    Tested,

    /// <summary>The game is newer than the tested version: the hooks are paused until an update is tested.</summary>
    Paused,

    /// <summary>The game is newer than the tested version and the player turned on "Enable game hooks on untested versions".</summary>
    Overridden,

    /// <summary>The game is older than the tested version (a stale client or a test server): allowed, noted in the log.</summary>
    Older,

    /// <summary>Either version is missing or unreadable: allowed, noted in the log; the kill switch only acts on evidence.</summary>
    Unknown,
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
/// into the assembly); a newer game pauses them until an update ships with the tested version bumped, unless the
/// player turned on "Enable game hooks on untested versions". An older or unreadable version leaves them on with a
/// log note: the switch acts only when the client is known to be newer.
/// <para>
/// One instance is shared by every hook consumer, which reads <see cref="HooksAllowed"/> and re-applies itself on
/// <see cref="Changed"/>. The Duty Finder unlock hint (P13, 0.9.0) draws beside a game addon too and is to take the
/// same instance. Not thread-safe: set and read on the framework thread.
/// </para>
/// </summary>
public sealed class HookGate
{
    private string runningVersion = string.Empty;
    private bool enableAnyway;

    public HookGate(string? testedVersion, string? runningVersion = null, bool enableAnyway = false)
    {
        TestedVersion = testedVersion?.Trim() ?? string.Empty;
        this.runningVersion = runningVersion?.Trim() ?? string.Empty;
        this.enableAnyway = enableAnyway;
        Decision = Decide(TestedVersion, this.runningVersion, enableAnyway);
    }

    /// <summary>Raised after <see cref="Decision"/> changed (a new running version or a flipped override).</summary>
    public event Action? Changed;

    /// <summary>The game version the hooks were tested on; empty in a build that carries none.</summary>
    public string TestedVersion { get; }

    /// <summary>The client's game version; empty until known.</summary>
    public string RunningVersion => runningVersion;

    /// <summary>The player's "Enable game hooks on untested versions" setting.</summary>
    public bool EnableAnyway => enableAnyway;

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

    /// <summary>Applies the player's override; raises <see cref="Changed"/> when the decision moves.</summary>
    public void SetEnableAnyway(bool value)
    {
        enableAnyway = value;
        Update();
    }

    /// <summary>
    /// The rule: same version → allowed; running newer → paused unless <paramref name="enableAnyway"/>; running older,
    /// or either version missing or unparseable → allowed (the decision's <see cref="HookGateDecision.LogNote"/> says so).
    /// </summary>
    public static HookGateDecision Decide(string? tested, string? running, bool enableAnyway)
    {
        var testedText = tested?.Trim() ?? string.Empty;
        var runningText = running?.Trim() ?? string.Empty;
        if (!GameVersion.TryParse(testedText, out var testedVersion) || !GameVersion.TryParse(runningText, out var runningVersion))
        {
            return new HookGateDecision(HookGateVerdict.Unknown, testedText, runningText);
        }

        var order = runningVersion.CompareTo(testedVersion);
        var verdict = order switch
        {
            0 => HookGateVerdict.Tested,
            < 0 => HookGateVerdict.Older,
            _ => enableAnyway ? HookGateVerdict.Overridden : HookGateVerdict.Paused,
        };
        return new HookGateDecision(verdict, testedText, runningText);
    }

    private void Update()
    {
        var next = Decide(TestedVersion, runningVersion, enableAnyway);
        if (next == Decision)
        {
            return;
        }

        Decision = next;
        Changed?.Invoke();
    }
}
