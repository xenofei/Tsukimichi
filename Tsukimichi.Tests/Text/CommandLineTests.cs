using Tsukimichi.Core.Text;

namespace Tsukimichi.Tests.Text;

public class CommandLineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_arguments_toggle_the_window(string? arguments)
    {
        Assert.Equal(Subcommand.Toggle, CommandLine.Parse(arguments).Kind);
    }

    [Theory]
    [InlineData("tour", Subcommand.Tour)]
    [InlineData("TOUR", Subcommand.Tour)]
    [InlineData("journal", Subcommand.Journal)]
    [InlineData("moonlit", Subcommand.Moonlit)]
    [InlineData("characters", Subcommand.Characters)]
    [InlineData("flight", Subcommand.Flight)]
    [InlineData("blues", Subcommand.Blues)]
    [InlineData("settings", Subcommand.Settings)]
    [InlineData("config", Subcommand.Settings)]
    [InlineData("help", Subcommand.Help)]
    [InlineData("nearby", Subcommand.Nearby)]
    [InlineData("todo", Subcommand.Todo)]
    [InlineData("zone", Subcommand.Zone)]
    [InlineData("which", Subcommand.Which)]
    [InlineData("glyphs", Subcommand.Glyphs)]
    public void A_first_word_names_its_subcommand(string word, Subcommand expected)
    {
        Assert.Equal(expected, CommandLine.Parse(word).Kind);
    }

    [Fact]
    public void Route_why_report_and_export_keep_the_rest_of_the_line()
    {
        var route = CommandLine.Parse("  route   Hallo Halatali  ");
        Assert.Equal(Subcommand.Route, route.Kind);
        Assert.Equal("Hallo Halatali", route.Rest);

        Assert.Equal(string.Empty, CommandLine.Parse("route").Rest);
        Assert.Equal("Into a Copper Hell", CommandLine.Parse("why Into a Copper Hell").Rest);
        Assert.Equal("moonlit csv", CommandLine.Parse("export moonlit csv").Rest);
    }

    [Fact]
    public void Any_other_line_is_a_search_for_the_whole_line()
    {
        var parsed = CommandLine.Parse("Hallo Halatali");
        Assert.Equal(Subcommand.Search, parsed.Kind);
        Assert.Equal("Hallo", parsed.Word);
        Assert.Equal("Hallo Halatali", parsed.SearchText);

        var explicitSearch = CommandLine.Parse("search tour guide");
        Assert.Equal(Subcommand.Search, explicitSearch.Kind);
        Assert.Equal("tour guide", explicitSearch.SearchText);
        Assert.Equal(string.Empty, CommandLine.Parse("search").SearchText);
    }

    [Theory]
    [InlineData("nearbt", "nearby")]
    [InlineData("neaby", "nearby")]
    [InlineData("naerby", "nearby")]
    [InlineData("Nearbye", "nearby")]
    [InlineData("tuor", "tour")]
    [InlineData("rout", "route")]
    [InlineData("moonlight", "moonlit")]
    [InlineData("charcters", "characters")]
    [InlineData("setings", "settings")]
    [InlineData("blue", "blues")]
    [InlineData("expotr", "export")]
    public void A_near_miss_suggests_the_subcommand(string word, string expected)
    {
        Assert.Equal(expected, CommandLine.DidYouMean(word));
    }

    [Theory]
    [InlineData("nearby")]
    [InlineData("Halatali")]
    [InlineData("Sastasha")]
    [InlineData("to")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("glyph")]
    public void A_real_word_or_a_far_one_suggests_nothing(string? word)
    {
        // "glyph" is one letter from glyphs, which works but is not listed to players.
        Assert.Null(CommandLine.DidYouMean(word));
    }

    [Fact]
    public void Glyphs_is_not_listed()
    {
        Assert.DoesNotContain("glyphs", CommandLine.ListedWords);
        Assert.Contains("tour", CommandLine.ListedWords);
        Assert.Contains("route", CommandLine.ListedWords);
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("tour", "tuor", 1)]
    [InlineData("nearby", "neaby", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void Distance_counts_edits_with_adjacent_swaps_as_one(string a, string b, int expected)
    {
        Assert.Equal(expected, CommandLine.Distance(a, b));
    }
}
