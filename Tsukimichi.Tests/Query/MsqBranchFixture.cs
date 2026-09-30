using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// A synthetic Evercold-style main scenario (P14): a linear lead-in, a shared start, three routes of different
/// lengths, a reconvergence quest and a linear stretch after it. All quests sit in journal section 1 in the order
/// listed, expansion 6 (so the region is routed), levels 39 to 42.
/// <code>
/// L1 → L2 ─┬─ A1 → A2 → A3 ──────────────┐
///          ├─ B1 → B2 → B3 → B4 → B5 ────┼─→ J → P1 → P2
///          └─ C1 → C2 ───────────────────┘
/// </code>
/// <see cref="Build"/> takes the join kind at J: <see cref="JoinKind.All"/> (every route needed) or
/// <see cref="JoinKind.Any"/> (one route opens J, the others become optional).
/// </summary>
internal static class MsqBranchFixture
{
    public const uint L1 = 70_001;
    public const uint L2 = 70_002;
    public const uint A1 = 70_011;
    public const uint A2 = 70_012;
    public const uint A3 = 70_013;
    public const uint B1 = 70_021;
    public const uint B2 = 70_022;
    public const uint B3 = 70_023;
    public const uint B4 = 70_024;
    public const uint B5 = 70_025;
    public const uint C1 = 70_031;
    public const uint C2 = 70_032;
    public const uint J = 70_040;
    public const uint P1 = 70_041;
    public const uint P2 = 70_042;

    public const byte Evercold = 6;

    public static readonly uint[] RouteA = [A1, A2, A3];
    public static readonly uint[] RouteB = [B1, B2, B3, B4, B5];
    public static readonly uint[] RouteC = [C1, C2];

    /// <summary>Every quest in journal order.</summary>
    public static readonly uint[] Story = [L1, L2, A1, A2, A3, B1, B2, B3, B4, B5, C1, C2, J, P1, P2];

    public static QuestCatalog Build(JoinKind join = JoinKind.All, byte expansion = Evercold)
    {
        var quests = new List<QuestRecord>
        {
            Msq(L1, "Frost on the Sill", 39, expansion),
            Msq(L2, "Three Roads North", 40, expansion, L1),
            Msq(A1, "The Ember Road", 40, expansion, L2),
            Msq(A2, "Ashes Underfoot", 40, expansion, A1),
            Msq(A3, "A Hearth Relit", 41, expansion, A2),
            Msq(B1, "The Glacier Road", 40, expansion, L2),
            Msq(B2, "Crevasse Crossing", 40, expansion, B1),
            Msq(B3, "The Ice Choir", 40, expansion, B2),
            Msq(B4, "Beneath the Floe", 41, expansion, B3),
            Msq(B5, "Thaw", 41, expansion, B4),
            Msq(C1, "The Aurora Road", 40, expansion, L2),
            Msq(C2, "Lights Unbound", 41, expansion, C1),
            Msq(J, "Where the Roads Meet", 41, expansion) with { PreviousQuests = new Prereq([A3, B5, C2], join) },
            Msq(P1, "Evercold", 42, expansion, J),
            Msq(P2, "The Long Night", 42, expansion, P1),
        };

        return QuestCatalog.Build(quests);
    }

    /// <summary>A main scenario quest (section 1, Dawntrail onward) with one previous quest, at its journal position in <see cref="Story"/>.</summary>
    public static QuestRecord Msq(uint rowId, string name, byte level, byte expansion, uint? after = null) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = name,
        Journal = new JournalRef(1, "Main Scenario (Evercold)", 30, "Evercold Main Scenario Quests", 300, "Evercold", 1000 + Array.IndexOf(Story, rowId)),
        Level = level,
        Expansion = expansion,
        PreviousQuests = after is { } previous ? new Prereq([previous], JoinKind.All) : Prereq.None,
    };

    /// <summary>
    /// The character's states as the evaluator resolves them: a gladiator at 50 entitled to Evercold with
    /// <paramref name="completed"/> done, so a quest whose previous quests are met reads Ready and the rest Blocked.
    /// </summary>
    public static Dictionary<uint, QuestEvaluation> Evaluate(QuestCatalog catalog, params uint[] completed)
    {
        var snapshot = Evaluation.Fixture.Snapshot(completed) with { MaxExpansion = Evercold, LevelCap = 110 };
        return StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);
    }

    /// <summary>The evaluator's states as a plain state map.</summary>
    public static Dictionary<uint, QuestState> States(QuestCatalog catalog, params uint[] completed) =>
        Evaluate(catalog, completed).ToDictionary(p => p.Key, p => p.Value.State);

    /// <summary><paramref name="parts"/> joined into one completion list.</summary>
    public static uint[] Done(params IEnumerable<uint>[] parts) => parts.SelectMany(p => p).ToArray();
}
