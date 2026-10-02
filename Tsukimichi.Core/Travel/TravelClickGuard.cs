namespace Tsukimichi.Core.Travel;

/// <summary>
/// Swallows the second click of a double click on a travel control. Walk and Go to giver turn into Stop the frame
/// after they start, and Teleport stays clickable until the cast shows, so a quick double click would start a walk and
/// stop it at once, or ask for a second teleport mid-cast. For <see cref="WindowMs"/> after a start, a Stop or another
/// start is ignored. Pure: the caller passes a millisecond clock.
/// </summary>
public sealed class TravelClickGuard
{
    /// <summary>How long after a start a further click is taken as part of the same double click.</summary>
    public const long WindowMs = 300;

    private long? startedAt;

    /// <summary>Records that a travel control started something at <paramref name="now"/>.</summary>
    public void Started(long now) => startedAt = now;

    /// <summary>True while a click at <paramref name="now"/> is still part of the click that started something.</summary>
    public bool Holding(long now) => startedAt is { } at && now - at >= 0 && now - at < WindowMs;
}
