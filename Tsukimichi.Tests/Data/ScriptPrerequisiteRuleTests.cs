using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The verifier's 1.22.0 prerequisite rule (owner ruling: the game's data wins over a wiki-only prerequisite): a quest the
/// wiki names that neither the sheet (previous quests, accept conditions) nor the quest's script constants
/// (<c>Quest.QuestParams</c>, <see cref="QuestScriptConstants"/>) name is one the game cannot be checking, so the wiki
/// row reads sourceWrong. The rule itself, then the committed report against the installed game.
/// </summary>
public sealed class ScriptPrerequisiteRuleTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    private const string RuleReason = "script constants (Quest.QuestParams) name none of them";

    /// <summary>What the rule adds only when it settles a row itself; a row another source reconciles never reads it.</summary>
    private const string RuleConclusion = "so the game does not check them: the source is wrong";

    [Fact]
    public void A_constant_names_a_quest_by_row_id_or_runtime_id()
    {
        (string, uint)[] constants = [("ACTOR0", 1010810), ("COMP_JOBREL014", 65896), ("QST_CHECK_STMBDY104", 2922)];

        // Named: the sheet row id (A Treasured Mother), the runtime id (A New Fishing Ex-spear-ience, 68458 - 65536).
        Assert.True(QuestScriptConstants.NamesQuest(constants, 65896));
        Assert.True(QuestScriptConstants.NamesQuest(constants, 68458));

        // Not named: One Man's Trash appears in no constant, as row id or runtime id.
        Assert.False(QuestScriptConstants.NamesQuest(constants, 66676));
        Assert.False(QuestScriptConstants.NamesQuest([], 66676));
    }

    /// <summary>The rows the rule settled in the committed report are the ten wiki-only prerequisites of the 1.22.0 ruling.</summary>
    [Fact]
    [Trait("Category", "Curated")]
    public void The_committed_report_settles_the_ruled_rows_by_the_rule()
    {
        Assert.Equal(
            [65895u, 65897, 65953, 67879, 67918, 67923, 68778, 69587, 69588, 70539],
            RuleRows().Select(f => uint.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray());
    }

    /// <summary>
    /// The rule settles wiki prerequisites only (owner ruling 2: game data wins over a wiki-only prerequisite): every row
    /// of the committed report the rule settled (its conclusion, of any fact, source or verdict) is a wiki prereqs row
    /// read sourceWrong. A row another source reconciled keeps the finding but never the conclusion.
    /// </summary>
    [Fact]
    [Trait("Category", "Curated")]
    public void Only_wiki_prerequisite_rows_are_settled_by_the_rule()
    {
        var settled = AllRows().Where(f => f[8].Contains(RuleConclusion, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(settled);
        Assert.All(settled, f => Assert.True(f[2] == "prereqs" && f[4] == "wiki" && f[7] == "sourceWrong" && f[8].EndsWith(RuleConclusion, StringComparison.Ordinal), $"{f[0]} {f[2]}/{f[4]} reads {f[7]}: {f[8]}"));
    }

    private static List<string[]> AllRows() => VerificationAllowlistTests.ReadCsv(Path.Combine(ExtraPrerequisitesDataTests.DocsDataDir(), "quest-verification.csv"));

    /// <summary>The rows the rule settled: any source, any verdict, so a Lodestone row it touched would be counted (and fail).</summary>
    private static List<string[]> RuleRows() => AllRows().Where(f => f[8].Contains(RuleConclusion, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// Every report row the rule settled names quests the installed game's script constants for that quest do not name,
    /// and the rule's real negatives hold: Method in His Malice's script names A Treasured Mother, Two Sides of a Coin's
    /// names Primal Awakening, so a wiki naming those would stay open.
    /// </summary>
    [GameDataFact]
    public void The_rows_the_rule_settled_name_quests_no_script_constant_names()
    {
        var excel = fixture.Game.Excel;
        var catalog = fixture.Bundle.Catalog;
        var byName = catalog.All.GroupBy(q => q.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(q => q.RowId).ToList(), StringComparer.OrdinalIgnoreCase);

        Assert.True(QuestScriptConstants.NamesQuest(QuestScriptConstants.Read(excel, 65895), 65896));
        Assert.True(QuestScriptConstants.NamesQuest(QuestScriptConstants.Read(excel, 67879), 66695));
        Assert.False(QuestScriptConstants.NamesQuest(QuestScriptConstants.Read(excel, 65895), 66676));

        var rows = RuleRows();
        var problems = new List<string>();
        foreach (var f in rows)
        {
            var rowId = uint.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture);
            var quest = catalog.GetByRowId(rowId)!;
            var constants = QuestScriptConstants.Read(excel, rowId);
            var start = f[8].IndexOf("does not require ", StringComparison.Ordinal) + "does not require ".Length;
            var end = f[8].IndexOf(" and the quest's", StringComparison.Ordinal);
            foreach (var name in f[8][start..end].Split("; "))
            {
                var bare = name.EndsWith(" (Quest)", StringComparison.Ordinal) ? name[..^" (Quest)".Length] : name;
                if (!byName.TryGetValue(bare, out var ids))
                {
                    problems.Add($"{rowId}: '{name}' names no quest of the catalog");
                    continue;
                }

                foreach (var id in ids)
                {
                    if (QuestScriptConstants.NamesQuest(constants, id))
                    {
                        problems.Add($"{rowId}: its script names {id} ({name}), so the rule does not hold");
                    }

                    if (quest.PreviousQuests.QuestIds.Contains(id) || quest.AcceptConditions.Contains(id))
                    {
                        problems.Add($"{rowId}: the sheet requires {id} ({name}), so the rule does not hold");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
