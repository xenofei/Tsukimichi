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

/// <summary>A badge of "How you'll clear it" (feature plan v7 C7; spec-1.19 C7, "Badges").</summary>
public enum DutyBadgeKind : byte
{
    /// <summary>"Solo with NPCs": Duty Support or Trust lists it.</summary>
    SoloWithNpcs,

    /// <summary>"Solo": a duty that seats one player (a quest battle).</summary>
    Solo,

    /// <summary>"Group of 4 / 8 / 24": players only, <see cref="DutyBadge.Players"/> of them.</summary>
    Group,

    /// <summary>"High-end": Savage, Extreme, Unreal, Ultimate and Chaotic.</summary>
    HighEnd,

    /// <summary>"Story-required": the main scenario needs it.</summary>
    StoryRequired,

    /// <summary>"Optional": the main scenario does not need it.</summary>
    Optional,
}

/// <summary>One badge, with the party size for <see cref="DutyBadgeKind.Group"/>.</summary>
public readonly record struct DutyBadge(DutyBadgeKind Kind, int Players = 0);

/// <summary>
/// The badges a duty wears wherever duties appear (spec-1.19 C7): first its size (Solo with NPCs, Solo, or Group of
/// N), then High-end when it is, then Story-required or Optional when that is known. Pure.
/// </summary>
public static class DutyBadgeRules
{
    /// <summary>The badges in order; <paramref name="storyRequired"/> null leaves the story badge out.</summary>
    public static IReadOnlyList<DutyBadge> For(DutyRunInfo duty, bool? storyRequired)
    {
        ArgumentNullException.ThrowIfNull(duty);
        var badges = new List<DutyBadge>(3);
        if (Size(duty) is { } size)
        {
            badges.Add(size);
        }

        if (duty.HighEnd)
        {
            badges.Add(new DutyBadge(DutyBadgeKind.HighEnd));
        }

        if (storyRequired is { } story)
        {
            badges.Add(new DutyBadge(story ? DutyBadgeKind.StoryRequired : DutyBadgeKind.Optional));
        }

        return badges;
    }

    /// <summary>The size badge alone (the Duties board's rows wear only this one); null when the party size is unknown.</summary>
    public static DutyBadge? Size(DutyRunInfo duty)
    {
        ArgumentNullException.ThrowIfNull(duty);
        var ways = DutyClear.Ways(duty);
        if ((ways & (DutyClearWays.DutySupport | DutyClearWays.Trust)) != 0)
        {
            return new DutyBadge(DutyBadgeKind.SoloWithNpcs);
        }

        if (ways == DutyClearWays.Solo)
        {
            return new DutyBadge(DutyBadgeKind.Solo, 1);
        }

        return duty.Players > 1 ? new DutyBadge(DutyBadgeKind.Group, duty.Players) : null;
    }
}
