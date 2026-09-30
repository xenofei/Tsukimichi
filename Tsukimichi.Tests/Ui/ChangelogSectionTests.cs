using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class ChangelogSectionTests
{
    private const string Sample = """
        # Changelog

        Intro text.

        ## [Unreleased]

        ### Added
        - Not shipped yet.

        ## [0.6.0] - 2026-10-05

        ### Added
        - What's new card.
        - Help topics: counts
          and quirks.

        ### Fixed
        - Installer icon.

        ## [0.5.1] - 2026-09-28

        ### Changed
        - Older change.
        """;

    [Fact]
    public void Find_returns_the_matching_section_with_groups_and_bullets()
    {
        var section = ChangelogSection.Find(Sample, "0.6.0");

        Assert.NotNull(section);
        Assert.Equal("0.6.0", section.Version);
        Assert.Equal("2026-10-05", section.Date);
        Assert.Collection(
            section.Groups,
            g =>
            {
                Assert.Equal("Added", g.Title);
                Assert.Equal(["What's new card.", "Help topics: counts and quirks."], g.Items);
            },
            g =>
            {
                Assert.Equal("Fixed", g.Title);
                Assert.Equal(["Installer icon."], g.Items);
            });
    }

    [Fact]
    public void Find_stops_at_the_next_version_heading()
    {
        var section = ChangelogSection.Find(Sample, "0.6.0");

        Assert.NotNull(section);
        Assert.DoesNotContain(section.Groups, g => g.Items.Contains("Older change."));
    }

    [Fact]
    public void Find_matches_a_four_part_assembly_version()
    {
        Assert.NotNull(ChangelogSection.Find(Sample, "0.6.0.0"));
    }

    [Theory]
    [InlineData("0.7.0")]
    [InlineData("Unreleased")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_returns_null_without_a_section(string? version)
    {
        Assert.Null(ChangelogSection.Find(Sample, version));
    }

    [Fact]
    public void Find_returns_null_for_a_section_without_bullets()
    {
        const string empty = "## [0.6.0]\n\n### Added\n\n## [0.5.1]\n- x\n";
        Assert.Null(ChangelogSection.Find(empty, "0.6.0"));
    }

    [Fact]
    public void Find_accepts_crlf_and_bullets_before_any_group_heading()
    {
        const string crlf = "## [0.6.0]\r\n- First.\r\n- Second.\r\n";
        var section = ChangelogSection.Find(crlf, "0.6.0");

        Assert.NotNull(section);
        var group = Assert.Single(section.Groups);
        Assert.Equal(string.Empty, group.Title);
        Assert.Equal(["First.", "Second."], group.Items);
    }

    [Theory]
    [InlineData("", "0.6.0", true, false, WhatsNewDecision.RecordSilently)]
    [InlineData(null, "0.6.0", true, false, WhatsNewDecision.RecordSilently)]
    [InlineData("0.6.0", "0.6.0", true, false, WhatsNewDecision.Nothing)]
    [InlineData("0.6.0", "0.6.0.0", true, true, WhatsNewDecision.Nothing)]
    [InlineData("0.5.1", "0.6.0", true, true, WhatsNewDecision.Show)]
    [InlineData("0.5.1", "0.6.0", true, false, WhatsNewDecision.Show)]
    [InlineData("0.5.1", "0.6.0", false, true, WhatsNewDecision.RecordSilently)]
    [InlineData("0.5.1", "", true, true, WhatsNewDecision.Nothing)]
    public void WhatsNew_shows_once_after_an_update_and_never_on_a_fresh_install(string? seen, string? running, bool hasSection, bool hasPriorConfig, WhatsNewDecision expected)
    {
        Assert.Equal(expected, WhatsNew.Decide(seen, running, hasSection, hasPriorConfig));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void WhatsNew_shows_after_an_update_from_a_build_that_never_recorded_a_version(string? seen)
    {
        // Every release before 0.6.0 left LastSeenVersion empty; a configuration that already existed tells that
        // update apart from a fresh install, which still records silently.
        Assert.Equal(WhatsNewDecision.Show, WhatsNew.Decide(seen, "0.6.0", hasSection: true, hasPriorConfig: true));
        Assert.Equal(WhatsNewDecision.RecordSilently, WhatsNew.Decide(seen, "0.6.0", hasSection: false, hasPriorConfig: true));
        Assert.Equal(WhatsNewDecision.RecordSilently, WhatsNew.Decide(seen, "0.6.0", hasSection: true, hasPriorConfig: false));
    }

    [Fact]
    public void WhatsNew_without_the_prior_config_flag_reads_an_empty_version_as_a_fresh_install()
    {
        Assert.Equal(WhatsNewDecision.RecordSilently, WhatsNew.Decide("", "0.6.0", hasSection: true));
        Assert.Equal(WhatsNewDecision.Show, WhatsNew.Decide("0.5.1", "0.6.0", hasSection: true));
    }

    [Fact]
    public void Repository_changelog_parses_a_section_for_every_released_version()
    {
        var path = Path.Combine(RepoRoot(), "CHANGELOG.md");
        var text = File.ReadAllText(path);
        var versions = text.Split('\n')
            .Where(l => l.StartsWith("## [", StringComparison.Ordinal) && !l.StartsWith("## [Unreleased]", StringComparison.Ordinal))
            .Select(l => l[4..l.IndexOf(']')])
            .ToList();

        Assert.NotEmpty(versions);
        foreach (var version in versions)
        {
            var section = ChangelogSection.Find(text, version);
            Assert.True(section is not null, $"CHANGELOG.md section [{version}] has no bullet");
        }

        Assert.Null(ChangelogSection.Find(text, "Unreleased"));
    }

    [Fact]
    public void Repository_changelog_keeps_the_section_of_the_version_the_plugin_is_built_as()
    {
        // The What's new card reads the running version's section; a merge that folds a released section into
        // [Unreleased] must fail here, not show an empty card after the next update.
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        var match = System.Text.RegularExpressions.Regex.Match(csproj, @"<Version>(\d+\.\d+\.\d+)(?:\.\d+)?</Version>");
        Assert.True(match.Success, "Tsukimichi.csproj has no <Version>");

        var text = File.ReadAllText(Path.Combine(RepoRoot(), "CHANGELOG.md"));
        Assert.True(ChangelogSection.Find(text, match.Groups[1].Value) is not null, $"CHANGELOG.md has no section for {match.Groups[1].Value}, the version in Tsukimichi.csproj");

        // [Unreleased] first, then every released version newest first: a section moved out of place is a bad merge.
        var headings = text.Split('\n').Where(l => l.StartsWith("## [", StringComparison.Ordinal)).Select(l => l[4..l.IndexOf(']')]).ToList();
        Assert.Equal("Unreleased", headings[0]);
        var released = headings.Skip(1).Select(Version.Parse).ToList();
        for (var i = 1; i < released.Count; i++)
        {
            Assert.True(released[i - 1] > released[i], $"CHANGELOG.md lists [{released[i]}] after [{released[i - 1]}]; versions must run newest first");
        }
    }

    [Tsukimichi.Tests.Data.GitHistoryFact]
    public void Released_changelog_sections_match_what_their_tag_shipped()
    {
        // A merge that files a new bullet under an already released version (or drops one) must fail here: every
        // vX.Y.Z tag's own section is compared with the working copy's, line for line.
        var tags = Tsukimichi.Tests.Data.GitHistoryFactAttribute.Git("tag", "--list", "v*");
        Assert.NotNull(tags);
        var current = Normalize(File.ReadAllText(Path.Combine(RepoRoot(), "CHANGELOG.md")));
        foreach (var tag in tags.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$"))
            {
                continue;
            }

            var shipped = Tsukimichi.Tests.Data.GitHistoryFactAttribute.Git("show", tag + ":CHANGELOG.md");
            var version = tag[1..];
            var then = shipped is null ? null : Section(Normalize(shipped), version);
            if (then is null)
            {
                continue; // before the changelog existed
            }

            Assert.True(then == Section(current, version), $"CHANGELOG.md section [{version}] differs from what {tag} shipped");
        }
    }

    private static string Normalize(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(l => l.TrimEnd()));

    private static string? Section(string text, string version)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"^## \[" + System.Text.RegularExpressions.Regex.Escape(version) + @"\][^\n]*\n.*?(?=^## \[|\z)",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return dir;
    }
}
