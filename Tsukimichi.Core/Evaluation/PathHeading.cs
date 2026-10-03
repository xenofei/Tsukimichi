using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// What the Path card's header says (feature plan v6 U5, owner point 8): how many quests still stand before the one
/// shown and which of them to do next, never the size of the whole road ("831 steps · 830 done" counted the main
/// scenario back to A Realm Reborn). Nothing at all when the quest is the next one to do or already done, since the
/// state pill says so; the totals live in <see cref="Tooltip"/>.
/// </summary>
/// <param name="QuestsBefore">Quests on the path before the target that are not completed.</param>
/// <param name="EarlierDone">Quests on the path before the target that are completed.</param>
/// <param name="Earlier">Quests on the path before the target.</param>
/// <param name="NextIndex">Path index of the first quest before the target that is not completed; -1 for none.</param>
public sealed record PathHeading(int QuestsBefore, int EarlierDone, int Earlier, int NextIndex)
{
    /// <summary>Nothing to say: no character read, a quest with no earlier quests, or the target done.</summary>
    public static readonly PathHeading None = new(0, 0, 0, -1);

    /// <summary>Whether an earlier quest is the one to do, so the header offers it as "Next".</summary>
    public bool HasNext => NextIndex >= 0;

    /// <summary>"3 quests before this one"; empty when nothing stands before the quest.</summary>
    public string Caption => QuestsBefore switch
    {
        <= 0 => string.Empty,
        1 => CoreText.T("Core.Path.QuestsBeforeOne", "1 quest before this one"),
        _ => F("Core.Path.QuestsBefore", "{0:N0} quests before this one", QuestsBefore),
    };

    /// <summary>The totals, for the caption's hover: "828 of 831 earlier quests done"; empty while there is no caption.</summary>
    public string Tooltip => QuestsBefore <= 0 ? string.Empty : F("Core.Path.EarlierDone", "{0:N0} of {1:N0} earlier quests done", EarlierDone, Earlier);

    /// <summary>
    /// The heading for <paramref name="path"/> (first step to target, as <see cref="PathFinder.PathTo"/> returns it).
    /// <paramref name="statesKnown"/> false (no character read yet) says nothing: the chart already says so.
    /// </summary>
    public static PathHeading For(IReadOnlyList<PathStep> path, bool statesKnown)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!statesKnown || path.Count <= 1 || path[^1].Done)
        {
            return None;
        }

        var earlier = path.Count - 1;
        var done = 0;
        var next = -1;
        for (var i = 0; i < earlier; i++)
        {
            if (path[i].Done)
            {
                done++;
            }
            else if (next < 0)
            {
                next = i;
            }
        }

        return next < 0 ? None : new PathHeading(earlier - done, done, earlier, next);
    }

    private static string F(string key, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);
}
