using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// Everything one catalog build produces: the quest catalog plus the small sheet-derived lookups the evaluator and UI need.
/// Immutable once built.
/// </summary>
/// <param name="Catalog">Every named quest, indexed.</param>
/// <param name="Names">Display names for tribes, Grand Companies, expansions, classes and tribe ranks.</param>
/// <param name="Jobs">ClassJobCategory membership.</param>
/// <param name="Language">Lumina language name the strings were read in, e.g. "English".</param>
public sealed record CatalogBundle(QuestCatalog Catalog, GameNames Names, ClassJobCategoryLookup Jobs, string Language)
{
    /// <summary>The per-job quest ladders over this catalog's ClassJob rows; the callers memoize one per bundle.</summary>
    public JobLadder BuildJobLadder()
    {
        var jobs = new LadderJob[Names.ClassJobInfos.Count];
        for (var i = 0; i < jobs.Length; i++)
        {
            jobs[i] = Names.ClassJobInfos[i].ToLadderJob();
        }

        return JobLadder.Build(Catalog, jobs, Jobs);
    }

    /// <summary>
    /// ClassJob row id to its <c>ClassJobParent</c> row id for every row that fits a byte, for
    /// <see cref="Core.Evaluation.EvalContext.ParentJob"/>. A class maps to itself, as the sheet has it.
    /// </summary>
    public Dictionary<byte, byte> JobParents()
    {
        var parents = new Dictionary<byte, byte>(Names.ClassJobInfos.Count);
        foreach (var info in Names.ClassJobInfos)
        {
            if (info.RowId is > 0 and <= byte.MaxValue && info.ParentRowId <= byte.MaxValue)
            {
                parents[(byte)info.RowId] = (byte)info.ParentRowId;
            }
        }

        return parents;
    }
}

/// <summary>
/// One ClassJob sheet row the UI can group and decorate: the base class a job grew out of (<see cref="ParentRowId"/>
/// equals <see cref="RowId"/> for classes and for jobs without a class), the quest that unlocks the job, the sheet's
/// role byte (1 tank, 2 melee, 3 ranged or caster, 4 healer, 0 none) and the crafter/gatherer flags from
/// ClassJobCategory 33 and 32 membership. <see cref="ExpArrayIndex"/> is the PlayerState level slot; −1 when none.
/// </summary>
public sealed record ClassJobInfo(
    uint RowId,
    string Name,
    string Abbreviation,
    uint ParentRowId,
    uint UnlockQuestRowId,
    byte Role,
    bool IsCrafter,
    bool IsGatherer,
    int ExpArrayIndex)
{
    /// <summary>Icon id in the game's 062000 icon set (062101 Gladiator … 062142 Pictomancer).</summary>
    public uint IconId => 62100u + RowId;

    /// <summary>The row as the job ladder builder takes it.</summary>
    public LadderJob ToLadderJob() => new(RowId, Name, ParentRowId, UnlockQuestRowId, Role, IsCrafter, IsGatherer);
}

/// <summary>
/// Small id-to-name lookups read from the BeastTribe, GrandCompany, ExVersion, ClassJob and BeastReputationRank sheets.
/// Keys are row ids; the byte-sized ids on <see cref="QuestRecord"/> widen implicitly.
/// </summary>
public sealed record GameNames(
    IReadOnlyDictionary<uint, string> Tribes,
    IReadOnlyDictionary<uint, string> GrandCompanies,
    IReadOnlyDictionary<uint, string> Expansions,
    IReadOnlyDictionary<uint, string> ClassJobs,
    IReadOnlyDictionary<uint, string> ClassJobAbbreviations,
    IReadOnlyDictionary<uint, string> TribeRanks,
    IReadOnlyList<ClassJobInfo> ClassJobInfos)
{
    public static readonly GameNames Empty = new(
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        []);

    /// <summary>The ClassJob row, or null for an id the sheet has no named row for.</summary>
    public ClassJobInfo? ClassJobInfo(uint id)
    {
        foreach (var info in ClassJobInfos)
        {
            if (info.RowId == id)
            {
                return info;
            }
        }

        return null;
    }

    public string Tribe(uint id) => Tribes.GetValueOrDefault(id, string.Empty);

    public string GrandCompany(uint id) => GrandCompanies.GetValueOrDefault(id, string.Empty);

    public string Expansion(uint id) => Expansions.GetValueOrDefault(id, string.Empty);

    public string ClassJob(uint id) => ClassJobs.GetValueOrDefault(id, string.Empty);

    public string ClassJobAbbreviation(uint id) => ClassJobAbbreviations.GetValueOrDefault(id, string.Empty);

    public string TribeRank(uint id) => TribeRanks.GetValueOrDefault(id, string.Empty);
}
