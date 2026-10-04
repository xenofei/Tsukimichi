using System.Globalization;
using System.Text;
using System.Text.Json;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Releases;

/// <summary>
/// One point of a release's What's new page (spec-1.22 W1, "The words"): a short semibold lead of a few words, then one
/// plain sentence, both written for players. <see cref="Line"/> is the two as one line, for a tooltip or the manifest.
/// </summary>
public sealed record ReleasePoint(string Lead, string Text)
{
    /// <summary>The lead and its sentence as one line ("Up next. Tonight starts with …").</summary>
    public string Line => Lead + " " + Text;
}

/// <summary>One release's page: its version, the day it shipped, its name and its 3 to 5 points.</summary>
public sealed record ReleaseNote(string Version, DateOnly Date, string Name, IReadOnlyList<ReleasePoint> Points)
{
    /// <summary>"4 October 2026": the date under a page's title. English only, as the notes are.</summary>
    public string LongDate => Date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"4 Oct": the date on a row of Settings › About › What's new.</summary>
    public string ShortDate => Date.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>
    /// The release as plain text for Dalamud's installer listing (spec-1.22 U1, "The plain notes in Dalamud"): the name,
    /// a blank line, then one "• lead sentence" line per point. <c>tools/make_pluginmaster.py</c> writes exactly this
    /// into the manifest's changelog (a test runs it and compares).
    /// </summary>
    public string ManifestText()
    {
        var text = new StringBuilder(Name).Append('\n');
        foreach (var point in Points)
        {
            text.Append('\n').Append(ManifestBullet).Append(point.Line);
        }

        return text.ToString();
    }

    /// <summary>The bullet in front of each point in <see cref="ManifestText"/>.</summary>
    public const string ManifestBullet = "• ";
}

/// <summary>
/// <c>Data/curated/whats_new.json</c> (spec-1.22 W1, "The words"): the plain notes of every release since 1.14.0, newest
/// first, written for players and kept apart from the technical CHANGELOG.md. The What's new popup pages through them,
/// Settings › About › What's new lists them, and <c>tools/make_pluginmaster.py</c> writes a release's points into
/// Dalamud's installer listing. Shape:
/// <code>
/// { "schema": 1, "note": "...", "releases": [ { "version": "1.22.0", "date": "2026-10-05", "name": "Welcome home",
///   "points": [ { "lead": "What's new, in pictures.", "text": "After each update, ..." } ] } ] }
/// </code>
/// Strict JSON, as every curated file. A release that does not parse is left out with a warning; the rest load.
/// Immutable.
/// </summary>
public sealed class ReleaseNotes
{
    public const string FileName = "whats_new.json";

    /// <summary>The fewest and most points a release has (spec-1.22 W1).</summary>
    public const int MinPoints = 3;

    /// <inheritdoc cref="MinPoints"/>
    public const int MaxPoints = 5;

    private const string ReleasesKey = "releases";

    private ReleaseNotes(IReadOnlyList<ReleaseNote> releases, IReadOnlyList<string> warnings)
    {
        Releases = releases;
        Warnings = warnings;
    }

    /// <summary>No releases: the file is missing or unreadable.</summary>
    public static readonly ReleaseNotes Empty = new([], []);

    /// <summary>Every release in the file, newest first.</summary>
    public IReadOnlyList<ReleaseNote> Releases { get; }

    /// <summary>What did not load, one line each, for the log.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The release whose version is <paramref name="version"/> (four parts allowed: "1.21.0.0"), or null.</summary>
    public ReleaseNote? Find(string? version)
    {
        var wanted = ChangelogSection.NormalizeVersion(version);
        if (wanted.Length == 0)
        {
            return null;
        }

        foreach (var release in Releases)
        {
            if (release.Version == wanted)
            {
                return release;
            }
        }

        return null;
    }

    /// <summary>
    /// The pages of the popup after an update (spec-1.22 W1, "Several releases at once"): every release above
    /// <paramref name="lastSeenVersion"/> and at most <paramref name="runningVersion"/>, newest first. An empty or
    /// unreadable last-seen version (a build before 0.6.0 recorded none) gives the running release alone. Empty when the
    /// running version is not a version or the last seen one is not older.
    /// </summary>
    public IReadOnlyList<ReleaseNote> Since(string? lastSeenVersion, string? runningVersion)
    {
        if (ParseVersion(runningVersion) is not { } running)
        {
            return [];
        }

        if (ParseVersion(lastSeenVersion) is not { } seen)
        {
            return Find(runningVersion) is { } only ? [only] : [];
        }

        if (seen >= running)
        {
            return [];
        }

        var found = new List<ReleaseNote>();
        foreach (var release in Releases)
        {
            if (ParseVersion(release.Version) is { } version && version > seen && version <= running)
            {
                found.Add(release);
            }
        }

        return found;
    }

    /// <summary>
    /// Settings › About › What's new (spec-1.22 W3): every release up to <paramref name="runningVersion"/>, newest
    /// first, so a build never lists notes for a version it is not; every release when the version is unreadable.
    /// </summary>
    public IReadOnlyList<ReleaseNote> History(string? runningVersion)
    {
        if (ParseVersion(runningVersion) is not { } running)
        {
            return Releases;
        }

        var found = new List<ReleaseNote>(Releases.Count);
        foreach (var release in Releases)
        {
            if (ParseVersion(release.Version) is { } version && version <= running)
            {
                found.Add(release);
            }
        }

        return found;
    }

    /// <summary>Reads <paramref name="path"/>; a missing or unreadable file gives no releases and one warning.</summary>
    public static ReleaseNotes Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : new ReleaseNotes([], [$"{FileName} is missing"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ReleaseNotes([], [$"{FileName} could not be read: {ex.Message}"]);
        }
    }

    /// <summary>Parses the file's text. Releases come out newest first whatever their order in the file.</summary>
    public static ReleaseNotes Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var warnings = new List<string>();
        var releases = new List<(Version Version, ReleaseNote Note)>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        }
        catch (JsonException ex)
        {
            return new ReleaseNotes([], [$"{FileName} is not valid JSON: {ex.Message}"]);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty(ReleasesKey, out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return new ReleaseNotes([], [$"{FileName} has no \"{ReleasesKey}\" list"]);
            }

            var index = 0;
            foreach (var item in list.EnumerateArray())
            {
                if (ParseRelease(item, out var note, out var error) && ParseVersion(note!.Version) is { } version)
                {
                    if (releases.Exists(r => r.Version == version))
                    {
                        warnings.Add($"release {note.Version} is listed twice; the first is kept");
                    }
                    else
                    {
                        releases.Add((version, note));
                    }
                }
                else
                {
                    warnings.Add($"release #{index + 1}: {error ?? "not a version"}");
                }

                index++;
            }
        }

        releases.Sort(static (a, b) => b.Version.CompareTo(a.Version));
        return new ReleaseNotes(releases.ConvertAll(static r => r.Note), warnings);
    }

    private static bool ParseRelease(JsonElement item, out ReleaseNote? note, out string? error)
    {
        note = null;
        if (item.ValueKind != JsonValueKind.Object)
        {
            error = "not an object";
            return false;
        }

        var version = ChangelogSection.NormalizeVersion(Text(item, "version"));
        var name = Text(item, "name").Trim();
        if (version.Length == 0 || version.Split('.').Length != 3)
        {
            error = "no x.y.z version";
            return false;
        }

        if (name.Length == 0)
        {
            error = $"{version} has no name";
            return false;
        }

        if (!DateOnly.TryParseExact(Text(item, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            error = $"{version} has no yyyy-MM-dd date";
            return false;
        }

        var points = new List<ReleasePoint>();
        if (item.TryGetProperty("points", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var point in list.EnumerateArray())
            {
                var lead = point.ValueKind == JsonValueKind.Object ? Text(point, "lead").Trim() : string.Empty;
                var text = point.ValueKind == JsonValueKind.Object ? Text(point, "text").Trim() : string.Empty;
                if (lead.Length == 0 || text.Length == 0)
                {
                    error = $"{version} has a point without its lead or its sentence";
                    return false;
                }

                points.Add(new ReleasePoint(lead, text));
            }
        }

        if (points.Count is < MinPoints or > MaxPoints)
        {
            error = $"{version} has {points.Count} points, not {MinPoints} to {MaxPoints}";
            return false;
        }

        note = new ReleaseNote(version, date, name, points);
        error = null;
        return true;
    }

    private static string Text(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    /// <summary>A dotted version as a comparable <see cref="Version"/> ("1.21.0.0" as 1.21.0); null when it is not one.</summary>
    public static Version? ParseVersion(string? version)
    {
        var normalized = ChangelogSection.NormalizeVersion(version);
        if (normalized.Length == 0)
        {
            return null;
        }

        var parts = normalized.Split('.');
        return Version.TryParse(parts.Length == 1 ? normalized + ".0" : normalized, out var parsed) ? parsed : null;
    }
}

/// <summary>
/// The release notes for anything outside the popup (spec-1.22 U1: the update note's hover, the moon icon's quick card):
/// the plain points of a version, from the notes the plugin loaded once at start (<see cref="Use"/>). Empty for a
/// version the shipped file does not know, such as a newer one Dalamud is offering; that caller then falls back to the
/// installer's own changelog.
/// </summary>
public static class WhatsNewNotes
{
    private static volatile ReleaseNotes current = ReleaseNotes.Empty;

    /// <summary>The notes loaded at start; <see cref="ReleaseNotes.Empty"/> until then.</summary>
    public static ReleaseNotes Current => current;

    /// <summary>Sets the notes every caller reads (the plugin, once at load).</summary>
    public static void Use(ReleaseNotes notes) => current = notes ?? ReleaseNotes.Empty;

    /// <summary>The plain points of <paramref name="version"/>; empty when the shipped notes do not know it.</summary>
    public static IReadOnlyList<ReleasePoint> For(string? version) => current.Find(version)?.Points ?? [];
}
