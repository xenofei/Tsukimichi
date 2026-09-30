namespace Tsukimichi.Verify.Verify;

/// <summary>Outcome of one (quest, fact, source) comparison. Only <see cref="CatalogWrong"/> and <see cref="Unresolved"/> fail the gate.</summary>
internal enum Verdict
{
    Match,
    CatalogWrong,
    SourceWrong,
    SourceLagging,
    NotModeled,
    NotListed,
    Ambiguous,
    Unresolved,
}

internal static class Verdicts
{
    public static string Name(Verdict v) => v switch
    {
        Verdict.Match => "match",
        Verdict.CatalogWrong => "catalogWrong",
        Verdict.SourceWrong => "sourceWrong",
        Verdict.SourceLagging => "sourceLagging",
        Verdict.NotModeled => "notModeled",
        Verdict.NotListed => "notListed",
        Verdict.Ambiguous => "ambiguous",
        Verdict.Unresolved => "unresolved",
        _ => v.ToString(),
    };

    public static Verdict Parse(string s) => s switch
    {
        "match" => Verdict.Match,
        "catalogWrong" => Verdict.CatalogWrong,
        "sourceWrong" => Verdict.SourceWrong,
        "sourceLagging" => Verdict.SourceLagging,
        "notModeled" => Verdict.NotModeled,
        "notListed" => Verdict.NotListed,
        "ambiguous" => Verdict.Ambiguous,
        "unresolved" => Verdict.Unresolved,
        _ => throw new FormatException($"unknown verdict '{s}'"),
    };

    /// <summary>Severity for "worst fact" summaries: gate failures first, then the informational verdicts, then match.</summary>
    public static int Severity(Verdict v) => v switch
    {
        Verdict.CatalogWrong => 7,
        Verdict.Unresolved => 6,
        Verdict.Ambiguous => 5,
        Verdict.SourceWrong => 4,
        Verdict.SourceLagging => 3,
        Verdict.NotListed => 2,
        Verdict.NotModeled => 1,
        _ => 0,
    };

    public static bool FailsGate(Verdict v) => v is Verdict.CatalogWrong or Verdict.Unresolved;
}

/// <summary>Source enum as it appears in the CSV.</summary>
internal static class SourceNames
{
    public const string Lodestone = "lodestone";
    public const string Wiki = "wiki";
    public const string Collect = "collect";
    public const string Garland = "garland";
    public const string Sheet = "sheet";
}

/// <summary>One long-form row of quest-verification.csv.</summary>
internal sealed record QuestRow(
    uint RowId,
    string Name,
    string Fact,
    string CatalogValue,
    string Source,
    string SourceValue,
    string SourceRef,
    Verdict Verdict,
    string Reason,
    string FixedIn)
{
    public string Key => $"{RowId}:{Fact}:{Source}";
}

/// <summary>One row of reward-verification.csv.</summary>
internal sealed record RewardRow(
    uint QuestRowId,
    string QuestName,
    string Kind,
    uint RewardId,
    uint ItemId,
    string RewardName,
    string CatalogClaim,
    string Source,
    string SourceValue,
    string SourceRef,
    Verdict Verdict,
    string Reason,
    string FixedIn)
{
    public string Key => $"{QuestRowId}:{Kind}:{RewardId}:{ItemId}:{Source}";
}
