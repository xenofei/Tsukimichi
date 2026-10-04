using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Releases;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Releases;

/// <summary>
/// What's new (spec-1.22 W1, W3, U1): the release notes' file and its invariants, the popup's pages, the once-per-update
/// decision and the first-install rule, and the plain notes Dalamud's installer gets from <c>tools/make_pluginmaster.py</c>.
/// </summary>
public sealed class ReleaseNotesTests
{
    private const string Sample = """
        {
          "schema": 1,
          "releases": [
            { "version": "1.19.0", "date": "2026-10-02", "name": "Three", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] },
            { "version": "1.21.0", "date": "2026-10-04", "name": "Five", "points": [ { "lead": "Up next.", "text": "Tonight starts here." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] },
            { "version": "1.20.0", "date": "2026-10-03", "name": "Four", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." }, { "lead": "D.", "text": "d." } ] },
            { "version": "1.18.0", "date": "2026-10-01", "name": "Two", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] }
          ]
        }
        """;

    private static ReleaseNotes Notes() => ReleaseNotes.Parse(Sample);

    private static string Versions(IEnumerable<ReleaseNote> releases) => string.Join(",", releases.Select(static r => r.Version));

    // ------------------------------------------------------------------ the file

    [Fact]
    public void Releases_come_out_newest_first_whatever_the_file_order()
    {
        var notes = Notes();
        Assert.Empty(notes.Warnings);
        Assert.Equal("1.21.0,1.20.0,1.19.0,1.18.0", Versions(notes.Releases));
        Assert.Equal("Five", notes.Find("1.21.0.0")?.Name);
        Assert.Null(notes.Find("1.17.0"));
        Assert.Null(notes.Find("Unreleased"));
        Assert.Equal(new DateOnly(2026, 10, 4), notes.Find("1.21.0")!.Date);
    }

    [Theory]
    [InlineData("""{ "version": "1.22.0", "date": "2026-10-05", "name": "Two points", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." } ] }""")]
    [InlineData("""{ "version": "1.22.0", "date": "5 October", "name": "Bad date", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] }""")]
    [InlineData("""{ "version": "1.22", "date": "2026-10-05", "name": "Two parts", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] }""")]
    [InlineData("""{ "version": "1.22.0", "date": "2026-10-05", "name": "", "points": [ { "lead": "A.", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] }""")]
    [InlineData("""{ "version": "1.22.0", "date": "2026-10-05", "name": "No lead", "points": [ { "lead": "", "text": "a." }, { "lead": "B.", "text": "b." }, { "lead": "C.", "text": "c." } ] }""")]
    public void A_release_that_breaks_the_shape_is_left_out_with_a_warning(string release)
    {
        var notes = ReleaseNotes.Parse("{ \"releases\": [ " + release + " ] }");
        Assert.Empty(notes.Releases);
        Assert.Single(notes.Warnings);
    }

    [Fact]
    public void A_missing_or_broken_file_gives_no_releases_and_a_warning()
    {
        Assert.Empty(ReleaseNotes.Parse("{ \"releases\": [ ").Releases);
        Assert.Single(ReleaseNotes.Parse("{ ").Warnings);
        Assert.Single(ReleaseNotes.Parse("{ \"schema\": 1 }").Warnings);
        var missing = ReleaseNotes.Load(Path.Combine(Path.GetTempPath(), "tsukimichi-no-such-dir", ReleaseNotes.FileName));
        Assert.Empty(missing.Releases);
        Assert.Single(missing.Warnings);
    }

    // ------------------------------------------------------------------ pages and the decision

    [Fact]
    public void Skipped_releases_become_pages_newest_first()
    {
        // A player on 1.18.0 who updates to 1.21.0 gets three pages, newest first; 1.18.0 was seen.
        Assert.Equal("1.21.0,1.20.0,1.19.0", Versions(Notes().Since("1.18.0", "1.21.0")));
        Assert.Equal("1.21.0,1.20.0,1.19.0", Versions(Notes().Since("1.18.0.0", "1.21.0.0")));

        // A player on the release before gets one page.
        Assert.Equal("1.21.0", Versions(Notes().Since("1.20.0", "1.21.0")));

        // Never past the running version.
        Assert.Equal("1.20.0,1.19.0", Versions(Notes().Since("1.18.0", "1.20.0")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Without_a_seen_version_only_the_running_release_shows(string? seen)
    {
        // A build before 0.6.0 recorded nothing; such an update gets one page, not every release.
        Assert.Equal("1.21.0", Versions(Notes().Since(seen, "1.21.0")));
        Assert.Empty(Notes().Since(seen, "1.17.0"));
    }

    [Theory]
    [InlineData("1.21.0", "1.21.0")]
    [InlineData("1.22.0", "1.21.0")]
    [InlineData("1.18.0", "Unreleased")]
    [InlineData("1.18.0", "")]
    public void Nothing_is_a_page_when_nothing_newer_arrived(string seen, string running)
    {
        Assert.Empty(Notes().Since(seen, running));
    }

    [Theory]
    // Fresh install: no previous configuration, so the version is recorded silently and nothing shows.
    [InlineData("", "1.21.0", false, WhatsNewDecision.RecordSilently)]
    // An update with notes shows once.
    [InlineData("1.18.0", "1.21.0", true, WhatsNewDecision.Show)]
    [InlineData("1.20.0", "1.21.0", true, WhatsNewDecision.Show)]
    // The same version again: nothing.
    [InlineData("1.21.0", "1.21.0", true, WhatsNewDecision.Nothing)]
    // An update to a build whose notes are missing records silently.
    [InlineData("1.21.0", "1.23.0", true, WhatsNewDecision.RecordSilently)]
    // An older build after a newer one (a downgrade) has nothing to show.
    [InlineData("1.22.0", "1.21.0", true, WhatsNewDecision.RecordSilently)]
    public void The_popup_shows_once_per_update_and_never_on_a_first_install(string seen, string running, bool hasPriorConfig, WhatsNewDecision expected)
    {
        var pages = hasPriorConfig || seen.Length > 0 ? Notes().Since(seen, running) : [];
        Assert.Equal(expected, Core.Ui.WhatsNew.Decide(seen, running, pages.Count > 0, hasPriorConfig));
    }

    [Fact]
    public void The_history_lists_every_release_up_to_the_running_one()
    {
        Assert.Equal("1.20.0,1.19.0,1.18.0", Versions(Notes().History("1.20.0")));
        Assert.Equal("1.21.0,1.20.0,1.19.0,1.18.0", Versions(Notes().History("not a version")));
    }

    [Fact]
    public void Other_surfaces_read_a_releases_plain_points()
    {
        var before = WhatsNewNotes.Current;
        try
        {
            WhatsNewNotes.Use(Notes());
            Assert.Equal(["Up next. Tonight starts here.", "B. b.", "C. c."], WhatsNewNotes.For("1.21.0").Select(static p => p.Line));
            Assert.Empty(WhatsNewNotes.For("1.23.0"));
            Assert.Empty(WhatsNewNotes.For(null));
        }
        finally
        {
            WhatsNewNotes.Use(before);
        }
    }

    // ------------------------------------------------------------------ the quiet moment

    private static readonly WhatsNewMoment Quiet = new(InWorld: true, SecondsInWorld: 30, InCombat: false, InDuty: false, InCutscene: false, GroupPose: false, Loading: false);

    [Fact]
    public void The_popup_waits_for_the_first_quiet_moment()
    {
        Assert.True(Core.Ui.WhatsNew.IsQuietMoment(Quiet));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { InWorld = false }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { SecondsInWorld = 9.9 }));
        Assert.True(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { SecondsInWorld = Core.Ui.WhatsNew.SettleSeconds }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { InCombat = true }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { InDuty = true }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { InCutscene = true }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { GroupPose = true }));
        Assert.False(Core.Ui.WhatsNew.IsQuietMoment(Quiet with { Loading = true }));
    }

    // ------------------------------------------------------------------ the shipped notes

    private static string RepoRoot() => ResxFiles.RepositoryRoot();

    private static ReleaseNotes Shipped() => ReleaseNotes.Load(Path.Combine(RepoRoot(), "Tsukimichi", "Data", "curated", ReleaseNotes.FileName));

    /// <summary>The version in Tsukimichi.csproj, three parts.</summary>
    private static string CsprojVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        var match = Regex.Match(csproj, @"<Version>(\d+\.\d+\.\d+)(?:\.\d+)?</Version>");
        Assert.True(match.Success, "Tsukimichi.csproj has no <Version>");
        return match.Groups[1].Value;
    }

    [Fact]
    public void The_shipped_notes_have_an_entry_for_the_version_the_plugin_is_built_as()
    {
        // Mirrors the changelog's check: a release without its plain notes would show no popup after the update and
        // send Dalamud's installer the technical changelog.
        var version = CsprojVersion();
        Assert.True(Shipped().Find(version) is not null, $"whats_new.json has no entry for {version}, the version in Tsukimichi.csproj; write the release's plain notes");
    }

    [Fact]
    public void The_shipped_notes_load_cleanly_and_cover_every_release_since_1_14_0()
    {
        var notes = Shipped();
        Assert.Empty(notes.Warnings);

        // Every CHANGELOG.md release from 1.14.0 up to the built version has its notes, on the same day.
        var changelog = File.ReadAllText(Path.Combine(RepoRoot(), "CHANGELOG.md"));
        var first = new Version(1, 14, 0);
        var built = Version.Parse(CsprojVersion());
        foreach (Match heading in Regex.Matches(changelog, @"^## \[(\d+\.\d+\.\d+)\] - (\d{4}-\d{2}-\d{2})", RegexOptions.Multiline))
        {
            var version = Version.Parse(heading.Groups[1].Value);
            if (version < first || version > built)
            {
                continue;
            }

            var note = notes.Find(heading.Groups[1].Value);
            Assert.True(note is not null, $"whats_new.json has no entry for {version}");
            Assert.Equal(heading.Groups[2].Value, note.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        // Nothing older than 1.14.0, and the dates never run backwards.
        Assert.All(notes.Releases, static r => Assert.True(ReleaseNotes.ParseVersion(r.Version) >= new Version(1, 14, 0), r.Version));
        for (var i = 1; i < notes.Releases.Count; i++)
        {
            Assert.True(notes.Releases[i - 1].Date >= notes.Releases[i].Date, $"{notes.Releases[i - 1].Version} is dated before {notes.Releases[i].Version}");
        }
    }

    [Fact]
    public void The_shipped_notes_keep_the_players_voice()
    {
        // 3 to 5 points; a short lead (spec: two to five words; its own example "The game has the last word." has six)
        // ending in a full stop, then a plain sentence or two; no Markdown, no class names.
        string[] classNames = ["WhatsNew", "Popup", "Configuration", "Window", ".cs", "IPC"];
        foreach (var release in Shipped().Releases)
        {
            Assert.InRange(release.Points.Count, ReleaseNotes.MinPoints, ReleaseNotes.MaxPoints);
            foreach (var point in release.Points)
            {
                var words = point.Lead.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.True(words is >= 1 and <= 6, $"{release.Version}: \"{point.Lead}\" has {words} words");
                Assert.EndsWith(".", point.Lead, StringComparison.Ordinal);
                Assert.EndsWith(".", point.Text, StringComparison.Ordinal);
                Assert.True(Regex.Matches(point.Text, @"[.!?](\s|$)").Count <= 2, $"{release.Version}: \"{point.Text}\" is more than two sentences");
                Assert.True(point.Line.Length <= 220, $"{release.Version}: \"{point.Lead}\" is too long for the popup");
                foreach (var mark in new[] { "**", "`", "#" })
                {
                    Assert.DoesNotContain(mark, point.Line, StringComparison.Ordinal);
                }

                foreach (var name in classNames)
                {
                    Assert.DoesNotContain(name, point.Line, StringComparison.Ordinal);
                }
            }
        }
    }

    // ------------------------------------------------------------------ the manifest's changelog

    [Fact]
    public void The_manifest_text_is_the_name_then_one_bullet_per_point()
    {
        var text = Notes().Find("1.21.0")!.ManifestText();
        Assert.Equal("Five\n\n• Up next. Tonight starts here.\n• B. b.\n• C. c.", text);
    }

    [Fact]
    public void Make_pluginmaster_writes_the_same_plain_notes_into_the_manifest()
    {
        // tools/make_pluginmaster.py builds pluginmaster.json at release; its --notes mode prints the changelog text it
        // would write. Python is a project requirement (the theme build's check runs in the gates).
        foreach (var release in Shipped().Releases)
        {
            var printed = RunPython("tools/make_pluginmaster.py", "--notes", release.Version);
            Assert.Equal(release.ManifestText(), printed);
        }
    }

    /// <summary>Runs a repository script with <c>py -3</c> (Windows) or <c>python</c>, and returns its UTF-8 output.</summary>
    private static string RunPython(params string[] args)
    {
        foreach (var (exe, lead) in new[] { ("py", "-3"), ("python", (string?)null) })
        {
            var start = new ProcessStartInfo(exe)
            {
                WorkingDirectory = RepoRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (lead is not null)
            {
                start.ArgumentList.Add(lead);
            }

            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            Process? process;
            try
            {
                process = Process.Start(start);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                continue;
            }

            using (process)
            {
                Assert.NotNull(process);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(30_000), "make_pluginmaster.py took longer than 30 s");
                Assert.True(process.ExitCode == 0, $"{exe} {string.Join(' ', args)} failed: {error.Result}");
                return output.Result;
            }
        }

        Assert.Fail("Python was not found (py -3 or python); it is needed for tools/make_pluginmaster.py");
        return string.Empty;
    }
}
