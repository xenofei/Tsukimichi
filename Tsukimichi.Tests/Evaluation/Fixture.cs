using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>Factories with sensible defaults so evaluation tests stay short.</summary>
internal static class Fixture
{
    public const uint A = 65600;
    public const uint B = 65601;
    public const uint C = 65602;
    public const uint D = 65603;
    public const uint E = 65604;
    public const uint Target = 65700;

    public const byte Gladiator = 1;
    public const byte Conjurer = 6;
    public const byte Paladin = 19;

    /// <summary>A level-1, any-job, listed quest with no gates. Override with <c>with { }</c>.</summary>
    public static QuestRecord Quest(uint rowId, string? name = null) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = name ?? $"Quest {rowId}",
        Journal = new JournalRef(1, "Section", 1, "Category", 1, "Genre", (int)rowId),
        Level = 1,
    };

    public static QuestCatalog Catalog(params QuestRecord[] quests) => QuestCatalog.Build(quests);

    /// <summary>Completion bitmask with the bit set for each quest row id.</summary>
    public static byte[] Bits(params uint[] completedRowIds)
    {
        var max = completedRowIds.Length == 0 ? 0 : completedRowIds.Max(r => QuestRecord.ToQuestId(r));
        var bits = new byte[(max >> 3) + 1];
        foreach (var rowId in completedRowIds)
        {
            var id = QuestRecord.ToQuestId(rowId);
            bits[id >> 3] |= (byte)(1 << (id & 7));
        }

        return bits;
    }

    public static Dictionary<byte, short> Levels(params (byte Job, short Level)[] levels) =>
        levels.ToDictionary(l => l.Job, l => l.Level);

    /// <summary>Gladiator at level 50 with the given quests completed and achievements loaded.</summary>
    public static CharacterSnapshot Snapshot(params uint[] completedRowIds) => new()
    {
        ContentId = 1,
        Name = "Michiru Tsukikage",
        CompletedBits = Bits(completedRowIds),
        CurrentJob = Gladiator,
        JobLevels = Levels((Gladiator, 50)),
        AchievementsLoaded = true,
    };

    public static AcceptedQuest Accepted(uint rowId, byte sequence = 1) => new(QuestRecord.ToQuestId(rowId), sequence);

    /// <summary>The single result of the given kind; fails when there are none or several.</summary>
    public static RequirementResult Only(IReadOnlyList<RequirementResult> results, RequirementKind kind) =>
        Assert.Single(results, r => r.Req.Kind == kind);

    /// <summary>Category lookup backed by a dictionary; unknown categories admit nothing.</summary>
    public sealed class Jobs : IClassJobCategoryLookup
    {
        private readonly Dictionary<uint, byte[]> categories;

        public Jobs(params (uint Category, byte[] Jobs)[] categories) =>
            this.categories = categories.ToDictionary(c => c.Category, c => c.Jobs);

        public bool Admits(uint categoryId, byte classJobId) =>
            categories.TryGetValue(categoryId, out var jobs) && jobs.Contains(classJobId);

        public IEnumerable<byte> JobsIn(uint categoryId) =>
            categories.TryGetValue(categoryId, out var jobs) ? jobs : [];
    }
}
