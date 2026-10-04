namespace Tsukimichi.Core.Companions;

/// <summary>
/// Which of a character's classes and jobs enter duties (feature plan v7 C7 and N4): the Disciples of War and Magic,
/// limited jobs (Blue Mage, Beastmaster) excepted. A crafter's or gatherer's gearset never queues, and a limited job
/// is outside the Duty Finder's roulettes, so neither counts for the item-level wall (<see cref="ItemLevelWall"/>) nor
/// for a roulette's level (<see cref="DutyBoard"/>). Pure.
/// </summary>
public static class DutyJobs
{
    /// <summary>Carpenter, the first Disciple of the Hand row.</summary>
    public const byte FirstCrafter = 8;

    /// <summary>Fisher, the last Disciple of the Land row.</summary>
    public const byte LastGatherer = 18;

    /// <summary>Blue Mage, the first limited job.</summary>
    public const byte BlueMage = 36;

    /// <summary>
    /// The rule without the ClassJob sheet, by the row ids that never move: 8 to 18 are the Disciples of the Hand and
    /// Land, 36 Blue Mage. The plugin passes <see cref="From"/> over the sheet instead, which knows later limited jobs.
    /// </summary>
    public static bool ByRowId(byte job) => job != 0 && job is not (>= FirstCrafter and <= LastGatherer) && job != BlueMage;

    /// <summary>
    /// The rule from the ClassJob sheet's facts: a row that is not a crafter, a gatherer or a limited job; a row the
    /// sheet lacks falls back to <see cref="ByRowId"/>.
    /// </summary>
    public static Func<byte, bool> From(IEnumerable<(uint RowId, bool Crafter, bool Gatherer, bool Limited)> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        var known = new Dictionary<byte, bool>();
        foreach (var (rowId, crafter, gatherer, limited) in jobs)
        {
            if (rowId is > 0 and <= byte.MaxValue)
            {
                known[(byte)rowId] = !crafter && !gatherer && !limited;
            }
        }

        return known.Count == 0 ? ByRowId : job => known.TryGetValue(job, out var queues) ? queues : ByRowId(job);
    }
}
