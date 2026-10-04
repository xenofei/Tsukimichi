namespace Tsukimichi.Core.Companions;

/// <summary>
/// One "Needs you" alert on the panel over the game (plan v7, 1.18.0, A5): its kind, its title in plain words ("You
/// were knocked out") and one line of what Tsukimichi did. <see cref="Fix"/> is the one safe fix it offers beside Stop
/// all and Dismiss (Reload navmesh and retry for Stuck), or <see cref="StopFix.None"/>.
/// </summary>
public sealed record NeedsYouAlert(NeedsYouKind Kind, string Title, string Line)
{
    public StopFix Fix { get; init; }

    /// <summary>
    /// The resource key of the kind's panel title ("NeedsYouTitleDeath"). Every kind has one with words in it, beside
    /// the "Needs you" eyebrow (<see cref="EyebrowKey"/>); a test holds the resource file to it.
    /// </summary>
    public static string TitleKey(NeedsYouKind kind) => "NeedsYouTitle" + kind;

    /// <summary>The resource key of the panel's eyebrow, "Needs you".</summary>
    public const string EyebrowKey = "NeedsYouEyebrow";
}

/// <summary>
/// The "Needs you" panel's queue (spec-1.18 A5): one alert at a time, the rest waiting behind "+1 more". An alert of a
/// kind already waiting replaces that one's words in place (a second tell is not a second alert). An alert stays until
/// the player dismisses it or its cause clears (<see cref="Clear"/>: the character is up again, the duty pop closed);
/// then it fades over <see cref="StopDock.LeaveSeconds"/> and the next one rises in. It rises
/// <see cref="Ui.MotionTokens.RiseLogical"/> px and fades in over <see cref="Ui.MotionTokens.Rise"/>, once: no pulse,
/// no blink. Under Reduce motion it appears and goes at once. Pure, so the rules are tested.
/// </summary>
public sealed class NeedsYouQueue
{
    private readonly List<NeedsYouAlert> waiting = [];
    private double shownAt;
    private double leavingAt = double.NaN;

    /// <summary>The alert on the panel, or null when none shows; a leaving alert stays here until it has faded.</summary>
    public NeedsYouAlert? Current => waiting.Count > 0 ? waiting[0] : null;

    /// <summary>How many more wait behind the one on the panel ("+1 more").</summary>
    public int More => Math.Max(0, waiting.Count - 1);

    /// <summary>Whether the alert on the panel is fading out.</summary>
    public bool Leaving => !double.IsNaN(leavingAt);

    /// <summary>Moves whenever the alert on the panel changes, so the panel knows to lay it out again.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Queues <paramref name="alert"/> at <paramref name="now"/>; one of a kind already waiting takes its place. An alert
    /// without a title throws: the copper bar is never the only carrier of "needs you".
    /// </summary>
    public void Raise(NeedsYouAlert alert, double now)
    {
        ArgumentNullException.ThrowIfNull(alert);
        if (string.IsNullOrWhiteSpace(alert.Title))
        {
            throw new ArgumentException("A Needs you alert needs a title in words beside its copper bar.", nameof(alert));
        }
        for (var i = 0; i < waiting.Count; i++)
        {
            if (waiting[i].Kind == alert.Kind)
            {
                waiting[i] = alert;
                if (i == 0)
                {
                    Version++;
                }

                return;
            }
        }

        waiting.Add(alert);
        if (waiting.Count == 1)
        {
            Show(now);
        }
    }

    /// <summary>The player's Dismiss: the alert on the panel fades out and the next one follows.</summary>
    public void Dismiss(double now)
    {
        if (waiting.Count > 0 && !Leaving)
        {
            leavingAt = now;
        }
    }

    /// <summary>
    /// The cause of every alert of <paramref name="kind"/> cleared: one waiting is dropped, the one on the panel fades.
    /// </summary>
    public void Clear(NeedsYouKind kind, double now)
    {
        for (var i = waiting.Count - 1; i >= 1; i--)
        {
            if (waiting[i].Kind == kind)
            {
                waiting.RemoveAt(i);
            }
        }

        if (Current is { } shown && shown.Kind == kind)
        {
            Dismiss(now);
        }
    }

    /// <summary>Whether an alert of <paramref name="kind"/> is on the panel or waiting.</summary>
    public bool Has(NeedsYouKind kind)
    {
        foreach (var alert in waiting)
        {
            if (alert.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finishes a fade-out: the next alert rises in. Returns whether an alert is on the panel afterwards.</summary>
    public bool Tick(double now, bool reduceMotion)
    {
        if (Leaving && (reduceMotion || now - leavingAt >= StopDock.LeaveSeconds))
        {
            waiting.RemoveAt(0);
            leavingAt = double.NaN;
            if (waiting.Count > 0)
            {
                Show(now);
            }
            else
            {
                Version++;
            }
        }

        return waiting.Count > 0;
    }

    /// <summary>The panel's opacity at <paramref name="now"/>.</summary>
    public float Alpha(double now, bool reduceMotion)
    {
        if (waiting.Count == 0)
        {
            return 0f;
        }

        if (Leaving)
        {
            return reduceMotion ? 0f : (float)Math.Clamp(1.0 - ((now - leavingAt) / StopDock.LeaveSeconds), 0.0, 1.0);
        }

        return reduceMotion ? 1f : (float)Math.Clamp((now - shownAt) / Ui.MotionTokens.Rise, 0.0, 1.0);
    }

    /// <summary>
    /// How far below its place the panel still is, in logical px: <see cref="Ui.MotionTokens.RiseLogical"/> when it
    /// appears, 0 once risen (eased out). Leaving it does not move; under Reduce motion it never does.
    /// </summary>
    public float Rise(double now, bool reduceMotion)
    {
        if (reduceMotion || waiting.Count == 0 || Leaving)
        {
            return 0f;
        }

        var t = (float)Math.Clamp((now - shownAt) / Ui.MotionTokens.Rise, 0.0, 1.0);
        return Ui.MotionTokens.RiseLogical * (1f - Ui.MotionMath.EaseOutCubic(t));
    }

    /// <summary>Whether the panel's buttons act at <paramref name="now"/>: risen in and not leaving.</summary>
    public bool Interactive(double now, bool reduceMotion) => waiting.Count > 0 && !Leaving && Alpha(now, reduceMotion) >= 1f;

    private void Show(double now)
    {
        shownAt = now;
        Version++;
    }
}
