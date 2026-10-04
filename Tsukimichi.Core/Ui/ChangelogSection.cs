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
/// The section of a Keep-a-Changelog file for one version. The technical CHANGELOG.md is the record the release
/// workflow and GitHub show; the tests hold it to its form with this parser. The plugin itself no longer reads it: since
/// 1.22 (spec-1.22 W1, W4) players get the plain notes of <c>whats_new.json</c> (<see cref="Tsukimichi.Core.Releases.ReleaseNotes"/>).
/// <see cref="NormalizeVersion"/> is the one version reading every caller shares. The heading form is
/// <c>## [x.y.z] - date</c>; <c>## [Unreleased]</c> is never a version.
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
