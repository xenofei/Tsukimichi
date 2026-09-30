using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

public class UniqueRewardCatalogTests
{
    private const uint MountQuest = 66001;
    private const uint EmoteQuest = 66002;
    private const uint TwoKindsQuest = 66003;
    private const uint PlainQuest = 66004;

    private static UniqueRewardEntry Entry(uint quest, RewardKind kind, uint rewardId, string name, Confidence confidence = Confidence.Static, string source = "test") =>
        new(quest, kind, rewardId, 0, name, confidence, source);

    private static UniqueRewardsData Data(params UniqueRewardEntry[] entries) => new("1.0", default, entries);

    private static readonly UniqueRewardsData Shipped = Data(
        Entry(MountQuest, RewardKind.Mount, 10, "Company Chocobo"),
        Entry(EmoteQuest, RewardKind.Emote, 20, "Most Gentlemanly"),
        Entry(TwoKindsQuest, RewardKind.Mount, 11, "Magitek Armor"),
        Entry(TwoKindsQuest, RewardKind.Minion, 30, "Wind-up Cid"));

    private static UniqueRewardCatalog Build(UniqueRewardsData? data = null, IReadOnlyDictionary<uint, UniqueOverride>? overrides = null, CuratedData? curated = null) =>
        UniqueRewardCatalog.Build(data ?? Shipped, overrides ?? new Dictionary<uint, UniqueOverride>(), curated ?? CuratedData.Empty);

    [Fact]
    public void Groups_entries_by_kind_in_enum_order_with_counts()
    {
        var catalog = Build();

        Assert.Equal(4, catalog.Count);
        Assert.Equal([(RewardKind.Emote, 1), (RewardKind.Mount, 2), (RewardKind.Minion, 1)], catalog.Kinds.Select(k => (k.Kind, k.Count)));
        Assert.Equal(["Company Chocobo", "Magitek Armor"], catalog.Entries(RewardKind.Mount).Select(e => e.RewardName));
        Assert.Empty(catalog.Entries(RewardKind.Title));
        Assert.Equal(catalog.Entries(RewardKind.Mount), catalog.ByKind[RewardKind.Mount]);
        Assert.False(catalog.ByKind.ContainsKey(RewardKind.Title));
    }

    [Fact]
    public void ForQuest_returns_every_entry_of_that_quest_and_IsUnique_reflects_the_view()
    {
        var catalog = Build();

        Assert.Equal([RewardKind.Mount, RewardKind.Minion], catalog.ForQuest(TwoKindsQuest).Select(e => e.Kind));
        Assert.Empty(catalog.ForQuest(PlainQuest));
        Assert.True(catalog.IsUnique(TwoKindsQuest));
        Assert.False(catalog.IsUnique(PlainQuest));
    }

    [Fact]
    public void Override_false_removes_the_quest_from_the_unique_view_but_ForQuest_still_finds_it()
    {
        var overrides = new Dictionary<uint, UniqueOverride> { [TwoKindsQuest] = new(false, "farmable now") };

        var catalog = Build(overrides: overrides);

        Assert.Equal(2, catalog.Count);
        Assert.Equal(["Company Chocobo"], catalog.Entries(RewardKind.Mount).Select(e => e.RewardName));
        Assert.False(catalog.ByKind.ContainsKey(RewardKind.Minion));
        Assert.DoesNotContain(catalog.Kinds, k => k.Kind == RewardKind.Minion);
        Assert.False(catalog.IsUnique(TwoKindsQuest));

        var kept = catalog.ForQuest(TwoKindsQuest);
        Assert.Equal(2, kept.Count);
        Assert.All(kept, e => Assert.Equal(Confidence.Static, e.Confidence));
    }

    [Fact]
    public void Hidden_overrides_are_enumerable_and_stay_out_of_the_view()
    {
        var overrides = new Dictionary<uint, UniqueOverride>
        {
            [TwoKindsQuest] = new(false, "farmable now"),
            [PlainQuest] = new(false, null),
            [MountQuest] = new(true, "still unique"),
        };

        var catalog = Build(overrides: overrides);

        // Both shipped entries of the hidden quest, plus a stand-in for the hidden quest no source lists.
        Assert.Equal(3, catalog.Hidden.Count);
        Assert.Equal([RewardKind.Mount, RewardKind.Minion], catalog.Hidden.Where(e => e.QuestRowId == TwoKindsQuest).Select(e => e.Kind));
        var standIn = Assert.Single(catalog.Hidden, e => e.QuestRowId == PlainQuest);
        Assert.Equal(RewardKind.Other, standIn.Kind);
        Assert.Equal(UniqueRewardCatalog.DefaultHiddenRewardName, standIn.RewardName);
        Assert.Equal(Confidence.UserOverride, standIn.Confidence);

        // Hidden entries never leak into the view, its kinds or its counts; the unique override is not "hidden".
        Assert.DoesNotContain(catalog.All, e => e.QuestRowId == TwoKindsQuest || e.QuestRowId == PlainQuest);
        Assert.Equal(2, catalog.Count);
        Assert.DoesNotContain(catalog.Hidden, e => e.QuestRowId == MountQuest);
        Assert.False(catalog.IsUnique(TwoKindsQuest));
        Assert.False(catalog.IsUnique(PlainQuest));
    }

    [Fact]
    public void Hidden_stand_in_alone_does_not_collapse_the_catalog_to_Empty()
    {
        var overrides = new Dictionary<uint, UniqueOverride> { [PlainQuest] = new(false, "never was") };

        var catalog = Build(Data(), overrides);

        Assert.Equal(0, catalog.Count);
        Assert.Equal("never was", Assert.Single(catalog.Hidden).RewardName);
        Assert.Empty(UniqueRewardCatalog.Empty.Hidden);
    }

    [Fact]
    public void Override_true_adds_a_user_entry_only_when_the_quest_had_none()
    {
        var overrides = new Dictionary<uint, UniqueOverride>
        {
            [PlainQuest] = new(true, "Gives the secret hat"),
            [MountQuest] = new(true, "already unique"),
        };

        var catalog = Build(overrides: overrides);

        var added = Assert.Single(catalog.ForQuest(PlainQuest));
        Assert.Equal(RewardKind.Other, added.Kind);
        Assert.Equal("Gives the secret hat", added.RewardName);
        Assert.Equal(Confidence.UserOverride, added.Confidence);
        Assert.Equal(UniqueRewardCatalog.UserSource, added.Source);
        Assert.Equal(0u, added.RewardId);
        Assert.Contains(added, catalog.Entries(RewardKind.Other));
        Assert.True(catalog.IsUnique(PlainQuest));

        // A quest that already has entries gains nothing and keeps its shipped entries.
        var mount = Assert.Single(catalog.ForQuest(MountQuest));
        Assert.Equal(Confidence.Static, mount.Confidence);
        Assert.Equal(5, catalog.Count);
    }

    [Fact]
    public void Override_true_without_a_note_uses_a_generic_name()
    {
        var overrides = new Dictionary<uint, UniqueOverride> { [PlainQuest] = new(true, null) };

        var catalog = Build(overrides: overrides);

        var added = Assert.Single(catalog.ForQuest(PlainQuest));
        Assert.False(string.IsNullOrWhiteSpace(added.RewardName));
    }

    [Fact]
    public void Curated_unlocks_supplement_shipped_data_without_duplicating_it()
    {
        var shipped = Data(
            Entry(MountQuest, RewardKind.Mount, 10, "Company Chocobo"),
            Entry(66038, RewardKind.SystemUnlock, 0, "Glamour Dresser", Confidence.Curated, "curated/system_unlocks.json"),
            Entry(66050, RewardKind.DutyUnlock, 4, "Sastasha", Confidence.Static, "ContentFinderCondition.UnlockCriteria"));

        using var tmp = new Storage.TempDir();
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.SystemUnlocksFileName), """{ "66038": { "label": "Glamour Dresser" }, "66039": { "label": "Retainer Ventures", "kind": "system" } }""");
        File.WriteAllText(Path.Combine(dir, CuratedData.DutyUnlocksFileName), """{ "66050": [4, 5], "66051": { "contentFinderConditionIds": [6], "note": "Copperbell" } }""");
        var curated = CuratedData.Load(dir);

        var catalog = Build(shipped, curated: curated);

        // 66038 is deduped (shipped already carries it); 66039 is new. 66050's cfc 4 is deduped, cfc 5 is new; 66051's cfc 6 is new.
        Assert.Equal(["Glamour Dresser", "Retainer Ventures"], catalog.Entries(RewardKind.SystemUnlock).Select(e => e.RewardName));
        Assert.Single(catalog.ForQuest(66038));
        Assert.Equal([4u, 5u], catalog.ForQuest(66050).Select(e => e.RewardId));
        Assert.Equal(Confidence.Curated, catalog.ForQuest(66050).Single(e => e.RewardId == 5).Confidence);

        // The curated claim outranks the shipped Static one, but a bare id carries no name, so the shipped name stays.
        var upgraded = catalog.ForQuest(66050).Single(e => e.RewardId == 4);
        Assert.Equal(Confidence.Curated, upgraded.Confidence);
        Assert.Equal("Sastasha", upgraded.RewardName);
        Assert.Equal("Copperbell", Assert.Single(catalog.ForQuest(66051)).RewardName);
        Assert.Equal(Confidence.Curated, catalog.ForQuest(66039).Single().Confidence);
        Assert.Equal(6, catalog.Count);
    }

    [Fact]
    public void Curated_online_store_marks_shipped_entries_by_item_or_by_collectible()
    {
        var shipped = Data(
            new UniqueRewardEntry(68546, RewardKind.Mount, 99, 22437, "Starlight bear", Confidence.Static, "s"),
            new UniqueRewardEntry(67079, RewardKind.Emote, 109, 0, "Bomb Dance", Confidence.Static, "Quest.EmoteReward"),
            new UniqueRewardEntry(66038, RewardKind.Emote, 114, 0, "Most Gentlemanly", Confidence.Static, "Quest.EmoteReward"),
            new UniqueRewardEntry(66001, RewardKind.Minion, 46, 6207, "Tender lamb", Confidence.Static, "s") { OtherSources = [OtherSource.OnlineStore] });

        using var tmp = new Storage.TempDir();
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.OnlineStoreFileName),
            """
            { "schema": 1, "entries": {
              "22437": { "name": "Starlight bear", "kind": "Mount", "rewardId": 99, "evidence": "https://x" },
              "12040": { "name": "Bomb Dance", "kind": "Emote", "rewardId": 109, "evidence": "https://x" },
              "6207": { "name": "Tender lamb", "kind": "Minion", "rewardId": 46, "evidence": "https://x" }
            } }
            """);
        var curated = CuratedData.Load(dir);
        Assert.Empty(curated.Warnings);

        var catalog = Build(shipped, curated: curated);

        Assert.True(catalog.ForQuest(68546).Single().SoldOnOnlineStore, "matched by the store item id");
        Assert.True(catalog.ForQuest(67079).Single().SoldOnOnlineStore, "matched by the emote the store item unlocks");
        Assert.False(catalog.ForQuest(66038).Single().SoldOnOnlineStore);
        Assert.Equal([OtherSource.OnlineStore], catalog.ForQuest(66001).Single().OtherSources);
        Assert.Equal(4, catalog.Count);
    }

    [Fact]
    public void Curated_other_sources_mark_shipped_entries_by_item_with_the_duties()
    {
        // A shipped file older than other_sources.json: the catalog still marks the drop and carries where it drops.
        var shipped = Data(
            new UniqueRewardEntry(66711, RewardKind.OptionalItem, 4520, 4520, "Darklight Band of Striking", Confidence.Static, "s"),
            new UniqueRewardEntry(66711, RewardKind.OptionalItem, 4523, 4523, "Darklight Band of Fending", Confidence.Static, "s")
                .WithOtherSource(OtherSource.DungeonDrop, "The Lost City of Amdapor"),
            new UniqueRewardEntry(66038, RewardKind.Emote, 114, 0, "Most Gentlemanly", Confidence.Static, "Quest.EmoteReward"));

        using var tmp = new Storage.TempDir();
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.OtherSourcesFileName),
            """
            { "schema": 1, "entries": {
              "4520": { "name": "Darklight Band of Striking", "source": "DungeonDrop", "where": "The Lost City of Amdapor", "evidence": "https://x", "note": "n" },
              "4523": { "name": "Darklight Band of Fending", "source": "DungeonDrop", "where": "The Lost City of Amdapor", "evidence": "https://x", "note": "n" }
            } }
            """);
        var curated = CuratedData.Load(dir);
        Assert.Empty(curated.Warnings);

        var catalog = Build(shipped, curated: curated);

        var entries = catalog.ForQuest(66711);
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal([OtherSource.DungeonDrop], e.OtherSources));
        Assert.All(entries, e => Assert.Equal("The Lost City of Amdapor", e.DropWhere));
        Assert.False(catalog.ForQuest(66038).Single().DropsInDuty);
    }

    [Fact]
    public void Duplicate_shipped_entries_keep_the_higher_confidence()
    {
        var shipped = Data(
            Entry(MountQuest, RewardKind.Mount, 10, "static name", Confidence.Static),
            Entry(MountQuest, RewardKind.Mount, 10, "curated name", Confidence.Curated));

        var catalog = Build(shipped);

        var only = Assert.Single(catalog.ForQuest(MountQuest));
        Assert.Equal("curated name", only.RewardName);
    }

    [Fact]
    public void View_pairs_each_entry_with_the_callers_obtained_verdict()
    {
        var catalog = Build();
        bool? IsObtained(UniqueRewardEntry e) => e.Kind switch
        {
            RewardKind.Mount => e.RewardId == 10,
            RewardKind.Emote => null,
            _ => false,
        };

        var mounts = catalog.View(RewardKind.Mount, IsObtained);
        Assert.Equal([(10u, (bool?)true), (11u, (bool?)false)], mounts.Select(r => (r.Entry.RewardId, r.Obtained)));

        var all = catalog.View(null, IsObtained);
        Assert.Equal(4, all.Count);
        Assert.Null(all.Single(r => r.Entry.Kind == RewardKind.Emote).Obtained);
        Assert.Equal(catalog.All.Select(e => e.RewardId), all.Select(r => r.Entry.RewardId));
    }

    [Fact]
    public void Counts_split_obtained_total_and_unknown()
    {
        var catalog = Build();
        bool? IsObtained(UniqueRewardEntry e) => e.Kind switch
        {
            RewardKind.Mount => e.RewardId == 10,
            RewardKind.Emote => null,
            _ => false,
        };

        Assert.Equal(new UniqueRewardCounts(1, 2, 0), catalog.Counts(RewardKind.Mount, IsObtained));
        Assert.Equal(new UniqueRewardCounts(0, 1, 1), catalog.Counts(RewardKind.Emote, IsObtained));
        Assert.Equal(new UniqueRewardCounts(0, 0, 0), catalog.Counts(RewardKind.Title, IsObtained));
        Assert.Equal(new UniqueRewardCounts(1, 4, 1), catalog.Counts(null, IsObtained));

        var (obtained, total, unknown) = catalog.Counts(RewardKind.Mount, IsObtained);
        Assert.Equal((1, 2, 0), (obtained, total, unknown));
    }

    [Fact]
    public void Counts_exclude_quests_overridden_as_not_unique()
    {
        var overrides = new Dictionary<uint, UniqueOverride> { [TwoKindsQuest] = new(false, null) };
        var catalog = Build(overrides: overrides);

        Assert.Equal(new UniqueRewardCounts(0, 1, 0), catalog.Counts(RewardKind.Mount, _ => false));
        Assert.Equal(new UniqueRewardCounts(0, 2, 0), catalog.Counts(null, _ => false));
    }

    [Fact]
    public void Empty_catalog_answers_everything_with_nothing()
    {
        var catalog = UniqueRewardCatalog.Empty;

        Assert.Equal(0, catalog.Count);
        Assert.Empty(catalog.All);
        Assert.Empty(catalog.Kinds);
        Assert.Empty(catalog.ForQuest(1));
        Assert.Empty(catalog.View(null, _ => true));
        Assert.Equal(default, catalog.Counts(null, _ => true));
        Assert.Same(catalog, UniqueRewardCatalog.Build(UniqueRewardsData.Empty, new Dictionary<uint, UniqueOverride>(), CuratedData.Empty));
    }

    [Fact]
    public void Build_rejects_nulls()
    {
        Assert.Throws<ArgumentNullException>(() => UniqueRewardCatalog.Build(null!, new Dictionary<uint, UniqueOverride>(), CuratedData.Empty));
        Assert.Throws<ArgumentNullException>(() => UniqueRewardCatalog.Build(Shipped, null!, CuratedData.Empty));
        Assert.Throws<ArgumentNullException>(() => UniqueRewardCatalog.Build(Shipped, new Dictionary<uint, UniqueOverride>(), null!));
    }
}
