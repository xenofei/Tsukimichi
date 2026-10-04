namespace Tsukimichi.Core.Ui;

/// <summary>
/// A draw path over the game that failed (the moon icon, its quick card, its particles): it rests, then tries again
/// after a back-off that doubles from <see cref="FirstSeconds"/> up to <see cref="MaxSeconds"/>, so one bad frame never
/// hides it for the session and a lasting fault costs next to nothing. Its failure is logged at most once every
/// <see cref="LogEverySeconds"/>. A frame that draws resets the back-off. Pure; the clock is the caller's.
/// </summary>
public sealed class DrawRetry
{
    /// <summary>The first rest after a failure.</summary>
    public const double FirstSeconds = 1.0;

    /// <summary>The longest rest.</summary>
    public const double MaxSeconds = 30.0;

    /// <summary>The least time between two logged failures.</summary>
    public const double LogEverySeconds = 60.0;

    private double retryAt = double.NegativeInfinity;
    private double loggedAt = double.NegativeInfinity;

    /// <summary>The rest after the last failure, in seconds; 0 while nothing failed since the last frame that drew.</summary>
    public double Backoff { get; private set; }

    /// <summary>Whether the path may draw at <paramref name="now"/> (it is not resting after a failure).</summary>
    public bool Ready(double now) => !(now < retryAt);

    /// <summary>The path drew: the next failure rests <see cref="FirstSeconds"/> again.</summary>
    public void Succeeded() => Backoff = 0.0;

    /// <summary>The path failed at <paramref name="now"/>: it rests, and the result says whether to log it.</summary>
    public bool Failed(double now)
    {
        Backoff = Backoff > 0.0 ? Math.Min(Backoff * 2.0, MaxSeconds) : FirstSeconds;
        retryAt = now + Backoff;
        if (now - loggedAt >= LogEverySeconds || now < loggedAt)
        {
            loggedAt = now;
            return true;
        }

        return false;
    }
}
