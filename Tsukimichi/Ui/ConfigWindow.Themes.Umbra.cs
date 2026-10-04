using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// The Follow Umbra tile in Settings › Themes › Palette (plan v8 M3, decision 4; spec-1.22 decision 17): beside Follow
/// Dalamud, off by default. It draws Umbra's colour profile as Tsukimichi maps it (<see cref="UmbraPalette"/>), or Night
/// when Umbra or its colours can't be read; its hover says which. Choosing it applies at once with Undo, like every tile.
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawFollowUmbraTile(ImDrawListPtr dl, Vector2 cell, Vector2 tile, float pitch, float gap, bool highContrast)
    {
        if (Umbra is not { } umbra)
        {
            return;
        }

        var s = Theme.Surface;
        var min = cell + new Vector2(MathF.Round(((pitch - gap) - tile.X) * 0.5f), 0f);
        ImGui.SetCursorScreenPos(min);
        using var id = ImRaii.PushId("followUmbra");
        var clicked = ImGui.InvisibleButton("##paletteTile", tile);
        var hovered = ImGui.IsItemHovered();
        var selected = settings.FollowUmbraPalette;
        var palette = highContrast ? umbra.Palette.HighContrast : umbra.Palette;
        DrawPaletteTile(dl, min, tile, palette);

        var hover = Motion.Hover(Motion.Key(CardHoverTag, 0x1FFu), hovered);
        var chosen = Motion.Select(Motion.Key(TileSelectTag, 0xFFu), selected);
        var rounding = UiMetrics.Px(4f);
        dl.AddRect(min, min + tile, Theme.WithAlpha(s.Line, 0.9f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (hover > 0.01f && chosen < 0.99f)
        {
            dl.AddRect(min, min + tile, Theme.WithAlpha(s.StrongLine, hover * (1f - chosen)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

        if (chosen > 0.01f)
        {
            var edge = MathF.Max(1f, UiMetrics.Px(2f));
            dl.AddRect(min - new Vector2(edge * 0.5f), min + tile + new Vector2(edge * 0.5f), Theme.WithAlpha(Theme.GoldLine, chosen), rounding + (edge * 0.5f), ImDrawFlags.None, edge);
        }

        using (Typography.Caption())
        {
            var name = Strings.PaletteNameFollowUmbra;
            var width = MathF.Min(pitch - gap, ImGui.CalcTextSize(name).X);
            Chrome.EllipsisTextAt(dl, new Vector2(cell.X + MathF.Round(((pitch - gap) - width) * 0.5f), min.Y + tile.Y + UiMetrics.Px(4f)), pitch - gap, name, Theme.U32(selected ? s.Text : s.TextSecondary));
        }

        if (hovered)
        {
            UiMetrics.Tooltip(Strings.PaletteNameFollowUmbra, FollowUmbraTileHint(umbra));
        }

        if (clicked && !selected)
        {
            settings.FollowUmbraPalette = true;
            Save();
            UndoToast.Show(string.Format(CultureInfo.CurrentCulture, Strings.UndoToastPaletteFormat, Strings.PaletteNameFollowUmbra), () =>
            {
                settings.FollowUmbraPalette = false;
                Save();
            });
        }
    }

    /// <summary>The tile's hover: which Umbra profile it follows, or why it shows Night.</summary>
    private static string FollowUmbraTileHint(Game.UmbraProbe umbra)
    {
        if (!umbra.Loaded)
        {
            return Strings.FollowUmbraTileNoUmbra;
        }

        if (umbra.PaletteFellBack || umbra.Read?.Colors is not { } colors)
        {
            return Strings.FollowUmbraTileUnread;
        }

        var hint = string.Format(CultureInfo.CurrentCulture, Strings.FollowUmbraTileFormat, colors.Name);
        return umbra.PaletteClamped ? hint + "\n" + Strings.FollowUmbraTileClamped : hint;
    }
}
