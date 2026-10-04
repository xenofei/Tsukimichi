using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Releases;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Advanced › What's new (spec-1.22 W3), beside Diagnostics, where About lives: whether the popup shows
/// after an update, and every release since 1.14.0, newest first. A row opens the popup on that release, with ‹ ›
/// walking the whole history; the running release wears an Installed chip. Six rows show, then "3 earlier releases,
/// back to 1.14.0", which unfolds the rest.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The rows shown before the rest fold under "earlier releases".</summary>
    private const int WhatsNewRowsShown = 6;

    private const float WhatsNewRowLogical = 36f;

    private bool whatsNewAllShown;
    private (int Count, string Oldest, int Language) earlierKey;
    private string earlierText = string.Empty;

    /// <summary>The What's new popup; set by the plugin. Null leaves the list out.</summary>
    public WhatsNewPopup? WhatsNew { get; set; }

    private void DrawWhatsNew()
    {
        Header(Strings.WhatsNew.Title);
        var show = settings.ShowWhatsNewAfterUpdate;
        if (Toggle(Strings.WhatsNew.ShowAfterUpdate, Strings.WhatsNew.ShowAfterUpdateHint, ref show, "whats new changelog release notes update popup"))
        {
            settings.ShowWhatsNewAfterUpdate = show;
            Save();
        }

        if (WhatsNew is not { } popup || !Setting(Strings.WhatsNew.History, Strings.WhatsNew.HistoryHint, "whats new changelog release notes history versions past", 0f))
        {
            return;
        }

        SettingBelow();
        var history = popup.History;
        if (history.Count == 0)
        {
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(Strings.WhatsNew.HistoryNone);
            }

            EndSetting();
            return;
        }

        var width = MathF.Max(1f, row.Right - row.Left);
        var shown = whatsNewAllShown ? history.Count : Math.Min(history.Count, WhatsNewRowsShown);
        for (var i = 0; i < shown; i++)
        {
            if (WhatsNewRow(history[i], i, width, history[i].Version == popup.RunningVersion))
            {
                popup.OpenAt(history[i]);
            }
        }

        if (history.Count > WhatsNewRowsShown)
        {
            var folded = history.Count - WhatsNewRowsShown;
            var oldest = history[^1].Version;
            if (earlierKey != (folded, oldest, Loc.Version))
            {
                earlierKey = (folded, oldest, Loc.Version);
                earlierText = (folded == 1
                    ? string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.EarlierOneFormat, oldest)
                    : string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.EarlierFormat, folded, oldest)) + Strings.WhatsNew.Chevron;
            }

            if (ImGui.Selectable((whatsNewAllShown ? Strings.WhatsNew.Fewer : earlierText) + "##whatsNewEarlier", false, ImGuiSelectableFlags.None, new Vector2(width, 0f)))
            {
                whatsNewAllShown = !whatsNewAllShown;
            }
        }

        EndSetting();
    }

    /// <summary>
    /// One release: its name, its version in Secondary, the Installed chip on the running one, and its date with "›"
    /// at the right end. The whole row is one focusable item; returns true when it was clicked.
    /// </summary>
    private static bool WhatsNewRow(ReleaseNote release, int index, float width, bool installed)
    {
        var s = Theme.Surface;
        var height = MathF.Max(UiMetrics.Px(WhatsNewRowLogical), ImGui.GetFrameHeight());
        var min = ImGui.GetCursorScreenPos();
        ImGui.PushID(index);
        var clicked = ImGui.InvisibleButton("##release", new Vector2(width, height));
        ImGui.PopID();
        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();
        var dl = ImGui.GetWindowDrawList();
        var max = min + new Vector2(width, height);
        if (hovered)
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Hover), UiMetrics.Px(4f));
            UiMetrics.Tooltip(Strings.WhatsNew.RowTooltip);
        }

        var line = ImGui.GetTextLineHeight();
        var y = MathF.Round(min.Y + ((height - line) * 0.5f));
        var pad = UiMetrics.Px(8f);

        // The date and the chevron on the right, then the name and the version in what room is left.
        var end = release.ShortDate + Strings.WhatsNew.Chevron;
        var endWidth = ImGui.CalcTextSize(end).X;
        dl.AddText(new Vector2(max.X - pad - endWidth, y), Theme.U32(s.TextSecondary), end);
        var x = min.X + pad;
        var room = MathF.Max(1f, max.X - pad - endWidth - UiMetrics.Px(12f) - x);
        var nameWidth = MathF.Min(ImGui.CalcTextSize(release.Name).X, room);
        Chrome.EllipsisTextAt(dl, new Vector2(x, y), room, release.Name, Theme.U32(s.Text));
        x += nameWidth + UiMetrics.Px(8f);
        var versionWidth = ImGui.CalcTextSize(release.Version).X;
        if (x + versionWidth <= min.X + pad + room)
        {
            dl.AddText(new Vector2(x, y), Theme.U32(s.TextSecondary), release.Version);
            x += versionWidth + UiMetrics.Px(8f);
        }

        if (installed)
        {
            var chip = Strings.WhatsNew.Installed;
            var chipSize = Chrome.PillSize(chip);
            if (x + chipSize.X <= min.X + pad + room)
            {
                Chrome.PillAt(dl, new Vector2(x, MathF.Round(min.Y + ((height - chipSize.Y) * 0.5f))), chipSize, chip, Theme.WithAlpha(s.Text, 0.06f), Theme.U32(s.Line), Theme.U32(s.Text));
            }
        }

        return clicked;
    }
}
