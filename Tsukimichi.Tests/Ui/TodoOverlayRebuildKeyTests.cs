using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Todo overlay rebuilds only when its key moves (<c>TodoOverlay.Refresh</c>), so everything <c>Rebuild</c> reads
/// must be in the key. Review fix (1.19.0, C10): the ending-soon order of the seasonal section reads
/// <c>EventWarnings.Current</c>, so the warnings' revision and "Warn before an event ends" must move the key, or a new
/// setting or an event entering the window leaves the order stale. The overlay is plugin code the tests cannot load,
/// so this reads its source.
/// </summary>
public sealed class TodoOverlayRebuildKeyTests
{
    private static readonly string Source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "TodoOverlay.cs"))
        .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Between(string start, string end)
    {
        var from = Source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"TodoOverlay.cs has no \"{start}\"");
        var to = Source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"TodoOverlay.cs has no \"{end}\" after \"{start}\"");
        return Source[from..to];
    }

    [Fact]
    public void The_rebuild_reads_the_ending_soon_warnings()
    {
        Assert.Contains("EndingSoon: settings.TodoShowSeasonal ? EventWarnings?.Current", Source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_warnings_revision_moves_the_key()
    {
        var revisions = Between("SourceRevisions()\n    {", "builtSources =");
        Assert.Contains("EventWarnings is { } warnings", revisions, StringComparison.Ordinal);
        Assert.Contains("warnings.Revision", revisions, StringComparison.Ordinal);
    }

    [Fact]
    public void The_warning_days_setting_moves_the_key()
    {
        Assert.Contains("settings.SeasonalWarnDays", Between("private int SettingsSignature() =>", ";"), StringComparison.Ordinal);
    }
}
