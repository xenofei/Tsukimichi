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
    [InlineData("", "0.6.0", true, WhatsNewDecision.RecordSilently)]
    [InlineData(null, "0.6.0", true, WhatsNewDecision.RecordSilently)]
    [InlineData("0.6.0", "0.6.0", true, WhatsNewDecision.Nothing)]
    [InlineData("0.6.0", "0.6.0.0", true, WhatsNewDecision.Nothing)]
    [InlineData("0.5.1", "0.6.0", true, WhatsNewDecision.Show)]
    [InlineData("0.5.1", "0.6.0", false, WhatsNewDecision.RecordSilently)]
    [InlineData("0.5.1", "", true, WhatsNewDecision.Nothing)]
    public void WhatsNew_shows_once_after_an_update_and_never_on_a_fresh_install(string? seen, string? running, bool hasSection, WhatsNewDecision expected)
    {
        Assert.Equal(expected, WhatsNew.Decide(seen, running, hasSection));
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
