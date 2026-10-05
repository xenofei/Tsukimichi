using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The eleven companions who carry the powers (plan v9 decision 22; <c>docs/design/v9/rich2/characters.md</c>), by
/// stable ids, each the same number as its power (<see cref="MoonfallPower"/>). Never renumber: progress holds them.
/// </summary>
public enum MoonfallCompanion : byte
{
    /// <summary>No companion: a level where the player picks, or none chosen.</summary>
    None = 0,

    Minfilia = 1,

    /// <summary>Alphinaud and Alisaie, one card.</summary>
    Twins = 2,

    Cid = 3,
    Raubahn = 4,
    Merlwyb = 5,
    Urianger = 6,
    KanESenna = 7,
    Tataru = 8,
    Yshtola = 9,
    Louisoix = 10,

    /// <summary>The moogle courier, The Far Shore's own.</summary>
    Moogle = 11,
}

/// <summary>How a companion shows on the characters grid, the map and Quick Play's picker (<c>characters.md</c>, "The spoiler shield").</summary>
public enum MoonfallCompanionState : byte
{
    /// <summary>The story has not introduced them: the Triple Triad card back, "Not yet met", the power still named; no name, art or role.</summary>
    NotMet,

    /// <summary>Met in the story, not reached in Moonfall: the card dimmed, "stage N" (<see cref="MoonfallCompanionInfo.Stage"/>).</summary>
    MetNotReached,

    /// <summary>Met and reached: face up, and offered in Quick Play.</summary>
    Available,
}

/// <summary>One companion's fixed facts.</summary>
/// <param name="Companion">Who.</param>
/// <param name="Key">A stable lower-case key ("yshtola"), for files (challenges, duel records).</param>
/// <param name="Name">The name as the card would print it, shown only once met.</param>
/// <param name="Power">The power they carry.</param>
/// <param name="CardIcon">Their Triple Triad card (<c>ui/icon/087000/&lt;icon&gt;_hr1.tex</c>).</param>
/// <param name="Campaign">The campaign whose stage they carry first.</param>
/// <param name="Stage">That stage (from 1): "meet at stage N".</param>
/// <param name="StoryNames">
/// The story's names for them (the game's English <c>ENpcResident</c> names): all must be met for the card to turn face
/// up. The twins need both, so their card stays face down until Alisaie is met (decision 23); the moogle needs none
/// (met in the first hours of any start).
/// </param>
public sealed record MoonfallCompanionInfo(
    MoonfallCompanion Companion,
    string Key,
    string Name,
    MoonfallPower Power,
    uint CardIcon,
    MoonfallCampaignKind Campaign,
    int Stage,
    IReadOnlyList<string> StoryNames)
{
    /// <summary>The level (from 0, in <see cref="Campaign"/>) that opens their stage.</summary>
    public int FirstLevelIndex => (Stage - 1) * MoonfallCharacters.LevelsPerStage;
}

/// <summary>
/// Who the player has met in the story (the spoiler shield's NPC rule): a name the story has not introduced, by the
/// player's own shield setting, is not met. A name the data does not place counts as met, because the shield never
/// guesses (<see cref="SpoilerNames"/>).
/// </summary>
public sealed class MoonfallStory
{
    private readonly Func<string, bool> hasMet;

    /// <param name="hasMet">Whether the story has introduced the person of this English name.</param>
    public MoonfallStory(Func<string, bool> hasMet)
    {
        this.hasMet = hasMet ?? throw new ArgumentNullException(nameof(hasMet));
    }

    /// <summary>Everyone met: no shield (browse mode, the shield off).</summary>
    public static MoonfallStory Everyone { get; } = new(static _ => true);

    /// <summary>The plugin's shield for the viewed character: met once <see cref="SpoilerMask.IsNameMasked"/> no longer masks the person.</summary>
    public static MoonfallStory FromShield(SpoilerMask shield)
    {
        ArgumentNullException.ThrowIfNull(shield);
        return new MoonfallStory(name => !shield.IsNameMasked(SpoilerKind.Npc, name));
    }

    /// <summary>Whether every one of the companion's story names is met.</summary>
    public bool HasMet(MoonfallCompanion companion)
    {
        if (!MoonfallCompanions.TryGet(companion, out var info))
        {
            return false;
        }

        foreach (var name in info.StoryNames)
        {
            if (!hasMet(name))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>The cast (plan v9 G5, G7, decision 22), and which of them the player may see and pick.</summary>
public static class MoonfallCompanions
{
    /// <summary>How many companions there are (ids 1 to 11).</summary>
    public const int Count = 11;

    private static readonly MoonfallCompanionInfo[] Table =
    [
        new(MoonfallCompanion.Minfilia, "minfilia", "Minfilia", MoonfallPower.SuperGuide, 87056, MoonfallCampaignKind.Base, 1, ["Minfilia"]),
        new(MoonfallCompanion.Twins, "twins", "Alphinaud & Alisaie", MoonfallPower.Multiball, 87059, MoonfallCampaignKind.Base, 2, ["Alphinaud", "Alisaie"]),
        new(MoonfallCompanion.Cid, "cid", "Cid Garlond", MoonfallPower.Wings, 87058, MoonfallCampaignKind.Base, 3, ["Cid"]),
        new(MoonfallCompanion.Raubahn, "raubahn", "Raubahn", MoonfallPower.Burst, 87067, MoonfallCampaignKind.Base, 4, ["Raubahn"]),
        new(MoonfallCompanion.Merlwyb, "merlwyb", "Merlwyb", MoonfallPower.Flippers, 87065, MoonfallCampaignKind.Base, 5, ["Merlwyb"]),
        new(MoonfallCompanion.Urianger, "urianger", "Urianger", MoonfallPower.Gate, 87050, MoonfallCampaignKind.Base, 6, ["Urianger"]),
        new(MoonfallCompanion.KanESenna, "kan-e-senna", "Kan-E-Senna", MoonfallPower.Bloom, 87066, MoonfallCampaignKind.Base, 7, ["Kan-E-Senna"]),
        new(MoonfallCompanion.Tataru, "tataru", "Tataru", MoonfallPower.Draw, 87019, MoonfallCampaignKind.Base, 8, ["Tataru"]),
        new(MoonfallCompanion.Yshtola, "yshtola", "Y'shtola", MoonfallPower.Fireball, 87049, MoonfallCampaignKind.Base, 9, ["Y'shtola"]),
        new(MoonfallCompanion.Louisoix, "louisoix", "Louisoix", MoonfallPower.Path, 87060, MoonfallCampaignKind.Base, 10, ["Louisoix"]),
        new(MoonfallCompanion.Moogle, "moogle", "Moogle courier", MoonfallPower.Bolt, 87020, MoonfallCampaignKind.Expansion, 11, []),
    ];

    /// <summary>All eleven, in stage order (the moogle last).</summary>
    public static IReadOnlyList<MoonfallCompanionInfo> All => Table;

    /// <summary>The companion's facts; false for <see cref="MoonfallCompanion.None"/> or an unknown id.</summary>
    public static bool TryGet(MoonfallCompanion companion, out MoonfallCompanionInfo info)
    {
        var index = (int)companion - 1;
        if ((uint)index < (uint)Table.Length)
        {
            info = Table[index];
            return true;
        }

        info = null!;
        return false;
    }

    /// <summary>The companion's facts; throws for <see cref="MoonfallCompanion.None"/> or an unknown id.</summary>
    public static MoonfallCompanionInfo Get(MoonfallCompanion companion) =>
        TryGet(companion, out var info) ? info : throw new ArgumentOutOfRangeException(nameof(companion), companion, "not a companion");

    /// <summary>The companion who carries <paramref name="power"/> (the same id), or <see cref="MoonfallCompanion.None"/>.</summary>
    public static MoonfallCompanion Carrying(MoonfallPower power) =>
        power is > MoonfallPower.None and <= MoonfallPower.Bolt ? (MoonfallCompanion)(byte)power : MoonfallCompanion.None;

    /// <summary>The companion by <see cref="MoonfallCompanionInfo.Key"/>; <see cref="MoonfallCompanion.None"/> for none.</summary>
    public static MoonfallCompanion ByKey(string? key)
    {
        foreach (var info in Table)
        {
            if (string.Equals(info.Key, key, StringComparison.Ordinal))
            {
                return info.Companion;
            }
        }

        return MoonfallCompanion.None;
    }

    /// <summary>
    /// Whether Moonfall has reached the companion's stage: its first level is open in Adventure (every level before it
    /// won). The moogle's stage is in The Far Shore, so it is reached only past the base campaign.
    /// </summary>
    public static bool Reached(MoonfallCompanion companion, MoonfallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return TryGet(companion, out var info) && progress.Cleared(info.Campaign) >= info.FirstLevelIndex;
    }

    /// <summary>
    /// How the companion shows: the story first (not met is always the card back, whatever Moonfall has reached), then
    /// Moonfall's progress (met but not reached is dimmed with its stage).
    /// </summary>
    public static MoonfallCompanionState State(MoonfallCompanion companion, MoonfallProgress progress, MoonfallStory story)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(story);
        if (!story.HasMet(companion))
        {
            return MoonfallCompanionState.NotMet;
        }

        return Reached(companion, progress) ? MoonfallCompanionState.Available : MoonfallCompanionState.MetNotReached;
    }

    /// <summary>
    /// The companions Quick Play offers: only <see cref="MoonfallCompanionState.Available"/> ones, in stage order. The
    /// research gives Quick Play no unlocks of its own ("Quick Play replays unlocked levels" [R §6 l.126]), so it adds
    /// none.
    /// </summary>
    public static IReadOnlyList<MoonfallCompanion> QuickPlay(MoonfallProgress progress, MoonfallStory story)
    {
        var list = new List<MoonfallCompanion>(Count);
        foreach (var info in Table)
        {
            if (State(info.Companion, progress, story) == MoonfallCompanionState.Available)
            {
                list.Add(info.Companion);
            }
        }

        return list;
    }
}
