using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Feature plan v5, decision 4 over the shipped <c>unique_quests.json</c> and the frozen catalog: no reward is counted
/// twice, the relic and special weapon quests fold to one row each with their repeatable twins, path variants collapse,
/// every soul crystal is an Item, and the anonymised character in <c>Fixtures/snapshot-v1.json</c> gets totals that
/// hide its other paths.
/// </summary>
public sealed class MoonlitTotalsDataTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>The repeatable "another job" relic quests that hand out their base quest's items again.</summary>
    private static readonly uint[] AnotherJobQuests = [69381, 70189, 70262, 70308, 70343];

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private UniqueRewardCatalog Rewards() =>
        UniqueRewardCatalog.Build(
            UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json")),
            new Dictionary<uint, UniqueOverride>(),
            fixture.Curated);

    private MoonlitGroups Groups(UniqueRewardCatalog rewards) =>
        MoonlitGroups.Build(rewards.All, id => Catalog.GetByRowId(id) is { IsRepeatable: true });

    [Fact]
    public void The_shipped_data_counts_no_reward_twice()
    {
        var rewards = Rewards();
        var groups = Groups(rewards);
        Assert.Equal(rewards.Count, groups.EntryCount);
        Assert.Equal(rewards.Count, groups.All.Sum(g => g.Entries.Count));

        // Every (kind, reward) pair lives in exactly one counted group.
        var owner = new Dictionary<RewardKey, int>();
        var twice = new List<string>();
        foreach (var group in groups.All)
        {
            foreach (var key in group.Entries.Select(RewardKey.Of).Distinct())
            {
                if (!owner.TryAdd(key, group.Index) && owner[key] != group.Index)
                {
                    twice.Add($"{key.Kind} {key.RewardId} {key.ItemId} {key.Name}");
                }
            }
        }

        Assert.True(twice.Count == 0, "counted in two groups: " + string.Join(", ", twice));

        // 2,635 entries less the 195 that repeat another row's (kind, reward), the relic items folded per quest; 1.19 (K5)
        // added the three city variants of guildleves and inn rooms and the second Thaumaturge class quest.
        var duplicates = rewards.All.Count - rewards.All.Select(RewardKey.Of).Distinct().Count();
        output.WriteLine($"entries {rewards.Count}, repeated (kind, reward) rows {duplicates}, counted rewards {groups.All.Count}");
        Assert.Equal(195, duplicates);
    }

    [Fact]
    public void Each_relic_or_special_weapon_quest_counts_once_with_its_repeatable_twin()
    {
        var groups = Groups(Rewards());
        var sets = groups.All.Where(g => g.Kind == RewardKind.ArtifactGear).ToList();

        Assert.All(sets, s => Assert.True(s.IsChoice));
        Assert.Equal(37 - AnotherJobQuests.Length, sets.Count);
        Assert.All(sets, s => Assert.InRange(s.Choices, 11, 22));
        Assert.All(sets, s => Assert.DoesNotContain(s.Quests[0], AnotherJobQuests));
        foreach (var again in AnotherJobQuests)
        {
            var set = Assert.Single(sets, s => s.Quests.Contains(again));
            Assert.Equal(2, set.Quests.Count);
            Assert.Equal(set.Choices * 2, set.Entries.Count);
        }
    }

    [Fact]
    public void Path_variants_fold_into_one_reward()
    {
        var groups = Groups(Rewards());
        MoonlitGroup Named(RewardKind kind, string name) =>
            Assert.Single(groups.All, g => g.Kind == kind && string.Equals(g.Primary.RewardName, name, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(3, Named(RewardKind.SystemUnlock, "Guildhests").Quests.Count);
        Assert.Equal(3, Named(RewardKind.SystemUnlock, "Grand Company enrollment").Quests.Count);
        Assert.Equal(3, Named(RewardKind.SystemUnlock, "Hunts (ARR)").Quests.Count);
        Assert.Equal(3, Named(RewardKind.ClassJob, "archer").Quests.Count);
        Assert.Equal(9, Assert.Single(groups.All, g => g.Kind == RewardKind.Achievement && g.Primary.RewardId == 313).Quests.Count);
    }

    /// <summary>
    /// A system unlock has no reward id, so its curated label is its key: two quests that share a label count once.
    /// That is right only for quests that are each other's alternatives (one per city, starting class or Grand
    /// Company, or a QuestLock set: <see cref="PathIndex"/> puts them on disjoint options of one group); tiers of one
    /// system (the 2★ and 3★ hunt bills, the later sightseeing entries) must carry labels of their own.
    /// </summary>
    [Fact]
    public void Quests_share_a_system_unlock_only_when_they_are_alternatives()
    {
        var index = PathIndex.For(Catalog);
        bool Alternatives(uint a, uint b)
        {
            foreach (var tag in index.TagsOf(a))
            {
                foreach (var other in index.TagsOf(b))
                {
                    if (tag.Group == other.Group && (tag.Options & other.Options) == 0)
                    {
                        return true;
                    }
                }
            }

            // A Grand Company's own version of a quest the sheet tags with that company.
            return Catalog.GetByRowId(a) is { GrandCompany: not 0 } qa && Catalog.GetByRowId(b) is { GrandCompany: not 0 } qb
                && qa.GrandCompany != qb.GrandCompany;
        }

        var groups = Groups(Rewards());
        var offenders = new List<string>();
        var shared = 0;
        foreach (var group in groups.All.Where(g => g.Kind == RewardKind.SystemUnlock && g.Quests.Count > 1))
        {
            shared++;
            for (var i = 0; i < group.Quests.Count; i++)
            {
                for (var j = i + 1; j < group.Quests.Count; j++)
                {
                    if (!Alternatives(group.Quests[i], group.Quests[j]))
                    {
                        offenders.Add($"\"{group.Primary.RewardName}\": {group.Quests[i]} and {group.Quests[j]}");
                    }
                }
            }
        }

        output.WriteLine($"{shared} system unlocks given by more than one quest");
        Assert.True(offenders.Count == 0, "quests that are not alternatives share a system unlock label (give each tier its own label in curated/system_unlocks.json):"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));

        // The real alternatives keep sharing one reward.
        Assert.True(shared >= 8, $"{shared} shared system unlocks");
    }

    [Fact]
    public void Every_soul_crystal_is_an_item()
    {
        var crystals = Rewards().All.Where(e => e.RewardName.StartsWith("Soul of the ", StringComparison.Ordinal)).ToList();
        Assert.True(crystals.Count >= 23, $"{crystals.Count} soul crystals");
        Assert.All(crystals, e =>
        {
            Assert.Equal(RewardKind.Item, e.Kind);
            Assert.NotEqual(0u, e.ItemId);
            Assert.Equal(e.ItemId, e.RewardId);
        });
    }

    [Fact]
    public void The_fixture_character_counts_each_reward_once_and_none_of_its_other_paths()
    {
        var snapshot = LoadSnapshot();
        var context = EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());
        var states = StateResolver.ResolveAll(Catalog, snapshot, context);
        var rewards = Rewards();
        var groups = Groups(rewards);
        var availability = AvailabilityContext.For(Catalog, fixture.Curated.Festivals, ServerFestivals.Of(snapshot), DateTime.UtcNow);

        // What RewardUnlockReader answers for a stored character: quest-following kinds from the completion bit,
        // flag-backed kinds unknown.
        bool? Obtained(UniqueRewardEntry e) => e.Kind is RewardKind.Action or RewardKind.GeneralAction or RewardKind.Trait
            or RewardKind.ClassJob or RewardKind.BlueMageSpell or RewardKind.SystemUnlock
            ? snapshot.IsCompleted(QuestRecord.ToQuestId(e.QuestRowId))
            : null;

        var (groupStates, totals) = MoonlitTally.Run(
            groups,
            Obtained,
            id => states.GetValueOrDefault(id),
            (e, evaluation) => RewardAvailabilities.Classify(e, Catalog.GetByRowId(e.QuestRowId), evaluation, availability),
            default);
        var gone = MoonlitTally.Totals(groups.All, groupStates, new MoonlitCountOptions(CountGone: true));

        var offPath = groupStates.Count(s => !s.OnPath);
        var byAvailability = groupStates.Where(s => s.OnPath).GroupBy(s => s.Availability.Kind).OrderBy(g => g.Key)
            .Select(g => $"{g.Key} {g.Count()}");
        output.WriteLine($"entries {rewards.Count}; counted rewards {groups.All.Count}; off the character's path {offPath}");
        output.WriteLine($"totals {totals.All.Obtained}/{totals.All.Total} ({totals.All.Unknown} unknown); with gone rewards {gone.All.Obtained}/{gone.All.Total}; missed {totals.Missed}");
        output.WriteLine("availability: " + string.Join(", ", byAvailability));
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            var counts = totals.For(kind);
            if (counts.Total > 0)
            {
                output.WriteLine($"  {kind}: {counts.Obtained}/{counts.Total} ({counts.Unknown} unknown)");
            }
        }

        // The character started in one city: two of the three Guildhests quests are another path, one reward counts.
        var guildhests = Assert.Single(groups.All, g => g.Kind == RewardKind.SystemUnlock && g.Primary.RewardName == "Guildhests");
        var shown = groupStates[guildhests.Index];
        Assert.True(shown.OnPath);
        Assert.False(MoonlitTally.IsOffPath(states.GetValueOrDefault(guildhests.Quests[shown.Representative])));
        Assert.Equal(1, guildhests.Quests.Count(q => !MoonlitTally.IsOffPath(states.GetValueOrDefault(q))));

        Assert.True(offPath > 0, "the character has paths not taken");
        Assert.True(totals.All.Total <= gone.All.Total);
        Assert.Equal(gone.All.Total - totals.All.Total, totals.Missed);
        Assert.True(totals.All.Total <= groups.All.Count - offPath);
        Assert.Equal(groups.All.Count - offPath, gone.All.Total);
    }

    private static CharacterSnapshot LoadSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var snapshot = new JsonSnapshotStore(tmp.Path).Load(1);
        Assert.NotNull(snapshot);
        return snapshot;
    }
}
