using System.Globalization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Judges the mounts a quest needs owned (1.11.0, C2): the sheet's <see cref="QuestRecord.MountRequired"/> and the
/// mount-collection gates of <c>curated/game_gates.json</c> (<see cref="QuestGate.Mounts"/>: the seven Lanners before
/// the Firebird, the seven Kamuy before Kamuy of the Nine Tails, …). The answers come from the owned mounts the capture
/// read with the collectibles (<see cref="CharacterSnapshot.Collectibles"/>, whose targets include
/// <see cref="QuestCatalog.MountWatch"/>), so a stored character is judged as it was last captured. A mount the capture
/// did not read (a file from before 1.11.0) leaves the requirement not checked: never met, so the quest reads Not
/// checked where it would read Ready, and never Blocked by it alone.
/// </summary>
public static class MountCheck
{
    /// <summary>The <see cref="CharacterSnapshot.Collectibles"/> key the owned mounts are saved under.</summary>
    private static readonly string MountKind = RewardKind.Mount.ToString();

    /// <summary>
    /// The requirement result for owning every one of <paramref name="mounts"/>, its detail in English.
    /// <paramref name="gate"/> is a collection gate's phrase ("all seven Heavensward Lanners"); null for the sheet's mount.
    /// </summary>
    public static RequirementResult Evaluate(IReadOnlyList<uint> mounts, string? gate, CharacterSnapshot s, Func<uint, string> mountName)
    {
        ArgumentNullException.ThrowIfNull(mounts);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(mountName);

        var all = mounts.Distinct().Order().ToArray();
        var missing = new List<uint>();
        var unread = false;
        foreach (var id in all)
        {
            switch (Owns(s, id))
            {
                case true:
                    break;
                case false:
                    missing.Add(id);
                    break;
                default:
                    unread = true;
                    break;
            }
        }

        bool? has = unread ? null : missing.Count == 0;
        var requirement = new MountRequirement(has) { Mounts = all, Missing = [.. missing], Gate = gate };
        return new(requirement, has == true, Detail(all, missing, has, gate, mountName));
    }

    /// <summary>Whether the capture read <paramref name="mountId"/> as owned; null when it did not read it.</summary>
    public static bool? Owns(CharacterSnapshot s, uint mountId)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!s.Collectibles.TryGetValue(MountKind, out var set) || set is null)
        {
            return null;
        }

        if (Contains(set.Owned, mountId))
        {
            return true;
        }

        return Contains(set.Missing, mountId) ? false : null;
    }

    /// <summary>
    /// "requires a mount: company chocobo", "needs all seven Heavensward Lanners, you have 4 of 7 (missing: rose lanner,
    /// dark lanner and Demonic lanner)"; a mount without a name reads "mount 76".
    /// </summary>
    private static string Detail(uint[] all, List<uint> missing, bool? has, string? gate, Func<uint, string> mountName)
    {
        if (gate is null)
        {
            var named = all.Length == 1 ? mountName(all[0]) : string.Empty;
            var which = Join([.. all], mountName);
            return has switch
            {
                true => named.Length > 0 ? $"owns the {named}" : "mount available",
                false => $"requires a mount: {which}",
                null => $"requires a mount: {which}, not checked",
            };
        }

        return has switch
        {
            true => $"has {gate}",
            false => string.Create(CultureInfo.InvariantCulture, $"needs {gate}, you have {all.Length - missing.Count} of {all.Length} (missing: {Join(missing, mountName)})"),
            null => $"needs {gate}, not checked",
        };
    }

    private static string Name(uint id, Func<uint, string> mountName) =>
        mountName(id) is { Length: > 0 } name ? name : "mount " + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string Join(List<uint> ids, Func<uint, string> mountName)
    {
        var names = ids.Select(id => Name(id, mountName)).ToList();
        return names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }

    private static bool Contains(IReadOnlyList<uint>? list, uint id)
    {
        if (list is null)
        {
            return false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
