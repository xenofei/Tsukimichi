using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Sheets = Lumina.Excel.Sheets;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The silent-Ready holes (1.11.0, C2) against the frozen catalog and a real character: the five mount-collection
/// quests and the quests the sheet gives a mount or a house never read Ready when nothing was read to say so, and the
/// collection quests read Blocked, naming what is missing, while the set is not complete.
/// </summary>
[Trait("Category", "Curated")]
public sealed class MountGatesDataTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint MyFeistyLittleChocobo = 66698;
    private const uint MagiteknicalDifficulties = 65700;
    private const uint BirdInHand = 67096;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context() =>
        EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());

    /// <summary>The real character with every prerequisite of <paramref name="quest"/> done and <paramref name="quest"/> itself not.</summary>
    private CharacterSnapshot ReadyFor(QuestRecord quest)
    {
        var prerequisites = Catalog.PrerequisitesOf(quest).QuestIds;
        var michiru = GameGatesDataTests.Without(GameGatesDataTests.Character(), quest.RowId);
        return prerequisites.Length == 0 ? michiru : GameGatesDataTests.With(michiru, [.. prerequisites]);
    }

    private static CharacterSnapshot Owning(CharacterSnapshot s, IEnumerable<uint> owned, IEnumerable<uint> missing) =>
        s with { Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = [.. owned.Order()], Missing = [.. missing.Order()] } } };

    [Fact]
    public void Every_mount_gate_names_seven_mounts_and_the_catalog_watches_them()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        foreach (var rowId in GameGatesDataTests.MountGates)
        {
            var set = fixture.Curated.GameGates[rowId].Mounts;
            Assert.NotNull(set);
            Assert.Equal(7, set.All.Length);
            Assert.Equal(set.All, Catalog.GameGateOf(rowId)!.Mounts!);
            Assert.All(set.All, id => Assert.Contains(id, Catalog.MountWatch));

            // The source is the collection mount the quest itself rewards, as the shipped reward data has it.
            var reward = Assert.Single(unique.Entries, e => e.QuestRowId == rowId && e.Kind == RewardKind.Mount);
            Assert.Equal(["Mount#" + reward.RewardId.ToString(CultureInfo.InvariantCulture)], set.Sources);
            Assert.DoesNotContain(reward.RewardId, set.All);
        }

        // The sheet's own mount gates: the company chocobo and the magitek armor.
        Assert.Equal(1u, Catalog.GetByRowId(MyFeistyLittleChocobo)!.MountRequired);
        Assert.Equal(6u, Catalog.GetByRowId(MagiteknicalDifficulties)!.MountRequired);
        Assert.Contains(1u, Catalog.MountWatch);
        Assert.Contains(6u, Catalog.MountWatch);
        Assert.True(Catalog.GetByRowId(BirdInHand)!.HouseRequired);
    }

    [Fact]
    public void The_mount_collection_quests_wait_for_the_whole_set()
    {
        var context = Context();
        foreach (var rowId in GameGatesDataTests.MountGates)
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var mounts = Catalog.GameGateOf(rowId)!.Mounts!;
            var ready = ReadyFor(quest);

            // Nothing read (a capture from before 1.11.0): never Ready. This read Ready from level 1 before.
            var unread = StateResolver.Resolve(quest, ready, Catalog, context);
            Assert.True(unread.State is QuestState.Unknown or QuestState.Blocked, $"{rowId} {quest.Name} reads {unread.State} with no mount read");
            Assert.Contains(unread.Requirements, r => r.Req is MountRequirement { HasMount: null });

            // One of the seven missing: Blocked by the collection, naming it.
            var oneShort = StateResolver.Resolve(quest, Owning(ready, mounts[1..], [mounts[0]]), Catalog, context);
            Assert.Equal(QuestState.Blocked, oneShort.State);
            var mount = Assert.Single(oneShort.Requirements, r => r.Req is MountRequirement);
            Assert.False(mount.Met);
            Assert.EndsWith($"you have 6 of 7 (missing: mount {mounts[0]})", mount.Detail, StringComparison.Ordinal);

            // The whole set: the collection is met.
            var all = RequirementEvaluator.Evaluate(quest, Owning(ready, mounts, []), Catalog, context);
            Assert.True(Assert.Single(all, r => r.Req is MountRequirement).Met);
        }
    }

    [Fact]
    public void The_sheet_mount_and_house_quests_never_read_a_silent_Ready()
    {
        var context = Context();
        foreach (var rowId in new[] { MyFeistyLittleChocobo, MagiteknicalDifficulties, BirdInHand })
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var state = StateResolver.Resolve(quest, ReadyFor(quest), Catalog, context).State;
            Assert.True(state is QuestState.Unknown or QuestState.Blocked, $"{rowId} {quest.Name} reads {state} with nothing read");
        }

        // The company chocobo owned: the mount is met; not owned: it blocks.
        var chocobo = Catalog.GetByRowId(MyFeistyLittleChocobo)!;
        var owned = RequirementEvaluator.Evaluate(chocobo, Owning(ReadyFor(chocobo), [1], []), Catalog, context);
        Assert.True(Assert.Single(owned, r => r.Req is MountRequirement).Met);
        var missing = StateResolver.Resolve(chocobo, Owning(ReadyFor(chocobo), [], [1]), Catalog, context);
        Assert.Equal(QuestState.Blocked, missing.State);
    }
}

/// <summary>
/// The mount-collection gates of <c>curated/game_gates.json</c> against the installed game (1.11.0, C2): each gate's
/// mounts are exactly the collection its source mount crowns in the Mount sheet (the mounts in its <c>UIPriority</c>
/// hundred with a ones digit: 30100 to 30109 for the Firebird's 30150), each a named mount, and the quest's script
/// checks owned mounts (<c>CheckMountAcquire</c>). The sheet's own mount gates come through the catalog as row ids.
/// </summary>
public sealed class MountGatesGameDataTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Every_mount_gate_lists_exactly_the_collection_of_its_source_mount()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var mounts = game.Game.GetExcelSheet<Sheets.Mount>()!;
        var quests = game.Game.GetExcelSheet<Sheets.Quest>()!;
        var problems = new List<string>();
        var gates = 0;
        foreach (var (rowId, gate) in curated.GameGates)
        {
            if (gate.Mounts is not { } set)
            {
                continue;
            }

            gates++;
            var derived = new SortedSet<uint>();
            foreach (var source in set.Sources)
            {
                var reward = uint.Parse(source["Mount#".Length..], CultureInfo.InvariantCulture);
                var priority = (int)mounts.GetRow(reward).UIPriority;
                if (priority % 100 != 50)
                {
                    problems.Add($"{rowId}: {source} has UIPriority {priority}, not a collection reward (x50)");
                    continue;
                }

                foreach (var mount in mounts)
                {
                    var p = (int)mount.UIPriority;
                    if (p / 100 == priority / 100 && p % 100 < 10 && mount.Singular.ExtractText().Length > 0)
                    {
                        derived.Add(mount.RowId);
                    }
                }
            }

            if (!derived.SequenceEqual(set.All))
            {
                problems.Add($"{rowId}: lists [{string.Join(", ", set.All)}], its sources give [{string.Join(", ", derived)}]");
            }

            foreach (var id in set.All)
            {
                if (mounts.GetRowOrDefault(id) is not { } row || row.Singular.ExtractText().Length == 0)
                {
                    problems.Add($"{rowId}: {id} is no named Mount row");
                }
            }

            var script = GameGateItemsGameDataTests.ScriptStrings(game.Game, quests.GetRow(rowId).Id.ExtractText());
            if (!script.Contains("CheckMountAcquire"))
            {
                problems.Add($"{rowId}: the quest's script does not check owned mounts (CheckMountAcquire)");
            }
        }

        Assert.Equal(GameGatesDataTests.MountGates.Length, gates);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [GameDataFact]
    public void The_sheet_mount_gates_reach_the_catalog_as_named_mounts()
    {
        var bundle = game.Bundle;
        var quests = game.Game.GetExcelSheet<Sheets.Quest>()!;
        var withMount = quests.Where(q => q.MountRequired.RowId != 0 && bundle.Catalog.GetByRowId(q.RowId) is not null).ToList();

        Assert.NotEmpty(withMount);
        foreach (var quest in withMount)
        {
            Assert.Equal(quest.MountRequired.RowId, bundle.Catalog.GetByRowId(quest.RowId)!.MountRequired);
        }

        Assert.All(bundle.Catalog.MountWatch, id => Assert.False(string.IsNullOrEmpty(bundle.MountName(id)), $"mount {id} has no name"));
        Assert.Equal("company chocobo", bundle.MountName(1), ignoreCase: true);
    }
}
