namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Answers which class/job row ids a ClassJobCategory row admits. Built once from the game sheets; safe to share across threads.
/// </summary>
public interface IClassJobCategoryLookup
{
    /// <summary>True when the category admits the class/job. Category 0 admits nothing; unknown categories admit nothing.</summary>
    bool Admits(uint categoryId, byte classJobId);

    /// <summary>Every class/job row id the category admits, in ascending id order.</summary>
    IEnumerable<byte> JobsIn(uint categoryId);
}
