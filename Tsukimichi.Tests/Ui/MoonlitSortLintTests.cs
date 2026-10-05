using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the Moonlit table's column sort (owner request after 1.22.0: "can't sort the columns by clicking on
/// them"): the table is sortable with ImGui's tristate, reads the headers' sort specs, sorts through the Core comparer
/// (<see cref="Core.Unique.MoonlitSort"/>) rather than inline, and remembers the sort in the configuration. The test
/// project references Core and GameData only, so it reads the plugin's sources from the repository.
/// </summary>
public sealed class MoonlitSortLintTests
{
    private static string Source => File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "MoonlitPane.cs"));

    [Fact]
    public void The_rewards_table_is_sortable_with_tristate()
    {
        var source = Source;
        Assert.Contains("ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_table_reads_the_headers_sort_specs_and_clears_the_dirty_flag()
    {
        var source = Source;
        Assert.Contains("ImGui.TableGetSortSpecs()", source, StringComparison.Ordinal);
        Assert.Contains("specs.SpecsDirty = false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rows_are_sorted_by_the_core_comparer_and_the_sort_is_part_of_the_cache_key()
    {
        var source = Source;
        Assert.Contains("MoonlitSort.Apply(picked, SortKeyOf, key.Sort)", source, StringComparison.Ordinal);
        Assert.Contains("MoonlitSortSpec Sort);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_sort_is_remembered_in_the_configuration_and_written_back_on_the_first_frame()
    {
        var source = Source;
        Assert.Contains("settings.MoonlitSortColumn = sort.Column;", source, StringComparison.Ordinal);
        Assert.Contains("ApplyInitialSort(settings.MoonlitSort())", source, StringComparison.Ordinal);
    }
}
