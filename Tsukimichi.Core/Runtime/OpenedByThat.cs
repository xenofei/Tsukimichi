using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Runtime;

/// <summary>What one or more quest completions close together opened: the completed quests and the quests that became available.</summary>
/// <param name="Serial">Counts up per batch taken this session; the "[Show]" link carries it.</param>
/// <param name="Completed">Row ids completed in the batch, in event order.</param>
/// <param name="Opened">Row ids that became available (Ready or Ready on another job) in the batch, in event order, each once.</param>
public sealed record OpenedBatch(uint Serial, IReadOnlyList<uint> Completed, IReadOnlyList<uint> Opened);

/// <summary>
/// "Opened by that" (feature plan v5, 1.7.0; R9 F1): gathers the quests that became available after a completion, so
/// one chat line can say what the turn-in opened. A <see cref="QuestEventKind.Completed"/> event opens a batch (or
/// extends the open one); every <see cref="QuestEventKind.NewlyAvailable"/> event that follows while the batch is open
/// joins it. The poller re-evaluates a poll or two after the completion, so the batch closes only once nothing new
/// arrived for a quiet spell (<see cref="Take"/>'s <c>quiet</c>, a few seconds), or after <see cref="MaxWait"/> at the
/// latest; several turn-ins in a row become one line.
/// <para>
/// A quest that became available with no completion before it (a level-up, a new day, an event starting) is not news
/// for this line; neither is a quest that was itself completed in the batch (a repeatable coming back). A batch that
/// opened nothing is dropped. Not thread-safe: the framework thread owns it.
/// </para>
/// </summary>
public sealed class OpenedBatcher
{
    /// <summary>The longest a batch stays open after its first completion, however busy the events keep it.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly List<uint> completed = [];
    private readonly List<uint> opened = [];
    private readonly HashSet<uint> seen = [];
    private DateTime startedUtc;
    private DateTime lastActivityUtc;
    private uint serial;

    /// <summary>Whether a batch is open.</summary>
    public bool Pending { get; private set; }

    /// <summary>
    /// Folds in events in the order they happened (earlier polls first, as <see cref="NoticeTracker.ScanEvents"/> lists
    /// them; a completion comes before the availability changes of its own poll). <paramref name="nowUtc"/> is when they
    /// were read, which starts or extends the quiet spell.
    /// </summary>
    public void Add(IReadOnlyList<QuestEvent> events, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case QuestEventKind.Completed:
                    if (!Pending)
                    {
                        Pending = true;
                        startedUtc = nowUtc;
                    }

                    lastActivityUtc = nowUtc;
                    if (!completed.Contains(e.RowId))
                    {
                        completed.Add(e.RowId);
                    }

                    // A quest completed in this batch never counts as opened by it (a repeatable that came back).
                    if (opened.Remove(e.RowId))
                    {
                        seen.Remove(e.RowId);
                    }

                    break;

                case QuestEventKind.NewlyAvailable when Pending:
                    if (!completed.Contains(e.RowId) && seen.Add(e.RowId))
                    {
                        opened.Add(e.RowId);
                        lastActivityUtc = nowUtc;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Closes the open batch once nothing joined it for <paramref name="quiet"/> (or <see cref="MaxWait"/> passed since it
    /// opened) and returns it; null while it is still gathering, when none is open, or when it opened nothing.
    /// </summary>
    public OpenedBatch? Take(DateTime nowUtc, TimeSpan quiet)
    {
        if (!Pending || (nowUtc - lastActivityUtc < quiet && nowUtc - startedUtc < MaxWait))
        {
            return null;
        }

        OpenedBatch? batch = null;
        if (opened.Count > 0)
        {
            serial = serial >= ChatLinkRegistry.MaxValue ? 1 : serial + 1;
            batch = new OpenedBatch(serial, [.. completed], [.. opened]);
        }

        Reset();
        return batch;
    }

    /// <summary>Drops the open batch (a logout or another character).</summary>
    public void Reset()
    {
        Pending = false;
        completed.Clear();
        opened.Clear();
        seen.Clear();
    }
}

/// <summary>How the quests one batch opened divide up for the "Opened:" line.</summary>
/// <param name="MainScenario">Main scenario quests (<see cref="FeaturePresets.IsMainScenario"/>).</param>
/// <param name="Feature">Feature quests (the session's feature set), main scenario ones left to that count.</param>
/// <param name="Side">Quests of the Sidequests journal section that are neither of the above.</param>
/// <param name="SideStories">Of <paramref name="Side"/>, the story sidequests (<see cref="Chains.StorySidequests"/>).</param>
/// <param name="Other">Everything else: job, allied society, Grand Company, seasonal and other quests.</param>
public readonly record struct OpenedCounts(int MainScenario, int Feature, int Side, int SideStories, int Other)
{
    public int Total => MainScenario + Feature + Side + Other;
}

/// <summary>
/// The counts and the words of the "Opened:" line ("2 unlock quests, 8 side quests (3 with a story)"). Counts only:
/// the line names no quest, so the spoiler shield has nothing to mask in it; the quests are listed (masked as usual) in
/// the main window when the player clicks Show.
/// </summary>
public static class OpenedSummary
{
    /// <summary>Counts <paramref name="rowIds"/> by kind; ids the catalog does not know are skipped.</summary>
    public static OpenedCounts Count(QuestCatalog catalog, IEnumerable<uint> rowIds, IReadOnlySet<uint> featureIds, IReadOnlySet<uint> storyIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rowIds);
        ArgumentNullException.ThrowIfNull(featureIds);
        ArgumentNullException.ThrowIfNull(storyIds);

        int msq = 0, feature = 0, side = 0, stories = 0, other = 0;
        foreach (var rowId in rowIds)
        {
            if (catalog.GetByRowId(rowId) is not { } quest)
            {
                continue;
            }

            if (FeaturePresets.IsMainScenario(quest))
            {
                msq++;
            }
            else if (featureIds.Contains(rowId))
            {
                feature++;
            }
            else if (quest.Journal.SectionId == Chains.StorySidequests.SidequestSectionId)
            {
                side++;
                if (storyIds.Contains(rowId))
                {
                    stories++;
                }
            }
            else
            {
                other++;
            }
        }

        return new OpenedCounts(msq, feature, side, stories, other);
    }

    /// <summary>"1 main scenario quest, 2 unlock quests, 8 side quests (3 with a story), 4 other quests", empty kinds left out; empty when nothing opened.</summary>
    public static string Text(OpenedCounts counts)
    {
        var parts = new List<string>(4);
        Add(parts, counts.MainScenario, CoreText.T("Core.Opened.MsqOne", "{0} main scenario quest"), CoreText.T("Core.Opened.MsqMany", "{0} main scenario quests"));
        Add(parts, counts.Feature, CoreText.T("Core.Opened.FeatureOne", "{0} unlock quest"), CoreText.T("Core.Opened.FeatureMany", "{0} unlock quests"));
        if (counts.Side > 0)
        {
            var side = Format(counts.Side == 1 ? CoreText.T("Core.Opened.SideOne", "{0} side quest") : CoreText.T("Core.Opened.SideMany", "{0} side quests"), counts.Side);
            parts.Add(counts.SideStories > 0
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Opened.WithStory", "{0} ({1} with a story)"), side, counts.SideStories)
                : side);
        }

        Add(parts, counts.Other, CoreText.T("Core.Opened.OtherOne", "{0} other quest"), CoreText.T("Core.Opened.OtherMany", "{0} other quests"));
        return string.Join(CoreText.T("Core.Opened.Separator", ", "), parts);
    }

    private static void Add(List<string> parts, int count, string one, string many)
    {
        if (count > 0)
        {
            parts.Add(Format(count == 1 ? one : many, count));
        }
    }

    private static string Format(string format, int count) => string.Format(CultureInfo.CurrentCulture, format, count);
}
