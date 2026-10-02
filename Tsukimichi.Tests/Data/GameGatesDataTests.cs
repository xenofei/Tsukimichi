using System.Globalization;
using System.Text.Json.Nodes;
using Lumina.Data;
using Lumina.Excel;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The shipped <c>curated/game_gates.json</c> against the frozen catalog, and what it does for a real character: the
/// gated quests never read Ready, and each is Blocked while its "after" quest is not done. His Dark Materia was
/// reported Ready in game (1.9.0) for a character who had not started the Zodiac line; the game gives it only to a
/// character wearing a relic weapon nexus. Also the audit that found it: a quest after the first of a chain with no
/// prerequisite at all is almost always a gate the data does not record.
/// </summary>
[Trait("Category", "Curated")]
public sealed class GameGatesDataTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint HisDarkMateria = 65897;
    private const uint SoulglazedRelics = 65742;
    private const uint WhereforeArtThouZodiac = 65892;
    private const uint MethodInHisMalice = 65895;
    private const uint Pagos = 68478;
    private const uint Pyros = 68148;
    private const uint Hydatos = 68149;
    private const uint EurekaAnemos = 68614;
    private const uint LightingTheWay = 68689;
    private const uint ItTakesAnEnclave = 68677;

    /// <summary>
    /// Quests after the first of their chain that have no prerequisite at all, with the reason each may stay so. Empty:
    /// every one found so far was a gate the data did not record.
    /// </summary>
    private static readonly Dictionary<uint, string> UnlinkedChainSteps = [];

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlyDictionary<uint, GameGate> Gates => fixture.Curated.GameGates;

    private EvalContext Context() =>
        EvalContextBuilder.Build(fixture.Curated.Festivals, fixture.Bundle.Jobs, static () => DateTime.UtcNow, jobParents: fixture.Bundle.JobParents());

    /// <summary>The real anonymised character of <c>Fixtures/snapshot-v1.json</c> (the one that reported His Dark Materia).</summary>
    private static CharacterSnapshot Character()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var snapshot = new JsonSnapshotStore(tmp.Path).Load(1);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    /// <summary><paramref name="snapshot"/> with <paramref name="rowIds"/> completed as well.</summary>
    private static CharacterSnapshot With(CharacterSnapshot snapshot, params uint[] rowIds)
    {
        var maxId = rowIds.Max(QuestRecord.ToQuestId);
        var bits = new byte[Math.Max(snapshot.CompletedBits.Length, (maxId >> 3) + 1)];
        snapshot.CompletedBits.CopyTo(bits, 0);
        foreach (var rowId in rowIds)
        {
            var id = QuestRecord.ToQuestId(rowId);
            bits[id >> 3] |= (byte)(1 << (id & 7));
        }

        return snapshot with { CompletedBits = bits };
    }

    /// <summary><paramref name="snapshot"/> with <paramref name="rowIds"/> not completed.</summary>
    private static CharacterSnapshot Without(CharacterSnapshot snapshot, params uint[] rowIds)
    {
        var bits = snapshot.CompletedBits.ToArray();
        foreach (var rowId in rowIds)
        {
            var id = QuestRecord.ToQuestId(rowId);
            if ((id >> 3) < bits.Length)
            {
                bits[id >> 3] &= (byte)~(1 << (id & 7));
            }
        }

        return snapshot with { CompletedBits = bits };
    }

    [Fact]
    public void The_file_is_sorted_and_every_gate_names_quests_of_the_catalog()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureCatalog.CuratedDir(), CuratedData.GameGatesFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var keys = root["entries"]!.AsObject().Select(kv => uint.Parse(kv.Key, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.Order().Distinct(), keys);
        Assert.Equal(keys.Count, Gates.Count);
        Assert.Equal([HisDarkMateria, Pyros, Hydatos, Pagos, LightingTheWay], Gates.Keys.Order());

        var byScript = Catalog.All.Where(q => q.InternalId.Length > 0).GroupBy(q => q.InternalId.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().RowId);
        var problems = new List<string>();
        foreach (var (rowId, gate) in Gates)
        {
            if (Catalog.GetByRowId(rowId) is not { } quest)
            {
                problems.Add($"{rowId} is not a quest of the catalog fixture");
                continue;
            }

            if (Catalog.GameGateOf(rowId)?.Gate != gate.Gate)
            {
                problems.Add($"{rowId}: the catalog does not carry its gate");
            }

            foreach (var id in gate.After)
            {
                if (Catalog.GetByRowId(id) is null)
                {
                    problems.Add($"{rowId} after {id}, which is not a quest of the catalog fixture");
                }
                else if (quest.PreviousQuests.QuestIds.Contains(id) || quest.AcceptConditions.Contains(id))
                {
                    problems.Add($"{rowId} after {id}, which the sheet already records; remove it");
                }
                else if (PrerequisiteCoverage.Requires(Catalog, id, rowId))
                {
                    problems.Add($"{rowId} after {id}, which itself waits for {rowId}: a cycle");
                }
                else if (!Catalog.PrerequisitesOf(quest).QuestIds.Contains(id))
                {
                    problems.Add($"{rowId} after {id}, which the catalog does not add to its prerequisites");
                }
            }

            // The text keys belong to the quests they speak for: the gated quest's own text, an after quest's text.
            if (gate.GameTextKey is { } key && (ExtraPrerequisitesGameTextTests.ScriptOf(key) is not { } script || byScript.GetValueOrDefault(script) != rowId))
            {
                problems.Add($"{rowId} gameTextKey {key} is not a row of the quest's own text");
            }

            if (gate.AfterTextKey is { } afterKey && (ExtraPrerequisitesGameTextTests.ScriptOf(afterKey) is not { } afterScript || !gate.After.Contains(byScript.GetValueOrDefault(afterScript))))
            {
                problems.Add($"{rowId} afterTextKey {afterKey} is not a row of an after quest's text");
            }

            if (!gate.Evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                problems.Add($"{rowId} evidence is not an https URL");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void His_Dark_Materia_is_Blocked_for_the_character_that_reported_it_Ready()
    {
        var context = Context();
        var michiru = Character();
        Assert.False(michiru.IsCompleted(QuestRecord.ToQuestId(SoulglazedRelics)));
        var quest = Catalog.GetByRowId(HisDarkMateria)!;

        var evaluation = StateResolver.Resolve(quest, michiru, Catalog, context);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        Assert.Contains(SoulglazedRelics, Catalog.PrerequisitesOf(quest).QuestIds);
        Assert.Equal(RequirementKind.PreviousQuests, evaluation.NextStep!.Req.Kind);
        Assert.Equal("Blocked · after: Mmmmmm, Soulglazed Relics", BlockerText.StatusText(evaluation, quest, fixture.Bundle.BlockerNames()));
        Assert.Contains(evaluation.Requirements, r => r.Req is GameGateRequirement { Gate: "a relic weapon nexus equipped" });

        // Once the nexus is possible, the game's own check is still one Tsukimichi cannot read: Not checked, never Ready.
        var nexusPossible = With(michiru, SoulglazedRelics, WhereforeArtThouZodiac, MethodInHisMalice);
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, nexusPossible, Catalog, context).State);
    }

    [Fact]
    public void Each_gated_quest_is_Blocked_without_its_after_quest_and_never_Ready()
    {
        var context = Context();
        var michiru = Character();
        foreach (var (rowId, gate) in Gates)
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var withoutAfter = Without(michiru, [.. gate.After, rowId]);
            var blocked = StateResolver.Resolve(quest, withoutAfter, Catalog, context);
            Assert.True(blocked.State == QuestState.Blocked, $"{rowId} {quest.Name} without {string.Join(", ", gate.After)} reads {blocked.State}");

            // Every other prerequisite done as well: still not Ready, the gate is not checked.
            var everything = With(Without(michiru, rowId), [.. Catalog.PrerequisitesOf(quest).QuestIds, .. gate.After]);
            var state = StateResolver.Resolve(quest, everything, Catalog, context).State;
            Assert.True(state is QuestState.Unknown or QuestState.Blocked, $"{rowId} {quest.Name} reads {state} with every prerequisite done");
        }
    }

    [Fact]
    public void The_reported_quests_no_longer_read_Ready_for_the_character()
    {
        var states = StateResolver.ResolveAll(Catalog, Character(), Context());

        // Blocked: the step that makes the gate possible is not done. Not checked: it is, and the rest cannot be read.
        Assert.Equal(QuestState.Blocked, states[HisDarkMateria].State);
        Assert.Equal(QuestState.Blocked, states[Pyros].State);
        Assert.Equal(QuestState.Blocked, states[Hydatos].State);
        Assert.Equal(QuestState.Completed, states[EurekaAnemos].State);
        Assert.Equal(QuestState.Unknown, states[Pagos].State);
        Assert.Equal(QuestState.Completed, states[ItTakesAnEnclave].State);
        Assert.Equal(QuestState.Unknown, states[LightingTheWay].State);
    }

    [Fact]
    public void Every_chain_step_after_the_first_has_a_prerequisite_or_an_allowlisted_reason()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var features = FeaturePresets.Derive(Catalog, fixture.Curated, unique.Entries);
        var chains = ChainCatalog.Build(Catalog, fixture.Curated, StorySidequests.Build(Catalog, features, fixture.Curated, unique.Entries));

        var unlinked = new List<string>();
        var seen = new HashSet<uint>();
        foreach (var chain in chains.Chains)
        {
            for (var i = 1; i < chain.RowIds.Count; i++)
            {
                var quest = Catalog.GetByRowId(chain.RowIds[i])!;
                if (Catalog.PrerequisitesOf(quest).QuestIds.Length > 0)
                {
                    continue;
                }

                seen.Add(quest.RowId);
                if (!UnlinkedChainSteps.ContainsKey(quest.RowId))
                {
                    unlinked.Add($"{chain.Name} step {i + 1}: {quest.RowId} {quest.Name}");
                }
            }
        }

        Assert.True(
            unlinked.Count == 0,
            "chain steps after the first with no prerequisite: likely a gate the data does not record (curated/extra_prerequisites.json or curated/game_gates.json), else allowlist with a reason:\n" + string.Join("\n", unlinked));
        Assert.All(UnlinkedChainSteps, kv => Assert.True(seen.Contains(kv.Key) && kv.Value.Length > 0, $"{kv.Key}: stale or reasonless allowlist entry"));
    }
}

/// <summary>
/// The text rows <c>curated/game_gates.json</c> cites, against the installed game: each exists in the sheet of the
/// quest it belongs to, and the gated quest's own row names the quest. The text is read, compared and never written.
/// </summary>
public sealed class GameGatesGameTextTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Every_cited_text_row_exists_and_the_quests_own_row_names_it()
    {
        var catalog = game.Bundle.Catalog;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var byScript = catalog.All.Where(q => q.InternalId.Length > 0).GroupBy(q => q.InternalId.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        var problems = new List<string>();
        var checkedCount = 0;
        foreach (var (rowId, gate) in curated.GameGates)
        {
            foreach (var key in new[] { gate.GameTextKey, gate.AfterTextKey }.OfType<string>())
            {
                if (ExtraPrerequisitesGameTextTests.ScriptOf(key) is not { } script || !byScript.TryGetValue(script, out var owner) || QuestTextReader.SheetName(owner.InternalId) is not { } sheetName)
                {
                    problems.Add($"{rowId}: {key} names no quest text sheet");
                    continue;
                }

                string? text = null;
                foreach (var row in game.Game.Excel.GetSheet<RawRow>(Language.English, sheetName))
                {
                    if (row.ReadStringColumn(0).ExtractText() == key)
                    {
                        text = row.ReadStringColumn(1).ExtractText();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    problems.Add($"{rowId}: {key} is not a row of {sheetName}");
                    continue;
                }

                var name = catalog.GetByRowId(rowId)!.Name.Trim();
                if (key == gate.GameTextKey && !text.Contains(name, StringComparison.Ordinal))
                {
                    problems.Add($"{rowId}: {key} does not name {name}");
                }

                checkedCount++;
            }
        }

        Assert.True(checkedCount >= 6, $"only {checkedCount} text rows checked");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
