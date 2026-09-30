namespace Tsukimichi.Core.Model;

/// <summary>
/// How quests without a journal genre are filed. <see cref="Refiled"/> runs the <c>JournalRefiler</c> after the
/// sheets are mapped (docs/data/unlisted-report.md section 4: retired rows are flagged, the rest move to the genre
/// their prerequisites, successors, Grand Company or issuer's zone point at, curated overrides last).
/// <see cref="Legacy"/> skips it, so genre-0 quests stay in the removed bucket exactly as releases before 0.6.1 showed
/// them: the in-field rollback for a misfiling.
/// </summary>
public enum JournalFiling
{
    Refiled,
    Legacy,
}
