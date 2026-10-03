namespace Tsukimichi.Core.Companions;

/// <summary>
/// Remembers that Tsukimichi started another plugin's current run (an AutoDuty duty, an Artisan craft, a Lifestream
/// task), so <c>/tsuki stop</c> (1.11.0, A1) stops only what Tsukimichi handed off and leaves a run the player started
/// in that plugin's own window alone. The claim is made when the hand-off is accepted and lasts while the plugin
/// reports busy; it ends the first time the plugin reads idle after it was seen busy, or still idle
/// <see cref="GraceMs"/> after the hand-off (it never started). Fed the plugin's busy state each frame while claimed.
/// </summary>
public sealed class HandOffClaim
{
    /// <summary>How long a plugin may take to report busy after the hand-off before the claim lapses.</summary>
    public const long GraceMs = 5_000;

    private long? claimedAt;
    private bool seenBusy;

    /// <summary>True while a run Tsukimichi handed off may still be under way.</summary>
    public bool Claimed => claimedAt is not null;

    /// <summary>The plugin accepted a hand-off at <paramref name="now"/> (milliseconds).</summary>
    public void Claim(long now)
    {
        claimedAt = now;
        seenBusy = false;
    }

    /// <summary>Drops the claim: the run was stopped, or the plugin went away.</summary>
    public void Release()
    {
        claimedAt = null;
        seenBusy = false;
    }

    /// <summary>
    /// One reading of the plugin's state. True when the run under way is Tsukimichi's: claimed and busy now. Ends the
    /// claim once the run is over (idle after busy) or never began (idle past <see cref="GraceMs"/>).
    /// </summary>
    public bool Observe(bool busy, long now)
    {
        if (claimedAt is not { } at)
        {
            return false;
        }

        if (busy)
        {
            seenBusy = true;
            return true;
        }

        if (seenBusy || now - at > GraceMs)
        {
            Release();
        }

        return false;
    }
}
