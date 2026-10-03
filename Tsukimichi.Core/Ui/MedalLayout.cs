using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The hero-size medals in the medal atlas (feature plan v6 G1; "Menphina's Medallion", round 5): the seven state
/// medals as designed, badges included, and Ready on another job once per role seat with the seat left empty for the
/// game's own job icon (concept.md, "Job-badge frame spec"). Order is the atlas's.
/// </summary>
public enum MedalSprite : byte
{
    Ready,
    InJournal,
    Blocked,
    DoneThisCycle,
    Completed,
    LockedOut,
    NotChecked,
    OtherJobTank,
    OtherJobHealer,
    OtherJobDps,

    /// <summary>A Disciple of the Hand or Land: no combat role, so a slate seat (<see cref="GlyphTokens.MedallionDetail.HandHex"/>).</summary>
    OtherJobHand,
}

/// <summary>The seat colour of a job badge: the game's combat roles, plus the crafters and gatherers.</summary>
public enum JobSeat : byte
{
    Tank,
    Healer,
    Dps,
    Hand,
}

/// <summary>
/// Layout of the medal atlas, <c>Tsukimichi/assets/ui/medals.png</c> (782 × 574) and <c>medals@2x.png</c> (1564 × 1148,
/// the same layout doubled), written by <c>docs/design/moon-v6/round5/gen_atlas.py</c> with <c>medals.json</c>; a test
/// holds this table to that file and to the PNG sizes. Every sprite is drawn at each of <see cref="Tiers"/> (48, 64,
/// 96 and 128 px at 1x), a band of rows per tier, sprites 2 px apart (4 px at 2x) so bilinear sampling never bleeds a
/// neighbour in. A sprite's square is the medal's whole 128-unit box.
///
/// <para>Image textures have one mip level and a linear sampler (brief-icon-rendering §B), so <see cref="Pick"/> takes
/// the smallest cell at or above the drawn size, from the 1x tiers and then the 2x ones: the medal is never shrunk by
/// more than 1.5× and only enlarged past 256 px.</para>
/// </summary>
public static class MedalLayout
{
    public const int Width = 782;
    public const int Height = 574;
    public const int Pad = 2;

    /// <summary>Medals smaller than this (device px across the 128-unit box) are the row tier: drawn as vector meshes, no badge.</summary>
    public const float RowTierMaxPx = 32f;

    /// <summary>Manifest resource names (the csproj's <c>LogicalName</c>s).</summary>
    public const string ResourceName1x = "Tsukimichi.assets.ui.medals.png";
    public const string ResourceName2x = "Tsukimichi.assets.ui.medals@2x.png";

    /// <summary>The cell sizes at 1x, smallest first; the 2x atlas holds each at twice the size.</summary>
    public static readonly int[] Tiers = [48, 64, 96, 128];

    /// <summary>The number of sprites (every <see cref="MedalSprite"/>).</summary>
    public const int SpriteCount = (int)MedalSprite.OtherJobHand + 1;

    /// <summary>The sprite's cell at the 1x tier <paramref name="tier"/> (one of <see cref="Tiers"/>); empty for anything else.</summary>
    public static AtlasRect Rect(MedalSprite sprite, int tier)
    {
        var i = (int)sprite;
        if (i < 0 || i >= SpriteCount)
        {
            return default;
        }

        var y = Pad;
        foreach (var cell in Tiers)
        {
            var perRow = Math.Min(SpriteCount, (Width - Pad) / (cell + Pad));
            if (cell == tier)
            {
                return new AtlasRect(Pad + (i % perRow) * (cell + Pad), y + (i / perRow) * (cell + Pad), cell, cell);
            }

            y += ((SpriteCount + perRow - 1) / perRow) * (cell + Pad);
        }

        return default;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/> (the same in both atlases).</summary>
    public static (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>
    /// The texture and tier to draw a medal <paramref name="sizePx"/> device px across: the smallest 1x tier at or above
    /// it, else the smallest 2x tier (twice a 1x tier) at or above it, else the largest 2x tier.
    /// </summary>
    public static (int Tier, bool TwoX) Pick(float sizePx) => Themes.ThemeAtlasRules.PickTier(Tiers, sizePx);

    /// <summary>The sprite for a quest state; Ready on another job takes its role's seat (<see cref="OtherJob"/>).</summary>
    public static MedalSprite For(QuestState state, JobSeat seat = JobSeat.Hand) => state switch
    {
        QuestState.Ready => MedalSprite.Ready,
        QuestState.ReadyOnOtherJob => OtherJob(seat),
        QuestState.Accepted => MedalSprite.InJournal,
        QuestState.Blocked => MedalSprite.Blocked,
        QuestState.DoneThisCycle => MedalSprite.DoneThisCycle,
        QuestState.Completed => MedalSprite.Completed,
        QuestState.Foreclosed => MedalSprite.LockedOut,
        _ => MedalSprite.NotChecked,
    };

    /// <summary>Ready on another job's medal with the seat for <paramref name="seat"/>.</summary>
    public static MedalSprite OtherJob(JobSeat seat) => seat switch
    {
        JobSeat.Tank => MedalSprite.OtherJobTank,
        JobSeat.Healer => MedalSprite.OtherJobHealer,
        JobSeat.Dps => MedalSprite.OtherJobDps,
        _ => MedalSprite.OtherJobHand,
    };

    /// <summary>The sprite's key in <c>medals.json</c> (the round 5 file stem for the state medals).</summary>
    public static string Key(MedalSprite sprite) => sprite switch
    {
        MedalSprite.Ready => "ready",
        MedalSprite.InJournal => "in-journal",
        MedalSprite.Blocked => "blocked",
        MedalSprite.DoneThisCycle => "done-this-cycle",
        MedalSprite.Completed => "completed",
        MedalSprite.LockedOut => "locked-out",
        MedalSprite.NotChecked => "not-checked",
        MedalSprite.OtherJobTank => "other-job-tank",
        MedalSprite.OtherJobHealer => "other-job-healer",
        MedalSprite.OtherJobDps => "other-job-dps",
        MedalSprite.OtherJobHand => "other-job-hand",
        _ => string.Empty,
    };
}
