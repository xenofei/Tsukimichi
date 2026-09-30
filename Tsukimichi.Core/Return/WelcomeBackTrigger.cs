using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Return;

/// <summary>What the login of one character does with "Since you were away".</summary>
public enum WelcomeBackDecision : byte
{
    /// <summary>Nothing: not away long enough, already shown, turned off, or another character was played meanwhile.</summary>
    None,

    /// <summary>Open the card with the summary since the stored capture.</summary>
    Show,

    /// <summary>No capture of this character to measure from: open the card on "When did you last play?".</summary>
    AskPatch,
}

/// <summary>
/// When "Since you were away" opens on its own (feature plan v3 P7, with the review panel's alt-nag guard): at a login
/// whose stored capture is at least <c>N</c> days old (Settings › Notices, default 14, 0 = off), once per return,
/// never for a character marked "Don't show again", at most once per plugin session across characters, and only when
/// the account was away: every stored character's newest capture is at least <c>N</c> days old. An alt whose file is
/// stale only because another character was played says nothing. A character with no capture at all (a fresh
/// install) is asked "When did you last play?" once, under the same account-away rule, so a brand-new alt made while
/// the main is active is not asked. Pure.
/// </summary>
public static class WelcomeBackTrigger
{
    /// <summary>Settings › Notices default: 14 days.</summary>
    public const int DefaultDays = 14;

    /// <summary>The slider's upper end.</summary>
    public const int MaxDays = 180;

    /// <summary>The decision for <paramref name="contentId"/>'s login.</summary>
    /// <param name="contentId">The character that logged in.</param>
    /// <param name="capturesAtLogin">Every stored capture as it was before this login's first save (the character's own included, when it has one).</param>
    /// <param name="state">The character's remembered state.</param>
    /// <param name="thresholdDays">Settings › Notices "after N days"; 0 or less turns the card off.</param>
    /// <param name="shownThisSession">Whether the card already opened on its own this plugin session.</param>
    /// <param name="nowUtc">The clock.</param>
    public static WelcomeBackDecision Decide(
        ulong contentId,
        IReadOnlyList<SnapshotSummary> capturesAtLogin,
        WelcomeBackState state,
        int thresholdDays,
        bool shownThisSession,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(capturesAtLogin);
        ArgumentNullException.ThrowIfNull(state);
        if (thresholdDays <= 0 || shownThisSession || state.Quiet)
        {
            return WelcomeBackDecision.None;
        }

        var threshold = TimeSpan.FromDays(Math.Min(thresholdDays, MaxDays));
        if (!AccountAway(capturesAtLogin, threshold, nowUtc))
        {
            return WelcomeBackDecision.None;
        }

        SnapshotSummary? own = null;
        foreach (var capture in capturesAtLogin)
        {
            if (capture.ContentId == contentId)
            {
                own = capture;
                break;
            }
        }

        if (own is null)
        {
            return state.Answered ? WelcomeBackDecision.None : WelcomeBackDecision.AskPatch;
        }

        // Once per return: the card was already shown for this very capture (a reload before the new one was saved).
        return state.ShownForUtc is { } shown && shown == own.TakenUtc ? WelcomeBackDecision.None : WelcomeBackDecision.Show;
    }

    /// <summary>
    /// Whether the account was away: every capture is at least <paramref name="threshold"/> old (true when there is
    /// none, a fresh install). A capture dated in the future counts as recent.
    /// </summary>
    public static bool AccountAway(IReadOnlyList<SnapshotSummary> captures, TimeSpan threshold, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(captures);
        foreach (var capture in captures)
        {
            if (nowUtc - capture.TakenUtc < threshold)
            {
                return false;
            }
        }

        return true;
    }
}
