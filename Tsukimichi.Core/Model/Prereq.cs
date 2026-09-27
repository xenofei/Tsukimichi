namespace Tsukimichi.Core.Model;

/// <summary>Previous-quest requirement: a set of quest row ids joined by All or Any.</summary>
public sealed record Prereq(uint[] QuestIds, JoinKind Join)
{
    public static readonly Prereq None = new([], JoinKind.All);

    public bool IsEmpty => QuestIds.Length == 0;
}
