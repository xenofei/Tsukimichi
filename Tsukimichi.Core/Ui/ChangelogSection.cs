using System.Text;

namespace Tsukimichi.Core.Ui;

/// <summary>One <c>### Added</c>-style group of a changelog section with its bullet items.</summary>
/// <param name="Title">The heading without its <c>### </c> prefix; empty for bullets that precede any heading.</param>
/// <param name="Items">Each bullet's text, continuation lines joined with a space.</param>
public sealed record ChangelogGroup(string Title, IReadOnlyList<string> Items)
{
    /// <summary>
    /// Per item, whether it is a sub-bullet (an indented <c>- </c> under the bullet before it). Shorter than
    /// <see cref="Items"/> (or empty) reads as top-level for the items it does not cover.
    /// </summary>
    public IReadOnlyList<bool> Nested { get; init; } = [];

    /// <summary>Whether item <paramref name="index"/> is a sub-bullet.</summary>
    public bool IsNested(int index) => index >= 0 && index < Nested.Count && Nested[index];
}

/// <summary>
/// The section of a Keep-a-Changelog file for one version, as the in-app "What's new" card shows it. Parsed from
/// the embedded CHANGELOG.md at runtime; no network. The heading form is <c>## [x.y.z] - date</c>; <c>## [Unreleased]</c>
/// is never a version.
/// </summary>
/// <param name="Version">The three-part version the section is for.</param>
/// <param name="Date">The text after <c>] - </c> on the heading, or empty.</param>
/// <param name="Groups">The section's groups in file order; a bullet before any <c>###</c> heading lands in a group with an empty title.</param>
public sealed record ChangelogSection(string Version, string Date, IReadOnlyList<ChangelogGroup> Groups)
{
    private const string HeadingPrefix = "## [";
    private const string GroupPrefix = "### ";
    private const string BulletPrefix = "- ";
    private const string Bold = "**";

    /// <summary>True when at least one bullet was found.</summary>
    public bool HasItems => Groups.Any(g => g.Items.Count > 0);

    /// <summary>
    /// Finds the <c>## [version]</c> section in <paramref name="changelog"/> and returns it, or null when the file has
    /// no such section or the section carries no bullet. <paramref name="version"/> may be four-part (0.6.0.0);
    /// only the first three parts are matched.
    /// </summary>
    public static ChangelogSection? Find(string? changelog, string? version)
    {
        var wanted = NormalizeVersion(version);
        if (string.IsNullOrEmpty(changelog) || wanted.Length == 0)
        {
            return null;
        }

        foreach (var section in Parse(changelog))
        {
            if (section.Version == wanted)
            {
                return section.HasItems ? section : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Every released section the player has not seen yet, newest first (feature plan v5, 1.7.0 "What's new collects
    /// every version you skipped"): the sections whose version is above <paramref name="lastSeenVersion"/> and at most
    /// <paramref name="runningVersion"/>, each with at least one bullet. An empty <paramref name="lastSeenVersion"/> (a
    /// build before 0.6.0 recorded none) gives the running version's section alone, so such an update is not handed
    /// twenty releases at once. Empty when the running version is not a version, or the last seen one is not older.
    /// </summary>
    public static IReadOnlyList<ChangelogSection> Since(string? changelog, string? lastSeenVersion, string? runningVersion)
    {
        var running = ParseVersion(runningVersion);
        if (string.IsNullOrEmpty(changelog) || running is null)
        {
            return [];
        }

        if (ParseVersion(lastSeenVersion) is not { } seen)
        {
            return Find(changelog, runningVersion) is { } only ? [only] : [];
        }

        if (seen >= running)
        {
            return [];
        }

        var found = new List<(System.Version Version, ChangelogSection Section)>();
        foreach (var section in Parse(changelog))
        {
            if (section.HasItems && ParseVersion(section.Version) is { } version && version > seen && version <= running)
            {
                found.Add((version, section));
            }
        }

        found.Sort(static (a, b) => b.Version.CompareTo(a.Version));
        return found.Select(static f => f.Section).ToArray();
    }

    /// <summary>
    /// A bullet's highlight, for the card's short view: the bold lead when it is a whole phrase (<c>**Companion
    /// plugins.** Settings…</c> gives "Companion plugins."; a trailing colon is dropped), else the first sentence of the
    /// bullet's plain text, else the whole plain text.
    /// </summary>
    public static string Highlight(string? item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return string.Empty;
        }

        var text = item.Trim();
        if (text.StartsWith(Bold, StringComparison.Ordinal))
        {
            var close = text.IndexOf(Bold, Bold.Length, StringComparison.Ordinal);
            if (close > Bold.Length)
            {
                var lead = text[Bold.Length..close].Trim();
                var rest = text[(close + Bold.Length)..];
                var leadEnds = lead.Length > 0 && lead[^1] is '.' or ':' or '?' or '!';
                if (leadEnds && (rest.Length == 0 || rest[0] == ' '))
                {
                    return lead[^1] == ':' ? lead[..^1].TrimEnd() : lead;
                }
            }
        }

        return FirstSentence(Plain(text));
    }

    /// <summary>The item without Markdown emphasis (<c>**</c>) or code marks (<c>`</c>).</summary>
    public static string Plain(string? item) =>
        string.IsNullOrEmpty(item) ? string.Empty : item.Replace(Bold, string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>
    /// The text up to and including the first full stop, question or exclamation mark that ends a sentence (one followed
    /// by a space and a capital letter, a quote or an opening bracket, or by the end); the whole text when none does.
    /// </summary>
    public static string FirstSentence(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('.' or '?' or '!'))
            {
                continue;
            }

            if (i == text.Length - 1)
            {
                return text;
            }

            if (text[i + 1] == ' ' && i + 2 < text.Length && (char.IsUpper(text[i + 2]) || text[i + 2] is '"' or '“' or '(' or '/'))
            {
                return text[..(i + 1)];
            }
        }

        return text;
    }

    /// <summary>
    /// The first three dotted parts of <paramref name="version"/> ("0.6.0.0" becomes "0.6.0"); empty when null, blank
    /// or not dotted digits, so "Unreleased" never names a section.
    /// </summary>
    public static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var parts = version.Trim().Split('.');
        if (parts.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit)))
        {
            return string.Empty;
        }

        return parts.Length <= 3 ? string.Join('.', parts) : string.Join('.', parts, 0, 3);
    }

    /// <summary><see cref="NormalizeVersion"/> as a comparable version; null when it is not one.</summary>
    private static System.Version? ParseVersion(string? version)
    {
        var normalized = NormalizeVersion(version);
        if (normalized.Length == 0)
        {
            return null;
        }

        // "1" and "1.2" parse too; Version needs at least two parts.
        var parts = normalized.Split('.');
        var text = parts.Length == 1 ? normalized + ".0" : normalized;
        return System.Version.TryParse(text, out var parsed) ? parsed : null;
    }

    /// <summary>Every <c>## [x.y.z]</c> section of the file in file order, with or without bullets; <c>[Unreleased]</c> is skipped.</summary>
    private static List<ChangelogSection> Parse(string changelog)
    {
        var sections = new List<ChangelogSection>();
        string? version = null;
        var date = string.Empty;
        var groups = new List<ChangelogGroup>();
        var groupTitle = string.Empty;
        var items = new List<string>();
        var nested = new List<bool>();
        StringBuilder? current = null;
        var currentNested = false;

        void FlushItem()
        {
            if (current is { Length: > 0 })
            {
                items.Add(current.ToString());
                nested.Add(currentNested);
            }

            current = null;
            currentNested = false;
        }

        void FlushGroup()
        {
            if (items.Count > 0)
            {
                groups.Add(new ChangelogGroup(groupTitle, items.ToArray()) { Nested = nested.ToArray() });
            }

            items.Clear();
            nested.Clear();
        }

        void FlushSection()
        {
            FlushItem();
            FlushGroup();
            if (version is not null)
            {
                sections.Add(new ChangelogSection(version, date, groups.ToArray()));
            }

            groups.Clear();
            groupTitle = string.Empty;
            version = null;
            date = string.Empty;
        }

        foreach (var raw in changelog.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(HeadingPrefix, StringComparison.Ordinal))
            {
                FlushSection();
                var close = line.IndexOf(']', HeadingPrefix.Length);
                var name = close < 0 ? string.Empty : NormalizeVersion(line[HeadingPrefix.Length..close]);
                if (name.Length == 0 || name != line[HeadingPrefix.Length..close])
                {
                    continue;
                }

                version = name;
                var rest = line[(close + 1)..].Trim();
                date = rest.StartsWith('-') ? rest[1..].Trim() : rest;
                continue;
            }

            if (version is null)
            {
                continue;
            }

            if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
            {
                FlushItem();
                FlushGroup();
                groupTitle = line[GroupPrefix.Length..].Trim();
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(BulletPrefix, StringComparison.Ordinal))
            {
                FlushItem();
                current = new StringBuilder(trimmed[BulletPrefix.Length..].Trim());
                currentNested = trimmed.Length < line.Length;
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushItem();
                continue;
            }

            // A wrapped bullet continues on an indented or plain line; text outside a bullet is dropped.
            current?.Append(' ').Append(trimmed);
        }

        FlushSection();
        return sections;
    }
}

/// <summary>
/// Whether the "What's new" card shows: once, on the first open of the main window after an update, and never on a
/// fresh install. The decision is pure so it can be tested; the plugin persists <c>LastSeenVersion</c>.
/// </summary>
public enum WhatsNewDecision
{
    /// <summary>Nothing to do: the running version was already seen.</summary>
    Nothing,

    /// <summary>Fresh install (no version seen yet) or no changelog section for this version: record the version silently.</summary>
    RecordSilently,

    /// <summary>Show the card for the running version.</summary>
    Show,
}

public static class WhatsNew
{
    /// <param name="lastSeenVersion">
    /// The persisted last-seen version: empty on a fresh install, and also after an update from a build that did not
    /// record it yet (every release before 0.6.0), which <paramref name="hasPriorConfig"/> tells apart.
    /// </param>
    /// <param name="runningVersion">The plugin's assembly version.</param>
    /// <param name="hasSection">
    /// Whether the changelog has anything to show: a non-empty section for the running version, or for any version
    /// skipped since <paramref name="lastSeenVersion"/> (<see cref="ChangelogSection.Since"/>).
    /// </param>
    /// <param name="hasPriorConfig">
    /// Whether a configuration existed before this load (the file was there). An empty
    /// <paramref name="lastSeenVersion"/> with one is an update from a build that predates the card and shows it; without
    /// one it is a fresh install, which records the version silently.
    /// </param>
    public static WhatsNewDecision Decide(string? lastSeenVersion, string? runningVersion, bool hasSection, bool hasPriorConfig)
    {
        var running = ChangelogSection.NormalizeVersion(runningVersion);
        var seen = ChangelogSection.NormalizeVersion(lastSeenVersion);
        if (running.Length == 0 || seen == running)
        {
            return WhatsNewDecision.Nothing;
        }

        var freshInstall = seen.Length == 0 && !hasPriorConfig;
        return freshInstall || !hasSection ? WhatsNewDecision.RecordSilently : WhatsNewDecision.Show;
    }

    /// <summary>
    /// <see cref="Decide(string?, string?, bool, bool)"/> for a caller that cannot say whether a configuration
    /// existed: an empty last-seen version then reads as a fresh install, so an update from a build before 0.6.0
    /// records silently. Callers with the configuration at hand pass <c>hasPriorConfig</c>.
    /// </summary>
    public static WhatsNewDecision Decide(string? lastSeenVersion, string? runningVersion, bool hasSection) =>
        Decide(lastSeenVersion, runningVersion, hasSection, hasPriorConfig: false);
}
