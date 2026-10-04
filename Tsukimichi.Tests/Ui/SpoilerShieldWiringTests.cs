using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source checks that keep the plugin's Moonlit surfaces on the wider spoiler shield's one rule (plan v7, 1.20.0 N6):
/// a Moonlit reward is shielded where the index places it (<c>SpoilerMask.RewardName</c>: a duty as a duty, an aether
/// current as its zone), never looked up as a reward by hand; its tooltips go through the shield; its placeholder
/// answers with the three-line hover and the reveal items; the game panels' unlock lines print the plan's tags through
/// the shield; and the Duties board's stand-in is the wider shield's form while that shield is on.
/// </summary>
public sealed partial class SpoilerShieldWiringTests
{
    private static string Source(params string[] path) => File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), .. path]));

    [Theory]
    [InlineData("Tsukimichi", "Ui", "MoonlitPane.cs")]
    [InlineData("Tsukimichi", "Game", "QuestBriefBuilder.cs")]
    [InlineData("Tsukimichi.Core", "Runtime", "WotsitOrder.cs")]
    [InlineData("Tsukimichi.Core", "Unique", "CollectionGrid.cs")]
    public void Moonlit_rewards_are_shielded_where_the_index_places_them(params string[] path)
    {
        var source = Source(path);
        Assert.DoesNotMatch(RewardLookup(), source);
        Assert.Matches(RewardHelper(), source);
    }

    [Fact]
    public void Moonlit_reward_tooltips_go_through_the_shield()
    {
        var source = Source("Tsukimichi", "Ui", "MoonlitPane.cs");
        var calls = RewardTooltipCall().Matches(source);
        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.Contains("spoilers:", call.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Moonlit_placeholders_answer_with_the_hover_and_the_reveal_items()
    {
        var source = Source("Tsukimichi", "Ui", "MoonlitPane.cs");
        Assert.Contains("ShieldText.Hover(", source, StringComparison.Ordinal);
        Assert.Contains("ShieldText.RevealItems(session, links, row.HiddenKind, row.HiddenName, row.Quest)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_game_panels_print_the_plans_unlocks_through_the_shield()
    {
        var source = Source("Tsukimichi", "Game", "QuestBriefBuilder.cs");
        Assert.Contains("UnlockPlan.Shield(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_duties_boards_stand_in_is_the_wider_shields_form_while_it_is_on()
    {
        var source = Source("Tsukimichi", "Ui", "DutyBoardSource.cs");
        Assert.Contains("spoilers.MasksNames", source, StringComparison.Ordinal);
        Assert.Contains("spoilers.DutyPlaceholder(duty)", source, StringComparison.Ordinal);
    }

    // A Moonlit name looked up as a reward by hand: what let a duty or an aether current through.
    [GeneratedRegex(@"(IsNameMasked|\.Name)\((Core\.Query\.|Query\.)?SpoilerKind\.Reward, *(rewardName|display|RewardNames\.Display|entry\.RewardName)")]
    private static partial Regex RewardLookup();

    [GeneratedRegex(@"\.(IsRewardMasked|RewardDisplay)\(")]
    private static partial Regex RewardHelper();

    [GeneratedRegex(@"RewardTooltip\.Draw\([^;]*;")]
    private static partial Regex RewardTooltipCall();
}
