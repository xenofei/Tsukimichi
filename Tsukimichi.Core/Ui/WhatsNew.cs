using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Whether the What's new popup shows (spec-1.22 W1): once per update, never on a fresh install. The decision is pure
/// so it can be tested; the plugin persists <c>LastSeenVersion</c>.
/// </summary>
public enum WhatsNewDecision
{
    /// <summary>Nothing to do: the running version was already seen.</summary>
    Nothing,

    /// <summary>Fresh install (no version seen yet) or no notes for what arrived: record the version silently.</summary>
    RecordSilently,

    /// <summary>Show the popup, at the first quiet moment.</summary>
    Show,
}

/// <summary>What the player is doing, as far as the What's new popup's quiet moment cares (spec-1.22 W1, "When it shows").</summary>
/// <param name="InWorld">Logged in with a character in the world.</param>
/// <param name="SecondsInWorld">How long the character has been in the world since login.</param>
/// <param name="InCombat">In combat.</param>
/// <param name="InDuty">Bound by a duty.</param>
/// <param name="InCutscene">Watching a cutscene or in a cutscene event.</param>
/// <param name="GroupPose">In group pose.</param>
/// <param name="Loading">Between areas (a loading screen).</param>
public readonly record struct WhatsNewMoment(bool InWorld, double SecondsInWorld, bool InCombat, bool InDuty, bool InCutscene, bool GroupPose, bool Loading);

public static class WhatsNew
{
    /// <summary>How long the character is in the world before the popup may show (spec-1.22 W1).</summary>
    public const double SettleSeconds = 10.0;

    /// <param name="lastSeenVersion">
    /// The persisted last-seen version: empty on a fresh install, and also after an update from a build that did not
    /// record it yet (every release before 0.6.0), which <paramref name="hasPriorConfig"/> tells apart.
    /// </param>
    /// <param name="runningVersion">The plugin's assembly version.</param>
    /// <param name="hasSection">
    /// Whether there is anything to show: notes for the running release or for any release skipped since
    /// <paramref name="lastSeenVersion"/> (<see cref="Tsukimichi.Core.Releases.ReleaseNotes.Since"/>).
    /// </param>
    /// <param name="hasPriorConfig">
    /// Whether a configuration existed before this load (the file was there). An empty
    /// <paramref name="lastSeenVersion"/> with one is an update from a build that predates the record and shows it;
    /// without one it is a fresh install, which records the version silently.
    /// </param>
    public static WhatsNewDecision Decide(string? lastSeenVersion, string? runningVersion, bool hasSection, bool hasPriorConfig)
    {
        var running = ChangelogSection.NormalizeVersion(runningVersion);
        var seen = ChangelogSection.NormalizeVersion(lastSeenVersion);
        if (running.Length == 0 || seen == running)
        {
            return WhatsNewDecision.Nothing;
        }

        var freshInstall = seen.Length == 0 && !hasPriorConfig;
        return freshInstall || !hasSection ? WhatsNewDecision.RecordSilently : WhatsNewDecision.Show;
    }

    /// <summary>
    /// <see cref="Decide(string?, string?, bool, bool)"/> for a caller that cannot say whether a configuration
    /// existed: an empty last-seen version then reads as a fresh install, so an update from a build before 0.6.0
    /// records silently. Callers with the configuration at hand pass <c>hasPriorConfig</c>.
    /// </summary>
    public static WhatsNewDecision Decide(string? lastSeenVersion, string? runningVersion, bool hasSection) =>
        Decide(lastSeenVersion, runningVersion, hasSection, hasPriorConfig: false);

    /// <summary>
    /// The first quiet moment after login (spec-1.22 W1): a character in the world for at least
    /// <see cref="SettleSeconds"/>, and not in combat, a duty, a cutscene, group pose or a loading screen. The popup
    /// never covers a fight or a scene (decision 1).
    /// </summary>
    public static bool IsQuietMoment(in WhatsNewMoment moment) =>
        moment.InWorld
        && moment.SecondsInWorld >= SettleSeconds
        && !moment.InCombat
        && !moment.InDuty
        && !moment.InCutscene
        && !moment.GroupPose
        && !moment.Loading;
}

/// <summary>
/// The What's new popup's fixed geometry (spec-1.22 W1, "Anatomy"), in logical px at UI scale 1.0, and the rules that
/// keep it still while the player pages: the notes block takes the tallest page's height, capped so the popup stays
/// within <see cref="ScreenShare"/> of the screen (past the cap a page's notes scroll), and the art band is either on
/// every page or on none. Pure.
/// </summary>
public static class WhatsNewLayout
{
    /// <summary>The popup's width.</summary>
    public const float Width = 560f;

    /// <summary>The header's height at Full and Quiet; Plain's band is <see cref="HeaderPlain"/>.</summary>
    public const float Header = 48f;

    /// <inheritdoc cref="Header"/>
    public const float HeaderPlain = 26f;

    /// <summary>The art band's height (536 × 220, the 2x art at half size), inset <see cref="ArtInset"/> from the sides.</summary>
    public const float ArtHeight = 220f;

    /// <inheritdoc cref="ArtHeight"/>
    public const float ArtInset = 12f;

    /// <summary>The footer's height at Full and Quiet; Plain's band is <see cref="FooterPlain"/>.</summary>
    public const float Footer = 56f;

    /// <inheritdoc cref="Footer"/>
    public const float FooterPlain = 30f;

    /// <summary>Room under the tallest page's last line.</summary>
    public const float NotesRoom = 14f;

    /// <summary>The most of the screen's height the popup takes.</summary>
    public const float ScreenShare = 0.8f;

    /// <summary>The least the notes block is given, however small the screen, so a line always shows.</summary>
    public const float MinBlock = 60f;

    /// <summary>
    /// Whether the popup has an art band: never at Plain (it loads no art, decision 4), else when at least one page has
    /// its picture. A page whose picture is missing then shows the band's flat sky, so the layout never changes as the
    /// player pages; with no picture on any page the band is left out.
    /// </summary>
    public static bool ArtBand(Flair flair, ReadOnlySpan<bool> pagesWithArt)
    {
        if (flair == Flair.Plain)
        {
            return false;
        }

        foreach (var has in pagesWithArt)
        {
            if (has)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The notes block's height in px: the tallest page's (<paramref name="tallestPage"/>) plus <see cref="NotesRoom"/>,
    /// capped so <paramref name="fixedHeight"/> (header, art and footer) plus the block stay within
    /// <see cref="ScreenShare"/> of <paramref name="screenHeight"/>, and never under <see cref="MinBlock"/>.
    /// </summary>
    /// <param name="scale">Px per logical px (the UI scale times the global scale).</param>
    public static float NotesBlock(float tallestPage, float fixedHeight, float screenHeight, float scale)
    {
        var wanted = MathF.Max(0f, tallestPage) + (NotesRoom * scale);
        var cap = MathF.Max(MinBlock * scale, (screenHeight * ScreenShare) - fixedHeight);
        return MathF.Ceiling(MathF.Min(wanted, cap));
    }

    /// <summary>The header, art and footer heights in px for <paramref name="flair"/>, with or without the art band.</summary>
    public static float FixedHeight(Flair flair, bool artBand, float scale)
    {
        var plain = flair == Flair.Plain;
        var header = plain ? HeaderPlain : Header;
        var footer = plain ? FooterPlain : Footer;
        return MathF.Round((header + footer + (artBand ? ArtHeight : 0f)) * scale);
    }

    /// <summary>
    /// The popup's top left at <paramref name="pos"/>, rounded and kept inside the screen's work area
    /// (<paramref name="workPos"/>, <paramref name="workSize"/>); a popup taller or wider than the screen keeps its top
    /// left on screen, so the header and × are always reachable.
    /// </summary>
    public static Vector2 Clamp(Vector2 pos, Vector2 size, Vector2 workPos, Vector2 workSize)
    {
        var high = workPos + workSize - size;
        return new Vector2(
            MathF.Round(Math.Clamp(pos.X, workPos.X, MathF.Max(workPos.X, high.X))),
            MathF.Round(Math.Clamp(pos.Y, workPos.Y, MathF.Max(workPos.Y, high.Y))));
    }
}

/// <summary>
/// Where the measured What's new popup goes, frame by frame (spec-1.22 W1, "Motion"): on the first frame after it is
/// measured always (at its target, 4 px low while the open's rise plays, at the target itself when motion is off), every
/// frame while it rises, and once more at the target when the rise ends; then it is left where it is. Every place is kept
/// inside the screen. <see cref="Reset"/> starts it again for the next open. Pure.
/// </summary>
public struct WhatsNewPlacement
{
    private bool settled;

    /// <summary>The popup was measured again (it opened): the next frame places it.</summary>
    public void Reset() => settled = false;

    /// <summary>
    /// The popup's top left this frame, or null to leave it where it is.
    /// </summary>
    /// <param name="target">Where it rests (centred and clamped when it was measured).</param>
    /// <param name="size">Its size this frame.</param>
    /// <param name="workPos">The screen's work area.</param>
    /// <param name="workSize">The screen's work area.</param>
    /// <param name="motion">Whether motion plays (Reduce motion off, not Plain).</param>
    /// <param name="sinceOpen">Seconds since it was measured.</param>
    /// <param name="scale">Px per logical px.</param>
    public Vector2? Next(Vector2 target, Vector2 size, Vector2 workPos, Vector2 workSize, bool motion, double sinceOpen, float scale)
    {
        if (settled)
        {
            return null;
        }

        var t = motion && double.IsFinite(sinceOpen) ? (float)(sinceOpen / MotionTokens.Rise) : 1f;
        var rise = 0f;
        if (t >= 1f)
        {
            settled = true;
        }
        else
        {
            rise = MathF.Round(MotionTokens.RiseLogical * scale * (1f - MotionMath.EaseOutCubic(t)));
        }

        return WhatsNewLayout.Clamp(target + new Vector2(0f, rise), size, workPos, workSize);
    }
}
