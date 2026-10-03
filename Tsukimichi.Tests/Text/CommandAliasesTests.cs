using Tsukimichi.Core.Text;

namespace Tsukimichi.Tests.Text;

public class CommandAliasesTests
{
    [Theory]
    [InlineData("/ts")]
    [InlineData("/moon")]
    [InlineData("/Quests2")]
    [InlineData("/q")]
    [InlineData("/abcdefghijklmnopqrstuvwx")]
    public void A_slash_then_letters_and_digits_is_an_alias(string alias)
    {
        Assert.True(CommandAliases.IsValid(alias));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("ts")]
    [InlineData("//ts")]
    [InlineData("/t s")]
    [InlineData(" /ts")]
    [InlineData("/ts!")]
    [InlineData("/quest-log")]
    [InlineData("/quest_log")]
    [InlineData("/月道")]
    [InlineData("/abcdefghijklmnopqrstuvwxy")]
    public void Anything_else_is_not(string? alias)
    {
        Assert.False(CommandAliases.IsValid(alias));
    }

    [Fact]
    public void The_built_in_aliases_are_valid_and_never_the_primary_command()
    {
        Assert.Equal(["/ts", "/moon"], CommandAliases.BuiltIn);
        Assert.All(CommandAliases.BuiltIn, static alias => Assert.True(CommandAliases.IsValid(alias)));
        Assert.DoesNotContain(CommandAliases.Primary, CommandAliases.BuiltIn);
        Assert.DoesNotContain(CommandAliases.FullName, CommandAliases.BuiltIn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ,  ")]
    public void An_empty_field_holds_no_aliases(string? text)
    {
        var parse = CommandAliases.Parse(text);
        Assert.Empty(parse.Aliases);
        Assert.Empty(parse.Invalid);
    }

    [Fact]
    public void The_field_splits_on_spaces_and_commas_and_keeps_order_lowercase_and_each_once()
    {
        var parse = CommandAliases.Parse("/Quests, /tm\t/QUESTS  /tm,/road");
        Assert.Equal(["/quests", "/tm", "/road"], parse.Aliases);
        Assert.Empty(parse.Invalid);
    }

    [Fact]
    public void Built_in_and_primary_names_are_dropped_as_they_are_registered_anyway()
    {
        var parse = CommandAliases.Parse("/ts /MOON /tsuki /tsukimichi /quests");
        Assert.Equal(["/quests"], parse.Aliases);
        Assert.Empty(parse.Invalid);
    }

    [Fact]
    public void Words_that_are_not_aliases_are_listed_as_typed_once()
    {
        var parse = CommandAliases.Parse("quests /ok /bad! quests /a-b");
        Assert.Equal(["/ok"], parse.Aliases);
        Assert.Equal(["quests", "/bad!", "/a-b"], parse.Invalid);
    }

    [Fact]
    public void Wanted_puts_the_built_in_aliases_first()
    {
        Assert.Equal(["/ts", "/moon", "/quests"], CommandAliases.Wanted("/quests /ts"));
        Assert.Equal(["/ts", "/moon"], CommandAliases.Wanted(null));
    }

    [Fact]
    public void Plan_adds_what_is_free_and_skips_what_something_else_answers_to()
    {
        var plan = CommandAliases.Plan([], ["/ts", "/moon", "/quests"], alias => alias == "/moon");
        Assert.Equal(["/ts", "/quests"], plan.Add);
        Assert.Equal(["/moon"], plan.Skipped);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void Plan_keeps_what_Tsukimichi_holds_without_asking_and_removes_what_is_no_longer_wanted()
    {
        var asked = new List<string>();
        var plan = CommandAliases.Plan(["/ts", "/moon", "/old"], ["/ts", "/moon", "/new"], alias =>
        {
            asked.Add(alias);
            return false;
        });

        Assert.Equal(["/old"], plan.Remove);
        Assert.Equal(["/new"], plan.Add);
        Assert.Empty(plan.Skipped);
        Assert.Equal(["/new"], asked);
    }

    [Fact]
    public void Plan_never_registers_the_primary_command_or_an_invalid_alias()
    {
        var plan = CommandAliases.Plan([], ["/tsuki", "/tsukimichi", "no slash", "/ok", "/OK"], static _ => false);
        Assert.Equal(["/ok"], plan.Add);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void A_skipped_alias_is_asked_about_again_on_the_next_plan()
    {
        var first = CommandAliases.Plan([], ["/ts"], static _ => true);
        Assert.Equal(["/ts"], first.Skipped);

        // The other plugin went away: the next apply takes the alias.
        var second = CommandAliases.Plan(first.Add, ["/ts"], static _ => false);
        Assert.Equal(["/ts"], second.Add);
        Assert.Empty(second.Skipped);
    }
}
