using System.Text;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Chains;

/// <summary>One quest of a recap: its name as shown, and its journal entries in the order the journal gave them.</summary>
/// <param name="RowId">Quest sheet row id.</param>
/// <param name="Title">The quest's name.</param>
/// <param name="Entries">The journal entries, offer to turn-in; empty when the client has no text for the quest.</param>
public sealed record RecapChapter(uint RowId, string Title, IReadOnlyList<string> Entries);

/// <summary>
/// The story recap, "Previously…" (feature plan v5, collector extras; R9 F5): the journal text of the last few main
/// scenario quests a character completed, or of every completed quest of one chain, joined into one page to read
/// before picking the story up again. Spoiler-safe by construction: only completed quests are ever picked, and a
/// completed quest's whole journal is text the character has seen. Pure; the plugin reads each quest's text through
/// its journal text reader.
/// </summary>
public static class StoryRecap
{
    /// <summary>How many main scenario quests the recap reads by default (Settings › Display › Free trial and story recap).</summary>
    public const int DefaultLength = 10;

    /// <summary>The fewest quests the length setting allows.</summary>
    public const int MinLength = 3;

    /// <summary>The most quests the length setting allows.</summary>
    public const int MaxLength = 40;

    /// <summary><paramref name="length"/> within <see cref="MinLength"/> and <see cref="MaxLength"/>.</summary>
    public static int ClampLength(int length) => Math.Clamp(length, MinLength, MaxLength);

    /// <summary>
    /// The last <paramref name="count"/> quests of <paramref name="ordered"/> (play order) that
    /// <paramref name="isCompleted"/> says are done, still in play order; fewer when fewer are done, none when
    /// <paramref name="count"/> is not positive. A quest not done is never picked, wherever it sits.
    /// </summary>
    public static IReadOnlyList<QuestRecord> LastCompleted(IReadOnlyList<QuestRecord> ordered, Func<uint, bool> isCompleted, int count)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(isCompleted);
        if (count <= 0)
        {
            return [];
        }

        var picked = new List<QuestRecord>(Math.Min(count, ordered.Count));
        for (var i = ordered.Count - 1; i >= 0 && picked.Count < count; i--)
        {
            if (isCompleted(ordered[i].RowId))
            {
                picked.Add(ordered[i]);
            }
        }

        picked.Reverse();
        return picked;
    }

    /// <summary>
    /// The main scenario part of the recap: the last <paramref name="count"/> completed quests of the main scenario
    /// (<see cref="MsqGraph.Story"/>, sections 0 and 1 in journal order).
    /// </summary>
    public static IReadOnlyList<QuestRecord> MainScenario(QuestCatalog catalog, Func<uint, bool> isCompleted, int count)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return LastCompleted(MsqGraph.For(catalog).Story, isCompleted, count);
    }

    /// <summary>A chain's recap: every completed quest of <paramref name="chain"/>, in the chain's play order.</summary>
    public static IReadOnlyList<QuestRecord> Chain(Chain chain, QuestCatalog catalog, Func<uint, bool> isCompleted)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(isCompleted);
        var picked = new List<QuestRecord>();
        foreach (var rowId in chain.RowIds)
        {
            if (isCompleted(rowId) && catalog.GetByRowId(rowId) is { } quest)
            {
                picked.Add(quest);
            }
        }

        return picked;
    }

    /// <summary>
    /// The recap as plain text for the clipboard: <paramref name="heading"/>, then each chapter's title and its entries
    /// as paragraphs, chapters apart by a blank line. A chapter without text keeps its title, so the story's order
    /// stays readable.
    /// </summary>
    public static string Compose(string heading, IReadOnlyList<RecapChapter> chapters)
    {
        ArgumentNullException.ThrowIfNull(chapters);
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(heading))
        {
            text.Append(heading.Trim()).Append("\n\n");
        }

        for (var i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            text.Append(chapter.Title);
            foreach (var entry in chapter.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    text.Append("\n\n").Append(entry.Trim());
                }
            }

            if (i + 1 < chapters.Count)
            {
                text.Append("\n\n");
            }
        }

        return text.ToString().TrimEnd();
    }
}
