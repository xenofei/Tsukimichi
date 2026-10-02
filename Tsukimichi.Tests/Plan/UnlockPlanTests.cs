using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// The frozen catalog, its feature quests and the shipped unique-reward data, with a hand-made duty table standing in
/// for the ContentFinderCondition sheet (the fixture carries no content types; <see cref="DutyIndexTests"/> checks the
/// real sheet gives the same kinds). Built once per test class.
/// </summary>
public sealed class PlanFixture
{
    private readonly Lazy<(UnlockTags Tags, IReadOnlySet<uint> Features)> built;

    public PlanFixture()
    {
        Catalog = new FixtureCatalog();
        built = new Lazy<(UnlockTags, IReadOnlySet<uint>)>(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public FixtureCatalog Catalog { get; }

    public UnlockTags Tags => built.Value.Tags;

    public IReadOnlySet<uint> Features => built.Value.Features;

    /// <summary>The A Realm Reborn duties the tests name, as the ContentFinderCondition sheet types them.</summary>
    public static readonly PlanDuties Duties = PlanDuties.From(
    [
        new PlanDuty(5, 0, UnlockKind.Dungeon, "the Aurum Vale"),
        new PlanDuty(7, 0, UnlockKind.Dungeon, "Halatali"),
        new PlanDuty(9, 0, UnlockKind.Dungeon, "the Sunken Temple of Qarn"),
        new PlanDuty(10, 0, UnlockKind.Dungeon, "the Wanderer's Palace"),
        new PlanDuty(12, 0, UnlockKind.Dungeon, "Cutter's Cry"),
        new PlanDuty(13, 0, UnlockKind.Dungeon, "Dzemael Darkhold"),
        new PlanDuty(14, 0, UnlockKind.Dungeon, "Amdapor Keep"),
        new PlanDuty(17, 0, UnlockKind.Dungeon, "Pharos Sirius"),
        new PlanDuty(18, 0, UnlockKind.Dungeon, "Copperbell Mines (Hard)"),
        new PlanDuty(19, 0, UnlockKind.Dungeon, "Haukke Manor (Hard)"),
        new PlanDuty(20, 0, UnlockKind.Dungeon, "Brayflox's Longstop (Hard)"),
        new PlanDuty(21, 0, UnlockKind.Dungeon, "Halatali (Hard)"),
        new PlanDuty(22, 0, UnlockKind.Dungeon, "the Lost City of Amdapor"),
        new PlanDuty(59, 0, UnlockKind.Trial, "the Bowl of Embers (Hard)"),
        new PlanDuty(60, 0, UnlockKind.Trial, "the Navel (Hard)"),
        new PlanDuty(61, 0, UnlockKind.Trial, "the Howling Eye (Hard)"),
        new PlanDuty(74, 0, UnlockKind.Trial, "A Relic Reborn: the Chimera"),
        new PlanDuty(75, 0, UnlockKind.Trial, "A Relic Reborn: the Hydra"),
        new PlanDuty(92, 0, UnlockKind.AllianceRaid, "the Labyrinth of the Ancients"),
        new PlanDuty(93, 0, UnlockKind.NormalRaid, "the Binding Coil of Bahamut - Turn 1"),
        new PlanDuty(204, 0, UnlockKind.System, "the Palace of the Dead (Floors 51-60)"),
        new PlanDuty(205, 0, UnlockKind.System, "the Palace of the Dead (Floors 61-70)"),
    ]);

    public CatalogBundle Bundle => Catalog.Bundle;

    public BlockerNames Names => Bundle.BlockerNames();

    private (UnlockTags, IReadOnlySet<uint>) Build()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        IReadOnlySet<uint> features = FeaturePresets.Derive(Catalog.Bundle.Catalog, Catalog.Curated, unique.Entries);
        var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), Catalog.Curated);
        var names = Catalog.Bundle.BlockerNames();
        return (UnlockTags.Build(Catalog.Bundle.Catalog, features, rewards, Duties, names.Tribe), features);
    }

    /// <summary>A level 1 Gladiator who has done nothing.</summary>
    public static CharacterSnapshot Fresh(params uint[] completed) => new()
    {
        ContentId = 1,
        Name = "Michiru Tsukikage",
        CompletedBits = Evaluation.Fixture.Bits(completed),
        CurrentJob = Evaluation.Fixture.Gladiator,
        JobLevels = Evaluation.Fixture.Levels((Evaluation.Fixture.Gladiator, 1)),
        AchievementsLoaded = true,
    };

    public IReadOnlyDictionary<uint, QuestEvaluation> States(CharacterSnapshot snapshot) =>
        StateResolver.ResolveAll(Bundle.Catalog, snapshot, new EvalContext { ClassJobs = Bundle.Jobs });

    public UnlockPlan Plan(CharacterSnapshot snapshot) => UnlockPlan.Build(Tags, States(snapshot), Names);
}

public class UnlockPlanTests(PlanFixture fixture) : IClassFixture<PlanFixture>
{
    // A Realm Reborn unlock quests.
    private const uint HalloHalatali = 66233;
    private const uint BravingNewDepths = 66300;
    private const uint IfritBleeds = 66584;
    private const uint LabyrinthOfTheAncients = 66738;
    private const uint LegacyOfAllag = 67245;
    private const uint PrimalAwakening = 66695;
    private const uint ARecurringProblem = 66583;
    private const uint PaladinUnlock = 66591;
    private const uint BrotherhoodOfAsh = 66754;
    private const uint PeaceForThanalan = 66753;
    private const uint AnIllConceivedVenture = 66970;

    // A job quest that also opens a duty, quasi-quests and a deep dungeon's later floors.
    private const uint ARelicRebornBravura = 66655;
    private const uint ScratchItRich = 66024;
    private const uint LevesOfKugane = 68457;
    private const uint WhatLiesBeneath = 67923;

    // Heavensward aether current sidequests.
    private const uint BridgeOverFrozenWater = 67280;
    private const uint ClippedWings = 67284;

    private const uint WesternThanalan = 140;

    private static PlanUnlock Only(IReadOnlyList<PlanUnlock> unlocks) => Assert.Single(unlocks);

    [Fact]
    public void A_fresh_characters_plan_starts_with_A_Realm_Reborn_and_its_first_dungeons_are_the_lowest_level_ones()
    {
        var plan = fixture.Plan(PlanFixture.Fresh());

        Assert.False(plan.IsEmpty);
        Assert.Equal(0, plan.Expansions[0].Expansion);
        Assert.Equal("A Realm Reborn", plan.Expansions[0].Name);
        Assert.Equal(plan.Expansions.Select(e => e.Expansion).Order(), plan.Expansions.Select(e => e.Expansion));

        // Nothing done, nothing removed, nothing seasonal: every entry is a plan quest the character can still do.
        Assert.All(plan.Entries, e =>
        {
            Assert.True(fixture.Features.Contains(e.Quest.RowId));
            Assert.False(e.Quest.IsRemoved);
            Assert.Equal(0, e.Quest.Festival);
            Assert.True(UnlockPlan.IsLeft(e.State));
            Assert.NotEmpty(e.Unlocks);
            Assert.NotEmpty(e.StatusText);
        });

        // The dungeons: A Realm Reborn first, Halatali (level 20) before the Sunken Temple of Qarn (35), and nothing in
        // the expansion's dungeon block starts lower than the first entry.
        var dungeons = plan.Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Dungeon)));
        var arr = dungeons.Expansions[0];
        Assert.Equal(0, arr.Expansion);
        var first = arr.Entries.Take(2).Select(e => e.Quest.RowId).ToArray();
        Assert.Equal([HalloHalatali, BravingNewDepths], first);
        Assert.Equal(arr.Entries.Min(e => e.Quest.DisplayLevel), arr.Entries.First().Quest.DisplayLevel);
        Assert.Equal(20, arr.Entries.First().Quest.DisplayLevel);
        Assert.Equal(WesternThanalan, arr.Zones[0].TerritoryId);

        // Within each zone, story order: level, then journal position.
        foreach (var zone in plan.Expansions.SelectMany(e => e.Zones))
        {
            var keys = zone.Entries.Select(e => (e.Quest.DisplayLevel, e.Quest.Journal.SortKey)).ToArray();
            Assert.Equal(keys.Order(), keys);
        }
    }

    [Fact]
    public void Tags_a_dungeon_a_trial_an_alliance_raid_a_job_a_society_and_flying()
    {
        var tags = fixture.Tags;

        var dungeon = Only(tags.For(HalloHalatali));
        Assert.Equal(new PlanUnlock(UnlockKind.Dungeon, "Halatali"), dungeon);
        Assert.Equal("Dungeon: Halatali", dungeon.Label);

        Assert.Equal(new PlanUnlock(UnlockKind.Trial, "The Bowl of Embers (Hard)"), Only(tags.For(IfritBleeds)));
        Assert.Equal(new PlanUnlock(UnlockKind.NormalRaid, "The Binding Coil of Bahamut - Turn 1"), Only(tags.For(PrimalAwakening)));

        // Crystal Tower: the quest named after the raid unlocks it; the series' other steps lead to it.
        Assert.Equal(new PlanUnlock(UnlockKind.AllianceRaid, "The Labyrinth of the Ancients"), Only(tags.For(LabyrinthOfTheAncients)));
        Assert.Equal(new PlanUnlock(UnlockKind.AllianceRaid, string.Empty, Inherited: true), Only(tags.For(LegacyOfAllag)));

        // A primal step with no duty of its own is a trial step, like the rest of the Primal Quests.
        Assert.Equal(new PlanUnlock(UnlockKind.Trial, string.Empty, Inherited: true), Only(tags.For(ARecurringProblem)));

        // The Paladin unlock: the job named first, then the action it teaches; the soul crystal (an Other reward) dropped.
        var paladin = tags.For(PaladinUnlock);
        Assert.Equal(new PlanUnlock(UnlockKind.Job, "Paladin"), paladin[0]);
        Assert.All(paladin, u => Assert.Equal(UnlockKind.Job, u.Kind));

        // Allied society: the society quests carry the tribe; the first one, without a tribe, takes the genre's kind.
        Assert.Equal(new PlanUnlock(UnlockKind.Society, "Amalj'aa"), Only(tags.For(BrotherhoodOfAsh)));
        Assert.Equal(UnlockKind.Society, tags.For(PeaceForThanalan)[0].Kind);

        // Flying: an aether current, named by its zone.
        Assert.Equal(new PlanUnlock(UnlockKind.Flying, "Coerthas Western Highlands"), Only(tags.For(BridgeOverFrozenWater)));
        Assert.Equal(new PlanUnlock(UnlockKind.Flying, "The Sea of Clouds"), Only(tags.For(ClippedWings)));

        // A system unlock from system_unlocks.json.
        Assert.Contains(new PlanUnlock(UnlockKind.System, "Retainers"), tags.For(AnIllConceivedVenture));
    }

    [Fact]
    public void A_relic_step_is_Trial_and_Job_a_quasi_quest_is_a_system_and_floor_sets_are_one_dungeon()
    {
        var tags = fixture.Tags;

        // A Relic Reborn opens the Chimera and the Hydra (curated/duty_unlocks.json) and is a step of the warrior's
        // quest line: Trial first, Job with it.
        Assert.Equal(
            [
                new PlanUnlock(UnlockKind.Trial, "A Relic Reborn: the Chimera"),
                new PlanUnlock(UnlockKind.Trial, "A Relic Reborn: the Hydra"),
                new PlanUnlock(UnlockKind.Job, string.Empty, Inherited: true),
            ],
            tags.For(ARelicRebornBravura));

        // Quasi-quests: named by system_unlocks.json when curated, System by default otherwise, never Other.
        Assert.Equal(new PlanUnlock(UnlockKind.System, "Mini Cactpot"), Only(tags.For(ScratchItRich)));
        Assert.Equal(new PlanUnlock(UnlockKind.System, string.Empty), Only(tags.For(LevesOfKugane)));
        Assert.All(
            tags.Quests.Where(q => q.EventIconType == FeaturePresets.QuasiQuestEventIconType),
            q => Assert.Contains(tags.For(q.RowId), u => u.Kind != UnlockKind.Other));

        // What Lies Beneath opens fifteen floor sets of the Palace of the Dead: the plan names the dungeon once.
        Assert.Equal(new PlanUnlock(UnlockKind.System, "The Palace of the Dead"), Only(tags.For(WhatLiesBeneath)));
    }

    [Fact]
    public void Removed_and_seasonal_quests_are_never_plan_quests()
    {
        var catalog = fixture.Bundle.Catalog;
        var seasonal = catalog.All.Where(q => fixture.Features.Contains(q.RowId) && q.Festival != 0).ToList();
        Assert.NotEmpty(seasonal);
        Assert.All(seasonal, q => Assert.False(fixture.Tags.Contains(q.RowId)));
        Assert.All(fixture.Tags.Quests, q => Assert.False(q.IsRemoved));
        Assert.True(fixture.Tags.Count > 1500, $"only {fixture.Tags.Count} plan quests");
    }

    [Fact]
    public void Completed_quests_leave_the_plan()
    {
        var fresh = fixture.Plan(PlanFixture.Fresh());
        Assert.Contains(fresh.Entries, e => e.Quest.RowId == HalloHalatali);
        Assert.Contains(fresh.Entries, e => e.Quest.RowId == IfritBleeds);

        var done = fixture.Plan(PlanFixture.Fresh(HalloHalatali, IfritBleeds));
        Assert.DoesNotContain(done.Entries, e => e.Quest.RowId == HalloHalatali);
        Assert.DoesNotContain(done.Entries, e => e.Quest.RowId == IfritBleeds);
        Assert.Equal(fresh.Count - 2, done.Count);
        Assert.Equal(done.Count, done.Expansions.Sum(e => e.Count));
    }

    [Fact]
    public void Without_states_every_plan_quest_is_listed_as_not_checked()
    {
        var plan = UnlockPlan.Build(fixture.Tags, new Dictionary<uint, QuestEvaluation>(), fixture.Names);
        Assert.Equal(fixture.Tags.Count, plan.Count);
        Assert.All(plan.Entries, e => Assert.Equal(QuestState.Unknown, e.State));
        Assert.Equal(0, plan.ReadyCount);
    }

    [Fact]
    public void Filters_keep_kinds_ready_quests_and_the_expansions_in_reach()
    {
        var plan = fixture.Plan(PlanFixture.Fresh());
        Assert.True(plan.ReadyCount > 0);

        var ready = plan.Filter(new PlanFilter(ReadyOnly: true));
        Assert.Equal(plan.ReadyCount, ready.Count);
        Assert.All(ready.Entries, e => Assert.True(e.IsReady));

        // Sprout mode's reach: nothing past A Realm Reborn.
        var sprout = plan.Filter(new PlanFilter(MaxExpansion: 0));
        Assert.Single(sprout.Expansions);
        Assert.Equal(plan.Expansions[0].Count, sprout.Count);

        var trialsAndRaids = plan.Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Trial, UnlockKind.AllianceRaid)));
        Assert.All(trialsAndRaids.Entries, e => Assert.Contains(e.Unlocks, u => u.Kind is UnlockKind.Trial or UnlockKind.AllianceRaid));
        Assert.Contains(trialsAndRaids.Entries, e => e.Quest.RowId == LabyrinthOfTheAncients);
        Assert.DoesNotContain(trialsAndRaids.Entries, e => e.Quest.RowId == HalloHalatali);

        Assert.Same(plan, plan.Filter(PlanFilter.None));
        Assert.True(plan.Filter(new PlanFilter(0)).IsEmpty);
    }

    [Fact]
    public void The_checklist_is_Markdown_with_masked_names_and_no_character()
    {
        var names = fixture.Names with
        {
            QuestName = q => q.RowId == BravingNewDepths ? "Main scenario quest (Lv 35)" : q.Name,
        };
        var snapshot = PlanFixture.Fresh();
        var plan = UnlockPlan.Build(fixture.Tags, fixture.States(snapshot), names)
            .Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Dungeon), MaxExpansion: 0));

        string Zone(PlanZone zone) => zone.TerritoryId == WesternThanalan ? "Western Thanalan" : string.Empty;
        var text = PlanChecklist.Write(plan, Zone);
        var lines = text.Split('\n');

        Assert.Equal("# Clear my blues (" + plan.Count + ")", lines[0]);
        Assert.Contains("## A Realm Reborn (" + plan.Count + ")", lines);
        Assert.Contains("- [ ] Hallo Halatali (Lv 20, Western Thanalan) — unlocks: Dungeon: Halatali", lines);
        Assert.Contains("- [ ] Main scenario quest (Lv 35) (Lv 35, Western Thanalan) — unlocks: Dungeon: The Sunken Temple of Qarn", lines);
        Assert.DoesNotContain("Braving New Depths", text, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.Name, text, StringComparison.Ordinal);
        Assert.Equal(plan.Count, lines.Count(l => l.StartsWith("- [ ] ", StringComparison.Ordinal)));
        Assert.EndsWith(PlanChecklist.Footer + "\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Discord_checklist_has_plain_bullets_optional_links_and_no_link_on_a_masked_name()
    {
        var names = fixture.Names with
        {
            QuestName = q => q.RowId == BravingNewDepths ? "Main scenario quest (Lv 35)" : q.Name,
        };
        var plan = UnlockPlan.Build(fixture.Tags, fixture.States(PlanFixture.Fresh()), names)
            .Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Dungeon), MaxExpansion: 0));

        string Zone(PlanZone zone) => zone.TerritoryId == WesternThanalan ? "Western Thanalan" : string.Empty;
        var plain = PlanChecklist.WriteDiscord(plan, Zone);
        Assert.DoesNotContain("- [ ]", plain, StringComparison.Ordinal);
        Assert.Contains("- Hallo Halatali (Lv 20, Western Thanalan) — unlocks: Dungeon: Halatali", plain.Split('\n'));
        Assert.Equal(plan.Count, plain.Split('\n').Count(l => l.StartsWith("- ", StringComparison.Ordinal)));

        // The plugin's link function returns null for a masked quest: its line keeps the placeholder, unlinked.
        var linked = PlanChecklist.WriteDiscord(plan, Zone, e => e.Quest.RowId == BravingNewDepths ? null : "https://example.org/q/" + e.Quest.RowId);
        Assert.Contains("- [Hallo Halatali](<https://example.org/q/" + HalloHalatali + ">) (Lv 20, Western Thanalan)", linked, StringComparison.Ordinal);
        Assert.Contains("- Main scenario quest (Lv 35) (Lv 35, Western Thanalan)", linked, StringComparison.Ordinal);
        Assert.DoesNotContain("q/" + BravingNewDepths, linked, StringComparison.Ordinal);
        Assert.All(Core.Text.DiscordText.Parts(linked), p => Assert.True(p.Length <= Core.Text.MessageSplitter.DiscordLimit));
    }

    [Fact]
    public void The_checklist_names_inherited_kinds_as_leads_to_and_caps_long_lists()
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(LegacyOfAllag)!;
        var step = new PlanEntry(quest, quest.Name, QuestState.Blocked, "Blocked", fixture.Tags.For(LegacyOfAllag));
        Assert.Equal("- [ ] Legacy of Allag (Lv 50) — leads to: Alliance raid", PlanChecklist.Line(step, string.Empty));

        var many = new PlanEntry(quest, "A *starred* name", QuestState.Ready, "Ready",
        [
            new PlanUnlock(UnlockKind.Dungeon, "A"), new PlanUnlock(UnlockKind.Dungeon, "B"), new PlanUnlock(UnlockKind.Dungeon, "C"),
            new PlanUnlock(UnlockKind.Dungeon, "D"), new PlanUnlock(UnlockKind.System, "Retainers"),
        ]);
        Assert.Equal("- [ ] A \\*starred\\* name (Lv 50, Mor Dhona) — unlocks: Dungeon: A, B, C +1 more; System: Retainers", PlanChecklist.Line(many, "Mor Dhona"));
    }
}
