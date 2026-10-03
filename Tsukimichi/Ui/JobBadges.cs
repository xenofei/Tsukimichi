using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The job in a Ready on another job badge (feature plan v6 G1; concept.md, "Job-badge frame spec"): the game's own
/// unframed job glyph (icon 062000 + ClassJob), read at runtime so every job works and nothing of Square Enix's is
/// shipped, the seat its role takes (<see cref="JobBadgeRules.SeatFor"/>), and its optical centre, measured once per
/// job from the icon's pixels exactly as round 5 measured its examples (<see cref="JobBadgeRules.OpticalOffset"/>).
/// Everything is cached per job; a sheet or file the game does not have falls back to the Hand seat and the default
/// offset. Draw thread only.
/// </summary>
internal static class JobBadges
{
    private static readonly Dictionary<byte, JobSeat> Seats = [];
    private static readonly Dictionary<byte, Vector2> Offsets = [];

    /// <summary>The seat colour for <paramref name="job"/> (ClassJob row id): its combat role, or Hand for crafters, gatherers and unknowns.</summary>
    public static JobSeat Seat(byte job)
    {
        if (Seats.TryGetValue(job, out var seat))
        {
            return seat;
        }

        seat = JobSeat.Hand;
        try
        {
            if (job != 0 && Plugin.DataManager?.GetExcelSheet<ClassJob>()?.GetRowOrDefault(job) is { } row)
            {
                seat = JobBadgeRules.SeatFor(row.Role, handOrLand: false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Debug(ex, $"Job badge: no ClassJob row {job}");
        }

        Seats[job] = seat;
        return seat;
    }

    /// <summary>
    /// Draws <paramref name="job"/>'s glyph in the square <paramref name="min"/>..<paramref name="max"/>, shifted so its
    /// optical centre lands on the square's centre; false (nothing drawn) while it loads or when the game has no such icon.
    /// </summary>
    public static bool TryDraw(ImDrawListPtr dl, byte job, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        var icon = JobBadgeRules.IconFor(job);
        var textures = Plugin.TextureProvider;
        var side = max.X - min.X;
        if (icon == 0 || textures is null || !(side > 0f))
        {
            return false;
        }

        var lookup = new GameIconLookup(icon, false, side >= JobBadgeRules.HiResFromPx);
        if (!textures.TryGetFromGameIcon(lookup, out var texture) || !texture.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        var shift = -Offset(job) * side;
        dl.AddImage(wrap.Handle, min + shift, max + shift, Vector2.Zero, Vector2.One, tint);
        return true;
    }

    /// <summary>The glyph's optical offset in its canvas (a fraction of the side), measured once from the game file.</summary>
    public static Vector2 Offset(byte job)
    {
        if (Offsets.TryGetValue(job, out var offset))
        {
            return offset;
        }

        offset = JobBadgeRules.DefaultOffset;
        try
        {
            var icon = JobBadgeRules.IconFor(job);
            var data = Plugin.DataManager;
            if (icon != 0 && data is not null)
            {
                var folder = $"ui/icon/{icon / 1000 * 1000:D6}/{icon:D6}";
                var tex = data.GetFile<TexFile>(folder + "_hr1.tex") ?? data.GetFile<TexFile>(folder + ".tex");
                if (tex is not null)
                {
                    // Lumina decodes to B8G8R8A8: alpha is the fourth byte either way.
                    int width = tex.Header.Width, height = tex.Header.Height;
                    offset = JobBadgeRules.OpticalOffset(tex.ImageData, width, height, width * 4);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Debug(ex, $"Job badge: could not measure job {job}'s icon");
        }

        Offsets[job] = offset;
        return offset;
    }
}
