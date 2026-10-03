using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Todo;

/// <summary>What the player is doing this frame, as far as the Todo overlay's hiding options care.</summary>
/// <param name="InCombat">In combat.</param>
/// <param name="Talking">Talking to an NPC, or in another event the game runs (a quest dialogue, a shop, a menu).</param>
/// <param name="GroupPose">In group pose.</param>
public readonly record struct TodoContext(bool InCombat, bool Talking, bool GroupPose);

/// <summary>
/// The Todo overlay's hiding options (feature plan v6 M3, decision 12: they exist and are off by default): the overlay
/// steps aside in combat, while talking to NPCs or in group pose, each only when the player turned it on. Pure.
/// </summary>
/// <param name="InCombat">Hide in combat.</param>
/// <param name="Talking">Hide while talking to NPCs.</param>
/// <param name="GroupPose">Hide in group pose.</param>
public readonly record struct TodoHideRules(bool InCombat, bool Talking, bool GroupPose)
{
    /// <summary>The defaults: nothing hides the overlay.</summary>
    public static TodoHideRules None => default;

    /// <summary>Whether the overlay steps aside in <paramref name="context"/>.</summary>
    public bool Hides(in TodoContext context) =>
        (InCombat && context.InCombat) || (Talking && context.Talking) || (GroupPose && context.GroupPose);
}

/// <summary>
/// The Todo overlay's completion beat (feature plan v6 M3): when a rebuild drops a row because its quest was completed,
/// the row stays a moment where it stood as a ghost (its moon fills, one halo, then it fades out with no collapse). This
/// finds those rows: for each old row, in order, whether it left the section and its quest is now completed. Pure and
/// allocation-free.
/// </summary>
public static class TodoBeat
{
    /// <summary>
    /// Adds to <paramref name="into"/> the (old index, row id) of every row of <paramref name="before"/> that is not in
    /// <paramref name="after"/> and whose quest <paramref name="completed"/> says is done now. Returns how many.
    /// </summary>
    public static int Ghosts(ReadOnlySpan<uint> before, ReadOnlySpan<uint> after, Func<uint, bool> completed, List<(int Index, uint RowId)> into)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(into);
        var found = 0;
        for (var i = 0; i < before.Length; i++)
        {
            var id = before[i];
            if (after.Contains(id) || !completed(id))
            {
                continue;
            }

            into.Add((i, id));
            found++;
        }

        return found;
    }

    /// <summary>
    /// A ghost row's look at <paramref name="progress"/> (0..1 over <see cref="MotionTokens.Beat"/>): its moon's lit
    /// fraction (from the half moon to full over the first 40 %), the halo's progress (0..1 over the middle, -1
    /// outside it) and the row's opacity (1, then fading out over the last 40 %).
    /// </summary>
    public static (float Lit, float Halo, float Alpha) Look(float progress)
    {
        if (!float.IsFinite(progress) || progress >= 1f)
        {
            return (1f, -1f, 0f);
        }

        var p = Math.Max(0f, progress);
        var lit = MotionTokens.WaxFrom + ((1f - MotionTokens.WaxFrom) * MotionMath.EaseOutCubic(p / 0.4f));
        var halo = p is >= 0.25f and < 0.85f ? (p - 0.25f) / 0.6f : -1f;
        var alpha = p < 0.6f ? 1f : 1f - MotionMath.EaseOutCubic((p - 0.6f) / 0.4f);
        return (lit, halo, alpha);
    }
}
