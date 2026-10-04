using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Travel;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The travel preflight in Setup (feature plan v7 A9; docs/design/v7/ui/spec-1.18.md § A9, <c>preflight.png</c>): the
/// row under the companion plugins in Settings › Automation, a table of four checks, Movement, Camera, vnavmesh and
/// Conflicts. Each row has three columns: what (110 px); the status, a 7 px dot then the status in words, with one line
/// under it on the consequence; and the fix (280 px, reserved even when empty, so every row has the same width), one
/// pill and its safety line. Dots: all clear a filled Secondary dot, information (first-person view) a hollow Tertiary
/// ring, needs a change the attention colour; always beside words, so colour is never the only carrier. Rows are at
/// least 58 px tall (40 at Plain).
/// <para>A fix runs only from its pill and changes one setting: "Switch to Standard" (the game's movement type; the
/// row then offers "Restore Legacy" until the plugin restarts), "Allow movement" (vnavmesh's runtime switch; Undo for 8
/// seconds). A conflict's pill opens Dalamud's plugin installer at the plugin: Tsukimichi never turns other plugins
/// off. The camera has no fix. Read live (cached briefly by the service), so there is no Check again.</para>
/// </summary>
public sealed partial class ConfigWindow
{
    private const double PreflightNoteSeconds = 8.0;
    private const float PreflightWhatWidth = 110f;
    private const float PreflightFixWidth = 280f;
    private const float PreflightRowHeight = 58f;
    private const float PreflightRowHeightPlain = 40f;
    private const float PreflightDot = 7f;

    private string? preflightNote;
    private double preflightNoteUntil;

    /// <summary>The travel preflight; set by the plugin. Null hides the row.</summary>
    public TravelPreflightService? TravelPreflight { get; set; }

    /// <summary>
    /// The "needs a change" cue: copper (spec-1.18, <see cref="Theme.Copper"/>), which the dot never carries alone (the
    /// status word and the fix beside it say the same).
    /// </summary>
    private static Vector4 PreflightAttention => Theme.Copper;

    private void DrawTravelPreflight()
    {
        if (TravelPreflight is not { } preflight)
        {
            return;
        }

        if (!Setting(Strings.ConfigTravelPreflight, Strings.ConfigTravelPreflightHint, "travel preflight before walk movement type legacy standard controller camera first person vnavmesh paused conflict WrongWarpFinder Lifestream", 0f))
        {
            return;
        }

        SettingBelow();
        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX;
        using (var table = ImRaii.Table("##travelPreflight", 3, flags))
        {
            if (table)
            {
                ImGui.TableSetupColumn("##what", ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(PreflightWhatWidth));
                ImGui.TableSetupColumn("##status", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##fix", ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(PreflightFixWidth));
                var minHeight = UiMetrics.Px(Theme.Flair == Flair.Plain ? PreflightRowHeightPlain : PreflightRowHeight);
                foreach (var result in preflight.Results)
                {
                    using var id = ImRaii.PushId((int)result.Item);
                    DrawPreflightRow(preflight, result, minHeight);
                }
            }
        }

        if (preflightNote is { } note && ImGui.GetTime() < preflightNoteUntil)
        {
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(note);
            }
        }

        EndSetting();
    }

    private void DrawPreflightRow(TravelPreflightService preflight, PreflightResult result, float minHeight)
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.None, minHeight);

        // What.
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.TravelPreflightLabel(result.Item));
        }

        // Status: the dot, the status in words, the consequence under it.
        ImGui.TableNextColumn();
        var head = result.Item == PreflightItem.Conflicts && result.Conflicts.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.TravelPreflightConflictHeadFormat, result.Conflicts[0].InternalName)
            : Strings.TravelPreflightHead(result.Item, result.State);
        var detail = result.Item == PreflightItem.Conflicts && result.Conflicts.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.TravelPreflightConflictFormat, Strings.TravelPreflightConflict(result.Conflicts[0].Key))
            : Strings.TravelPreflightStatus(result.Item, result.State);
        DrawPreflightDot(result.State);
        Chrome.SemiboldTextWrapped(head, Theme.Surface.Text);
        var hovered = ImGui.IsItemHovered();
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(detail);
        }

        hovered |= ImGui.IsItemHovered();
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.TravelPreflightWord(result.State), Strings.TravelPreflightWhy(result.Item));
        }

        // Fix: reserved even when empty.
        ImGui.TableNextColumn();
        DrawPreflightFix(preflight, result);
    }

    /// <summary>A 7 px dot at the start of the status line: filled Secondary, hollow Tertiary ring, or the attention colour.</summary>
    private static void DrawPreflightDot(PreflightState state)
    {
        var size = UiMetrics.Px(PreflightDot);
        var start = ImGui.GetCursorScreenPos();
        var center = new Vector2(start.X + (size * 0.5f), start.Y + (ImGui.GetTextLineHeight() * 0.5f));
        var dl = ImGui.GetWindowDrawList();
        switch (state)
        {
            case PreflightState.Warn:
                dl.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(PreflightAttention));
                break;
            case PreflightState.Ok:
                dl.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(Theme.Surface.TextSecondary));
                break;
            default:
                dl.AddCircle(center, (size * 0.5f) - UiMetrics.Px(0.5f), ImGui.GetColorU32(Theme.Surface.TextTertiary), 0, UiMetrics.Px(1f));
                break;
        }

        ImGui.Dummy(new Vector2(size, ImGui.GetTextLineHeight()));
        ImGui.SameLine(0f, UiMetrics.Px(6f));
    }

    /// <summary>The row's one pill and its safety line; then "Restore Legacy" or Undo while what the fix changed still reads as the fix set it.</summary>
    private void DrawPreflightFix(TravelPreflightService preflight, PreflightResult result)
    {
        if (result.Fix != PreflightFix.None)
        {
            if (PreflightPill(Strings.TravelPreflightFixLabel(result.Fix)))
            {
                var done = preflight.Fix(result.Item);
                if (result.Fix != PreflightFix.OpenPluginInstaller || !done)
                {
                    ShowPreflightNote(done ? Strings.TravelPreflightFixed(result.Item) : Strings.TravelPreflightFixFailed);
                }
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TravelPreflightFixTooltip(result.Fix));
            }

            PreflightSafetyLine(Strings.TravelPreflightSafety(result.Fix));
            return;
        }

        if (!preflight.CanUndo(result.Item))
        {
            return;
        }

        if (PreflightPill(Strings.TravelPreflightUndoLabel(result.Item)))
        {
            ShowPreflightNote(preflight.Undo(result.Item) ? Strings.TravelPreflightUndone : Strings.TravelPreflightFixFailed);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TravelPreflightUndoTooltip);
        }
    }

    /// <summary>A pill-shaped button (the card rounding at Plain) with a hairline edge.</summary>
    private static bool PreflightPill(string label)
    {
        var rounding = Theme.Flair == Flair.Plain ? UiMetrics.Px(Theme.Spacing.CardRounding) : ImGui.GetFrameHeight() * 0.5f;
        using var round = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, rounding);
        using var edge = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, Math.Max(1f, UiMetrics.Px(1f)));
        return ImGui.Button(label);
    }

    private static void PreflightSafetyLine(string text)
    {
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextWrapped(text);
        }
    }

    private void ShowPreflightNote(string note)
    {
        preflightNote = note;
        preflightNoteUntil = ImGui.GetTime() + PreflightNoteSeconds;
    }
}
