using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The Questionable cross-check gate (1.5.0 Gates), offline: every prerequisite link Questionable adds by hand, as
/// committed in <c>docs/data/questionable-prerequisites.json</c>, is implied by the catalog the plugin builds (previous
/// quests, accept conditions, curated extras, directly or through another prerequisite) or excused by a live
/// <c>prereqs</c>/<c>questionable</c> entry of <c>docs/data/verification-allowlist.json</c>; no such entry outlives its
/// link. <c>Tsukimichi.Verify questionable</c> runs the same check against the installed game.
/// </summary>
[Trait("Category", "Curated")]
public sealed class QuestionableLinksTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>, IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static PrerequisiteLinks Links() => PrerequisiteLinks.Load(Path.Combine(ExtraPrerequisitesDataTests.DocsDataDir(), PrerequisiteLinks.FileName));

    [Fact]
    public void The_committed_links_name_their_source_commit()
    {
        var links = Links();
        Assert.Equal("https://github.com/PunishXIV/Questionable", links.Source);
        Assert.Equal("Questionable/Data/QuestData.cs", links.File);
        Assert.Matches("^[0-9a-f]{40}$", links.Commit);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", links.Extracted);
        Assert.True(links.Pairs.Count >= 90, $"only {links.Pairs.Count} links; was the file truncated?");
    }

    [Fact]
    public void Every_Questionable_link_is_implied_by_the_catalog_or_allowlisted()
    {
        var current = VerificationAllowlistTests.PluginVersion();
        var excuses = VerificationAllowlistTests.Entries()
            .Where(e => (string?)e["fact"] == "prereqs" && (string?)e["source"] == "questionable")
            .Where(e => !VerificationAllowlistTests.Expired(e, current))
            .Select(e => (Quest: uint.Parse((string)e["rowId"]!, CultureInfo.InvariantCulture), Required: (string?)e["prereqId"]))
            .ToList();
        var catalog = fixture.Bundle.Catalog;

        var gaps = PrerequisiteCoverage.Check(catalog, Links().Pairs);
        var used = new HashSet<int>();
        var open = new List<string>();
        foreach (var gap in gaps)
        {
            var index = excuses.FindIndex(e => e.Quest == gap.QuestRowId && (e.Required is null || e.Required == gap.RequiredRowId.ToString(CultureInfo.InvariantCulture)));
            if (index >= 0)
            {
                used.Add(index);
                continue;
            }

            open.Add($"{gap.QuestRowId} {catalog.GetByRowId(gap.QuestRowId)?.Name} <- {gap.RequiredRowId} {catalog.GetByRowId(gap.RequiredRowId)?.Name} ({gap.Gap})");
        }

        Assert.True(open.Count == 0,
            "Questionable links the catalog does not imply; add each to curated/extra_prerequisites.json with a second source, or allowlist it (fact prereqs, source questionable, prereqId) with a reason:\n"
            + string.Join("\n", open));

        var stale = excuses.Where((_, i) => !used.Contains(i)).Select(e => $"{e.Quest} -> {e.Required ?? "*"}").ToList();
        Assert.True(stale.Count == 0, "verification-allowlist.json questionable entries that excuse no open link; remove them: " + string.Join(", ", stale));
    }

    [Fact]
    public void Extraction_keeps_only_live_calls_with_their_constants_resolved()
    {
        // A made-up file in the shape of the calls: two live, one commented out, one in a block comment, one with an
        // empty id, a named constant and a repeat.
        const string code = """
            const int someQuest = 2095;
            AddPreviousQuest(new(3246), new(3314));
            AddPreviousQuest(new QuestId(3655), new QuestId(someQuest));
            // AddPreviousQuest(new(142), new(1212));
            /* AddPreviousQuest(new QuestId(3811), new QuestId(2922)); */
            AddPreviousQuest(new QuestId(1036), new QuestId());
            AddPreviousQuest(new(3246), new(3314));
            """;

        Assert.Equal([(68782u, 68850u), (69191u, 67631u)], PrerequisiteLinks.Extract(code));
    }

    [Fact]
    public void Links_round_trip_through_the_file_and_bad_files_are_refused()
    {
        var path = tmp.File(PrerequisiteLinks.FileName);
        var links = new PrerequisiteLinks("https://github.com/PunishXIV/Questionable", "new-main", new string('a', 40), "Questionable/Data/QuestData.cs", "2026-10-01", [(69191, 67631), (68782, 68850)]);
        links.Write(path);

        var read = PrerequisiteLinks.Load(path);
        Assert.Equal([(68782u, 68850u), (69191u, 67631u)], read.Pairs);
        Assert.Equal(links.Commit, read.Commit);

        var text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace(new string('a', 40), "0bd61ef", StringComparison.Ordinal));
        Assert.Contains("commit", Assert.Throws<InvalidDataException>(() => PrerequisiteLinks.Load(path)).Message, StringComparison.Ordinal);

        File.WriteAllText(path, text.Replace("[68782, 68850]", "[68782]", StringComparison.Ordinal));
        Assert.Contains("pairs[0]", Assert.Throws<InvalidDataException>(() => PrerequisiteLinks.Load(path)).Message, StringComparison.Ordinal);

        var root = JsonNode.Parse(text)!.AsObject();
        root["pairs"] = new JsonArray(new JsonArray(69191, 67631), new JsonArray(68782, 68850));
        File.WriteAllText(path, root.ToJsonString());
        Assert.Contains("sorted", Assert.Throws<InvalidDataException>(() => PrerequisiteLinks.Load(path)).Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// <c>docs/data/verification-allowlist.json</c> entries excuse a known row only until the release named in <c>until</c>
/// (R10): CI fails as soon as the plugin's csproj <c>&lt;Version&gt;</c> reaches it, so an excuse cannot silently
/// outlive the release that was meant to resolve it. Resolve the row, or extend <c>until</c> with a reason.
/// </summary>
[Trait("Category", "Curated")]
public sealed partial class VerificationAllowlistTests
{
    /// <summary>The plugin's csproj <c>&lt;Version&gt;</c> as major.minor.patch.</summary>
    internal static Version PluginVersion()
    {
        var csproj = Path.Combine(FixtureCatalog.ShippedDataDir(), "..", "Tsukimichi.csproj");
        var m = VersionElement().Match(File.ReadAllText(csproj));
        Assert.True(m.Success, $"no <Version> in {csproj}");
        return Pad(m.Groups[1].Value)!;
    }

    internal static List<JsonObject> Entries()
    {
        var path = Path.Combine(ExtraPrerequisitesDataTests.DocsDataDir(), "verification-allowlist.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        return root["entries"]!.AsArray().Select(e => e!.AsObject()).ToList();
    }

    /// <summary>Whether an entry no longer applies at <paramref name="current"/> (the verifier's rule: <c>until</c> at or below the version).</summary>
    internal static bool Expired(JsonObject entry, Version current) => Pad((string?)entry["until"]) is { } until && current >= until;

    [Fact]
    public void No_allowlist_entry_is_at_or_past_its_until_release()
    {
        var current = PluginVersion();
        var problems = new List<string>();
        foreach (var e in Entries())
        {
            var label = $"verification-allowlist.json {(string?)e["rowId"]} {(string?)e["fact"]}{((string?)e["source"] is { } s ? "/" + s : string.Empty)}";
            if (Pad((string?)e["until"]) is not { } until)
            {
                problems.Add($"{label} has no until release (major.minor.patch)");
            }
            else if (until <= current)
            {
                problems.Add($"{label} expired: until {(string?)e["until"]} <= plugin version {current}; resolve the row, or extend until with a reason");
            }

            if (string.IsNullOrWhiteSpace((string?)e["reason"]))
            {
                problems.Add($"{label} has no reason");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Theory]
    [InlineData("1.5.0", "1.5.0", true)]
    [InlineData("1.5.0", "1.4.2", false)]
    [InlineData("1.5", "1.5.0", true)]
    [InlineData("1.6.0", "1.5.0", false)]
    [InlineData("1.4.9", "1.5.0", true)]
    public void An_entry_expires_once_the_version_reaches_its_until(string until, string version, bool expired)
    {
        Assert.Equal(expired, Expired(new JsonObject { ["until"] = until }, Pad(version)!));
    }

    /// <summary>"1.5" and "1.5.0" compare equal; null for anything that is no version.</summary>
    private static Version? Pad(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Count(c => c == '.') == 1 ? text + ".0" : text, out var v))
        {
            return null;
        }

        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    [GeneratedRegex(@"<Version>([\d.]+)</Version>", RegexOptions.CultureInvariant)]
    private static partial Regex VersionElement();
}
