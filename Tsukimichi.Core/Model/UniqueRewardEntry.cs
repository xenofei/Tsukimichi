using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Model;

/// <summary>Names used in <see cref="UniqueRewardEntry.OtherSources"/>: other places the same reward can come from.</summary>
public static class OtherSource
{
    /// <summary>Sold on the FFXIV Online Store (Mog Station); from <c>curated/online_store.json</c>.</summary>
    public const string OnlineStore = "OnlineStore";

    /// <summary>
    /// Drops in a duty (a dungeon's treasure coffers); from <c>curated/other_sources.json</c>, whose <c>where</c> text
    /// (the duties) travels in <see cref="UniqueRewardEntry.OtherSourceNotes"/>.
    /// </summary>
    public const string DungeonDrop = "DungeonDrop";

    /// <summary>
    /// Whether <paramref name="name"/> comes from a curated file (<see cref="OnlineStore"/>, <see cref="DungeonDrop"/>)
    /// rather than from the item sheets, so the source text's <c>;otherSource=</c> provenance never names it.
    /// </summary>
    public static bool IsCurated(string name) =>
        string.Equals(name, OnlineStore, StringComparison.Ordinal) || string.Equals(name, DungeonDrop, StringComparison.Ordinal);

    /// <summary>The item is not flagged untradable.</summary>
    public const string Tradable = "Tradable";

    /// <summary>The item has a market board search category.</summary>
    public const string Marketable = "Marketable";

    /// <summary>A gil shop lists the item (including quest-reward reacquisition menus).</summary>
    public const string GilShopItem = "GilShopItem";

    /// <summary>A special shop (tomestones, scrips, seals, ...) hands the item out.</summary>
    public const string SpecialShop = "SpecialShop";

    /// <summary>A recipe produces the item.</summary>
    public const string Recipe = "Recipe";

    /// <summary>A gathering node yields the item.</summary>
    public const string GatheringItem = "GatheringItem";

    /// <summary>An achievement rewards the item.</summary>
    public const string Achievement = "Achievement";
}

/// <summary>One reward obtainable only through a quest, as shipped in unique_quests.json or overridden by the user.</summary>
/// <param name="Source">Where the claim comes from: sheet and field, curated file name, or "user".</param>
public sealed record UniqueRewardEntry(
    uint QuestRowId,
    RewardKind Kind,
    uint RewardId,
    uint ItemId,
    string RewardName,
    Confidence Confidence,
    string Source)
{
    private static readonly string[] NoSources = [];

    private static readonly IReadOnlyDictionary<string, string> NoNotes = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IReadOnlyList<string> otherSources = NoSources;

    private readonly IReadOnlyDictionary<string, string> otherSourceNotes = NoNotes;

    /// <summary>
    /// Other places the same reward can be had (<see cref="OtherSource"/> names), in generator order; empty when the
    /// reward is quest-exclusive as far as the data knows. Files written before this field existed load as empty.
    /// </summary>
    public IReadOnlyList<string> OtherSources
    {
        get => otherSources;
        init => otherSources = value ?? NoSources;
    }

    /// <summary>
    /// Free text per <see cref="OtherSources"/> name, where the data has more to say than the name: for
    /// <see cref="OtherSource.DungeonDrop"/> the duties the item drops in ("Snowcloak, Sastasha (Hard) and ..."). Empty
    /// for most entries and omitted from the file then; files written before this field existed load as empty.
    /// </summary>
    [OmitWhenEmpty]
    public IReadOnlyDictionary<string, string> OtherSourceNotes
    {
        get => otherSourceNotes;
        init => otherSourceNotes = value ?? NoNotes;
    }

    /// <summary>Whether the reward is also sold on the FFXIV Online Store (<see cref="OtherSource.OnlineStore"/>).</summary>
    [JsonIgnore]
    public bool SoldOnOnlineStore => HasOtherSource(OtherSource.OnlineStore);

    /// <summary>Whether the reward also drops in a duty (<see cref="OtherSource.DungeonDrop"/>).</summary>
    [JsonIgnore]
    public bool DropsInDuty => HasOtherSource(OtherSource.DungeonDrop);

    /// <summary>
    /// Where the reward drops (<see cref="OtherSourceNotes"/> for <see cref="OtherSource.DungeonDrop"/>); empty when it
    /// does not drop, or drops somewhere the data does not name.
    /// </summary>
    [JsonIgnore]
    public string DropWhere => DropsInDuty ? OtherSourceNote(OtherSource.DungeonDrop) : string.Empty;

    /// <summary>The note <see cref="OtherSourceNotes"/> carries for <paramref name="name"/>; empty when none.</summary>
    public string OtherSourceNote(string name) =>
        otherSourceNotes.TryGetValue(name, out var note) && note is not null ? note : string.Empty;

    /// <summary>Whether <see cref="OtherSources"/> names <paramref name="name"/> (ordinal).</summary>
    public bool HasOtherSource(string name)
    {
        for (var i = 0; i < otherSources.Count; i++)
        {
            if (string.Equals(otherSources[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A copy with <paramref name="name"/> appended to <see cref="OtherSources"/>; the same entry when already listed.</summary>
    public UniqueRewardEntry WithOtherSource(string name) => WithOtherSource(name, null);

    /// <summary>
    /// A copy with <paramref name="name"/> appended to <see cref="OtherSources"/> (when not listed yet) and, when
    /// <paramref name="note"/> is not blank, the note stored for it in <see cref="OtherSourceNotes"/> (replacing an older
    /// one); the same entry when nothing changes.
    /// </summary>
    public UniqueRewardEntry WithOtherSource(string name, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var result = this;
        if (!HasOtherSource(name))
        {
            var extended = new string[otherSources.Count + 1];
            for (var i = 0; i < otherSources.Count; i++)
            {
                extended[i] = otherSources[i];
            }

            extended[^1] = name;
            result = result with { OtherSources = extended };
        }

        if (!string.IsNullOrWhiteSpace(note) && !string.Equals(OtherSourceNote(name), note, StringComparison.Ordinal))
        {
            var notes = new Dictionary<string, string>(otherSourceNotes, StringComparer.Ordinal) { [name] = note };
            result = result with { OtherSourceNotes = notes };
        }

        return result;
    }

    /// <summary>Value equality including the <see cref="OtherSources"/> sequence (a list property would otherwise compare by reference).</summary>
    public bool Equals(UniqueRewardEntry? other) =>
        other is not null
        && QuestRowId == other.QuestRowId
        && Kind == other.Kind
        && RewardId == other.RewardId
        && ItemId == other.ItemId
        && string.Equals(RewardName, other.RewardName, StringComparison.Ordinal)
        && Confidence == other.Confidence
        && string.Equals(Source, other.Source, StringComparison.Ordinal)
        && otherSources.SequenceEqual(other.otherSources, StringComparer.Ordinal)
        && NotesEqual(otherSourceNotes, other.otherSourceNotes);

    public override int GetHashCode() => HashCode.Combine(QuestRowId, Kind, RewardId, ItemId, RewardName, Confidence, Source, HashCode.Combine(otherSources.Count, otherSourceNotes.Count));

    private static bool NotesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
