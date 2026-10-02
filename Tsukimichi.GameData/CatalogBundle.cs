using Tsukimichi.Core.Evaluation;
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
    /// <summary>
    /// The <c>ParamGrow</c> rows the quest EXP formula reads (<see cref="Core.Rewards.QuestExp"/>), read at catalog build;
    /// empty for a bundle from the frozen fixture, where every quest's EXP is then unknown.
    /// </summary>
    public Core.Rewards.QuestExpTable ExpTable { get; init; } = Core.Rewards.QuestExpTable.Empty;

    /// <summary>The name lookups <see cref="BlockerText"/> prints with, over this bundle's catalog and sheet names; the callers memoize one per bundle.</summary>
    public BlockerNames BlockerNames() => new()
    {
        Catalog = Catalog,
        Tribe = id => Names.Tribe(id),
        TribeRank = id => Names.TribeRank(id) is { Length: > 0 } rank ? rank : TribeRanks.Name(id),
        GrandCompany = id => Names.GrandCompany(id) is { Length: > 0 } company ? company : GrandCompanies.Name(id),
        Expansion = id => Names.Expansion(id) is { Length: > 0 } expansion ? expansion : Expansions.Name(id),
        JobAbbreviation = Names.ClassJobAbbreviation,
        ClassJobCategory = Names.ClassJobCategory,
        Duty = Names.Duty,
        SatisfactionNpc = id => Names.SatisfactionNpc(id),
    };

    /// <summary>
    /// Whether a ClassJobCategory admits every class and job a character levels normally: every named ClassJob row
    /// that is not a limited job (<see cref="ClassJobInfo.IsLimited"/>). True for "All Classes" (1) and for "All classes
    /// and jobs (excluding limited jobs)" (130), which leaves out Blue Mage and Beastmaster and so falls short of the
    /// sheet's column count; the Job column reads "Any" for both. False when the bundle has no ClassJob rows.
    /// </summary>
    public bool AdmitsEveryJob(uint category)
    {
        var any = false;
        foreach (var info in Names.ClassJobInfos)
        {
            if (info.IsLimited || info.RowId is 0 or > byte.MaxValue)
            {
                continue;
            }

            any = true;
            if (!Jobs.Admits(category, (byte)info.RowId))
            {
                return false;
            }
        }

        return any;
    }

    /// <summary>
    /// How the Job column groups a ClassJobCategory: <see cref="JobGroup.Any"/> when it admits every job
    /// (<see cref="AdmitsEveryJob"/>) or none is listed, <see cref="JobGroup.Single"/> with the job when it admits one,
    /// else the Disciples it spans. The plugin maps the group to its label.
    /// </summary>
    public (JobGroup Group, byte Single) ClassifyJobs(uint category)
    {
        var count = 0;
        var hand = 0;
        var land = 0;
        byte single = 0;
        foreach (var job in Jobs.JobsIn(category))
        {
            count++;
            single = job;
            if (job is >= 8 and <= 15)
            {
                hand++;
            }
            else if (job is >= 16 and <= 18)
            {
                land++;
            }
        }

        if (count == 0 || AdmitsEveryJob(category))
        {
            return (JobGroup.Any, 0);
        }

        if (count == 1)
        {
            return (JobGroup.Single, single);
        }

        var war = count - hand - land;
        if (war == 0)
        {
            return (hand == 0 ? JobGroup.Land : land == 0 ? JobGroup.Hand : JobGroup.HandAndLand, 0);
        }

        return (hand + land == 0 ? JobGroup.WarAndMagic : JobGroup.Multi, 0);
    }

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

    /// <summary>ClassJob row id to the sheet's role byte for every row that fits a byte, for <see cref="Core.Evaluation.EvalContext.JobRole"/>.</summary>
    public Dictionary<byte, byte> JobRoles()
    {
        var roles = new Dictionary<byte, byte>(Names.ClassJobInfos.Count);
        foreach (var info in Names.ClassJobInfos)
        {
            if (info.RowId is > 0 and <= byte.MaxValue)
            {
                roles[(byte)info.RowId] = info.Role;
            }
        }

        return roles;
    }
}

/// <summary>How the Job column groups the jobs a ClassJobCategory admits (<see cref="CatalogBundle.ClassifyJobs"/>).</summary>
public enum JobGroup
{
    /// <summary>Every job a character levels normally (the limited jobs aside), or no job listed.</summary>
    Any,

    /// <summary>Exactly one class or job.</summary>
    Single,

    /// <summary>Disciples of the Land only.</summary>
    Land,

    /// <summary>Disciples of the Hand only.</summary>
    Hand,

    /// <summary>Disciples of the Hand and the Land, no combat job.</summary>
    HandAndLand,

    /// <summary>Disciples of War and Magic only.</summary>
    WarAndMagic,

    /// <summary>Combat jobs with crafters or gatherers, short of every job.</summary>
    Multi,
}

/// <summary>
/// One ClassJob sheet row the UI can group and decorate: the base class a job grew out of (<see cref="ParentRowId"/>
/// equals <see cref="RowId"/> for classes and for jobs without a class), the quest that unlocks the job, the sheet's
/// role byte (1 tank, 2 melee, 3 ranged or caster, 4 healer, 0 none) and the crafter/gatherer flags from
/// ClassJobCategory 33 and 32 membership. <see cref="ExpArrayIndex"/> is the PlayerState level slot; −1 when none.
/// <see cref="IsLimited"/> is the sheet's <c>IsLimitedJob</c> (Blue Mage, Beastmaster): the jobs the "excluding limited
/// jobs" categories leave out. A frozen catalog written before the column was read has it false everywhere.
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
    int ExpArrayIndex,
    bool IsLimited = false)
{
    /// <summary>Icon id in the game's 062000 icon set (062101 Gladiator … 062142 Pictomancer).</summary>
    public uint IconId => 62100u + RowId;

    /// <summary>The row as the job ladder builder takes it.</summary>
    public LadderJob ToLadderJob() => new(RowId, Name, ParentRowId, UnlockQuestRowId, Role, IsCrafter, IsGatherer);
}

/// <summary>
/// Small id-to-name lookups read from the BeastTribe, GrandCompany, ExVersion, ClassJob, BeastReputationRank,
/// ClassJobCategory and ContentFinderCondition sheets. Keys are row ids; the byte-sized ids on <see cref="QuestRecord"/>
/// widen implicitly. <paramref name="Duties"/> is keyed by InstanceContent row id (what
/// <see cref="QuestRecord.InstanceContentRequired"/> holds), named after the duty's Duty Finder entry;
/// <paramref name="SatisfactionNpcs"/> by SatisfactionNpc row id (<see cref="QuestRecord.SatisfactionNpc"/>), the
/// custom delivery client's name.
/// </summary>
public sealed record GameNames(
    IReadOnlyDictionary<uint, string> Tribes,
    IReadOnlyDictionary<uint, string> GrandCompanies,
    IReadOnlyDictionary<uint, string> Expansions,
    IReadOnlyDictionary<uint, string> ClassJobs,
    IReadOnlyDictionary<uint, string> ClassJobAbbreviations,
    IReadOnlyDictionary<uint, string> TribeRanks,
    IReadOnlyList<ClassJobInfo> ClassJobInfos,
    IReadOnlyDictionary<uint, string> ClassJobCategories,
    IReadOnlyDictionary<uint, string> Duties,
    IReadOnlyDictionary<uint, string> SatisfactionNpcs)
{
    public static readonly GameNames Empty = new(
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        [],
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>());

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

    public string ClassJobCategory(uint id) => ClassJobCategories.GetValueOrDefault(id, string.Empty);

    public string Duty(uint instanceContentId) => Duties.GetValueOrDefault(instanceContentId, string.Empty);

    /// <summary>The custom delivery client's name by SatisfactionNpc row id ("M'naago"); empty for an unknown row.</summary>
    public string SatisfactionNpc(uint id) => SatisfactionNpcs.GetValueOrDefault(id, string.Empty);
}
