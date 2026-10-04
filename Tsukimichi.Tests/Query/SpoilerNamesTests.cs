using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The wider spoiler shield (plan v7, 1.20.0 N6) over a small hand-built story: each quest's story anchor, the names
/// placed from the unlock index, the rewards and the givers, the placeholders, the setting, search, find by unlock and
/// the unlock rows' shielded form.
/// </summary>
public class SpoilerNamesTests
{
    // The story: 1 → 2 → 3 (opens Kugane, gives the Lunar Whale) → 4 (opens the Sirensong Sea) → 5.
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
    private const uint Sirensong = 238;

    private static QuestRecord Msq(uint rowId, string name, byte level, uint previous, string giver, params RewardRef[] rewards) =>
        Quest(rowId, name, section: 0, category: 1, genre: 1, sortKey: (int)rowId, level: level, rewards: rewards) with
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
        Msq(Start, "Coming to Ul'dah", 1, 0, "Momodi", Reward(RewardKind.Item, "Potion", 4551)),
        Msq(Path, "The Path", 50, Start, "Alphinaud"),
        Msq(Opener, "Not without Incident", 61, Path, "Alphinaud", Reward(RewardKind.Mount, "Lunar Whale", 7), Reward(RewardKind.Item, "Potion", 4551)),
        Msq(Dungeon, "Once More to the Ruby Sea", 62, Opener, "Hancock"),
        Msq(Far, "The Far Edge", 70, Dungeon, "Alphinaud"),
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
        ],
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

    /// <summary>The character whose next main scenario quest is <paramref name="next"/>, with nothing revealed ahead.</summary>
    private static SpoilerMask At(uint next, SpoilerOptions? options = null)
    {
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = quest.RowId < next || (quest.RowId == EarlySide && next > Start) ? QuestState.Completed
                : quest.RowId == next ? QuestState.Ready
                : QuestState.Blocked;
        }

        return SpoilerMask.Build(Catalog, states, options ?? SpoilerOptions.Default with { Ahead = 0 }, names: Index.Names);
    }

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
    public void Before_the_opener_its_zone_region_duty_reward_and_people_are_masked()
    {
        var mask = At(Path);

        Assert.True(mask.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Area, "Hingashi"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Reward, "Kojin Blade"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.True(mask.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));
        Assert.Equal("Area ahead (Lv 61)", mask.Name(SpoilerKind.Area, "Kugane"));
        Assert.Equal("Duty ahead (Lv 62)", mask.Name(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.Equal("Reward ahead (Lv 61)", mask.Name(SpoilerKind.Reward, "Lunar Whale"));
        Assert.Equal("Someone ahead (Lv 61)", mask.Name(SpoilerKind.Npc, "Hancock"));

        // What the story has already introduced stays: a potion the first quest gives, its givers and its city.
        Assert.False(mask.IsNameMasked(SpoilerKind.Reward, "Potion"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Alphinaud"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Momodi"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, "Ul'dah - Steps of Nald"));
        Assert.Equal("Potion", mask.Name(SpoilerKind.Reward, "Potion"));
    }

    [Fact]
    public void Names_unlock_as_the_story_reaches_them()
    {
        var atOpener = At(Opener);
        Assert.False(atOpener.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.False(atOpener.IsNameMasked(SpoilerKind.Reward, "Lunar Whale"));
        Assert.True(atOpener.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));

        var atDungeon = At(Dungeon);
        Assert.False(atDungeon.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.False(atDungeon.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.True(atDungeon.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));

        // "Quests ahead to reveal" reveals what those quests introduce too.
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

    [Fact]
    public void A_completed_story_masks_no_name()
    {
        var mask = SpoilerMask.Build(Catalog, States(Catalog, QuestState.Completed), SpoilerOptions.Default, names: Index.Names);

        Assert.False(mask.IsNameMasked(SpoilerKind.Npc, "Somebody Late"));
        Assert.False(mask.IsNameMasked(SpoilerKind.Area, "Kugane"));
    }

    [Fact]
    public void Search_never_finds_a_quest_by_a_masked_reward_or_unlock()
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

        // A reward the story already gave is still found.
        Assert.True(search.Matches(EarlySide, "potion", before, Index));

        // Find by unlock never names a masked zone, even through a quest the shield shows.
        Assert.Contains(Index.Find("ruby", id => !before.IsMasked(id)), m => m.Find.Name == "The Ruby Sea");
        Assert.DoesNotContain(Index.Find("ruby", id => !before.IsMasked(id), spoilers: before), m => m.Find.Name == "The Ruby Sea");
        Assert.Contains(Index.Find("ruby", id => !after.IsMasked(id), spoilers: after), m => m.Find.Name == "The Ruby Sea");
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
        Assert.Equal("Area ahead (Lv 61)", shielded.Name);
        Assert.Equal("Area", shielded.Caption);
        Assert.Equal(ruby.TargetId, shielded.TargetId);
        Assert.Equal("Area", UnlockView.CaptionOf(ruby, before));
        Assert.Equal("Area ahead (Lv 61)", UnlockView.NameOf(ruby, Catalog, before));
        Assert.Equal("Area ahead (Lv 61)", UnlockText.Places(shown));

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
        Assert.Equal("Area ahead (Lv 61)", source.Places(KuganeSide, spoilers: before));
        Assert.Equal("The Ruby Sea", source.Places(KuganeSide, spoilers: At(Opener)));
        Assert.Contains("Area ahead (Lv 61)", source.OpensLine(KuganeSide, spoilers: before), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_catalog_places_nothing()
    {
        Assert.Equal(0, SpoilerNames.Build(QuestCatalog.Empty, QuestUnlocks.Empty).Count);
        Assert.False(SpoilerNames.Empty.TryGet(SpoilerKind.Area, "Kugane", out _));
        Assert.Equal(0u, SpoilerNames.Empty.AnchorOf(Opener));
    }
}
