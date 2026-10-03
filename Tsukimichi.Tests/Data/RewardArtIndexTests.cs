using Lumina.Data;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Moonlit's rewards each wear their own game art (feature plan v6 G6): read from the sheets for the shipped and
/// curated rewards, every collectible has an icon of its own, the walls of one shared icon are gone, and every texture
/// named exists in the game.
/// </summary>
public class RewardArtIndexTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private static readonly RewardKind[] OwnArtKinds =
    [
        RewardKind.Mount, RewardKind.Minion, RewardKind.Emote, RewardKind.Orchestrion, RewardKind.TripleTriadCard,
        RewardKind.Barding, RewardKind.Ornament, RewardKind.Hairstyle,
    ];

    [Fact]
    public void A_titles_achievement_is_read_from_its_source()
    {
        Assert.Equal(1030u, RewardArtIndex.AchievementOf("Achievement.Key;achievement=1030;type=6"));
        Assert.Equal(0u, RewardArtIndex.AchievementOf("curated/system_unlocks.json"));
        Assert.Equal(0u, RewardArtIndex.AchievementOf(null));
        var title = new UniqueRewardEntry(65580, RewardKind.Title, 178, 0, "The Final Witness", Confidence.Static, "Achievement.Key;achievement=1030;type=6");
        Assert.Equal((RewardKind.Achievement, 1030u), RewardArtIndex.KeyOf(title));
        var item = new UniqueRewardEntry(65549, RewardKind.OptionalItem, 2558, 2558, "Rauni", Confidence.Static, "Quest.Reward");
        Assert.Equal((RewardKind.Item, 2558u), RewardArtIndex.KeyOf(item));
    }

    [Fact]
    public void System_unlocks_point_at_the_menu_they_belong_to()
    {
        Assert.Equal(MoonlitKindIcons.MainCommandGoldSaucer, MoonlitKindIcons.SystemUnlockCommand("Triple Triad"));
        Assert.Equal(MoonlitKindIcons.MainCommandSightseeingLog, MoonlitKindIcons.SystemUnlockCommand("Sightseeing Log (Dawntrail) · entries 29-45"));
        Assert.Equal(MoonlitKindIcons.MainCommandActionsAndTraits, MoonlitKindIcons.SystemUnlockCommand("Egi glamours (Summoner)"));
        Assert.Equal(MoonlitKindIcons.MainCommandArmouryChest, MoonlitKindIcons.SystemUnlockCommand("Glamour (cast/dispel)"));
        Assert.Equal(MoonlitKindIcons.MainCommandHuntingLog, MoonlitKindIcons.SystemUnlockCommand("Hunts (Endwalker elite)"));
        Assert.Equal(MoonlitKindIcons.MainCommandDutyFinder, MoonlitKindIcons.SystemUnlockCommand("Palace of the Dead"));
        Assert.Equal(0u, MoonlitKindIcons.SystemUnlockCommand("Retainers"));
        Assert.Equal(0u, MoonlitKindIcons.SystemUnlockCommand(null));
    }

    [GameDataFact]
    public void Every_collectible_wears_its_own_art_and_the_textures_exist()
    {
        var entries = Entries();
        var index = Build(entries);
        output.WriteLine($"{index.Count} icons, {index.ArtCount} guide pictures for {entries.Count} rewards");

        foreach (var kind in OwnArtKinds)
        {
            var ofKind = entries.Where(e => e.Kind == kind).ToList();
            var icons = ofKind.Select(e => index.Icon(kind, e.RewardId)).ToList();
            output.WriteLine($"{kind}: {ofKind.Count} rewards, {icons.Count(i => i != 0)} with art, {icons.Where(i => i != 0).Distinct().Count()} distinct");
            Assert.All(ofKind, e => Assert.True(index.Icon(kind, e.RewardId) != 0, $"{kind} {e.RewardId} ({e.RewardName}) has no art"));
            Assert.All(icons, icon => Assert.True(fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), $"{kind} icon {icon} is not in the game"));
        }

        // The 79 orchestrion rolls used to share the roll item's icon: their categories tell them apart now.
        Assert.True(entries.Where(e => e.Kind == RewardKind.Orchestrion).Select(e => index.Icon(RewardKind.Orchestrion, e.RewardId)).Distinct().Count() > 5);

        // Every mount and minion its own icon, and the guide picture beside it.
        foreach (var kind in new[] { RewardKind.Mount, RewardKind.Minion })
        {
            var ofKind = entries.Where(e => e.Kind == kind).Select(e => e.RewardId).Distinct().ToList();
            Assert.Equal(ofKind.Count, ofKind.Select(id => index.Icon(kind, id)).Distinct().Count());
            Assert.True(ofKind.Count(id => index.Art(kind, id) != 0) >= ofKind.Count * 9 / 10, $"most {kind}s have a guide picture");
        }

        // The unicorn: its Mount Guide icon and picture, not the whistle's item icon.
        Assert.Equal(4007u, index.Icon(RewardKind.Mount, 15));
        Assert.Equal(4007u + RewardArtIndex.GuideArtOffset, index.Art(RewardKind.Mount, 15));
    }

    [GameDataFact]
    public void Titles_and_system_unlocks_are_no_longer_blank()
    {
        var entries = Entries();
        var index = Build(entries);

        var titles = entries.Where(e => e.Kind == RewardKind.Title).ToList();
        var titled = titles.Count(e => RewardArtIndex.KeyOf(e) is var (kind, id) && index.Icon(kind, id) != 0);
        output.WriteLine($"Titles: {titled} of {titles.Count} wear their achievement's icon");
        Assert.True(titled >= titles.Count * 9 / 10, $"only {titled} of {titles.Count} titles have an icon");

        var systems = entries.Where(e => e.Kind == RewardKind.SystemUnlock).ToList();
        var mapped = systems.Count(e => index.MenuIcon(MoonlitKindIcons.SystemUnlockCommand(e.RewardName)) != 0);
        output.WriteLine($"System unlocks: {mapped} of {systems.Count} wear a game menu's icon; the rest their kind's glyph");
        Assert.True(mapped >= systems.Count / 2, $"only {mapped} of {systems.Count} system unlocks have a menu icon");

        foreach (var row in MoonlitKindIcons.SystemUnlockCommands().Distinct())
        {
            Assert.True(index.MenuIcon(row) != 0, $"MainCommand {row} has no icon");
        }
    }

    private RewardArtIndex Build(IReadOnlyList<UniqueRewardEntry> entries) =>
        RewardArtIndex.Build(fixture.Game.Excel, Language.English, entries, icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), message => output.WriteLine(message));

    /// <summary>The shipped rewards with the curated ones merged in, as the plugin reads their art.</summary>
    internal static IReadOnlyList<UniqueRewardEntry> Entries()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        return UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated).All;
    }
}
