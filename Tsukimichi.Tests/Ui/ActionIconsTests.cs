using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The game's own icons on the buttons, the Route window's header and the requirement lines (UI-5e).</summary>
public class ActionIconsTests
{
    private sealed class FakeSheets : IPaneIconSheets
    {
        public uint ContentTypeIcon(uint contentType) => contentType == NodeIcons.GrandCompanyContent ? 61_811u : 0u;

        public uint TribeIcon(byte tribe) => tribe == 0 ? 0 : 65_000u + tribe;

        public uint TribeReputationIcon(byte tribe) => 0;

        public uint GrandCompanyRankIcon(byte grandCompany, byte rank) => rank == 0 ? 0 : 83_000u + rank;

        public uint AchievementIcon(uint achievement) => achievement == 7 ? 3_007u : 0u;

        public uint MountIcon(uint mount) => mount == 1 ? 4_001u : 0u;
    }

    private static readonly FakeSheets Sheets = new();

    private static byte? Family(uint rowId) => rowId switch
    {
        10 => 3,
        20 => 8,
        30 => 1,
        _ => null,
    };

    private static GameIconRef For(Requirement requirement, uint job = 0, Func<uint, uint>? duty = null) =>
        ActionIcons.Requirement(requirement, job, Family, duty, Sheets);

    [Theory]
    [InlineData(3, NodeIcons.MsqMarker)]
    [InlineData(8, NodeIcons.FeatureMarker)]
    [InlineData(10, NodeIcons.FeatureMarker)]
    [InlineData(1, NodeIcons.SidequestMarker)]
    [InlineData(0, NodeIcons.SidequestMarker)]
    public void A_quest_wears_its_own_map_marker(byte eventIconType, uint marker)
    {
        Assert.Equal(marker, ActionIcons.QuestMarker(eventIconType));
        var goTo = ActionIcons.GoTo(new QuestRecord { RowId = 1, EventIconType = eventIconType });
        Assert.Equal(GameIconRef.Symbol(marker), goTo);
    }

    [Fact]
    public void The_travel_icons_are_the_games_and_drawn_in_their_style()
    {
        Assert.Equal(new GameIconRef(60453, IconStyle.MapSymbol), ActionIcons.TeleportIcon);
        Assert.Equal(new GameIconRef(104, IconStyle.Tile), ActionIcons.WalkIcon);
        Assert.Equal(new GameIconRef(60561, IconStyle.MapSymbol), ActionIcons.FlagIcon);
        Assert.Equal(new GameIconRef(60430, IconStyle.MapSymbol), ActionIcons.AethernetIcon);
        Assert.Equal(new GameIconRef(7, IconStyle.Tile), ActionIcons.MapIcon);
        Assert.False(GameIconRef.None.HasIcon);

        // Never the red "!" tile: "Read the journal" is the book glyph, not a game icon.
        Assert.DoesNotContain(5u, ActionIcons.FixedIcons);
    }

    [Fact]
    public void A_job_icon_is_62100_plus_the_job()
    {
        Assert.Equal(62136u, ActionIcons.Job(36));
        Assert.Equal(62101u, ActionIcons.Job(1));
        Assert.Equal(0u, ActionIcons.Job(0));
        Assert.Equal(0u, ActionIcons.Job(500));
    }

    [Fact]
    public void Instance_glyphs_are_the_game_fonts_one_to_nine()
    {
        Assert.Equal('', ActionIcons.InstanceGlyph(1));
        Assert.Equal('', ActionIcons.InstanceGlyph(9));
        Assert.Equal('\0', ActionIcons.InstanceGlyph(0));
        Assert.Equal('\0', ActionIcons.InstanceGlyph(10));
    }

    [Fact]
    public void The_route_header_wears_the_targets_own_icon()
    {
        var quest = new QuestRecord { RowId = 10, EventIconType = 3 };

        // A quest target: its marker; a known icon wins; a job's is a tile; without either, the FontAwesome sign stays.
        Assert.Equal(GameIconRef.Symbol(NodeIcons.MsqMarker), ActionIcons.RouteHeader(RouteTarget.ForQuest(10, "x"), quest));
        Assert.Equal(GameIconRef.Tile(62136), ActionIcons.RouteHeader(RouteTarget.ForJob("Blue Mage", 10) with { Icon = 62136 }, quest));
        Assert.Equal(GameIconRef.None, ActionIcons.RouteHeader(RouteTarget.ForJob("Blue Mage", 10), quest));
        Assert.Equal(GameIconRef.None, ActionIcons.RouteHeader(null, quest));
        Assert.Equal(GameIconRef.None, ActionIcons.RouteHeader(RouteTarget.ForQuest(10, "x"), null));
    }

    [Fact]
    public void Requirement_lines_wear_the_thing_they_name()
    {
        // A pinned job its icon; a category the Class & Job emblem; Level the job the check was made for.
        Assert.Equal(GameIconRef.Tile(62136), For(new ClassJobRequirement(0, 36, 19)));
        Assert.Equal(GameIconRef.Tile(NodeIcons.ClassJobEmblem), For(new ClassJobRequirement(30, 0, 19)));
        Assert.Equal(GameIconRef.Tile(62136), For(new LevelRequirement(10, 1), job: 36));
        Assert.False(For(new LevelRequirement(10, 1)).HasIcon);

        // The first previous quest the catalog knows sets the marker.
        Assert.Equal(GameIconRef.Symbol(NodeIcons.FeatureMarker), For(new PreviousQuestsRequirement([99, 20, 10], JoinKind.All, 0)));
        Assert.False(For(new PreviousQuestsRequirement([99], JoinKind.All, 0)).HasIcon);

        Assert.Equal(GameIconRef.Tile(65_007), For(new TribeRankRequirement(7, 3, 1)));
        Assert.Equal(GameIconRef.Tile(65_007), For(new TribeReputationRequirement(7, 100, 5)));
        Assert.Equal(GameIconRef.Tile(83_011), For(new GrandCompanyRankRequirement(1, 11, 9)));
        Assert.Equal(GameIconRef.Tile(61_811), For(new GrandCompanyRequirement(1, 0)));
        Assert.Equal(GameIconRef.Tile(3_007), For(new AchievementRequirement(7, true)));
    }

    [Fact]
    public void A_mount_or_a_duty_falls_back_to_the_actions_tile()
    {
        Assert.Equal(GameIconRef.Tile(4_001), For(new MountRequirement(false) { Mounts = [1, 2], Missing = [1] }));
        Assert.Equal(GameIconRef.Tile(ActionIcons.Mount), For(new MountRequirement(false) { Mounts = [2], Missing = [2] }));
        Assert.Equal(GameIconRef.Tile(ActionIcons.Mount), For(new MountRequirement(null)));

        Assert.Equal(GameIconRef.Tile(61_201), For(new DutyCompletionRequirement([5], JoinKind.All, 0), duty: id => id == 5 ? 61_201u : 0u));
        Assert.Equal(GameIconRef.Tile(ActionIcons.DutyFinder), For(new DutyCompletionRequirement([6], JoinKind.All, 0), duty: id => 0u));
    }

    [Fact]
    public void A_requirement_with_no_game_thing_behind_it_has_no_icon()
    {
        Assert.False(For(new SeasonalRequirement(1, false)).HasIcon);
        Assert.False(For(new LevelCapRequirement(90, 80)).HasIcon);
        Assert.False(For(new HouseRequirement(null)).HasIcon);
    }
}
