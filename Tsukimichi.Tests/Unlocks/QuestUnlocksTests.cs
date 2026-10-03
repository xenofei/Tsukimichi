using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// The unlock index (feature plan v6 K1): grouping and order, precedence (curated over sheet over derived), the dedupe
/// rules, curated aetherytes, next quests, the reverse lookup, the masks and the one-line forms, without game files.
/// </summary>
public class QuestUnlocksTests
{
    private const uint Opener = 68_005;
    private const uint Next = 68_006;
    private const uint Locked = 68_007;
    private const uint Kugane = 628;
    private const uint Sirensong = 238;
    private const uint SirensongInstance = 62;
    private const uint Onokoro = 106;

    private static readonly PlanDuties Duties = PlanDuties.From(
    [
        new PlanDuty(Sirensong, SirensongInstance, UnlockKind.Dungeon, "the Sirensong Sea"),
        new PlanDuty(174, 0, UnlockKind.System, "the Palace of the Dead (Floors 1-10)"),
        new PlanDuty(175, 0, UnlockKind.System, "the Palace of the Dead (Floors 11-20)"),
    ]);

    private static QuestRecord OpenerQuest() => Quest(Opener, "Not without Incident") with
    {
        EventIconType = UnlockAreas.MainScenarioIconType,
        Expansion = 2,
        Level = 61,
        Rewards =
        [
            new RewardRef(RewardKind.Instance, SirensongInstance, 0, 1, "the Sirensong Sea", 0),
            new RewardRef(RewardKind.Emote, 154, 0, 1, "Eastern Bow", 246315),
        ],
    };

    private static QuestCatalog Quests() => Catalog(
        OpenerQuest(),
        Quest(Next, "The Man from Ul'dah") with { EventIconType = UnlockAreas.MainScenarioIconType, Expansion = 2, PreviousQuests = new Prereq([Opener], JoinKind.All) },
        Quest(Locked, "A Lock Only") with { QuestLocks = [Opener] });

    private static UnlockLinks Links() => new()
    {
        Zones = [new UnlockZone(Kugane, "Kugane", "Hingashi", 371, 2, 111, 628)],
        Aetherytes = [new UnlockAetheryte(Onokoro, Kugane, "Onokoro", 10f, 10f, true)],
        Warps = [new UnlockWarp(Opener, Kugane)],
        Touches = [new UnlockTouch(Opener, Kugane, 0f, 0f)],
        Duties = [new UnlockDuty(Sirensong, 61801, 61, 2)],
        AreaIcon = 7,
    };

    private static UniqueRewardCatalog Rewards(params UniqueRewardEntry[] entries) =>
        UniqueRewardCatalog.Build(new UniqueRewardsData("test", default, entries), new Dictionary<uint, UniqueOverride>(), CuratedData.Empty);

    private static UniqueRewardEntry DutyEntry(uint quest, uint cfc, string name, Confidence confidence = Confidence.Curated) =>
        new(quest, RewardKind.DutyUnlock, cfc, 0, name, confidence, "curated/duty_unlocks.json");

    private static QuestUnlocks Build(UniqueRewardCatalog? rewards = null, CuratedData? curated = null) =>
        QuestUnlocks.Build(Quests(), rewards ?? Rewards(DutyEntry(Opener, Sirensong, "the Sirensong Sea")), Duties, Links(), curated);

    [Fact]
    public void Rows_come_grouped_in_display_order_with_next_quests_last()
    {
        var entries = Build().For(Opener);

        Assert.Equal(
            [UnlockGroup.Area, UnlockGroup.Aetheryte, UnlockGroup.Duty, UnlockGroup.ActionEmote, UnlockGroup.NextQuest],
            entries.Select(e => e.Group).ToArray());
        Assert.Equal(["Kugane", "Onokoro", "The Sirensong Sea", "Eastern Bow", "The Man from Ul'dah"], entries.Select(e => e.Name).ToArray());
        Assert.Equal("Dungeon · Lv 61", entries[2].Caption);
        Assert.Equal("Area · Hingashi", entries[0].Caption);
        Assert.Equal(61801u, entries[2].Icon);
    }

    [Fact]
    public void A_zone_a_warp_opens_and_the_rule_finds_is_one_sheet_row()
    {
        var zone = Assert.Single(Build().For(Opener), e => e.Target == UnlockTarget.Zone);

        Assert.Equal(UnlockSource.Sheet, zone.Source);
        Assert.False(zone.IsLikely);
        Assert.Equal(Kugane, zone.PlaceId);
    }

    [Fact]
    public void An_instance_and_the_duty_unlock_of_one_duty_are_one_curated_row()
    {
        var duty = Assert.Single(Build().For(Opener), e => e.Group == UnlockGroup.Duty);

        Assert.Equal(UnlockTarget.Dungeon, duty.Target);
        Assert.Equal(Sirensong, duty.TargetId);
        Assert.Equal(UnlockSource.Curated, duty.Source);
        Assert.Equal(RewardKind.DutyUnlock, duty.Reward);
    }

    [Fact]
    public void A_deep_dungeons_floor_sets_fold_into_one_row_and_its_curated_feature_into_it()
    {
        var unlocks = Build(Rewards(
            DutyEntry(Opener, 174, "the Palace of the Dead (Floors 1-10)"),
            DutyEntry(Opener, 175, "the Palace of the Dead (Floors 11-20)"),
            new UniqueRewardEntry(Opener, RewardKind.SystemUnlock, 0, 0, "Palace of the Dead", Confidence.Curated, "curated/system_unlocks.json")));

        var palace = Assert.Single(unlocks.For(Opener), e => e.Name.Contains("Palace", StringComparison.Ordinal));
        Assert.Equal("The Palace of the Dead", palace.Name);
        Assert.Equal(UnlockTarget.OtherDuty, palace.Target);
    }

    [Fact]
    public void Actions_and_collectables_the_rewards_already_show_are_marked()
    {
        var emote = Assert.Single(Build().For(Opener), e => e.Target == UnlockTarget.Emote);

        Assert.True(emote.InRewards);
        Assert.Equal(246315u, emote.Icon);
        Assert.Equal(RewardKind.Emote, emote.Reward);
    }

    [Fact]
    public void Next_quests_leave_out_lock_only_dependents()
    {
        var next = Build().For(Opener).Where(e => e.Target == UnlockTarget.NextQuest).ToList();

        Assert.Equal([Next], next.Select(e => e.TargetId).ToArray());
    }

    [Fact]
    public void A_curated_aetheryte_replaces_the_rules_row()
    {
        var dir = Directory.CreateTempSubdirectory("tsukimichi-unlocks-").FullName;
        try
        {
            File.WriteAllText(
                Path.Combine(dir, CuratedData.AetheryteUnlocksFileName),
                $$"""{ "schema": 1, "entries": { "{{Onokoro}}": { "name": "Onokoro", "quests": [ {{Next}} ], "evidence": "https://example.com/onokoro", "note": "Attuned on the next step." } } }""");
            var curated = CuratedData.Load(dir);
            Assert.Empty(curated.Warnings);

            var unlocks = Build(curated: curated);

            Assert.DoesNotContain(unlocks.For(Opener), e => e.Target == UnlockTarget.Aetheryte);
            var onokoro = Assert.Single(unlocks.For(Next), e => e.Target == UnlockTarget.Aetheryte);
            Assert.Equal(UnlockSource.Curated, onokoro.Source);
            Assert.Equal("Attuned on the next step.", onokoro.Note);
            Assert.Equal([Next], unlocks.UnlockedBy(UnlockTarget.Aetheryte, Onokoro).ToArray());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void The_reverse_lookup_masks_headline_and_search_text_agree()
    {
        var unlocks = Build();

        Assert.Equal([Opener], unlocks.UnlockedBy(UnlockTarget.Zone, Kugane).ToArray());
        Assert.Equal([Opener], unlocks.UnlockedBy(UnlockTarget.Dungeon, Sirensong).ToArray());
        Assert.Equal([Opener], unlocks.UnlockedBy(UnlockTarget.NextQuest, Next).ToArray());
        Assert.Empty(unlocks.UnlockedBy(UnlockTarget.Zone, 1));

        var mask = unlocks.GroupMask(Opener);
        Assert.True((mask & UnlockTargets.Bit(UnlockGroup.Area)) != 0);
        Assert.True((mask & UnlockTargets.Bit(UnlockGroup.Duty)) != 0);
        Assert.Equal(0, mask & UnlockTargets.Bit(UnlockGroup.Feature));

        Assert.Equal("Kugane", unlocks.Headline(Opener)!.Name);
        Assert.Null(unlocks.Headline(Next));
        Assert.Equal("kugane\nonokoro\nthe sirensong sea\neastern bow", unlocks.SearchText(Opener));
        Assert.Empty(unlocks.For(Locked));
    }

    [Fact]
    public void The_headline_prefers_a_stated_row_over_a_likely_one()
    {
        var links = Links() with { Warps = [] };
        var unlocks = QuestUnlocks.Build(Quests(), Rewards(), PlanDuties.Empty, links);

        // Kugane and Onokoro are only inferred now; the duty the quest's own rewards name is stated.
        Assert.True(unlocks.For(Opener)[0].IsLikely);
        Assert.True(unlocks.For(Opener)[1].IsLikely);
        Assert.Equal(UnlockGroup.Duty, unlocks.Headline(Opener)!.Group);
        Assert.Equal("The Sirensong Sea", unlocks.Headline(Opener)!.Name);
    }

    [Fact]
    public void The_one_line_forms_name_four_and_count_the_rest()
    {
        var entries = Build().For(Opener);

        Assert.Equal("Kugane (area) · Onokoro (aetheryte) · The Sirensong Sea (dungeon) · Eastern Bow (emote)", UnlockText.Summary(entries));
        Assert.Equal("Kugane · Onokoro · +2", UnlockText.Names(entries, 2));
        Assert.Equal("Opens Kugane (area) · +3", UnlockText.OpensLine(entries, 1));
        Assert.Empty(UnlockText.Summary(Build().For(Next).Where(e => e.Target == UnlockTarget.NextQuest).ToList()));
    }

    [Fact]
    public void An_empty_catalog_or_no_links_build_without_failing()
    {
        Assert.Same(QuestUnlocks.Empty, QuestUnlocks.Build(QuestCatalog.Empty, Rewards(), PlanDuties.Empty, UnlockLinks.Empty));
        var bare = QuestUnlocks.Build(Quests(), Rewards(), PlanDuties.Empty, UnlockLinks.Empty);
        Assert.DoesNotContain(bare.For(Opener), e => e.Group is UnlockGroup.Area or UnlockGroup.Aetheryte);
        Assert.Contains(bare.For(Opener), e => e.Group == UnlockGroup.Duty && e.Target == UnlockTarget.OtherDuty);
    }

    [Fact]
    public void The_source_builds_once_per_catalog_and_memoizes_the_summary()
    {
        var catalog = Quests();
        QuestCatalog? current = null;
        var builds = 0;
        var source = new QuestUnlocksSource(
            () => current,
            c =>
            {
                builds++;
                return QuestUnlocks.Build(c, Rewards(DutyEntry(Opener, Sirensong, "the Sirensong Sea")), Duties, Links());
            },
            start: work => Task.FromResult(work()));

        Assert.Same(QuestUnlocks.Empty, source.Current);
        current = catalog;
        Assert.NotEmpty(source.For(Opener));
        Assert.True(source.IsCurrent);
        var summary = source.Summary(Opener);
        Assert.Same(summary, source.Summary(Opener));
        Assert.Equal("Kugane · Onokoro · The Sirensong Sea · Eastern Bow", source.Names(Opener));
        _ = source.Current;
        Assert.Equal(1, builds);

        current = null;
        Assert.Same(QuestUnlocks.Empty, source.Current);
    }
}

/// <summary>The spoiler shield over the unlocks (unlocks spec §3.3), on the frozen catalog with a character that has done nothing.</summary>
public class UnlockSpoilerTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    [Fact]
    public void A_masked_quest_shows_no_unlock_and_a_next_quest_prints_its_placeholder()
    {
        var catalog = fixture.Bundle.Catalog;
        var mask = SpoilerMask.Build(catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default);
        var masked = catalog.All.First(q => mask.IsMasked(q) && !q.IsRemoved);
        var entries = new[]
        {
            new UnlockEntry(UnlockTarget.Zone, 628, "Kugane", 7, UnlockSource.Sheet, 2),
            new UnlockEntry(UnlockTarget.NextQuest, masked.RowId, masked.Name, 0, UnlockSource.Sheet, masked.Expansion),
        };

        Assert.Empty(UnlockView.Visible(entries, masked: true));
        Assert.Same(entries, UnlockView.Visible(entries, masked: false));
        Assert.Equal(SpoilerMask.Placeholder(masked), UnlockView.NameOf(entries[1], catalog, mask));
        Assert.Equal("Kugane", UnlockView.NameOf(entries[0], catalog, mask));
        Assert.Equal(masked.Name, UnlockView.NameOf(entries[1], catalog, SpoilerMask.None));
    }

    [Fact]
    public void Sprout_mode_leaves_out_rows_past_the_reach()
    {
        var entries = new[]
        {
            new UnlockEntry(UnlockTarget.Zone, 132, "New Gridania", 7, UnlockSource.Derived, 0),
            new UnlockEntry(UnlockTarget.Zone, 628, "Kugane", 7, UnlockSource.Sheet, 2),
        };

        Assert.Equal(["New Gridania"], UnlockView.Visible(entries, masked: false, reach: 0).Select(e => e.Name).ToArray());
        Assert.Equal(2, UnlockView.Visible(entries, masked: false, reach: 2).Count);
    }
}
