using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Model;

/// <summary>
/// The client's allied society rank byte (<c>BeastReputationWork.Rank</c>) sets its high bit on the day the rank went
/// up; <see cref="TribeStanding.FromClient"/> masks it off the rank and keeps it as a flag.
/// </summary>
public sealed class TribeStandingTests
{
    [Theory]
    [InlineData(0x00, 0, false)]
    [InlineData(0x04, 4, false)]
    [InlineData(0x08, 8, false)]
    [InlineData(0x7F, 0x7F, false)]
    [InlineData(0x80, 0, true)]
    [InlineData(0x84, 4, true)]
    [InlineData(0x88, 8, true)]
    [InlineData(0xFF, 0x7F, true)]
    public void FromClient_masks_the_rank_up_bit_into_the_flag(int raw, int rank, bool rankedUpToday)
    {
        var standing = TribeStanding.FromClient((byte)raw, 250);

        Assert.Equal((byte)rank, standing.Rank);
        Assert.Equal((ushort)250, standing.Value);
        Assert.Equal(rankedUpToday, standing.RankedUpToday);
    }

    [Fact]
    public void FromClient_keeps_a_flag_passed_in()
    {
        Assert.True(TribeStanding.FromClient(4, 0, rankedUpToday: true).RankedUpToday);
    }

    [Fact]
    public void Masked_repairs_a_raw_rank_and_leaves_a_real_one_alone()
    {
        Assert.Equal(new TribeStanding(4, 30, true), new TribeStanding(0x84, 30).Masked());
        Assert.Equal(new TribeStanding(4, 30), new TribeStanding(4, 30).Masked());
        Assert.Equal(new TribeStanding(4, 30, true), new TribeStanding(4, 30, true).Masked());
    }

    [Fact]
    public void WithMaskedTribeRanks_masks_only_raw_ranks_and_returns_the_same_instance_when_clean()
    {
        var clean = new CharacterSnapshot { Tribes = new Dictionary<byte, TribeStanding> { [1] = new(4, 0), [2] = new(8, 0) } };
        Assert.Same(clean, clean.WithMaskedTribeRanks());

        var raw = clean with { Tribes = new Dictionary<byte, TribeStanding> { [1] = new(0x85, 10), [2] = new(8, 0) } };
        var repaired = raw.WithMaskedTribeRanks();

        Assert.Equal(new TribeStanding(5, 10, true), repaired.Tribes[1]);
        Assert.Equal(new TribeStanding(8, 0), repaired.Tribes[2]);
        Assert.Equal((byte)0x85, raw.Tribes[1].Rank);
    }
}
