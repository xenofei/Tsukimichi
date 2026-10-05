using System.Globalization;

namespace Tsukimichi.Core.Moonfall;

/// <summary>One stage of Adventure: five levels, carried by one companion or picked by the player.</summary>
/// <param name="Campaign">Its campaign.</param>
/// <param name="Number">Its number in the campaign, from 1.</param>
/// <param name="Name">Its name, themed to the companion's home (plan v9 decision 25).</param>
/// <param name="Companion">Who carries it; <see cref="MoonfallCompanion.None"/> on the stage where the player picks.</param>
/// <param name="LevelIds">Its five levels' ids, in play order.</param>
public sealed record MoonfallStage(MoonfallCampaignKind Campaign, int Number, string Name, MoonfallCompanion Companion, IReadOnlyList<string> LevelIds)
{
    /// <summary>Whether the player picks the companion on this stage's levels (the last stage of each campaign).</summary>
    public bool PlayerPicks => Companion == MoonfallCompanion.None;

    /// <summary>The first level's index in the campaign (from 0).</summary>
    public int FirstLevelIndex => (Number - 1) * MoonfallCharacters.LevelsPerStage;
}

/// <summary>Where a level id sits in Adventure.</summary>
/// <param name="Campaign">Its campaign.</param>
/// <param name="Index">Its index in the campaign, from 0.</param>
public readonly record struct MoonfallLevelPlace(MoonfallCampaignKind Campaign, int Index)
{
    /// <summary>Its stage, from 1.</summary>
    public int Stage => MoonfallCharacters.Stage(Index);

    /// <summary>Its number in the campaign, from 1 (the number <see cref="MoonfallGame"/> takes: greens from 3).</summary>
    public int Number => Index + 1;
}

/// <summary>
/// Adventure's structure (plan v9 G6, G7): The Moon Road (base) is 11 stages of 5 levels, ten carried by the companions
/// in <c>characters.md</c>'s order and an eleventh where the player picks [R §6 l.126: "55 levels, 5 per Master, 10
/// Masters, plus 5 Master levels where the player chooses any Master"]. The Far Shore (expansion) opens once every base
/// level is won, as the original's second game followed its first, and is 12 stages of 5: the same ten, then the moogle
/// courier's Storm Post (the second game's new power), then a last stage where the player picks [R §6 l.130: "60 levels
/// … final Master levels where any Master can be used"]; it ends on the moon (decision 20).
/// <para>
/// Levels are named by id, so their content is authored apart: <c>base-01</c> to <c>base-55</c> and
/// <c>expansion-01</c> to <c>expansion-60</c>, the file names <see cref="MoonfallCampaigns.LoadBuiltIn"/> reads. A level
/// not authored yet is simply absent: Adventure stops before it.
/// </para>
/// </summary>
public static class MoonfallStages
{
    /// <summary>Levels in The Moon Road.</summary>
    public const int BaseLevels = 55;

    /// <summary>Levels in The Far Shore.</summary>
    public const int ExpansionLevels = 60;

    /// <summary>The Moon Road's stage names (decision 25; <c>spec-rich2.md</c> §3), stage 1 first.</summary>
    private static readonly string[] BaseNames =
    [
        "The Waking Sands",
        "Vesper Bay",
        "The Night Skyway",
        "The Sunlit Steps",
        "Harbour Lights",
        "The Silent Stars",
        "The Shroud by Night",
        "The Market Lanterns",
        "Mor Dhona's Glass",
        "Silvertear by Night",
        "Your Pick",
    ];

    /// <summary>
    /// [J] The Far Shore's stage names: the spec names only The Moon Road's, so these follow the level method's road for
    /// the expansion ("the sea voyage and what lies past it: harbours, islands, the sky over open water, and at its end
    /// the moon itself") and its three approved pilots (The Domes of Sharlayan, The Ferry in the Stars, The Sea of
    /// Sorrows), each themed to its companion. Proposals for the owner.
    /// </summary>
    private static readonly string[] ExpansionNames =
    [
        "The Lantern Quay",
        "The Twin Lights",
        "The Skyward Deck",
        "The Sunlit Isles",
        "The Admiral's Sea",
        "The Ferry in the Stars",
        "The Floating Grove",
        "The Night Market Boats",
        "The Domes of Sharlayan",
        "The Archon's Crossing",
        "The Courier's Wake",
        "The Sea of Sorrows",
    ];

    private static readonly MoonfallStage[] BaseStages = Build(MoonfallCampaignKind.Base, BaseNames, MoonfallCharacters.BaseOrder);

    private static readonly MoonfallStage[] ExpansionStages = Build(MoonfallCampaignKind.Expansion, ExpansionNames, MoonfallCharacters.ExpansionOrder);

    /// <summary>The campaign's stages, in order.</summary>
    public static IReadOnlyList<MoonfallStage> Of(MoonfallCampaignKind campaign) =>
        campaign == MoonfallCampaignKind.Expansion ? ExpansionStages : BaseStages;

    /// <summary>How many levels the campaign has by design (some may not be authored yet).</summary>
    public static int LevelCount(MoonfallCampaignKind campaign) =>
        campaign == MoonfallCampaignKind.Expansion ? ExpansionLevels : BaseLevels;

    /// <summary>The stage holding level <paramref name="index"/> (from 0) of <paramref name="campaign"/>; null past its end.</summary>
    public static MoonfallStage? StageOf(MoonfallCampaignKind campaign, int index)
    {
        var stages = Of(campaign);
        var stage = index / MoonfallCharacters.LevelsPerStage;
        return index >= 0 && stage < stages.Count ? stages[stage] : null;
    }

    /// <summary>The id of level <paramref name="index"/> (from 0) of <paramref name="campaign"/>: <c>base-07</c>, <c>expansion-12</c>.</summary>
    public static string LevelId(MoonfallCampaignKind campaign, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, LevelCount(campaign));
        return Prefix(campaign) + (index + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Where <paramref name="id"/> sits in Adventure; false for an id that is not one of its levels.</summary>
    public static bool TryPlace(string? id, out MoonfallLevelPlace place)
    {
        place = default;
        if (id is null)
        {
            return false;
        }

        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            var prefix = Prefix(campaign);
            if (id.Length == prefix.Length + 2
                && id.StartsWith(prefix, StringComparison.Ordinal)
                && char.IsAsciiDigit(id[^2]) && char.IsAsciiDigit(id[^1]))
            {
                var number = ((id[^2] - '0') * 10) + (id[^1] - '0');
                if (number >= 1 && number <= LevelCount(campaign))
                {
                    place = new MoonfallLevelPlace(campaign, number - 1);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The companion Adventure gives level <paramref name="index"/>: its stage's, or <see cref="MoonfallCompanion.None"/>
    /// where the player picks (or past the campaign's end).
    /// </summary>
    public static MoonfallCompanion AdventureCompanion(MoonfallCampaignKind campaign, int index) =>
        StageOf(campaign, index)?.Companion ?? MoonfallCompanion.None;

    private static string Prefix(MoonfallCampaignKind campaign) => campaign == MoonfallCampaignKind.Expansion ? "expansion-" : "base-";

    private static MoonfallStage[] Build(MoonfallCampaignKind campaign, string[] names, ReadOnlySpan<byte> order)
    {
        var stages = new MoonfallStage[names.Length];
        for (var s = 0; s < stages.Length; s++)
        {
            var ids = new string[MoonfallCharacters.LevelsPerStage];
            for (var k = 0; k < ids.Length; k++)
            {
                ids[k] = Prefix(campaign) + ((s * MoonfallCharacters.LevelsPerStage) + k + 1).ToString("00", CultureInfo.InvariantCulture);
            }

            var companion = s < order.Length ? MoonfallCompanions.Carrying((MoonfallPower)order[s]) : MoonfallCompanion.None;
            stages[s] = new MoonfallStage(campaign, s + 1, names[s], companion, ids);
        }

        return stages;
    }
}
