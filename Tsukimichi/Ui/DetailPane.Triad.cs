using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Triad;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Unlocks line for a quest that opens a Triple Triad opponent (feature plan v7, 1.21.0 P6;
/// spec-1.21 P6 "Elsewhere"): "Triple Triad opponent · Elaisse, The Pillars" with the card icon, after the Unlocks
/// groups, or "Triple Triad opponent · An opponent ahead, a zone ahead" when the spoiler shield hides the opponent
/// (its place past the story point). A masked quest's section stays its one masked line.
/// </summary>
public sealed partial class DetailPane
{
    private readonly List<(string Name, string Place, bool Masked, string? HiddenZone)> triadLines = [];
    private uint triadRowId = uint.MaxValue;
    private int triadVersion = -1;
    private TriadOpponents? triadIndex;

    /// <summary>The Triple Triad opponents (1.21 P6); null, or none read yet, draws no line. Set by the plugin.</summary>
    public Func<TriadOpponents?>? TriadOpponents { get; set; }

    /// <summary>The opponents <paramref name="quest"/> opens, composed once per quest, session and index.</summary>
    private List<(string Name, string Place, bool Masked, string? HiddenZone)> TriadUnlockLines(SessionState session, QuestRecord quest)
    {
        var index = TriadOpponents?.Invoke();
        if (triadRowId == quest.RowId && triadVersion == session.Version && ReferenceEquals(index, triadIndex))
        {
            return triadLines;
        }

        triadRowId = quest.RowId;
        triadVersion = session.Version;
        triadIndex = index;
        triadLines.Clear();
        if (index is null)
        {
            return triadLines;
        }

        foreach (var opponent in index.OpenedBy(quest.RowId))
        {
            if (opponent.Spot is null)
            {
                continue;
            }

            var zoneHidden = opponent.Zone.Length > 0 && session.Spoilers.IsNameMasked(SpoilerKind.Area, opponent.Zone);
            var masked = session.Spoilers.IsMasked(quest) || zoneHidden;
            triadLines.Add((TriadBoardSource.Capitalize(opponent.Name), opponent.Zone, masked, zoneHidden ? opponent.Zone : null));
        }

        return triadLines;
    }

    /// <summary>One line per opponent the quest opens: the card icon, the kind, the name in semibold and the place.</summary>
    private void DrawTriadUnlockLines(List<(string Name, string Place, bool Masked, string? HiddenZone)> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var s = Theme.Surface;
        var line = ImGui.GetTextLineHeight();
        foreach (var (name, place, masked, hiddenZone) in lines)
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetCursorScreenPos();
            var width = MathF.Max(1f, cardRight - min.X - UiMetrics.Px(4f));
            var height = MathF.Max(line, UiMetrics.Px(20f));
            ImGui.Dummy(new Vector2(width, height));
            var hovered = ImGui.IsItemHovered();
            var side = MathF.Round(MathF.Min(UiMetrics.Px(18f), height));
            var iconMin = new Vector2(min.X, min.Y + ((height - side) * 0.5f));
            if (!GameIcon.DrawAt(dl, textures, RouteTarget.TriadCardIcon, iconMin, iconMin + new Vector2(side), UiMetrics.Px(3f)))
            {
                MoonGlyph.DrawVeiled(dl, iconMin + new Vector2(side * 0.5f), side * 0.32f, 0.6f);
            }

            var x = iconMin.X + side + UiMetrics.Px(8f);
            var y = min.Y + ((height - line) * 0.5f);
            var right = min.X + width;
            var prefix = Strings.TriadUnlockPrefix;
            dl.AddText(new Vector2(x, y), Theme.U32(s.TextSecondary), prefix);
            x += ImGui.CalcTextSize(prefix).X;
            var onPlaceholder = false;
            if (masked)
            {
                var shown = Strings.TriadUnlockMasked;
                var cut = Chrome.EllipsisTextAt(dl, new Vector2(x, y), MathF.Max(1f, right - x), shown, Theme.U32(s.TextSecondary));

                // "An opponent ahead, a zone ahead": the shield's hover and right-click (spec-1.20 N6). A zone ahead
                // reveals the zone; a quest the shield hides reveals the quest's names (the opponent shows with them).
                if (shieldSession is { } shieldFor)
                {
                    var textMax = new Vector2(MathF.Min(right, x + ImGui.CalcTextSize(shown).X), y + line);
                    onPlaceholder = hiddenZone is not null
                        ? ShieldText.Interact(new Vector2(x, y), textMax, shieldFor, SpoilerKind.Area, hiddenZone, shown, model.Quest, links, lead: cut ? shown : null, duties: model.DutyNames)
                        : model.Quest is { } hiddenQuest && ShieldText.InteractQuest(new Vector2(x, y), textMax, shieldFor, hiddenQuest, shown, links, lead: cut ? shown : null);
                }
            }
            else
            {
                var nameWidth = ImGui.CalcTextSize(name).X;
                var ink = Theme.U32(s.Text);
                Chrome.EllipsisTextAt(dl, new Vector2(x, y), MathF.Max(1f, right - x), name, ink, nameWidth);
                dl.AddText(new Vector2(x + MathF.Max(0.5f, UiMetrics.Px(0.5f)), y), ink, name);
                x += nameWidth + MathF.Max(0.5f, UiMetrics.Px(0.5f));
                if (place.Length > 0 && x < right)
                {
                    var text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TriadUnlockPlaceFormat, place);
                    Chrome.EllipsisTextAt(dl, new Vector2(x, y), MathF.Max(1f, right - x), text, Theme.U32(s.TextSecondary));
                }
            }

            if (hovered && !onPlaceholder)
            {
                UiMetrics.Tooltip(Strings.TriadUnlockTip);
            }
        }
    }
}
