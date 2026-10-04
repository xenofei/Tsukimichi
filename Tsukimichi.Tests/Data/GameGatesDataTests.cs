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
    /// The relic weapon steps the game offers only to a character wearing (or holding) the weapon at a stage, from each
    /// quest's own text and script: Zodiac, Anima, Resistance, Manderville and Phantom weapons.
    /// </summary>
    internal static readonly uint[] GearGates =
    [
        65742, 65892, HisDarkMateria, 66096, 66097, 66971, 66972, 66998, 67000, 67823,
        67749, 67750, 67820, 67864, 67915, 67932, 67934, 67940,
        69506, 69507, 69574, 69576, 69637,
        70261, 70262, 70307, 70308, 70342, 70343,
        70918, 70992, 71040, 71041,

        // Relic tools (feature plan v7 C3): Skysteel and Splendorous, held.
        69429, 69430, 69520, 70267, 70268, 70304, 70305, 70339, 70340, 70341,
    ];

    /// <summary>
    /// The unlock-link gates (1.19, C3), judged from the links a capture reads: the chocobo companion before two main
    /// scenario quests, the Occult Record entries and the Forked Tower, and the blue magic each blue mage quest asks for;
    /// since 1.21 the Palace of the Dead floor clears the quest's own row names (The Nightmare's End, What Lies Beneath,
    /// Dead but Not Gone).
    /// </summary>
    internal static readonly uint[] UnlockLinkGates =
    [
        67199, 69307, 70057,
        70851, 70852, 70853, 71054,
        68730, 68731, 68732, 68733, 68734, 69269, 69270, 69271, 69272, 69526, 69527, 69528, 69529, 70310, 70311, 70312,
        67093, 67923, 67924,
    ];

    /// <summary>
    /// The 1.19 gates Tsukimichi cannot read (C3): deep-dungeon floors, Resistance ranks and mettle; and the Dun Scaith
    /// raid unlocked, met by the quest that opens it. Since 1.21 also the beasts a beastmaster has befriended (Free for
    /// All, Mastery Rematch).
    /// </summary>
    internal static readonly uint[] UnreadGates = [68667, 68668, 70199, 70941, 70942, 69481, 69482, 69483, 69484, 69485, 69486, 69487, 69563, 69564, 67016, 71036, 71037];

    /// <summary>
    /// The 1.22.0 gates (owner rulings of 2026-10-04), never judged, so each quest reads Not checked until the player says
    /// it is done: the Eureka Orthos, Pilgrim's Traverse and variant-dungeon record gates the Lodestone's requirement
    /// line confirms, The Adventurer with All the Cards that Questionable confirms, the Island Sanctuary ranks the wiki
    /// alone states (player-confirmed), and Abridged Too Far, which A Spellbinding Read's own text confirms.
    /// </summary>
    internal static readonly uint[] ConfirmGates = [70200, 70201, 70943, 70185, 70270, 70326, 70978, 69617, 70181, 70265, 70299, 70995];

    /// <summary>
    /// The mount-collection quests (1.11.0, C2), each offered only once the seven extreme-trial mounts of its expansion
    /// are owned: Fiery Wings, Fiery Hearts (Firebird), A Lone Wolf No More (Kamuy of the Nine Tails), The Dragon Made
    /// (Landerwaffe), Wings of Hope (apocryphal Bahamut) and The Wing Spirit Cometh (wings of legacy).
    /// </summary>
    internal static readonly uint[] MountGates = [67086, 68736, 69593, 70331, 71005];

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
    internal static CharacterSnapshot Character()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.Path, "characters", "1.json"));
        var snapshot = new JsonSnapshotStore(tmp.Path).Load(1);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    /// <summary><paramref name="snapshot"/> with <paramref name="rowIds"/> completed as well.</summary>
    internal static CharacterSnapshot With(CharacterSnapshot snapshot, params uint[] rowIds)
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
    internal static CharacterSnapshot Without(CharacterSnapshot snapshot, params uint[] rowIds)
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
        Assert.Equal(GearGates.Concat(MountGates).Concat(UnlockLinkGates).Concat(UnreadGates).Concat(ConfirmGates).Concat([Pyros, Hydatos, Pagos, LightingTheWay]).Order(), Gates.Keys.Order());
        Assert.Equal(UnlockLinkGates.Order(), Gates.Where(kv => kv.Value.UnlockLinks is not null).Select(kv => kv.Key).Order());
        Assert.Equal(GearGates.Order(), Gates.Where(kv => kv.Value.Items is not null).Select(kv => kv.Key).Order());
        Assert.Equal(MountGates.Order(), Gates.Where(kv => kv.Value.Mounts is not null).Select(kv => kv.Key).Order());

        var byScript = Catalog.All.Where(q => q.InternalId.Length > 0).GroupBy(q => q.InternalId.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().RowId);
        var lodestoneHashes = LodestoneHashes();
        var locks = QuestionableLocks.Load(Path.Combine(ExtraPrerequisitesDataTests.DocsDataDir(), QuestionableLocks.FileName));
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

            if (gate.RequiredTextKey is { } requiredKey
                && (ExtraPrerequisitesGameTextTests.ScriptOf(requiredKey) is not { } requiredScript
                    || byScript.GetValueOrDefault(requiredScript) is not (> 0 and var owner)
                    || !(quest.PreviousQuests.QuestIds.Contains(owner) || quest.AcceptConditions.Contains(owner))))
            {
                problems.Add($"{rowId} requiredTextKey {requiredKey} is not a row of a quest the sheet requires");
            }

            // Two sources (the C3 rule, 1.22.0 sources added): the game's text, the sheets, the wiki, the Lodestone,
            // Questionable; the player only where the wiki alone states a gate that is never judged.
            if (gate.SourceKinds.Count < 2)
            {
                problems.Add($"{rowId} is confirmed by {gate.SourceKinds.Count} source(s) ({string.Join(", ", gate.SourceKinds)}); every gate needs two of the game's text, the sheets, the wiki, the Lodestone and Questionable");
            }

            // The Lodestone source is the quest's own Lodestone page, as the verifier matched it (external_ids.json).
            if (gate.Lodestone is { } page && (!lodestoneHashes.TryGetValue(rowId, out var hash) || page != CuratedData.LodestoneQuestPrefix + hash + "/"))
            {
                problems.Add($"{rowId} lodestone {page} is not the quest's own Lodestone page ({(lodestoneHashes.TryGetValue(rowId, out var known) ? known : "none known")})");
            }

            // The Questionable source holds only for a quest Questionable holds back on a check of its own.
            if (gate.Questionable && locks.Of(rowId) is null)
            {
                problems.Add($"{rowId} cites Questionable, but docs/data/questionable-locks.json lists no lock for it");
            }

            // The player stands in for a second source only where the wiki alone states a gate Tsukimichi never judges.
            if (gate.PlayerConfirmed is not null && (!gate.NeverJudged || !gate.SourceKinds.SequenceEqual([QuestGate.WikiSource, QuestGate.PlayerSource])))
            {
                problems.Add($"{rowId} is player-confirmed, but it is judged or has a source besides the wiki ({string.Join(", ", gate.SourceKinds)})");
            }

            if (!Catalog.GameGateOf(rowId)!.Sources.SequenceEqual(gate.SourceKinds))
            {
                problems.Add($"{rowId}: the catalog does not carry its sources");
            }

            // A met-by quest is one of the catalog, and not one the sheet already requires (it would always be done).
            foreach (var id in gate.MetByIds)
            {
                if (Catalog.GetByRowId(id) is null)
                {
                    problems.Add($"{rowId} metBy {id}, which is not a quest of the catalog fixture");
                }
                else if (quest.PreviousQuests.QuestIds.Contains(id))
                {
                    problems.Add($"{rowId} metBy {id}, which the sheet already requires");
                }
            }

            // The accept conditions a gate stands for are the quest's own.
            var fromAccept = gate.UnlockLinks is { } set && set.Sources.Any(src => src.StartsWith("QuestAcceptAdditionCondition#", StringComparison.Ordinal)) ? set.All : [];
            foreach (var id in gate.AcceptConditionIds.Concat(fromAccept))
            {
                if (!quest.AcceptConditions.Contains(id))
                {
                    problems.Add($"{rowId}: {id} is not one of its accept conditions [{string.Join(", ", quest.AcceptConditions)}]");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// The 1.22.0 gates never read Ready: each is never judged, so the quest reads Not checked (with "I've done this")
    /// until the player confirms it, and every one is confirmed by the source its ruling names.
    /// </summary>
    [Fact]
    public void The_1_22_gates_are_never_judged_and_carry_the_ruled_sources()
    {
        foreach (var rowId in ConfirmGates)
        {
            var gate = Gates[rowId];
            Assert.True(gate.NeverJudged, $"{rowId} is judged");
            Assert.Contains(QuestGate.WikiSource, gate.SourceKinds);
        }

        Assert.Equal([70185u, 70200, 70201, 70270, 70326, 70943, 70978], Gates.Where(kv => kv.Value.Lodestone is not null).Select(kv => kv.Key).Order().ToArray());
        Assert.Equal([69617u], Gates.Where(kv => kv.Value.Questionable).Select(kv => kv.Key).Order().ToArray());
        Assert.Equal([70181u, 70265, 70299], Gates.Where(kv => kv.Value.PlayerConfirmed is not null).Select(kv => kv.Key).Order().ToArray());
        Assert.Equal([70995u], Gates.Where(kv => kv.Value.RequiredTextKey is not null).Select(kv => kv.Key).Order().ToArray());
    }

    /// <summary>Quest row id to its Lodestone page hash, from <c>Tsukimichi/Data/external_ids.json</c> (the verifier's match).</summary>
    private static Dictionary<uint, string> LodestoneHashes()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureCatalog.ShippedDataDir(), "external_ids.json")))!.AsObject();
        return root["quests"]!.AsObject()
            .Where(kv => kv.Value is JsonArray { Count: > 0 } a && ((string?)a[0])?.Length > 0)
            .ToDictionary(kv => uint.Parse(kv.Key, CultureInfo.InvariantCulture), kv => (string)kv.Value!.AsArray()[0]!);
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
        foreach (var (rowId, gate) in Gates.Where(kv => kv.Value.After.Count > 0))
        {
            var quest = Catalog.GetByRowId(rowId)!;
            if (michiru.Accepted.Any(a => a.QuestId == quest.QuestId))
            {
                // In the character's journal (Delve into Myth): the game let it through, and the journal state wins.
                continue;
            }

            var withoutAfter = Without(michiru, [.. gate.After, .. gate.MetByIds, rowId]);
            var blocked = StateResolver.Resolve(quest, withoutAfter, Catalog, context);
            Assert.True(blocked.State == QuestState.Blocked, $"{rowId} {quest.Name} without {string.Join(", ", gate.After)} reads {blocked.State}");

            // Every other prerequisite done as well: still not Ready, the gate is not checked; unless a quest that shows
            // it passed is among them (the Dun Scaith raid is unlocked by the very quest the gate waits for).
            var everything = With(Without(michiru, [rowId, .. gate.MetByIds.Except(gate.After)]), [.. Catalog.PrerequisitesOf(quest).QuestIds, .. gate.After]);
            var state = StateResolver.Resolve(quest, everything, Catalog, context).State;
            var proven = gate.MetByIds.Any(gate.After.Contains);
            Assert.True(
                proven ? state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked : state is QuestState.Unknown or QuestState.Blocked,
                $"{rowId} {quest.Name} reads {state} with every prerequisite done");
        }
    }

    /// <summary>
    /// The Palace of the Dead gates of 1.21 (The Nightmare's End and What Lies Beneath after floor 50, Dead but Not Gone
    /// after floor 100) are the links their own Quest rows name, judged from the capture like any unlock link: met with the
    /// link set, unmet without it, and not checked by a capture that did not read it. Dead but Not Gone also waits for
    /// What Lies Beneath, which opens the floors past 50.
    /// </summary>
    [Fact]
    public void The_Palace_of_the_Dead_floor_gates_are_judged_from_the_links_their_rows_name()
    {
        var context = Context();
        var michiru = Character();
        var watch = Catalog.GateUnlockLinkWatch;
        Assert.Contains(67923u, Catalog.PrerequisitesOf(Catalog.GetByRowId(67924)!).QuestIds);
        foreach (var (rowId, link) in new (uint RowId, uint Link)[] { (67093, 320), (67923, 320), (67924, 327) })
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var gate = Catalog.GameGateOf(rowId)!;
            Assert.Equal([link], Assert.IsType<uint[]>(gate.UnlockLinks));
            Assert.Contains(link, watch);

            var ready = With(Without(michiru, [rowId, .. gate.MetBy]), [.. Catalog.PrerequisitesOf(quest).QuestIds]);
            GameGateRequirement Gate(CharacterSnapshot s, out bool met)
            {
                var r = RequirementEvaluator.Evaluate(quest, s, Catalog, context).Single(r => r.Req.Kind == RequirementKind.GameGate);
                met = r.Met;
                return Assert.IsType<GameGateRequirement>(r.Req);
            }

            var set = ready with { GateUnlockLinks = new CollectibleSet { Owned = [link], Missing = [.. watch.Where(l => l != link)] } };
            Assert.True(Gate(set, out var metWithLink).Judged);
            Assert.True(metWithLink, $"{rowId} {quest.Name}: the gate is unmet with link {link} set");

            var unset = ready with { GateUnlockLinks = new CollectibleSet { Owned = [.. watch.Where(l => l != link)], Missing = [link] } };
            Assert.Equal([link], Gate(unset, out var metWithout).MissingLinks);
            Assert.False(metWithout, $"{rowId} {quest.Name}: the gate is met without link {link}");
            Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, unset, Catalog, context).State);

            Assert.True(Gate(ready with { GateUnlockLinks = null }, out _).IsNotChecked);
        }
    }

    /// <summary>A capture against the catalog's watch list with <paramref name="equipped"/> worn and <paramref name="held"/> carried as well.</summary>
    private CharacterSnapshot Wearing(CharacterSnapshot s, uint[] equipped, params uint[] held) =>
        s with { GateItems = new GateItemCapture(Catalog.GateItemFingerprint, [.. equipped.Order()], [.. equipped.Concat(held).Distinct().Order()]) };

    [Fact]
    public void Every_gear_gate_lists_weapons_from_its_sources_and_the_verified_stages_hold()
    {
        foreach (var rowId in GearGates)
        {
            var set = Gates[rowId].Items;
            Assert.NotNull(set);
            Assert.NotEmpty(set.Sources);
            Assert.True(set.Groups.Length >= 10, $"{rowId} lists only {set.Groups.Length} weapon groups");
            Assert.All(set.Groups, g => Assert.InRange(g.Length, 1, 2));
            Assert.Equal(set.Groups.SelectMany(g => g).Count(), set.Groups.SelectMany(g => g).Distinct().Count());
            Assert.Equal(set.Groups, Catalog.GameGateOf(rowId)!.Items!.Groups);
            Assert.Equal(set.Hold, Catalog.GameGateOf(rowId)!.Items!.Hold);
        }

        // Verified by name in the game's Item sheet: Curtana Nexus with Holy Shield Nexus, Thyrus Nexus.
        var materia = Gates[HisDarkMateria].Items!;
        Assert.Equal(GateHold.Equipped, materia.Hold);
        Assert.Contains([8649u, 8658u], materia.Groups);
        Assert.Contains([8654u], materia.Groups);
        Assert.DoesNotContain([8658u], materia.Groups); // the shield only with the sword

        // Up in Arms: Curtana Zenith (6257) counts alone and the Holy Shield Zenith (6266) not at all.
        Assert.Contains([6257u], Gates[66971].Items!.Groups);
        Assert.DoesNotContain(Gates[66971].Items!.Groups, g => g.Contains(6266u));

        // Trials of the Braves: Curtana Atma (7824) or Holy Shield Atma (7833), each alone.
        Assert.Contains([7824u], Gates[66972].Items!.Groups);
        Assert.Contains([7833u], Gates[66972].Items!.Groups);

        // The Vital Title takes the zeta (Excalibur Zeta 10054 with Aegis Shield Zeta 10063) or its replica (12124, 12213).
        Assert.Contains([10054u, 10063u], Gates[66097].Items!.Groups);
        Assert.Contains([12124u, 12213u], Gates[66097].Items!.Groups);
        Assert.Contains([12124u, 12213u], Gates[67823].Items!.Groups);

        // Anima: Toughening Up wants the animated weapon (Animated Hauteclaire 13611 with Animated Prytwen 13624),
        // Finding Your Voice the anima stage held (Almace 13223 with Ancile 13236), Body and Soul the complete one (Aettir 15251).
        Assert.Contains([13611u, 13624u], Gates[67749].Items!.Groups);
        Assert.Equal(GateHold.Held, Gates[67820].Items!.Hold);
        Assert.Contains([13223u, 13236u], Gates[67820].Items!.Groups);
        Assert.Contains([15251u, 15264u], Gates[67934].Items!.Groups);
        Assert.Contains([15251u, 15264u], Gates[67940].Items!.Groups);

        Assert.Contains(8649u, Catalog.GateItemWatch);
        Assert.Equal(Catalog.GateItemWatch.Order(), Catalog.GateItemWatch);
        Assert.NotEqual(0u, Catalog.GateItemFingerprint);
    }

    [Fact]
    public void A_gear_gate_reads_Not_checked_without_a_capture_and_is_judged_with_one()
    {
        var context = Context();
        var michiru = Character();
        Assert.Null(michiru.GateItems);
        foreach (var rowId in GearGates)
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var gate = Catalog.GameGateOf(rowId)!;
            var prerequisites = Catalog.PrerequisitesOf(quest).QuestIds;
            var ready = prerequisites.Length == 0 ? Without(michiru, rowId) : With(Without(michiru, rowId), [.. prerequisites]);

            // No capture (a stored character from before 1.10): the gate is not checked and the quest never Ready.
            var unknown = StateResolver.Resolve(quest, ready, Catalog, context);
            Assert.True(unknown.State is QuestState.Unknown or QuestState.Blocked, $"{rowId} {quest.Name} reads {unknown.State} without a capture");
            Assert.Contains(unknown.Requirements, r => r.Req is GameGateRequirement { Checked: null });

            // Nothing of the line on the character: an unmet gate, judged.
            var none = RequirementEvaluator.Evaluate(quest, Wearing(ready, []), Catalog, context).Single(r => r.Req.Kind == RequirementKind.GameGate);
            Assert.False(none.Met);
            Assert.Equal(gate.Items!.Hold, ((GameGateRequirement)none.Req).Checked);
            Assert.NotEqual(QuestState.Ready, StateResolver.Resolve(quest, Wearing(ready, []), Catalog, context).State);

            // The first group worn: met.
            var worn = RequirementEvaluator.Evaluate(quest, Wearing(ready, gate.Items.Groups[0]), Catalog, context).Single(r => r.Req.Kind == RequirementKind.GameGate);
            Assert.True(worn.Met, $"{rowId} {quest.Name}: {worn.Detail}");
        }
    }

    [Fact]
    public void His_Dark_Materia_reads_Ready_with_a_nexus_equipped_and_Blocked_with_a_novus()
    {
        var context = Context() with { ItemName = id => id switch { 7863 => "Curtana Novus", 7872 => "Holy Shield Novus", 8649 => "Curtana Nexus", 8658 => "Holy Shield Nexus", _ => string.Empty } };
        var quest = Catalog.GetByRowId(HisDarkMateria)!;
        var nexusPossible = With(Character(), SoulglazedRelics, WhereforeArtThouZodiac, MethodInHisMalice);

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Wearing(nexusPossible, [8649, 8658]), Catalog, context).State);

        var novus = StateResolver.Resolve(quest, Wearing(nexusPossible, [7863, 7872]), Catalog, context);
        Assert.Equal(QuestState.Blocked, novus.State);
        Assert.Equal("needs a relic weapon nexus equipped, you have Curtana Novus and Holy Shield Novus equipped", novus.NextStep!.Detail);
        Assert.Equal("Blocked · needs a relic weapon nexus equipped, you have Curtana Novus and Holy Shield Novus equipped", BlockerText.StatusText(novus, quest, fixture.Bundle.BlockerNames()));

        // The nexus in the Armoury Chest: equip it.
        var carried = StateResolver.Resolve(quest, Wearing(nexusPossible, [7863, 7872], 8649, 8658), Catalog, context);
        Assert.Equal("needs a relic weapon nexus equipped, equip Curtana Nexus and Holy Shield Nexus", carried.NextStep!.Detail);

        // A paladin with the nexus sword and another shield is not let through.
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Wearing(nexusPossible, [8649]), Catalog, context).State);
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
            foreach (var key in new[] { gate.GameTextKey, gate.AfterTextKey, gate.RequiredTextKey }.OfType<string>())
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

                // The repeatable relic steps open their name with an icon glyph (private use area) the text does not carry.
                var name = new string(catalog.GetByRowId(rowId)!.Name.Where(c => c is < '' or > '').ToArray()).Trim();
                // The quest's own row states its gate: it names the quest, speaks of "this quest" (the deep-dungeon,
                // Resistance and Occult rows), or names what the gate wants (a blue mage trainer's "upon learning X").
                if (key == gate.GameTextKey && !text.Contains(name, StringComparison.Ordinal) && !text.Contains("this quest", StringComparison.Ordinal)
                    && !(GateSubject(gate.Gate) is { } subject && text.Contains(subject, StringComparison.Ordinal)))
                {
                    problems.Add($"{rowId}: {key} does not name {name}, this quest or what the gate wants");
                }

                checkedCount++;
            }
        }

        Assert.True(checkedCount >= 6, $"only {checkedCount} text rows checked");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The spell of a blue magic gate ("the blue magic Blood Drain learned"); null for any other gate.</summary>
    private static string? GateSubject(string gate)
    {
        const string Prefix = "the blue magic ";
        const string Suffix = " learned";
        return gate.StartsWith(Prefix, StringComparison.Ordinal) && gate.EndsWith(Suffix, StringComparison.Ordinal)
            ? gate[Prefix.Length..^Suffix.Length]
            : null;
    }
}
