using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Find by unlock (1.19.0, K3; 1.19.0 review): the rows' kind names and "via …" lines are worded from Strings, so the
/// rows' cache key carries the plugin's language and a language change words them again. The test project references
/// Core and GameData only, so it reads the plugin's source from the repository.
/// </summary>
public sealed class FindUnlockLanguageTests
{
    [Fact]
    public void Find_by_unlock_rows_are_worded_again_on_a_language_change()
    {
        var source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "MainWindow.FindUnlock.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var rows = GameCallsLintTests.Method.Matches(source).Single(static m => m.Groups[1].Value == "UnlockRows").Groups[2].Value;
        Assert.Contains("Localization.Loc.Version", rows, StringComparison.Ordinal);
    }
}
