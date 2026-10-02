using System.Globalization;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The Settings window's sections (feature plan v5, 1.7.0 "Settings"; R1 proposal 5, R3 proposal 7), in the order
/// the section index lists them: <see cref="SettingsSections.Order"/>. The values are not persisted.
/// </summary>
public enum SettingsSection
{
    Display,
    TodoOverlay,
    Routes,
    Notices,
    Spoilers,
    Keyboard,
    Integrations,
    Data,
    Advanced,
    About,
}

/// <summary>The order of the Settings window's section index and of the search results.</summary>
public static class SettingsSections
{
    /// <summary>
    /// Display (with Look and Flair) first, the overlay and the road next, the notices and the shields, the keyboard,
    /// the other plugins, then the player's data, the rarely touched Advanced (polling, journal filing, the hook gate
    /// override) and About last.
    /// </summary>
    public static IReadOnlyList<SettingsSection> Order { get; } =
    [
        SettingsSection.Display,
        SettingsSection.TodoOverlay,
        SettingsSection.Routes,
        SettingsSection.Notices,
        SettingsSection.Spoilers,
        SettingsSection.Keyboard,
        SettingsSection.Integrations,
        SettingsSection.Data,
        SettingsSection.Advanced,
        SettingsSection.About,
    ];

    /// <summary>The number of sections.</summary>
    public static int Count => Order.Count;

    /// <summary>Where <paramref name="section"/> sits in <see cref="Order"/>; -1 for a value outside the enum.</summary>
    public static int IndexOf(SettingsSection section)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i] == section)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// What the Settings search box asks for: the text split into words at white space. A setting matches when every word
/// is found in its label, its hint or its keywords (any of them, in any order), ignoring letter case and accents, so
/// "ui scale" finds "Window scale" through its keywords and "overlay lock" finds "Locked (click-through)" under the
/// Todo overlay. An empty or blank text matches everything.
/// </summary>
public sealed class SettingsQuery
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    private readonly string[] terms;

    private SettingsQuery(string text, string[] terms)
    {
        Text = text;
        this.terms = terms;
    }

    /// <summary>The query that matches everything.</summary>
    public static SettingsQuery Empty { get; } = new(string.Empty, []);

    /// <summary>The text as typed.</summary>
    public string Text { get; }

    /// <summary>True when there are no words to look for: every setting shows.</summary>
    public bool IsEmpty => terms.Length == 0;

    /// <summary>The words looked for, in the order typed.</summary>
    public IReadOnlyList<string> Terms => terms;

    /// <summary>Splits <paramref name="text"/> into words; null or blank text gives <see cref="Empty"/>.</summary>
    public static SettingsQuery Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? Empty : new SettingsQuery(text, words);
    }

    /// <summary>
    /// Whether every word is found in <paramref name="label"/>, <paramref name="hint"/> or <paramref name="keywords"/>
    /// (each may be null). Always true for <see cref="Empty"/>. Allocates nothing.
    /// </summary>
    public bool Matches(string? label, string? hint = null, string? keywords = null)
    {
        foreach (var term in terms)
        {
            if (!Contains(label, term) && !Contains(hint, term) && !Contains(keywords, term))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string? text, string term) =>
        text is { Length: > 0 } && Compare.IndexOf(text, term, Options) >= 0;
}

/// <summary>
/// The Settings window's search, one frame at a time, without ImGui so it can be tested. The window calls
/// <see cref="BeginFrame"/>, then for each section it draws <see cref="BeginSection"/>, for each block of settings
/// <see cref="BeginBlock"/> and, when the block names itself, <see cref="Heading"/>, and for each setting
/// <see cref="Row"/>, which says whether the setting shows; <see cref="EndFrame"/> keeps the counts for the section
/// index. While a query is active the headings wait: <see cref="TakeSectionHeading"/> and
/// <see cref="TakeBlockHeading"/> hand them out once, just before the first setting of their section or block that
/// shows, so a section or block with no match prints nothing. A query that matches a section's title shows the whole
/// section, and one that matches a block's heading or keywords shows the whole block.
/// </summary>
public sealed class SettingsFilter
{
    private readonly int[] counting = new int[SettingsSections.Count];
    private readonly int[] shown = new int[SettingsSections.Count];
    private SettingsQuery query = SettingsQuery.Empty;
    private int sectionIndex;
    private bool sectionWhole;
    private bool sectionHeadingPending;
    private string? sectionTitle;
    private string? blockHeading;
    private int countingTotal;

    /// <summary>The query as typed.</summary>
    public string Text => query.Text;

    /// <summary>True while the query has words: only matching settings show, and headings wait for them.</summary>
    public bool Active => !query.IsEmpty;

    /// <summary>The parsed query.</summary>
    public SettingsQuery Query => query;

    /// <summary>The section <see cref="BeginSection"/> named last.</summary>
    public SettingsSection Section { get; private set; } = SettingsSection.Display;

    /// <summary>True when the current block shows whole: its section's title, its heading or its keywords matched.</summary>
    public bool BlockWhole { get; private set; }

    /// <summary>How many settings the current block registered through <see cref="Row"/>, shown or not.</summary>
    public int RowsInBlock { get; private set; }

    /// <summary>How many of the current block's settings show.</summary>
    public int VisibleInBlock { get; private set; }

    /// <summary>How many settings showed so far in this frame, across every section.</summary>
    public int VisibleThisFrame => countingTotal;

    /// <summary>How many settings showed in the last finished frame, across every section.</summary>
    public int ShownTotal { get; private set; }

    /// <summary>Replaces the query; returns false when the words did not change.</summary>
    public bool SetText(string? text)
    {
        var next = SettingsQuery.Parse(text);
        if (string.Equals(next.Text, query.Text, StringComparison.Ordinal))
        {
            return false;
        }

        query = next;
        return true;
    }

    /// <summary>Starts a frame: the counts start again from zero.</summary>
    public void BeginFrame()
    {
        Array.Clear(counting);
        countingTotal = 0;
        sectionHeadingPending = false;
        blockHeading = null;
        BlockWhole = false;
        RowsInBlock = 0;
        VisibleInBlock = 0;
    }

    /// <summary>Ends a frame: its counts become what <see cref="Shown"/> and <see cref="ShownTotal"/> report.</summary>
    public void EndFrame()
    {
        Array.Copy(counting, shown, counting.Length);
        ShownTotal = countingTotal;
    }

    /// <summary>Starts <paramref name="section"/>, titled <paramref name="title"/> (the title can match the query).</summary>
    public void BeginSection(SettingsSection section, string title)
    {
        Section = section;
        sectionIndex = Math.Max(0, SettingsSections.IndexOf(section));
        sectionTitle = title;
        sectionWhole = Active && query.Matches(title);
        sectionHeadingPending = Active;
        blockHeading = null;
        BlockWhole = sectionWhole;
        RowsInBlock = 0;
        VisibleInBlock = 0;
    }

    /// <summary>Starts a block of settings with optional search <paramref name="keywords"/> that show it whole.</summary>
    public void BeginBlock(string? keywords = null)
    {
        blockHeading = null;
        RowsInBlock = 0;
        VisibleInBlock = 0;
        BlockWhole = sectionWhole || (Active && keywords is { Length: > 0 } && query.Matches(keywords));
    }

    /// <summary>
    /// Names the current block. A heading equal to the section's title is not repeated (the section already says it).
    /// Returns whether the caller should draw the heading now: always without a query, and with one only when the block
    /// shows whole; otherwise it waits for <see cref="TakeBlockHeading"/>.
    /// </summary>
    public bool Heading(string heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        if (Active && !BlockWhole && query.Matches(heading))
        {
            BlockWhole = true;
        }

        blockHeading = string.Equals(heading, sectionTitle, StringComparison.Ordinal) ? null : heading;
        return !Active || BlockWhole;
    }

    /// <summary>
    /// One setting: whether it shows. Without a query every setting shows; with one, a setting shows when its block
    /// shows whole or when <paramref name="label"/>, <paramref name="hint"/> or <paramref name="keywords"/> match.
    /// </summary>
    public bool Row(string label, string? hint = null, string? keywords = null)
    {
        RowsInBlock++;
        if (Active && !BlockWhole && !query.Matches(label, hint, keywords))
        {
            return false;
        }

        VisibleInBlock++;
        counting[sectionIndex]++;
        countingTotal++;
        return true;
    }

    /// <summary>True once per section, while a query is active, the first time it is asked: draw the section's title.</summary>
    public bool TakeSectionHeading()
    {
        if (!sectionHeadingPending)
        {
            return false;
        }

        sectionHeadingPending = false;
        return true;
    }

    /// <summary>The current block's heading, once; null when it was taken, never given or equals the section title.</summary>
    public string? TakeBlockHeading()
    {
        var heading = blockHeading;
        blockHeading = null;
        return heading;
    }

    /// <summary>How many settings of <paramref name="section"/> showed in the last finished frame.</summary>
    public int Shown(SettingsSection section)
    {
        var index = SettingsSections.IndexOf(section);
        return index < 0 ? 0 : shown[index];
    }
}
