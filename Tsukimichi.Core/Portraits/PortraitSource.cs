namespace Tsukimichi.Core.Portraits;

/// <summary>
/// Where a giver's portrait comes from (feature plan v7 F1): the families of NPC art the game install already holds.
/// The Giver card names the family in its "Portrait: …" source line. The numeric values are stable (they are not
/// stored, but the curated file names the families by these names).
/// </summary>
public enum PortraitSource : byte
{
    /// <summary>No portrait: the giver gets a fallback (<see cref="PortraitFallback"/>).</summary>
    None = 0,

    /// <summary>
    /// A Duty Support or Trust member's tall bust (<c>DawnQuestMember.BigImageOld</c>, 188 × 480 at hr; every size here is
    /// the <c>_hr1</c> texture's): a full-colour official render that crops to a clean face.
    /// </summary>
    TrustBust = 1,

    /// <summary>
    /// A Triple Triad card's art (icon <c>087000</c> + card row, 208 × 256): a full-colour face inside a gold frame.
    /// </summary>
    TripleTriadCard = 2,

    /// <summary>
    /// A painted "battle talk" face (<c>073001</c>–<c>073299</c>, 640 × 512): a sepia bust on a brass slash, named by
    /// a quest battle's <c>FACE_GRAPHIC_*</c> script variable, a Scion note, a mahjong costume or the curated file.
    /// </summary>
    BattleTalk = 3,

    /// <summary>A custom delivery client's portrait (<c>SatisfactionNpc.Icon</c>, 400 × 480).</summary>
    Delivery = 4,

    /// <summary>
    /// A Duty Support or Trust member's wide face strip (<c>DawnQuestMember.BigImageNew</c>, 640 × 180, the face at the
    /// right): the last resort, for members the game gives no tall bust.
    /// </summary>
    TrustStrip = 5,
}

/// <summary>The order the families are tried in, and their names in the curated file.</summary>
public static class PortraitSources
{
    /// <summary>
    /// Best first: Trust bust, Triple Triad card, battle-talk face, delivery portrait, Trust strip. The optional
    /// portrait pack (F4) will go in front of them all.
    /// </summary>
    public static readonly IReadOnlyList<PortraitSource> Priority =
    [
        PortraitSource.TrustBust,
        PortraitSource.TripleTriadCard,
        PortraitSource.BattleTalk,
        PortraitSource.Delivery,
        PortraitSource.TrustStrip,
    ];

    /// <summary>The rank of a family in <see cref="Priority"/> (0 best); <see cref="PortraitSource.None"/> ranks last.</summary>
    public static int Rank(PortraitSource source) => source switch
    {
        PortraitSource.TrustBust => 0,
        PortraitSource.TripleTriadCard => 1,
        PortraitSource.BattleTalk => 2,
        PortraitSource.Delivery => 3,
        PortraitSource.TrustStrip => 4,
        _ => int.MaxValue,
    };

    /// <summary>
    /// A family's texture size at hr (the <c>_hr1</c> texture; the 1x one is half each side), the px the design spec and
    /// the curated boxes are measured in. Crops are texture coordinates, so they fit both. (0, 0) for <see cref="PortraitSource.None"/>.
    /// </summary>
    public static (int Width, int Height) TextureSize(PortraitSource source) => source switch
    {
        PortraitSource.TrustBust => (188, 480),
        PortraitSource.TripleTriadCard => (208, 256),
        PortraitSource.BattleTalk => (640, 512),
        PortraitSource.Delivery => (400, 480),
        PortraitSource.TrustStrip => (640, 180),
        _ => (0, 0),
    };

    /// <summary>
    /// The part of a family's texture that is art a crop may show: inside a Triple Triad card's gold frame; the whole
    /// texture for the other families.
    /// </summary>
    public static PortraitCrop ArtBounds(PortraitSource source) => source switch
    {
        PortraitSource.TripleTriadCard => new PortraitCrop(0.067f, 0.055f, 0.933f, 0.945f),
        _ => PortraitCrop.Full,
    };

    /// <summary>
    /// The family an icon id belongs to by its range: 072621–072680 Trust busts, 072681–072799 Trust strips,
    /// 073001–073999 battle-talk faces, 087001–087999 Triple Triad cards, 061661–061699 delivery portraits;
    /// <see cref="PortraitSource.None"/> outside them.
    /// </summary>
    public static PortraitSource FamilyOfIcon(uint icon) => icon switch
    {
        >= 72621 and <= 72680 => PortraitSource.TrustBust,
        >= 72681 and <= 72799 => PortraitSource.TrustStrip,
        >= 73001 and <= 73999 => PortraitSource.BattleTalk,
        >= 87001 and <= 87999 => PortraitSource.TripleTriadCard,
        >= 61661 and <= 61699 => PortraitSource.Delivery,
        _ => PortraitSource.None,
    };

    /// <summary>Reads a family by its enum name ("BattleTalk"), ignoring case; <see cref="PortraitSource.None"/> is never accepted.</summary>
    public static bool TryParse(string? text, out PortraitSource source)
    {
        source = PortraitSource.None;
        if (string.IsNullOrWhiteSpace(text) || !Enum.TryParse(text.Trim(), ignoreCase: true, out PortraitSource parsed)
            || parsed == PortraitSource.None || !Enum.IsDefined(parsed) || int.TryParse(text, out _))
        {
            return false;
        }

        source = parsed;
        return true;
    }
}
