using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// Moonfall's view of Tsukimichi's spoiler shield for places (the owner's decision: the Far Shore follows the shield).
/// A place (<see cref="MoonfallPlace"/>) is hidden while the shield masks its area (<see cref="SpoilerMask.IsNameMasked"/>
/// with <see cref="SpoilerKind.Area"/>): the same rule, settings and session reveals as every other place name in
/// Tsukimichi, so revealing the area ("Reveal this name") opens what it hides. A place the shield does not place shows,
/// as the shield never guesses. Reads the shield live, so a reveal or a story step shows at once; <see cref="Version"/>
/// changes whenever what it hides may have.
/// </summary>
public sealed class MoonfallShield
{
    private readonly Func<string, bool> masked;
    private readonly Func<string, string> placeholder;
    private readonly Func<int> version;

    /// <param name="masked">Whether the shield hides the area of this English name.</param>
    /// <param name="placeholder">What the shield prints in its place ("Endwalker area 3").</param>
    /// <param name="version">Changes whenever what the shield hides may have (the mask's fingerprint); null for a fixed shield.</param>
    public MoonfallShield(Func<string, bool> masked, Func<string, string> placeholder, Func<int>? version = null)
    {
        this.masked = masked ?? throw new ArgumentNullException(nameof(masked));
        this.placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
        this.version = version ?? (static () => 0);
    }

    /// <summary>Hides nothing: browse mode, the shield off, the offline renderer by default.</summary>
    public static MoonfallShield Open { get; } = new(static _ => false, static zone => zone);

    /// <summary>The shield of one built mask (it does not follow later reveals: the plugin reads its session's mask live instead).</summary>
    public static MoonfallShield FromMask(SpoilerMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        return new MoonfallShield(zone => mask.IsNameMasked(SpoilerKind.Area, zone), zone => mask.Name(SpoilerKind.Area, zone), () => mask.Fingerprint);
    }

    /// <summary>Changes whenever what the shield hides may have changed (a reveal, a story step, a setting).</summary>
    public int Version => version();

    /// <summary>Whether <paramref name="place"/> is past the player's story: its area is hidden by the shield.</summary>
    public bool Hides(MoonfallPlace place) => place.Zone is { } zone && masked(zone);

    /// <summary>The shield's placeholder for <paramref name="place"/>'s area ("Endwalker area 3"); empty for a place of no area.</summary>
    public string Placeholder(MoonfallPlace place) => place.Zone is { } zone ? placeholder(zone) : string.Empty;
}
