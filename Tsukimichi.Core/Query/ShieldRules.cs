using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Query;

/// <summary>
/// How the panes apply the wider spoiler shield (plan v7, 1.20.0 N6; spec-1.20 "Where it applies") where a rule takes
/// more than one call of <see cref="SpoilerMask"/>: the Journal table's title, a journal node's hidden area, travel to a
/// place either character's story has not reached, a duty row's mask, and the names "Reveal names in this quest"
/// reveals, the quest's duties among them.
/// </summary>
public static class ShieldRules
{
    /// <summary>
    /// A journal section, category or genre node as its parent's name and its own, through the shield
    /// (<see cref="SpoilerMask.NodeName"/>): "Dawntrail Sidequests › Dawntrail area 5 Sidequests", never the hidden area's
    /// name. Null for another kind of scope or an id the catalog does not file. Scans the catalog once; callers memoize.
    /// </summary>
    public static (string Parent, string Name)? JournalScope(QuestScope scope, QuestCatalog catalog, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(spoilers);
        if (scope.Kind is not (ScopeKind.Section or ScopeKind.Category or ScopeKind.Genre))
        {
            return null;
        }

        foreach (var quest in catalog.All)
        {
            var j = quest.Journal;
            switch (scope.Kind)
            {
                case ScopeKind.Section when j.SectionId == scope.Id:
                    return (string.Empty, spoilers.NodeName(j.SectionName));
                case ScopeKind.Category when j.CategoryId == scope.Id:
                    return (spoilers.NodeName(j.SectionName), spoilers.NodeName(j.CategoryName));
                case ScopeKind.Genre when j.GenreId == scope.Id:
                    return (spoilers.NodeName(j.CategoryName), spoilers.NodeName(j.GenreName));
            }
        }

        return null;
    }

    /// <summary>
    /// The name of the NPC an issuer scope ("Quests from …") lists, through the shield: "Dawntrail character" for a
    /// person the story has not introduced. Null when the NPC hands out no quest.
    /// </summary>
    public static string? IssuerName(QuestCatalog catalog, uint npcId, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        return Discovery.QuestDiscovery.IssuerName(catalog, npcId) is { } name ? spoilers.Name(SpoilerKind.Npc, name) : null;
    }

    /// <summary>The quest's journal genre as it prints: the hidden area's placeholder in place of its name ("Dawntrail area 5 Sidequests").</summary>
    public static string Genre(QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(spoilers);
        return spoilers.NodeName(quest.Journal.GenreName);
    }

    /// <summary>The quest's journal category as it prints, through the shield as <see cref="Genre"/>.</summary>
    public static string Category(QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(spoilers);
        return spoilers.NodeName(quest.Journal.CategoryName);
    }

    /// <summary>
    /// The hidden area a journal node's name starts with (the one <see cref="SpoilerMask.NodeName"/> replaces): the
    /// whole name, or its longest leading run of words that is a hidden area. Null when the shield leaves it whole.
    /// What "Reveal this name" reveals on the node's placeholder.
    /// </summary>
    public static string? NodeArea(SpoilerMask spoilers, string? name)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        if (string.IsNullOrEmpty(name) || !spoilers.MasksNames)
        {
            return null;
        }

        if (spoilers.IsNameMasked(SpoilerKind.Area, name))
        {
            return name;
        }

        for (var end = name.LastIndexOf(' '); end > 0; end = name.LastIndexOf(' ', end - 1))
        {
            var lead = name[..end];
            if (spoilers.IsNameMasked(SpoilerKind.Area, lead))
            {
                return lead;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether travel to <paramref name="place"/> is hidden (spec-1.20 N6: "Travel buttons to a hidden place are not
    /// shown"): when the viewed character's story has not reached it (the pane names it by its placeholder), or the
    /// logged-in character's has not (the game's map, the one the trip uses, would name it). Either mask may be null.
    /// </summary>
    public static bool PlaceHidden(SpoilerMask? viewed, SpoilerMask? live, string? place)
    {
        if (string.IsNullOrEmpty(place))
        {
            return false;
        }

        return viewed?.IsNameMasked(SpoilerKind.Area, place) == true || live?.IsNameMasked(SpoilerKind.Area, place) == true;
    }

    /// <summary>
    /// Whether a duty row is masked, its own icon and name hidden: every quest it is shown through is masked
    /// (<see cref="SpoilerMask.HidesDuty"/>), or the wider shield hides the duty's own name ("Dungeon (Lv 97)").
    /// </summary>
    public static bool HidesDuty(SpoilerMask spoilers, IReadOnlyCollection<QuestRecord> shownThrough, string? name)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        return spoilers.HidesDuty(shownThrough) || spoilers.IsNameMasked(SpoilerKind.Duty, name);
    }

    /// <summary>The names of the duties a quest involves (<see cref="QuestDuties.For"/>), in its order; empty without an index.</summary>
    public static string[] DutyNames(QuestRecord quest, DutyRunIndex? index, CuratedData? curated, IReadOnlyList<UniqueRewardEntry>? rewardEntries)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (index is null)
        {
            return [];
        }

        var duties = QuestDuties.For(quest, index, curated, rewardEntries);
        var names = new string[duties.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = duties[i].Duty.Name;
        }

        return names;
    }

    /// <summary>
    /// The names "Reveal names in this quest" reveals (<see cref="SpoilerNames.NamesIn"/>): the giver, the place and its
    /// region, the duties the quest involves, its rewards and what it opens.
    /// </summary>
    public static List<(SpoilerKind Kind, string Name)> QuestNames(QuestRecord quest, QuestUnlocks unlocks, string? place, string? region, IEnumerable<string>? duties) =>
        SpoilerNames.NamesIn(quest, unlocks, place, region, duties);

    /// <summary>Whether the shield hides anything of the quest: its name, or one of <paramref name="names"/>.</summary>
    public static bool HidesAny(SpoilerMask spoilers, QuestRecord quest, IReadOnlyList<(SpoilerKind Kind, string Name)> names)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);
        if (spoilers.IsMasked(quest))
        {
            return true;
        }

        if (!spoilers.MasksNames)
        {
            return false;
        }

        foreach (var (kind, name) in names)
        {
            if (spoilers.IsNameMasked(kind, name))
            {
                return true;
            }
        }

        return false;
    }
}
