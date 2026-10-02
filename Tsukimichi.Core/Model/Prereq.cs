using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Model;

/// <summary>Previous-quest requirement: a set of quest row ids joined by All or Any.</summary>
public sealed record Prereq(uint[] QuestIds, JoinKind Join)
{
    public static readonly Prereq None = new([], JoinKind.All);

    public bool IsEmpty => QuestIds.Length == 0;

    /// <summary>
    /// The ids of <see cref="QuestIds"/> needed whatever <see cref="Join"/> says: the accept conditions that name a
    /// quest outside an Any join's previous quests (<see cref="QuestCatalog.PrerequisitesOf"/>), needed beside one of
    /// the alternatives. Empty otherwise; under an All join every id is needed anyway. Worked out by the catalog, never
    /// stored with a record.
    /// </summary>
    [JsonIgnore]
    public uint[] Required { get; init; } = [];

    /// <summary>Whether <paramref name="rowId"/> is one of <see cref="Required"/>, not an alternative of an Any join.</summary>
    public bool IsRequired(uint rowId) => Required.Length > 0 && Array.IndexOf(Required, rowId) >= 0;
}
