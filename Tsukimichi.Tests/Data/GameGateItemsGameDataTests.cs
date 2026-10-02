using System.Text;
using Lumina.Data;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using LuminaGameData = Lumina.GameData;
using Sheets = Lumina.Excel.Sheets;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The weapons of <c>curated/game_gates.json</c>'s gear gates against the installed game: each set is what its
/// <c>sources</c> give when derived again (<see cref="Derive"/>), every item is a weapon or shield, a
/// <c>QuestClassJobReward</c> source is the gated quest's own reward row (the weapon its turn-in takes), and a
/// <c>RelicItem</c> stage is the one the quest's script checks (<c>IsRelicWeapon040Equipped</c> is the nexus, row 5).
/// </summary>
public sealed class GameGateItemsGameDataTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    /// <summary>The script check of each Zodiac stage to its RelicItem row: 010 zenith (1) to 060 zeta (7).</summary>
    private static readonly Dictionary<string, uint> RelicStageRows = new()
    {
        ["IsRelicWeapon010Equipped"] = 1,
        ["IsRelicWeapon020Equipped"] = 2,
        ["IsRelicWeapon025Equipped"] = 3,
        ["IsRelicWeapon030Equipped"] = 4,
        ["IsRelicWeapon040Equipped"] = 5,
        ["IsRelicWeapon050Equipped"] = 6,
        ["IsRelicWeapon060Equipped"] = 7,
    };

    [GameDataFact]
    public void Every_gear_gate_lists_exactly_the_weapons_its_sources_give()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var problems = new List<string>();
        var gear = 0;
        foreach (var (rowId, gate) in curated.GameGates)
        {
            if (gate.Items is not { } set)
            {
                continue;
            }

            gear++;
            List<uint[]> derived;
            try
            {
                derived = Derive(game.Game, set.Sources, set.Shield);
            }
            catch (InvalidOperationException ex)
            {
                problems.Add($"{rowId}: {ex.Message}");
                continue;
            }

            var listed = string.Join(" ", set.Groups.Select(g => "[" + string.Join(",", g) + "]"));
            var expected = string.Join(" ", derived.Select(g => "[" + string.Join(",", g) + "]"));
            if (listed != expected)
            {
                problems.Add($"{rowId}: lists {listed}\n  sources give {expected}");
            }

            var quest = game.Game.GetExcelSheet<Sheets.Quest>()!.GetRow(rowId);
            foreach (var source in set.Sources)
            {
                var (sheet, row) = Split(source);
                if (sheet == "QuestClassJobReward" && (quest.Reward[0].RowType != typeof(Sheets.QuestClassJobReward) || quest.Reward[0].RowId != row))
                {
                    problems.Add($"{rowId}: {source} is not the quest's own reward row");
                }

                if (sheet == "RelicItem")
                {
                    var checks = ScriptStrings(game.Game, quest.Id.ExtractText()).Where(RelicStageRows.ContainsKey).Distinct().ToList();
                    if (checks.Count != 1 || RelicStageRows[checks[0]] != row)
                    {
                        problems.Add($"{rowId}: {source}, but the script checks {string.Join(", ", checks)}");
                    }
                }
            }
        }

        Assert.True(gear >= 30, $"only {gear} gear gates");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [GameDataFact]
    public void The_plugin_catalog_carries_the_weapon_names()
    {
        var bundle = game.Bundle;
        Assert.Equal("Curtana Nexus", bundle.GateItemName(8649));
        Assert.Equal("Holy Shield Nexus", bundle.GateItemName(8658));
        Assert.Equal(bundle.Catalog.GateItemWatch.Length, bundle.GateItemNames.Count);
    }

    [GameDataFact]
    public void The_plugin_catalog_carries_who_wears_each_weapon()
    {
        const byte Gladiator = 1;
        const byte Conjurer = 6;
        const byte Paladin = 19;
        const byte WhiteMage = 24;
        var bundle = game.Bundle;
        Assert.Equal(bundle.Catalog.GateItemWatch.Length, bundle.GateItemJobCategories.Count);

        // Curtana Zenith and Holy Shield Nexus: a paladin, not a gladiator (a relic is a job's weapon); Thyrus Zenith: a white mage.
        foreach (var sword in new uint[] { 6257, 8658 })
        {
            Assert.False(bundle.Jobs.Admits(bundle.GateItemJobCategory(sword), Gladiator), $"{sword}");
            Assert.True(bundle.Jobs.Admits(bundle.GateItemJobCategory(sword), Paladin), $"{sword}");
            Assert.False(bundle.Jobs.Admits(bundle.GateItemJobCategory(sword), Conjurer), $"{sword}");
        }

        Assert.False(bundle.Jobs.Admits(bundle.GateItemJobCategory(6262), Conjurer));
        Assert.True(bundle.Jobs.Admits(bundle.GateItemJobCategory(6262), WhiteMage));
        Assert.False(bundle.Jobs.Admits(bundle.GateItemJobCategory(6262), Paladin));
    }

    [GameDataFact]
    public void Up_in_Arms_with_Curtana_Zenith_carried_reads_Ready_on_the_paladin()
    {
        // Review 1.10's repro: a conjurer at 30 with a gladiator-paladin at 50 and Curtana Zenith in the Armoury Chest.
        const uint UpInArms = 66971;
        const uint CurtanaZenith = 6257;
        const byte Gladiator = 1;
        const byte Conjurer = 6;
        const byte Paladin = 19;
        var bundle = game.Bundle;
        var catalog = bundle.Catalog;
        var context = Core.Evaluation.EvalContextBuilder.Build(CuratedData.Empty.Festivals, bundle.Jobs, static () => DateTime.UtcNow, jobParents: bundle.JobParents(), jobRoles: bundle.JobRoles())
            with { ItemName = bundle.GateItemName, ItemJobCategory = bundle.GateItemJobCategory };
        var snapshot = new CharacterSnapshot
        {
            ContentId = 1,
            Name = "Michiru Tsukikage",
            CompletedBits = Evaluation.Fixture.Bits(),
            CurrentJob = Conjurer,
            JobLevels = new Dictionary<byte, short> { [Conjurer] = 30, [Gladiator] = 50, [Paladin] = 50 },
            AchievementsLoaded = true,
            GateItems = new GateItemCapture(catalog.GateItemFingerprint, [], [CurtanaZenith]),
        };

        var result = Core.Evaluation.StateResolver.Resolve(catalog.GetByRowId(UpInArms)!, snapshot, catalog, context);

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Paladin, result.ReadyOnJob);

        // A gladiator who has not become a paladin cannot wear it: Blocked, the weapon to equip named first.
        var gladiator = snapshot with { JobLevels = new Dictionary<byte, short> { [Conjurer] = 30, [Gladiator] = 50 } };
        var blocked = Core.Evaluation.StateResolver.Resolve(catalog.GetByRowId(UpInArms)!, gladiator, catalog, context);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal("needs a relic weapon zenith equipped, equip Curtana Zenith", blocked.NextStep!.Detail);
    }

    private static (string Sheet, uint Row) Split(string source)
    {
        var hash = source.IndexOf('#', StringComparison.Ordinal);
        return (source[..hash], uint.Parse(source[(hash + 1)..], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The groups a gate's sources give: a <c>QuestClassJobReward</c> row's RequiredItem per subrow (a paladin's subrow
    /// holds sword and shield); the item columns of a <c>RelicItem</c> or <c>AnimaWeaponItem</c> row, or the
    /// <c>REPLICA_</c> script parameters of a <c>Quest</c> row, grouped by the <paramref name="shield"/> rule (both:
    /// items of one ClassJobCategory together; either: each alone; none: each alone without the off-hand items). Every
    /// item must be a main-hand, off-hand or two-handed weapon. Sorted as the file writes them.
    /// </summary>
    internal static List<uint[]> Derive(LuminaGameData game, IReadOnlyList<string> sources, string? shield)
    {
        var items = game.GetExcelSheet<Sheets.Item>()!;
        var groups = new List<uint[]>();
        foreach (var source in sources)
        {
            var (sheetName, row) = Split(source);
            var flat = new List<uint>();
            switch (sheetName)
            {
                case "RelicItem" or "AnimaWeaponItem":
                {
                    var sheet = game.Excel.GetSheet<RawRow>(Language.English, sheetName);
                    var r = sheet.GetRow(row);
                    for (var i = 0; i < sheet.Columns.Count; i++)
                    {
                        if (sheet.Columns[i].Type == ExcelColumnDataType.UInt32 && r.ReadColumn(i) is uint id && id != 0)
                        {
                            flat.Add(id);
                        }
                    }

                    break;
                }

                case "QuestClassJobReward":
                    foreach (var subrow in game.GetSubrowExcelSheet<Sheets.QuestClassJobReward>()!.GetRow(row))
                    {
                        var group = subrow.RequiredItem.Select(x => x.RowId).Where(x => x != 0).ToArray();
                        if (group.Length > 0)
                        {
                            groups.Add(group);
                        }
                    }

                    break;

                case "Quest":
                    foreach (var param in game.GetExcelSheet<Sheets.Quest>()!.GetRow(row).QuestParams)
                    {
                        if (param.ScriptInstruction.ExtractText().StartsWith("REPLICA_", StringComparison.Ordinal) && param.ScriptArg != 0)
                        {
                            flat.Add(param.ScriptArg);
                        }
                    }

                    break;

                default:
                    throw new InvalidOperationException($"unknown source {source}");
            }

            if (flat.Count > 0)
            {
                groups.AddRange(shield switch
                {
                    "both" => flat.GroupBy(id => items.GetRow(id).ClassJobCategory.RowId).Select(g => g.ToArray()),
                    "either" => flat.Select(id => new[] { id }),
                    "none" => flat.Where(id => items.GetRow(id).EquipSlotCategory.RowId != 2).Select(id => new[] { id }),
                    _ => throw new InvalidOperationException($"{source} needs a shield rule"),
                });
            }
        }

        foreach (var id in groups.SelectMany(g => g))
        {
            if (items.GetRow(id).EquipSlotCategory.RowId is not (1 or 2 or 13))
            {
                throw new InvalidOperationException($"{id} {items.GetRow(id).Name.ExtractText()} is no weapon or shield");
            }
        }

        return groups.Select(g => g.Distinct().Order().ToArray()).DistinctBy(g => string.Join(",", g)).OrderBy(g => g[0]).ToList();
    }

    /// <summary>The printable strings of a quest's script (<c>game_script/quest/&lt;nnn&gt;/&lt;id&gt;.luab</c>): the game functions it calls.</summary>
    private static List<string> ScriptStrings(LuminaGameData game, string questId)
    {
        var under = questId.LastIndexOf('_');
        var file = game.GetFile($"game_script/quest/{questId.Substring(under + 1, 3)}/{questId}.luab");
        var strings = new List<string>();
        if (file is null)
        {
            return strings;
        }

        var sb = new StringBuilder();
        foreach (var b in file.Data)
        {
            if (b is >= 32 and < 127)
            {
                sb.Append((char)b);
                continue;
            }

            if (sb.Length >= 6)
            {
                strings.Add(sb.ToString());
            }

            sb.Clear();
        }

        return strings;
    }
}
