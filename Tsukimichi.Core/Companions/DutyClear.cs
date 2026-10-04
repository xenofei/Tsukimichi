namespace Tsukimichi.Core.Companions;

/// <summary>
/// How a duty can be cleared (feature plan v7 C7, "how you'll clear it"): alone, with NPCs, through the Duty Finder or
/// only with a party the player forms. A duty shows one badge per way it has.
/// </summary>
[Flags]
public enum DutyClearWays : byte
{
    None = 0,

    /// <summary>A solo duty (a quest battle): it seats one player.</summary>
    Solo = 1 << 0,

    /// <summary>The Duty Support window lists it: NPCs fill the party.</summary>
    DutySupport = 1 << 1,

    /// <summary>The Trust window lists it: NPCs fill the party, and level with the player.</summary>
    Trust = 1 << 2,

    /// <summary>The Duty Finder matches players for it.</summary>
    DutyFinder = 1 << 3,

    /// <summary>The Duty Finder does not match it: the player forms the full party (the Party Finder, friends, a static).</summary>
    PartyOnly = 1 << 4,
}

/// <summary>The badges of <see cref="DutyClearWays"/> for a duty, from the duty index's sheet facts. Pure.</summary>
public static class DutyClear
{
    /// <summary>The badge order: the ways that need nobody first.</summary>
    public static readonly IReadOnlyList<DutyClearWays> Order =
        [DutyClearWays.Solo, DutyClearWays.DutySupport, DutyClearWays.Trust, DutyClearWays.DutyFinder, DutyClearWays.PartyOnly];

    /// <summary>
    /// The ways <paramref name="duty"/> can be cleared. A duty that seats one player is solo and nothing else. Any other
    /// lists Duty Support and Trust when they list it, then the Duty Finder when it matches players for it, else Party
    /// only (the Ultimate raids, the current Savage tier and the Chaotic raid read so on the sheet).
    /// </summary>
    public static DutyClearWays Ways(DutyRunInfo duty)
    {
        ArgumentNullException.ThrowIfNull(duty);
        if (duty.Players == 1)
        {
            return DutyClearWays.Solo;
        }

        var ways = DutyClearWays.None;
        if (duty.OffersDutySupport)
        {
            ways |= DutyClearWays.DutySupport;
        }

        if (duty.OffersTrust)
        {
            ways |= DutyClearWays.Trust;
        }

        ways |= duty.InDutyFinder ? DutyClearWays.DutyFinder : DutyClearWays.PartyOnly;
        return ways;
    }

    /// <summary>Whether a player can clear it without other players: solo, Duty Support or Trust.</summary>
    public static bool WithoutOthers(DutyClearWays ways) =>
        (ways & (DutyClearWays.Solo | DutyClearWays.DutySupport | DutyClearWays.Trust)) != 0;

    /// <inheritdoc cref="WithoutOthers(DutyClearWays)"/>
    public static bool WithoutOthers(DutyRunInfo duty) => WithoutOthers(Ways(duty));
}
