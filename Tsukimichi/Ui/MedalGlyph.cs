using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>How the glyph window forces a medal to draw (the A/B sheet's columns); <see cref="Auto"/> everywhere else.</summary>
public enum MedalRenderer
{
    /// <summary>The atlas at hero sizes under Flair Full and Quiet, the vector medal otherwise.</summary>
    Auto,

    /// <summary>Always the vector medal (what rows, high contrast and Plain draw).</summary>
    Vector,

    /// <summary>The atlas whenever it has loaded, at any size (to compare the two).</summary>
    Atlas,
}

/// <summary>
/// The quest-state medals, "Menphina's Medallion" (feature plan v6 G1; docs/design/moon-v6/round5/medallion-r5).
/// <para><b>Rows</b> (a medal under <see cref="MedalLayout.RowTierMaxPx"/>, 32 device px): the vector medal from
/// <see cref="MedalArt"/>, pre-built meshes written straight into the draw list (they batch with text), with no badge.
/// The badge's content goes beside the medal at text height instead (<see cref="DrawRowBadge"/>): the open padlock on
/// Ready, the journal on In journal, the closed padlock on Blocked, and the game's own icon of the job a quest is ready
/// on.</para>
/// <para><b>Hero sizes</b> (32 px and up): the pre-rendered atlas (<see cref="MedalAtlas"/>), badges included, under
/// Flair Full and Quiet; Ready on another job takes its role's seat and gets the job icon drawn into it
/// (<see cref="JobBadges"/>). The vector medal and badge stand in while the atlas loads, at Flair Plain, and under the
/// high-contrast palette, where <see cref="MedalTokens"/> swaps every colour for the palette's flat ladder.</para>
/// <para>Every draw takes an optional <c>alpha</c>: the hook for U8a's wax transition, which passes its progress there
/// (nothing else animates the medals yet).</para>
/// </summary>
public static class MedalGlyph
{
    /// <summary>The glyph window's override (G3's A/B sheet); <see cref="MedalRenderer.Auto"/> everywhere else.</summary>
    internal static MedalRenderer Renderer { get; set; } = MedalRenderer.Auto;

    /// <summary>The medal's 128-unit box per pixel of keyline radius: the box is a hair larger than the keyline.</summary>
    private const float BoxPerRadius = 64f / MedalArt.KeylineRadius;

    /// <summary>
    /// Draws the medal of <paramref name="state"/> centred at <paramref name="center"/> with keyline radius
    /// <paramref name="radius"/> px. <paramref name="job"/> is the ClassJob a Ready on another job quest is ready on (0:
    /// unknown, an empty seat). <paramref name="alpha"/> fades the whole medal (U8a's wax hook).
    /// </summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job = 0, float alpha = 1f)
    {
        if (!(radius > 0.5f) || !(alpha > 0f))
        {
            return;
        }

        var (min, size) = Box(center, radius);
        var tokens = MedalTokens.For(Theme.Glyphs);
        var hero = size >= MedalLayout.RowTierMaxPx;
        var tint = Theme.WithAlpha(Vector4.One, alpha);

        if (UseAtlas(tokens, hero))
        {
            var seat = state == QuestState.ReadyOnOtherJob ? JobBadges.Seat(job) : JobSeat.Hand;
            if (MedalAtlas.TryDraw(dl, MedalLayout.For(state, seat), min, min + new Vector2(size), tint))
            {
                if (state == QuestState.ReadyOnOtherJob)
                {
                    DrawJobInSeat(dl, min, size, job, tint);
                }

                return;
            }
        }

        DrawMesh(dl, MedalArt.Medal(state, tokens, row: !hero), min, size, alpha);
        if (!hero)
        {
            return;
        }

        var badge = MedalArt.BadgeOf(state);
        if (badge == MedalBadge.None)
        {
            return;
        }

        var jobSeat = badge == MedalBadge.Job ? JobBadges.Seat(job) : JobSeat.Hand;
        DrawMesh(dl, MedalArt.Badge(badge, jobSeat, tokens), min, size, alpha);
        if (badge == MedalBadge.Job)
        {
            DrawJobInSeat(dl, min, size, job, tint);
        }
    }

    /// <summary>
    /// Reserves a <paramref name="size"/> square item at the cursor and draws the medal inside it at
    /// <see cref="MoonGlyph.InlineRadiusFraction"/> of the box, as the moons were.
    /// </summary>
    public static void DrawInline(QuestState state, float size, byte job = 0)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        Draw(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * MoonGlyph.InlineRadiusFraction, state, job);
    }

    /// <summary>
    /// Whether a row medal of <paramref name="state"/> has badge content to draw beside it: Ready, In journal and Blocked
    /// always, Ready on another job when the job is known.
    /// </summary>
    public static bool HasRowBadge(QuestState state, byte job = 0) => MedalArt.BadgeOf(state) switch
    {
        MedalBadge.None => false,
        MedalBadge.Job => job != 0,
        _ => true,
    };

    /// <summary>
    /// The row fallback (concept.md §4–6): a row medal's badge content at text height in the square
    /// <paramref name="min"/>..<paramref name="min"/> + <paramref name="side"/>: the open lock, the journal, the closed
    /// lock, or the job's icon. Nothing for the other states. Returns whether anything was drawn.
    /// </summary>
    public static bool DrawRowBadge(ImDrawListPtr dl, Vector2 min, float side, QuestState state, byte job = 0, float alpha = 1f)
    {
        if (!(side > 1f) || !(alpha > 0f))
        {
            return false;
        }

        min = new Vector2(MathF.Round(min.X), MathF.Round(min.Y));
        side = MathF.Round(side);
        var badge = MedalArt.BadgeOf(state);
        switch (badge)
        {
            case MedalBadge.None:
                return false;
            case MedalBadge.Job:
                return job != 0 && JobBadges.TryDraw(dl, job, min, min + new Vector2(side), Theme.WithAlpha(Vector4.One, alpha));
            default:
                DrawMesh(dl, MedalArt.RowGlyph(badge, MedalTokens.For(Theme.Glyphs)), min, side, alpha);
                return true;
        }
    }

    /// <summary>
    /// Reserves a <paramref name="side"/> square at the cursor whatever the state (so a column of rows stays aligned)
    /// and draws <see cref="DrawRowBadge"/> in it.
    /// </summary>
    public static void DrawRowBadgeInline(QuestState state, float side, byte job = 0)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(side, side));
        DrawRowBadge(ImGui.GetWindowDrawList(), pos, side, state, job);
    }

    /// <summary>
    /// A vector medal with every vertex's alpha scaled by <paramref name="alpha"/>, in the box
    /// <paramref name="min"/>..<paramref name="min"/> + <paramref name="size"/> (the 128-unit box). Rows draw dozens of
    /// medals of about a thousand vertices each, so after one reservation per part the vertices and indices are written
    /// straight into the draw list's buffers, as <c>PrimWriteVtx</c> / <c>PrimWriteIdx</c> do one call at a time.
    /// Allocation-free.
    /// </summary>
    internal static unsafe void DrawMesh(ImDrawListPtr dl, MedalMesh mesh, Vector2 min, float size, float alpha = 1f)
    {
        var k = size / 128f;
        var uv = ImGui.GetFontTexUvWhitePixel();
        var fade = Math.Clamp(alpha, 0f, 1f);
        foreach (var part in mesh.Parts)
        {
            if (size < part.MinSizePx)
            {
                continue;
            }

            var positions = part.Positions;
            var offsets = part.Offsets;
            var colors = part.Colors;
            var indices = part.Indices;
            dl.PrimReserve(indices.Length, positions.Length);

            ref var vertexCursor = ref dl.VtxWritePtr;
            ref var currentIndex = ref dl.VtxCurrentIdx;
            var i0 = currentIndex;
            var vertex = vertexCursor.Handle;
            for (var i = 0; i < positions.Length; i++)
            {
                var color = colors[i];
                if (fade < 1f)
                {
                    color = (color & 0x00FFFFFFu) | ((uint)MathF.Round((color >> 24) * fade) << 24);
                }

                vertex[i] = new ImDrawVert(min + positions[i] * k + offsets[i], uv, color);
            }

            vertexCursor = new ImDrawVertPtr(vertex + positions.Length);
            currentIndex += (uint)positions.Length;

            var index = dl.IdxWritePtr;
            for (var j = 0; j < indices.Length; j++)
            {
                index[j] = (ushort)(i0 + indices[j]);
            }

            dl.IdxWritePtr = index + indices.Length;
        }
    }

    /// <summary>The medal's box for a keyline radius: whole-pixel size and corner, so atlas texels and rims land on pixels.</summary>
    internal static (Vector2 Min, float Size) Box(Vector2 center, float radius)
    {
        var size = MathF.Max(1f, MathF.Round(2f * radius * BoxPerRadius));
        return (new Vector2(MathF.Round(center.X - size * 0.5f), MathF.Round(center.Y - size * 0.5f)), size);
    }

    private static bool UseAtlas(MedalTokens tokens, bool hero) => Renderer switch
    {
        MedalRenderer.Vector => false,
        MedalRenderer.Atlas => !tokens.Flat,
        _ => hero && !tokens.Flat && Theme.Flair != Flair.Plain,
    };

    /// <summary>The job's icon in the badge seat (the icon slot, <see cref="MedalArt.JobIconSlot"/> units square on the badge centre).</summary>
    private static void DrawJobInSeat(ImDrawListPtr dl, Vector2 min, float size, byte job, uint tint)
    {
        if (job == 0)
        {
            return;
        }

        var k = size / 128f;
        var half = MedalArt.JobIconSlot * 0.5f * k;
        var center = min + MedalArt.BadgeCenter * k;
        JobBadges.TryDraw(dl, job, center - new Vector2(half), center + new Vector2(half), tint);
    }
}
