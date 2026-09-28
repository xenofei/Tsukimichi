using System.Text;

namespace Tsukimichi.Core.Ui;

/// <summary>One <c>### Added</c>-style group of a changelog section with its bullet items.</summary>
/// <param name="Title">The heading without its <c>### </c> prefix; empty for bullets that precede any heading.</param>
/// <param name="Items">Each bullet's text, continuation lines joined with a space.</param>
public sealed record ChangelogGroup(string Title, IReadOnlyList<string> Items);

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

        var groups = new List<ChangelogGroup>();
        var date = string.Empty;
        var capturing = false;
        var groupTitle = string.Empty;
        var items = new List<string>();
        StringBuilder? current = null;

        foreach (var raw in changelog.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(HeadingPrefix, StringComparison.Ordinal))
            {
                if (capturing)
                {
                    break;
                }

                var close = line.IndexOf(']', HeadingPrefix.Length);
                if (close < 0 || line[HeadingPrefix.Length..close] != wanted)
                {
                    continue;
                }

                capturing = true;
                var rest = line[(close + 1)..].Trim();
                date = rest.StartsWith('-') ? rest[1..].Trim() : rest;
                continue;
            }

            if (!capturing)
            {
                continue;
            }

            if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
            {
                FlushItem(items, ref current);
                FlushGroup(groups, groupTitle, items);
                groupTitle = line[GroupPrefix.Length..].Trim();
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(BulletPrefix, StringComparison.Ordinal))
            {
                FlushItem(items, ref current);
                current = new StringBuilder(trimmed[BulletPrefix.Length..].Trim());
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushItem(items, ref current);
                continue;
            }

            // A wrapped bullet continues on an indented or plain line; text outside a bullet is dropped.
            current?.Append(' ').Append(trimmed);
        }

        if (!capturing)
        {
            return null;
        }

        FlushItem(items, ref current);
        FlushGroup(groups, groupTitle, items);
        var section = new ChangelogSection(wanted, date, groups);
        return section.HasItems ? section : null;
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

    private static void FlushItem(List<string> items, ref StringBuilder? current)
    {
        if (current is { Length: > 0 })
        {
            items.Add(current.ToString());
        }

        current = null;
    }

    private static void FlushGroup(List<ChangelogGroup> groups, string title, List<string> items)
    {
        if (items.Count > 0)
        {
            groups.Add(new ChangelogGroup(title, items.ToArray()));
        }

        items.Clear();
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
    /// <param name="lastSeenVersion">The persisted last-seen version, empty on a fresh install.</param>
    /// <param name="runningVersion">The plugin's assembly version.</param>
    /// <param name="hasSection">Whether the changelog has a non-empty section for the running version.</param>
    public static WhatsNewDecision Decide(string? lastSeenVersion, string? runningVersion, bool hasSection)
    {
        var running = ChangelogSection.NormalizeVersion(runningVersion);
        var seen = ChangelogSection.NormalizeVersion(lastSeenVersion);
        if (running.Length == 0 || seen == running)
        {
            return WhatsNewDecision.Nothing;
        }

        return seen.Length == 0 || !hasSection ? WhatsNewDecision.RecordSilently : WhatsNewDecision.Show;
    }
}
