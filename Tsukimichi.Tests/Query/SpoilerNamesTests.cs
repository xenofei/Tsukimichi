using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Tests.Localization;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The wider spoiler shield (plan v7, 1.20.0 N6; spec-1.20 "A wider spoiler shield") over a small hand-built story:
/// each quest's story anchor, the names placed from the unlock index, the rewards and the givers, the placeholders
/// (kind words with safe locators), the expansion rule, session reveals, the setting and its upgrade, search, find by
/// unlock, the count line and the unlock rows' shielded form.
/// </summary>
public class SpoilerNamesTests
{
    private const string Nbsp = " ";

    // The story: 1 → 2 (A Realm Reborn) → 3 (Stormblood; opens Kugane, gives the Lunar Whale) → 4 (opens the
    // Sirensong Sea) → 5.
    private const uint Start = 1;
    private const uint Path = 2;
    private const uint Opener = 3;
    private const uint Dungeon = 4;
    private const uint Far = 5;

    // Side quests: given in Kugane with no previous quest (it opens the Ruby Sea); after the start; after the end of the story.
    private const uint KuganeSide = 10;
    private const uint EarlySide = 11;
    private const uint LateSide = 12;

    private const uint Kugane = 628;
    private const uint RubySea = 613;
    private const uint Uldah = 130;
    private const uint Tuliyollal = 1185;
    private const uint Sirensong = 238;
    private const uint Markets = 9001;

    private static QuestRecord Msq(uint rowId, string name, byte level, byte expansion, uint previous, string giver, params RewardRef[] rewards) =>
        Quest(rowId, name, section: 0, category: 1, genre: 1, sortKey: (int)rowId, level: level, expansion: expansion, rewards: rewards) with
        {
            PreviousQuests = previous == 0 ? Prereq.None : new Prereq([previous], JoinKind.All),
            Issuer = new Issuer(rowId, giver, Uldah, 0, 0f, 0f, 0f),
        };

    private static QuestRecord Side(uint rowId, string name, uint previous, string giver, uint territory, params RewardRef[] rewards) =>
        Quest(rowId, name, section: 2, category: 10, genre: 100, level: 10, rewards: rewards) with
        {
            PreviousQuests = previous == 0 ? Prereq.None : new Prereq([previous], JoinKind.All),
            Issuer = new Issuer(rowId, giver, territory, 0, 0f, 0f, 0f),
        };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Msq(Start, "Coming to Ul'dah", 1, 0, 0, "Momodi", Reward(RewardKind.Item, "Potion", 4551)),
        Msq(Path, "The Path", 50, 0, Start, "Alphinaud"),
        Msq(Opener, "Not without Incident", 61, 2, Path, "Alphinaud", Reward(RewardKind.Mount, "Lunar Whale", 7), Reward(RewardKind.Item, "Potion", 4551)),
        Msq(Dungeon, "Once More to the Ruby Sea", 62, 2, Opener, "Hancock", Reward(RewardKind.Orchestrion, "Ruby Tide", 8)),
        Msq(Far, "The Far Edge", 70, 2, Dungeon, "Alphinaud"),
        Side(KuganeSide, "Leves of the East", 0, "Hancock", Kugane, Reward(RewardKind.Item, "Kojin Blade", 9001)),
        Side(EarlySide, "A Thirst for Water", Start, "Momodi", Uldah, Reward(RewardKind.Item, "Potion", 4551)),
        Side(LateSide, "After It All", Far, "Somebody Late", Uldah),
    ]);

    private static UnlockLinks Links() => new()
    {
        Zones =
        [
            new UnlockZone(Kugane, "Kugane", "Hingashi", 371, 2, 111, 628),
            new UnlockZone(RubySea, "The Ruby Sea", "Othard", 372, 2, 0, 613),
            new UnlockZone(Uldah, "Ul'dah - Steps of Nald", "Thanalan", 1, 0, 9, 130),
            // A zone of an expansion past the whole story, which no quest opens.
            new UnlockZone(Tuliyollal, "Tuliyollal", "Yok Tural", 900, 5, 0, 1185),
        ],
        Aetherytes = [new UnlockAetheryte(Markets, Kugane, "Kogane Dori Markets", 0f, 0f, IsAetheryte: false)],
        GatedAethernet = [new UnlockGatedAethernet(Opener, Markets)],
        Warps = [new UnlockWarp(Opener, Kugane), new UnlockWarp(KuganeSide, RubySea)],
        Duties = [new UnlockDuty(Sirensong, 61801, 61, 2)],
        AreaIcon = 7,
    };

    private static readonly PlanDuties Duties = PlanDuties.From([new PlanDuty(Sirensong, 62, UnlockKind.Dungeon, "the Sirensong Sea")]);

    private static readonly QuestUnlocks Index = QuestUnlocks.Build(
        Catalog,
        UniqueRewardCatalog.Build(
            new UniqueRewardsData("test", default, [new UniqueRewardEntry(Dungeon, RewardKind.DutyUnlock, Sirensong, 0, "the Sirensong Sea", Confidence.Curated, "curated/duty_unlocks.json")]),
            new Dictionary<uint, UniqueOverride>(),
            CuratedData.Empty),
        Duties,
        Links());

    private static Dictionary<uint, QuestState> StatesAt(uint next)
    {
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = quest.RowId < next || (quest.RowId == EarlySide && next > Start) ? QuestState.Completed
                : quest.RowId == next ? QuestState.Ready
                : QuestState.Blocked;
        }

        return states;
    }

    /// <summary>The character whose next main scenario quest is <paramref name="next"/>, with nothing revealed ahead.</summary>
    private static SpoilerMask At(uint next, SpoilerOptions? options = null, IEnumerable<(SpoilerKind, string)>? revealed = null, IReadOnlySet<uint>? revealedQuests = null) =>
        SpoilerMask.Build(Catalog, StatesAt(next), options ?? SpoilerOptions.Default with { Ahead = 0 }, revealedQuests, names: Index.Names, revealedNames: revealed);

    [Fact]
    public void Each_quest_is_anchored_at_the_latest_story_quest_it_needs()
    {
        var names = Index.Names;

        Assert.Equal(Opener, names.AnchorOf(Opener));
        // Given in Kugane with no previous quest: anchored where the story first opens Kugane.
        Assert.Equal(Opener, names.AnchorOf(KuganeSide));
        Assert.Equal(Start, names.AnchorOf(EarlySide));
        Assert.Equal(Far, names.AnchorOf(LateSide));
    }

    [Fact]
    public void Before_the_opener_its_zone_region_aetheryte_duty_rewards_and_people_are_masked()
    {
        var mask = At(Path);

        Assert.True(mask.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Area, "Hingashi"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Reward, "Kojin Blade"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));

        // What the story has already introduced stays: a potion the first quest gives, its givers and its city.
        Assert.False(mask.IsNameMasked(SpoilerKind.Reward, "Potion"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Alphinaud"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Momodi"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, "Ul'dah - Steps of Nald"));
        Assert.Equal("Potion", mask.Name(SpoilerKind.Reward, "Potion"));
    }

    [Fact]
    public void Each_kind_prints_a_kind_word_with_a_safe_locator()
    {
        var mask = At(Path);

        // An area: the expansion and its place among that expansion's zones in sheet order (the Ruby Sea, 613, is
        // first; Kugane, 628, second); the same number on every surface.
        Assert.Equal("Stormblood area" + Nbsp + "2", mask.Name(SpoilerKind.Area, "Kugane"));
        Assert.Equal("Stormblood area" + Nbsp + "1", mask.Name(SpoilerKind.Area, "The Ruby Sea"));
        Assert.Equal("Stormblood region", mask.Name(SpoilerKind.Area, "Hingashi"));
        // An aetheryte names its area's number.
        Assert.Equal("Stormblood aetheryte · area" + Nbsp + "2", mask.Name(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        // A duty by its content type and level.
        Assert.Equal("Dungeon (Lv" + Nbsp + "61)", mask.Name(SpoilerKind.Duty, "The Sirensong Sea"));
        // Rewards by kind.
        Assert.Equal("A mount", mask.Name(SpoilerKind.Reward, "Lunar Whale"));
        Assert.Equal("An item", mask.Name(SpoilerKind.Reward, "Kojin Blade"));
        Assert.Equal("An orchestrion roll", mask.Name(SpoilerKind.Reward, "Ruby Tide"));
        // People by the expansion that introduces them.
        Assert.Equal("Stormblood character", mask.Name(SpoilerKind.Npc, "Hancock"));
        // A masked main scenario quest keeps its non-breaking locator too.
        Assert.Equal("Main scenario quest (Lv" + Nbsp + "61)", mask.DisplayName(Catalog.GetByRowId(Opener)!));

        // A slot that holds a name and a place shortens the place to its locator first.
        Assert.Equal("area" + Nbsp + "2", mask.Locator(SpoilerKind.Area, "Kugane"));
        Assert.Equal("area" + Nbsp + "2", mask.Locator(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        Assert.Equal("Ul'dah - Steps of Nald", mask.Locator(SpoilerKind.Area, "Ul'dah - Steps of Nald"));
    }

    [Fact]
    public void Expansion_names_come_from_the_game_data()
    {
        var index = QuestUnlocks.Build(Catalog, UniqueRewardCatalog.Empty, Duties, Links(), expansionName: id => id == 2 ? "紅蓮" : string.Empty);
        var mask = SpoilerMask.Build(Catalog, StatesAt(Path), SpoilerOptions.Default with { Ahead = 0 }, names: index.Names);

        Assert.Equal("紅蓮 area" + Nbsp + "2", mask.Name(SpoilerKind.Area, "Kugane"));
        // An expansion the data does not name falls back to the built-in English name.
        Assert.Equal("Dawntrail area" + Nbsp + "1", mask.Name(SpoilerKind.Area, "Tuliyollal"));
    }

    [Fact]
    public void Area_numbers_follow_the_sheet_order_within_each_expansion()
    {
        var numbers = SpoilerNames.AreaNumbers(
        [
            new UnlockZone(1190, "Shaaloani", string.Empty, 0, 5, 0, 1190),
            new UnlockZone(1185, "Tuliyollal", string.Empty, 0, 5, 0, 1185),
            new UnlockZone(1191, "Heritage Found", string.Empty, 0, 5, 0, 1191),
            // A second row of a zone keeps the first one's number.
            new UnlockZone(1300, "Tuliyollal", string.Empty, 0, 5, 0, 1300),
            new UnlockZone(628, "Kugane", string.Empty, 0, 2, 0, 628),
        ]);

        Assert.Equal((byte)5, numbers["Tuliyollal"].Expansion);
        Assert.Equal(1, numbers["Tuliyollal"].Number);
        Assert.Equal(2, numbers["Shaaloani"].Number);
        Assert.Equal(3, numbers["Heritage Found"].Number);
        Assert.Equal(1, numbers["Kugane"].Number);
    }

    [Fact]
    public void A_place_of_an_expansion_past_the_story_hides_though_no_quest_introduces_it()
    {
        // Tuliyollal: Dawntrail, opened by nothing in this catalog.
        Assert.True(At(Path).IsNameMasked(SpoilerKind.Area, "Tuliyollal"));
        Assert.True(At(Dungeon).IsNameMasked(SpoilerKind.Area, "Tuliyollal"));
        Assert.Equal("Dawntrail area" + Nbsp + "1", At(Dungeon).Name(SpoilerKind.Area, "Tuliyollal"));
        Assert.Equal("Dawntrail region", At(Dungeon).Name(SpoilerKind.Area, "Yok Tural"));

        // A Realm Reborn's own city never hides, and nothing does once the story is done.
        Assert.False(At(Path).IsNameMasked(SpoilerKind.Area, "Ul'dah - Steps of Nald"));
        var done = SpoilerMask.Build(Catalog, States(Catalog, QuestState.Completed), SpoilerOptions.Default, names: Index.Names);
        Assert.False(done.IsNameMasked(SpoilerKind.Area, "Tuliyollal"));
    }

    [Fact]
    public void Names_unlock_as_the_story_reaches_them()
    {
        var atOpener = At(Opener);
        Assert.False(atOpener.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(atOpener.IsNameMasked(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        Assert.False(atOpener.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(atOpener.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));

        var atDungeon = At(Dungeon);
        Assert.False(atDungeon.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.False(atDungeon.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.True(atDungeon.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));

        // "Quests ahead to reveal" reveals what those quests introduce too, even an area of a later expansion.
        Assert.False(At(Path, SpoilerOptions.Default with { Ahead = 1 }).IsNameMasked(SpoilerKind.Area, "Kugane"));
    }

    [Fact]
    public void Names_match_whatever_their_case_and_unknown_names_are_shown()
    {
        var mask = At(Path);

        Assert.True(mask.IsNameMasked(SpoilerKind.Duty, "the sirensong sea"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, "Somewhere Unplaced"));
        Assert.Equal("Somewhere Unplaced", mask.Name(SpoilerKind.Area, "Somewhere Unplaced"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, null));
        Assert.Equal(string.Empty, mask.Name(SpoilerKind.Area, null));
        // The kinds are apart: a person named like a zone is not the zone.
        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Kugane"));
        Assert.Same(mask.Name(SpoilerKind.Area, "Kugane"), mask.Name(SpoilerKind.Area, "Kugane"));
    }

    [Fact]
    public void A_placeholder_and_a_string_that_holds_one_are_told_apart_from_names()
    {
        var mask = At(Path);
        var area = mask.Name(SpoilerKind.Area, "Kugane");
        var quest = mask.DisplayName(Catalog.GetByRowId(Opener)!);

        // Either is Secondary as a whole wherever it prints (spec-1.20, "How a placeholder looks").
        Assert.True(SpoilerMask.IsPlaceholder(area));
        Assert.True(SpoilerMask.IsPlaceholder(quest));
        Assert.True(SpoilerMask.HoldsPlaceholder(area));
        var composed = UnlockFindKinds.Label(UnlockTarget.Flying, area);
        Assert.Equal("Flying in Stormblood area" + Nbsp + "2", composed);
        Assert.False(SpoilerMask.IsPlaceholder(composed));
        Assert.True(SpoilerMask.HoldsPlaceholder(composed));
        Assert.True(SpoilerMask.HoldsPlaceholder(composed));

        Assert.False(SpoilerMask.HoldsPlaceholder("Kugane"));
        Assert.False(SpoilerMask.HoldsPlaceholder("Flying in Kugane"));
        Assert.False(SpoilerMask.HoldsPlaceholder(null));
        Assert.False(SpoilerMask.IsPlaceholder(string.Empty));
    }

    [Fact]
    public void A_journal_node_named_after_a_hidden_area_takes_its_placeholder()
    {
        var mask = At(Path);

        Assert.Equal("Stormblood area" + Nbsp + "2", mask.NodeName("Kugane"));
        Assert.Equal("Stormblood area" + Nbsp + "2 Sidequests", mask.NodeName("Kugane Sidequests"));
        Assert.Same("Ul'dah Sidequests", mask.NodeName("Ul'dah Sidequests"));
        Assert.Equal("Kugane Sidequests", At(Opener).NodeName("Kugane Sidequests"));
    }

    [Fact]
    public void Reveal_this_name_shows_one_name_for_the_session()
    {
        var plain = At(Path);
        var revealed = At(Path, revealed: [(SpoilerKind.Area, "kugane")]);

        Assert.False(revealed.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.Equal("Kugane", revealed.Name(SpoilerKind.Area, "Kugane"));
        // The aetheryte follows its area; everything else stays hidden.
        Assert.False(revealed.IsNameMasked(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        // The integrations re-register when it changes.
        Assert.NotEqual(plain.Fingerprint, revealed.Fingerprint);
        Assert.Equal(revealed.Fingerprint, At(Path, revealed: [(SpoilerKind.Area, "Kugane")]).Fingerprint);
    }

    [Fact]
    public void Reveal_names_in_this_quest_shows_its_name_giver_place_duty_rewards_and_unlocks()
    {
        var quest = Catalog.GetByRowId(Dungeon)!;
        var names = SpoilerNames.NamesIn(quest, Index, place: "Kugane", region: "Hingashi", duties: ["The Sirensong Sea"]);

        Assert.Contains((SpoilerKind.Npc, "Hancock"), names);
        Assert.Contains((SpoilerKind.Area, "Kugane"), names);
        Assert.Contains((SpoilerKind.Area, "Hingashi"), names);
        Assert.Contains((SpoilerKind.Duty, "The Sirensong Sea"), names);
        Assert.Contains((SpoilerKind.Reward, "Ruby Tide"), names);

        var before = At(Path);
        var revealed = At(Path, revealed: names, revealedQuests: new HashSet<uint> { Dungeon });
        Assert.True(before.IsMasked(Dungeon));
        Assert.False(revealed.IsMasked(Dungeon));
        foreach (var (kind, name) in names)
        {
            Assert.True(before.IsNameMasked(kind, name), name);
            Assert.False(revealed.IsNameMasked(kind, name), name);
        }

        // Another quest's names stay hidden.
        Assert.True(revealed.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Reward, "Kojin Blade"));
    }

    [Fact]
    public void Revealing_a_quests_name_is_no_story_progress()
    {
        // "Reveal this name" on Not without Incident shows its title only: Kugane, its aetheryte and the Lunar Whale
        // are what reaching it would show, and stay hidden.
        var revealed = At(Path, revealedQuests: new HashSet<uint> { Opener });

        Assert.False(revealed.IsMasked(Opener));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Aetheryte, "Kogane Dori Markets"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(revealed.IsNameMasked(SpoilerKind.Reward, "Kojin Blade"));
        Assert.Equal(At(Path).MaskedNameCount, revealed.MaskedNameCount);
        Assert.NotEqual(At(Path).Fingerprint, revealed.Fingerprint);

        // Reaching it does show them.
        Assert.False(At(Opener).IsNameMasked(SpoilerKind.Area, "Kugane"));
    }

    [Fact]
    public void A_moonlit_reward_is_shielded_where_the_index_places_it()
    {
        var mask = At(Path);
        var duty = new UniqueRewardEntry(Dungeon, RewardKind.DutyUnlock, Sirensong, 0, "the Sirensong Sea", Confidence.Curated, "curated/duty_unlocks.json");
        var current = new UniqueRewardEntry(KuganeSide, RewardKind.AetherCurrent, 1, 0, "Aether Current (The Ruby Sea)", Confidence.Static, "test");
        var mount = new UniqueRewardEntry(Opener, RewardKind.Mount, 7, 0, "Lunar Whale", Confidence.Static, "test");

        // The duty is placed as a duty and the current as flying in its zone: never as a reward.
        Assert.False(mask.IsNameMasked(SpoilerKind.Reward, "the Sirensong Sea"));
        Assert.Equal((SpoilerKind.Duty, "the Sirensong Sea"), mask.RewardName(duty, "the Sirensong Sea"));
        Assert.Equal((SpoilerKind.Area, "The Ruby Sea"), mask.RewardName(current, "Aether Current (The Ruby Sea)"));
        Assert.Equal((SpoilerKind.Reward, "Lunar Whale"), mask.RewardName(mount, "Lunar Whale"));

        Assert.True(mask.IsRewardMasked(duty, "the Sirensong Sea"));
        Assert.True(mask.IsRewardMasked(current, "Aether Current (The Ruby Sea)"));
        Assert.True(mask.IsRewardMasked(mount, "Lunar Whale"));
        Assert.Equal("Dungeon (Lv" + Nbsp + "61)", mask.RewardDisplay(duty, "the Sirensong Sea"));
        // The Ruby Sea is Stormblood's first zone in the sheet's order here (Kugane the second).
        Assert.Equal("Aether Current (Stormblood area" + Nbsp + "1)", mask.RewardDisplay(current, "Aether Current (The Ruby Sea)"));
        Assert.True(SpoilerMask.HoldsPlaceholder(mask.RewardDisplay(current, "Aether Current (The Ruby Sea)")));
        Assert.Equal("A mount", mask.RewardDisplay(mount, "Lunar Whale"));

        // Wotsit registers none of them until the story reaches them.
        var rewards = UniqueRewardCatalog.Build(new UniqueRewardsData("test", default, [duty, current, mount]), new Dictionary<uint, UniqueOverride>(), CuratedData.Empty);
        Assert.DoesNotContain(WotsitOrder.Items(Catalog, rewards, k => k.ToString(), "English", mask), i => i.IsReward);
        var done = At(Far);
        Assert.Equal(3, WotsitOrder.Items(Catalog, rewards, k => k.ToString(), "English", done).Count(i => i.IsReward));
        Assert.Same("Lunar Whale", done.RewardDisplay(mount, "Lunar Whale"));

        // A reward the names do not place (a curated unlock named by its note) hides while its quest lies ahead.
        var note = new UniqueRewardEntry(Dungeon, RewardKind.DutyUnlock, 99, 0, "Once More -> a duty the note names", Confidence.Curated, "curated/duty_unlocks.json");
        Assert.True(mask.IsRewardMasked(note, note.RewardName));
        Assert.Equal("A duty", mask.RewardDisplay(note, note.RewardName));
        Assert.True(SpoilerMask.IsPlaceholder(mask.RewardDisplay(note, note.RewardName)));
        Assert.False(done.IsRewardMasked(note, note.RewardName));
        Assert.False(mask.IsRewardMasked(new UniqueRewardEntry(EarlySide, RewardKind.DutyUnlock, 99, 0, "Unplaced", Confidence.Curated, "test"), "Unplaced"));
    }

    [Fact]
    public void A_duty_hidden_through_its_quests_prints_the_wider_shields_form()
    {
        var mask = At(Path);
        var placed = new DutyRunInfo(Sirensong, 0, 0, DutyRunInfo.Dungeons, "The Sirensong Sea", false, false) { LevelRequired = 61 };
        var unplaced = new DutyRunInfo(4242, 0, 0, DutyRunInfo.Trials, "The Unplaced Trial", false, false) { LevelRequired = 90 };

        Assert.Equal(mask.Name(SpoilerKind.Duty, "The Sirensong Sea"), mask.DutyPlaceholder(placed));
        Assert.Equal("Trial (Lv" + Nbsp + "90)", mask.DutyPlaceholder(unplaced));
        Assert.True(SpoilerMask.IsPlaceholder(mask.DutyPlaceholder(unplaced)));
        Assert.Same(mask.DutyPlaceholder(unplaced), mask.DutyPlaceholder(unplaced));
    }

    [Fact]
    public void A_placeholder_inside_a_string_counts_only_as_a_whole()
    {
        var trait = SpoilerMask.Register("A trait");
        var dungeon = SpoilerMask.Register("Dungeon");
        SpoilerMask.Register("Field operation");
        SpoilerMask.Register("Duty");
        SpoilerMask.Register("Holdtest area" + Nbsp + "1");

        // On word boundaries, whole, and not a kind label.
        Assert.False(SpoilerMask.HoldsPlaceholder("A traitor's tale"));
        Assert.False(SpoilerMask.HoldsPlaceholder("Dungeon: Sastasha"));
        Assert.False(SpoilerMask.HoldsPlaceholder("Field operation: Eureka Anemos"));
        Assert.False(SpoilerMask.HoldsPlaceholder("Open in Duty Finder"));
        Assert.False(SpoilerMask.HoldsPlaceholder("The Dungeon of Dreams"));
        Assert.False(SpoilerMask.HoldsPlaceholder("Flying in Holdtest area" + Nbsp + "10"));

        Assert.True(SpoilerMask.HoldsPlaceholder(trait));
        Assert.True(SpoilerMask.HoldsPlaceholder("Job: A trait"));
        Assert.True(SpoilerMask.HoldsPlaceholder("Moonlit: A trait and 2 more"));
        Assert.True(SpoilerMask.HoldsPlaceholder(dungeon));
        Assert.True(SpoilerMask.HoldsPlaceholder("Dungeon: Dungeon"));
        Assert.True(SpoilerMask.HoldsPlaceholder("Flying in Holdtest area" + Nbsp + "1"));
        Assert.True(SpoilerMask.HoldsPlaceholder("Holdtest area" + Nbsp + "1 Sidequests"));
    }

    [Fact]
    public void Telling_a_placeholder_apart_allocates_nothing_per_frame()
    {
        SpoilerMask.Register("Holdtest area" + Nbsp + "2");
        SpoilerMask.HoldsPlaceholder(new string("Flying in Holdtest area" + Nbsp + "2"));
        var allocated = long.MaxValue;
        // Another test registering a placeholder meanwhile rebuilds the matcher once; a clean pass allocates nothing.
        for (var attempt = 0; attempt < 3 && allocated != 0; attempt++)
        {
            // Fresh instances every pass, as a string composed every frame is: no answer is kept per instance.
            var texts = new string[64];
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i] = i % 2 == 0 ? new string("Flying in Holdtest area" + Nbsp + "2") : "Flying in Kugane, quest " + i;
            }

            var start = GC.GetAllocatedBytesForCurrentThread();
            foreach (var text in texts)
            {
                SpoilerMask.HoldsPlaceholder(text);
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        }

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void The_setting_the_shield_and_no_names_mask_nothing()
    {
        var states = new Dictionary<uint, QuestState> { [Start] = QuestState.Completed, [Path] = QuestState.Ready };

        var related = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 }, names: Index.Names);
        var off = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0, HideRelated = false }, names: Index.Names);
        var shieldOff = SpoilerMask.Build(Catalog, states, SpoilerOptions.Off, names: Index.Names);
        var noNames = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });

        Assert.True(related.MasksNames);
        Assert.True(related.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.True(off.IsMasked(Opener));
        Assert.False(off.MasksNames);
        Assert.False(off.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(shieldOff.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(noNames.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(SpoilerMask.None.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.NotEqual(related.Fingerprint, off.Fingerprint);
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void On_upgrade_the_switch_takes_the_value_of_hide_story_names_ahead(bool? saved, bool hideNames, bool expected) =>
        Assert.Equal(expected, SpoilerOptions.HideRelatedOnLoad(saved, hideNames));

    [Fact]
    public void A_completed_story_masks_no_name()
    {
        var mask = SpoilerMask.Build(Catalog, States(Catalog, QuestState.Completed), SpoilerOptions.Default, names: Index.Names);

        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.Equal(0, mask.MaskedNameCount);
    }

    [Fact]
    public void The_count_line_counts_story_names_and_other_names()
    {
        var mask = At(Path);
        var hidden = 0;
        foreach (var kind in new[] { SpoilerKind.Area, SpoilerKind.Aetheryte, SpoilerKind.Duty, SpoilerKind.Reward, SpoilerKind.Npc })
        {
            hidden += Index.Names.All(kind).Count(pair => mask.IsNameMasked(kind, pair.Key));
        }

        Assert.Equal(3, mask.MaskedCount);
        Assert.Equal(hidden, mask.MaskedNameCount);
        Assert.True(mask.MaskedNameCount >= 10, $"{mask.MaskedNameCount} names");

        var format = ResxFiles.Load(string.Empty)["SpoilerHiddenCountFormat"];
        Assert.Equal(
            "212 story names and 486 other names hidden for Michiru.",
            string.Format(System.Globalization.CultureInfo.InvariantCulture, format, 212, 486, "Michiru"));
    }

    [Fact]
    public void Search_never_finds_a_hidden_name_and_never_hints_at_one()
    {
        var search = SearchIndex.Build(Catalog);
        var before = At(Path);
        var after = At(Dungeon);

        // The Kugane side quest is shown (a side quest), but its blade and the Ruby Sea it opens sit past the story point.
        Assert.False(before.IsMasked(KuganeSide));
        Assert.True(search.Matches(KuganeSide, "kojin", null, Index));
        Assert.False(search.Matches(KuganeSide, "kojin", before, Index));
        Assert.True(search.Matches(KuganeSide, "kojin", after, Index));
        Assert.True(search.Matches(KuganeSide, "ruby", null, Index));
        Assert.False(search.Matches(KuganeSide, "ruby", before, Index));
        Assert.True(search.Matches(KuganeSide, "ruby", after, Index));

        // A hidden name matches only its placeholder, typed with plain spaces.
        Assert.True(search.Matches(KuganeSide, "stormblood area 1", before, Index));
        Assert.True(search.Matches(KuganeSide, "an item", before, Index));

        // A reward the story already gave is still found.
        Assert.True(search.Matches(EarlySide, "potion", before, Index));

        // Find by unlock: typing a name from ahead finds nothing, and no row says something was hidden.
        Assert.Contains(Index.Find("ruby", id => !before.IsMasked(id)), m => m.Find.Name == "The Ruby Sea");
        Assert.Empty(Index.Find("ruby", id => !before.IsMasked(id), spoilers: before));
        var shown = Assert.Single(Index.Find("ruby", id => !after.IsMasked(id), spoilers: after), m => m.Find.Name == "The Ruby Sea");
        Assert.False(shown.Hidden);
        Assert.Equal("The Ruby Sea", shown.Label);

        // Its placeholder finds it, listed under the placeholder.
        var hidden = Assert.Single(Index.Find("stormblood area 1", id => !before.IsMasked(id), spoilers: before));
        Assert.True(hidden.Hidden);
        Assert.Equal("Stormblood area" + Nbsp + "1", hidden.Label);

        // A masked quest is never the way to a find, as before.
        Assert.DoesNotContain(Index.Find("kugane", id => !before.IsMasked(id), spoilers: before), m => m.Find.Name == "Kugane");
    }

    [Fact]
    public void Unlock_rows_print_placeholders_without_place_note_or_icon_of_their_own()
    {
        var before = At(Path);
        var rows = Index.For(KuganeSide);
        var ruby = Assert.Single(rows, e => e.Target == UnlockTarget.Zone);
        Assert.Equal("Area · Othard", ruby.Caption);

        var shown = UnlockView.Visible(rows, masked: false, spoilers: before);
        var shielded = Assert.Single(shown, e => e.Target == UnlockTarget.Zone);
        Assert.Equal("Stormblood area" + Nbsp + "1", shielded.Name);
        Assert.Equal("Area", shielded.Caption);
        Assert.Equal(ruby.TargetId, shielded.TargetId);
        Assert.Equal("Area", UnlockView.CaptionOf(ruby, before));
        Assert.Equal("Stormblood area" + Nbsp + "1", UnlockView.NameOf(ruby, Catalog, before));
        Assert.Equal("Stormblood area" + Nbsp + "1", UnlockText.Places(shown));

        // An aethernet shard follows its area and keeps the generic marker.
        var shard = Assert.Single(Index.For(Opener), e => e.Target == UnlockTarget.AethernetShard);
        var shardShown = UnlockView.Shielded(shard, before);
        Assert.Equal("Stormblood aetheryte · area" + Nbsp + "2", shardShown.Name);
        Assert.Equal(shard.Icon, shardShown.Icon);

        // Without the shield, and once the story is there, the row is itself.
        Assert.Same(rows, UnlockView.Visible(rows, masked: false));
        Assert.Same(rows, UnlockView.Visible(rows, masked: false, spoilers: At(Opener)));
        Assert.Equal("The Ruby Sea", UnlockView.NameOf(ruby, Catalog, At(Opener)));
    }

    [Fact]
    public void The_one_line_forms_follow_the_shield_they_are_given()
    {
        var source = new QuestUnlocksSource(() => Catalog, _ => Index, start: work => Task.FromResult(work()));
        var before = At(Path);

        Assert.Equal("The Ruby Sea", source.Places(KuganeSide));
        Assert.Equal("Stormblood area" + Nbsp + "1", source.Places(KuganeSide, spoilers: before));
        Assert.Equal("The Ruby Sea", source.Places(KuganeSide, spoilers: At(Opener)));
        Assert.Contains("Stormblood area" + Nbsp + "1", source.OpensLine(KuganeSide, spoilers: before), StringComparison.Ordinal);
    }

    [Fact]
    public void Places_vendors_routes_and_blockers_print_through_the_shield()
    {
        var before = At(Path);
        var after = At(Dungeon);

        // A place with its region: the placeholder alone for a masked place; the place alone under a masked region.
        Assert.Equal("Stormblood area" + Nbsp + "2", before.Place("Hingashi", "Kugane", "{0} › {1}"));
        Assert.Equal("Hingashi › Kugane", after.Place("Hingashi", "Kugane", "{0} › {1}"));
        Assert.Equal("Ul'dah - Steps of Nald", before.Place("Hingashi", "Ul'dah - Steps of Nald", "{0} › {1}"));
        Assert.Equal(string.Empty, before.Place("Hingashi", null, "{0} › {1}"));

        // Where to get: a vendor the story has not introduced, standing in a zone it has not reached.
        var vendor = new Core.Sources.Vendor(1, "Hancock", new Core.Sources.WorldSpot(Kugane, "Kugane", 0f, 0f, 10f, 11f));
        Assert.True(Core.Sources.SourceText.Shielded(vendor, before));
        Assert.Equal("Stormblood character, Stormblood area" + Nbsp + "2", Core.Sources.SourceText.VendorWithPlace(vendor, before));
        Assert.Equal("Hancock, Kugane (10.0, 11.0)", Core.Sources.SourceText.VendorWithPlace(vendor, after));
        Assert.False(Core.Sources.SourceText.Shielded(vendor, after));

        // A route to a duty is titled by its placeholder, and so is each part; the quests stay.
        var duty = new Core.Route.RouteTarget(Core.Route.RouteTargetKind.Duty, "The Sirensong Sea", [Dungeon]) { Icon = 61801 };
        var shown = duty.Through(before);
        Assert.Equal("Dungeon (Lv" + Nbsp + "61)", shown.Label);
        Assert.Equal(0u, shown.Icon);
        Assert.Equal(duty.QuestRowIds, shown.QuestRowIds);
        Assert.Same(duty, duty.Through(after));
        var union = Core.Route.RouteTarget.Union(Core.Route.RouteTargetKind.Duty, "Roulette", [duty]);
        Assert.Equal("Dungeon (Lv" + Nbsp + "61)", union.Through(before).Parts[0].Label);
        var ruby = Core.Route.RouteTarget.ForUnlock(Index.Finds.Single(f => f.Name == "The Ruby Sea"));
        Assert.Equal("Stormblood area" + Nbsp + "1", ruby.Through(before).Label);

        // Blockers name a duty past the story point by its placeholder.
        var names = Core.Evaluation.BlockerNames.Default with { Duty = _ => "The Sirensong Sea" };
        Assert.Equal("Dungeon (Lv" + Nbsp + "61)", names.Through(before).Duty(62));
        Assert.Equal("The Sirensong Sea", names.Through(after).Duty(62));
    }

    [Fact]
    public void An_empty_catalog_places_nothing()
    {
        Assert.Equal(0, SpoilerNames.Build(QuestCatalog.Empty, QuestUnlocks.Empty).Count);
        Assert.False(SpoilerNames.Empty.TryGet(SpoilerKind.Area, "Kugane", out _));
        Assert.Equal(0u, SpoilerNames.Empty.AnchorOf(Opener));
    }

    [Fact]
    public void A_side_quest_revealed_for_the_session_no_longer_lies_ahead()
    {
        // 1.21.0 review: "A side story ahead" and "Sidequest (Lv 90)" offer "Reveal this name", which reveals the quest's
        // row id; the quest stops lying ahead then, though its anchor (the main scenario quest it needs) stays masked.
        var before = At(Path);
        Assert.True(before.IsAhead(LateSide));

        var revealed = At(Path, revealedQuests: new HashSet<uint> { LateSide });
        Assert.False(revealed.IsAhead(LateSide));
        Assert.True(revealed.IsAhead(Far));
        Assert.True(revealed.IsMasked(Far));
        Assert.NotEqual(before.Fingerprint, revealed.Fingerprint);
    }

    [Fact]
    public void A_followed_route_keeps_what_places_its_target_through_a_save()
    {
        // 1.21.0 review: Up next's "Your route to …" and the overlay read the stored route; a target composed from a
        // name ("The Ruby Sea") lost what placed it, so its label printed past the story point.
        var ruby = Core.Route.RouteTarget.ForUnlock(Index.Finds.Single(f => f.Name == "The Ruby Sea"));
        var stored = Core.Route.SavedRoute.From(ruby, 1).ToTarget();

        Assert.Equal(ruby.Placed, stored.Placed);
        Assert.Equal("Stormblood area" + Nbsp + "1", stored.Through(At(Path)).Label);
        Assert.Equal("Stormblood area" + Nbsp + "1", stored.ShownLabel(At(Path), Catalog));
        Assert.Equal("The Ruby Sea", stored.ShownLabel(At(Far), Catalog));

        // A route to a quest is titled by its name, a masked one by its placeholder.
        var quest = new Core.Route.RouteTarget(Core.Route.RouteTargetKind.Quest, "The Far Edge", [Far]);
        Assert.Equal(SpoilerMask.Placeholder(Catalog.ByRowId[Far]), quest.ShownLabel(At(Path), Catalog));
    }
}
