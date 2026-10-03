using System.Collections.Frozen;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Unlocks;

/// <summary>Which step of <see cref="DutyArt"/>'s chain gave a duty its icon.</summary>
public enum DutyArtStep : byte
{
    /// <summary>The duty's own emblem (<c>ContentFinderCondition.Icon</c>): seasonal events, the Great Hunt, Blunderville.</summary>
    Duty,

    /// <summary>Its Duty Finder category tile (<c>ContentType.Icon</c>): Dungeons, Trials, Raids, PvP.</summary>
    ContentType,

    /// <summary>The category's small Duty Finder mark (<c>ContentType.IconDutyFinder</c>).</summary>
    ContentTypeDutyFinder,

    /// <summary>Its journal genre's icon (<c>ContentFinderCondition.JournalGenre</c> → <c>JournalGenre.Icon</c>).</summary>
    Genre,

    /// <summary>A PvP instance with no category of its own (A Pup No Longer's): PvP's tile.</summary>
    Pvp,

    /// <summary>Nothing of its own: the game's Duty Finder menu icon.</summary>
    DutyFinder,

    /// <summary>Not even the Duty Finder menu icon could be read: the stand-in.</summary>
    None,
}

/// <summary>What the sheets say about one duty's art, every field 0 or empty when the sheet has nothing.</summary>
/// <param name="DutyIcon"><c>ContentFinderCondition.Icon</c>: an emblem on a 136 × 168 card, drawn fitted.</param>
/// <param name="ContentTypeIcon"><c>ContentType.Icon</c> of the duty's category.</param>
/// <param name="ContentTypeDutyFinderIcon"><c>ContentType.IconDutyFinder</c> of the duty's category.</param>
/// <param name="GenreIcon"><c>JournalGenre.Icon</c> of the duty's journal genre.</param>
/// <param name="IsPvpInstance">The duty links an InstanceContent row of the PvP instance type.</param>
public readonly record struct DutyArtSources(uint DutyIcon, uint ContentTypeIcon, uint ContentTypeDutyFinderIcon, uint GenreIcon, bool IsPvpInstance);

/// <summary>
/// The icon and name every surface draws for a duty or instance (owner point 9, UI-5b), so no duty shows a placeholder
/// when the game has something better. The icon is the first the game has of:
/// <list type="number">
/// <item>the duty's own emblem (<c>ContentFinderCondition.Icon</c>; ten duties have one in the 2026.10 client);</item>
/// <item>its Duty Finder category tile (<c>ContentType.Icon</c>);</item>
/// <item>the category's Duty Finder mark (<c>ContentType.IconDutyFinder</c>);</item>
/// <item>its journal genre's icon (<c>JournalGenre.Icon</c>);</item>
/// <item>PvP's tile for a PvP instance with no category (A Pup No Longer's solo instance);</item>
/// <item>the Duty Finder menu icon (<c>MainCommand</c> row <see cref="MoonlitKindIcons.MainCommandDutyFinder"/>).</item>
/// </list>
/// The name is the duty's own, else its territory's place name, else (a PvP instance) PvP's name, as 1.14.0 named A Pup
/// No Longer's. Core cannot read the sheets: <c>Tsukimichi.GameData.DutyArtReader</c> and the Duty Finder hint hand the
/// fields in.
/// </summary>
public static class DutyArt
{
    /// <summary><c>ContentType</c> row of PvP, whose tile and name a PvP instance with neither takes.</summary>
    public const uint PvpContentType = FeatureArt.ContentPvp;

    /// <summary><c>InstanceContentType</c> of a PvP instance (Crystalline Conflict, A Pup No Longer's solo instance).</summary>
    public const uint PvpInstanceContentType = 5;

    /// <summary>The <c>MainCommand</c> row whose icon is the last step: the Duty Finder.</summary>
    public const uint DutyFinderMenu = MoonlitKindIcons.MainCommandDutyFinder;

    /// <summary>The first icon of the chain the game has, and the step that gave it.</summary>
    /// <param name="sources">The duty's fields.</param>
    /// <param name="pvpIcon">PvP's tile (<c>ContentType.Icon</c> of <see cref="PvpContentType"/>); 0 when unread.</param>
    /// <param name="dutyFinderIcon">The Duty Finder menu icon; 0 when unread.</param>
    public static (uint Icon, DutyArtStep Step) Resolve(in DutyArtSources sources, uint pvpIcon, uint dutyFinderIcon) =>
        sources.DutyIcon != 0 ? (sources.DutyIcon, DutyArtStep.Duty)
        : sources.ContentTypeIcon != 0 ? (sources.ContentTypeIcon, DutyArtStep.ContentType)
        : sources.ContentTypeDutyFinderIcon != 0 ? (sources.ContentTypeDutyFinderIcon, DutyArtStep.ContentTypeDutyFinder)
        : sources.GenreIcon != 0 ? (sources.GenreIcon, DutyArtStep.Genre)
        : sources.IsPvpInstance && pvpIcon != 0 ? (pvpIcon, DutyArtStep.Pvp)
        : dutyFinderIcon != 0 ? (dutyFinderIcon, DutyArtStep.DutyFinder)
        : (0u, DutyArtStep.None);

    /// <summary>The icon alone (<see cref="Resolve"/>).</summary>
    public static uint Icon(in DutyArtSources sources, uint pvpIcon, uint dutyFinderIcon) => Resolve(sources, pvpIcon, dutyFinderIcon).Icon;

    /// <summary>
    /// A duty's name: its own, else its territory's place name ("The Palaistra"), else PvP's name for a PvP instance;
    /// empty when none. Surrounding white space is trimmed.
    /// </summary>
    public static string Name(string? dutyName, string? placeName, bool isPvpInstance, string? pvpName)
    {
        var own = dutyName?.Trim() ?? string.Empty;
        if (own.Length > 0)
        {
            return own;
        }

        var place = placeName?.Trim() ?? string.Empty;
        if (place.Length > 0)
        {
            return place;
        }

        return isPvpInstance ? pvpName?.Trim() ?? string.Empty : string.Empty;
    }
}

/// <summary>
/// Every duty's icon through <see cref="DutyArt"/>'s chain, by ContentFinderCondition id and by InstanceContent id, read
/// from the client's sheets (<c>Tsukimichi.GameData.DutyArtReader</c>); tests build it by hand. A duty the sheets do not
/// know wears <see cref="Fallback"/>, the Duty Finder menu icon. Immutable.
/// </summary>
public sealed class DutyIcons
{
    public static readonly DutyIcons Empty = new(new Dictionary<uint, uint>(), new Dictionary<uint, uint>(), 0);

    private readonly FrozenDictionary<uint, uint> byCondition;
    private readonly FrozenDictionary<uint, uint> byInstance;

    /// <param name="byCondition">ContentFinderCondition id to its icon.</param>
    /// <param name="byInstance">InstanceContent id to its icon (the first Duty Finder entry linking it).</param>
    /// <param name="fallback">The Duty Finder menu icon; 0 leaves an unknown duty on the stand-in.</param>
    public DutyIcons(IReadOnlyDictionary<uint, uint> byCondition, IReadOnlyDictionary<uint, uint> byInstance, uint fallback)
    {
        ArgumentNullException.ThrowIfNull(byCondition);
        ArgumentNullException.ThrowIfNull(byInstance);
        this.byCondition = byCondition.Where(static kv => kv.Key != 0 && kv.Value != 0).ToFrozenDictionary();
        this.byInstance = byInstance.Where(static kv => kv.Key != 0 && kv.Value != 0).ToFrozenDictionary();
        Fallback = fallback;
    }

    /// <summary>How many Duty Finder entries have an icon of their own.</summary>
    public int Count => byCondition.Count;

    /// <summary>The Duty Finder menu icon, which a duty the sheets do not know wears; 0 when it could not be read.</summary>
    public uint Fallback { get; }

    /// <summary>The icon of a Duty Finder entry (ContentFinderCondition id); <see cref="Fallback"/> when unknown.</summary>
    public uint For(uint contentFinderConditionId) => byCondition.GetValueOrDefault(contentFinderConditionId, Fallback);

    /// <summary>The icon of an instance (InstanceContent id); <see cref="Fallback"/> when unknown.</summary>
    public uint ForInstance(uint instanceContentId) => byInstance.GetValueOrDefault(instanceContentId, Fallback);
}
