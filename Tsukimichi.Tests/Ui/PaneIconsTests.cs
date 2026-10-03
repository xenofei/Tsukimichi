using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The icons of UI-5d over stand-in sheet columns shaped like the real ones: roles, job groups, allied societies,
/// Grand Company ranks, the plan's unlock kinds and the expansions. The game-data half is <c>PaneIconDataTests</c>.
/// </summary>
public sealed class PaneIconsTests
{
    private sealed class FakeSheets : IPaneIconSheets
    {
        public uint ContentTypeIcon(uint contentType) => contentType == 0 ? 0 : 61800 + contentType;

        public uint TribeIcon(byte tribe) => tribe is 0 or 7 ? 0 : 65000u + tribe;

        public uint TribeReputationIcon(byte tribe) => tribe == 0 ? 0 : 61900u + tribe;

        public uint GrandCompanyRankIcon(byte grandCompany, byte rank) => rank is 0 or > 19 ? 0 : 83000u + ((grandCompany - 1u) * 50u) + rank;

        public uint AchievementIcon(uint achievement) => achievement == 0 ? 0 : 1000u + achievement;

        public uint MountIcon(uint mount) => mount == 0 ? 0 : 4000u + mount;
    }

    private static readonly FakeSheets Sheets = new();

    [Fact]
    public void Every_role_has_its_framed_icon_and_family()
    {
        Assert.Equal(62581u, PaneIcons.Role(JobRole.Tank));
        Assert.Equal(62582u, PaneIcons.Role(JobRole.Healer));
        Assert.Equal(62584u, PaneIcons.Role(JobRole.Melee));
        Assert.Equal(62586u, PaneIcons.Role(JobRole.PhysicalRanged));
        Assert.Equal(62587u, PaneIcons.Role(JobRole.MagicalRanged));
        Assert.Equal(0u, PaneIcons.Role((JobRole)0));

        foreach (var role in Enum.GetValues<JobRole>())
        {
            // A role's family draws the role's own icon.
            Assert.Equal(PaneIcons.Role(role), PaneIcons.Family(PaneIcons.FamilyOf(role), Sheets));
        }

        Assert.Equal(JobFamily.None, PaneIcons.FamilyOf((JobRole)0));
    }

    [Fact]
    public void Crafters_and_gatherers_wear_their_Duty_Finder_tiles_and_a_mix_the_emblem()
    {
        Assert.Equal(61800u + NodeIcons.HandContent, PaneIcons.Family(JobFamily.Hand, Sheets));
        Assert.Equal(61800u + NodeIcons.LandContent, PaneIcons.Family(JobFamily.Land, Sheets));
        Assert.Equal(NodeIcons.ClassJobEmblem, PaneIcons.Family(JobFamily.Mixed, Sheets));
        Assert.Equal(0u, PaneIcons.Family(JobFamily.None, Sheets));
    }

    [Theory]
    [InlineData(false, false, false, JobFamily.None)]
    [InlineData(true, false, false, JobFamily.Hand)]
    [InlineData(false, true, false, JobFamily.Land)]
    [InlineData(true, true, false, JobFamily.Mixed)]
    [InlineData(false, false, true, JobFamily.Mixed)]
    [InlineData(true, false, true, JobFamily.Mixed)]
    [InlineData(true, true, true, JobFamily.Mixed)]
    public void A_job_group_is_its_discipline_or_a_mix(bool hand, bool land, bool combat, JobFamily expected) =>
        Assert.Equal(expected, PaneIcons.Disciples(hand, land, combat));

    [Fact]
    public void A_society_wears_its_emblem_else_its_mark()
    {
        Assert.Equal(65001u, PaneIcons.Tribe(1, Sheets));
        Assert.Equal(61907u, PaneIcons.Tribe(7, Sheets));
        Assert.Equal(0u, PaneIcons.Tribe(0, Sheets));
    }

    [Fact]
    public void A_Grand_Company_rank_wears_its_insignia_else_the_company_tile()
    {
        Assert.Equal(83011u, PaneIcons.GrandCompanyRank(1, 11, Sheets));
        Assert.Equal(83061u, PaneIcons.GrandCompanyRank(2, 11, Sheets));
        Assert.Equal(83111u, PaneIcons.GrandCompanyRank(3, 11, Sheets));

        // Rank 0 (or one the sheet does not know): the Grand Company tile; no company, nothing.
        var tile = 61800u + NodeIcons.GrandCompanyContent;
        Assert.Equal(tile, PaneIcons.GrandCompanyRank(1, 0, Sheets));
        Assert.Equal(tile, PaneIcons.GrandCompanyRank(2, 40, Sheets));
        Assert.Equal(0u, PaneIcons.GrandCompanyRank(0, 5, Sheets));
        Assert.Equal(0u, PaneIcons.GrandCompanyRank(4, 5, Sheets));
    }

    [Fact]
    public void Every_unlock_kind_but_Other_has_an_icon()
    {
        foreach (var kind in UnlockKinds.All)
        {
            var icon = PaneIcons.UnlockKind(kind, Sheets);
            Assert.True(kind == UnlockKind.Other ? icon == 0 : icon != 0, $"{kind}: {icon}");
        }

        Assert.Equal(61800u + PaneIcons.DungeonsContent, PaneIcons.UnlockKind(UnlockKind.Dungeon, Sheets));
        Assert.Equal(61800u + PaneIcons.TrialsContent, PaneIcons.UnlockKind(UnlockKind.Trial, Sheets));

        // A normal and an alliance raid share the Raids tile, as in the Duty Finder.
        Assert.Equal(PaneIcons.UnlockKind(UnlockKind.NormalRaid, Sheets), PaneIcons.UnlockKind(UnlockKind.AllianceRaid, Sheets));
        Assert.Equal(NodeIcons.ClassJobEmblem, PaneIcons.UnlockKind(UnlockKind.Job, Sheets));
        Assert.Equal(61800u + NodeIcons.SocietyContent, PaneIcons.UnlockKind(UnlockKind.Society, Sheets));
        Assert.Equal(QuestUnlocks.AetherCurrentIcon, PaneIcons.UnlockKind(UnlockKind.Flying, Sheets));
        Assert.Equal(NodeIcons.FeatureMarker, PaneIcons.UnlockKind(UnlockKind.System, Sheets));
    }

    [Fact]
    public void Every_known_expansion_has_its_ring()
    {
        for (var expansion = 0; expansion < Core.Evaluation.Expansions.Count; expansion++)
        {
            var icon = PaneIcons.Expansion((byte)expansion);
            Assert.Equal(NodeIcons.FirstExpansionRingIcon + (uint)expansion, icon);
            Assert.True(NodeIcons.IsExpansionRing(icon));
        }

        Assert.Equal(0u, PaneIcons.Expansion((byte)Core.Evaluation.Expansions.Count));
    }

    [Fact]
    public void The_hand_lists_name_every_fixed_icon_and_content_row_once()
    {
        Assert.Equal(PaneIcons.FixedIcons.Count, PaneIcons.FixedIcons.Distinct().Count());
        Assert.Equal(PaneIcons.ContentTypeRows.Count, PaneIcons.ContentTypeRows.Distinct().Count());
        foreach (var role in Enum.GetValues<JobRole>())
        {
            Assert.Contains(PaneIcons.Role(role), PaneIcons.FixedIcons);
        }
    }
}
