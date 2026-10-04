using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Localization;
using Tsukimichi.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Themes › Mix moons by state (1.17, plan v7 T10; docs/design/v7/ui/spec-1.17.md §A, <c>mix.png</c>):
/// <list type="bullet">
/// <item><b>Eight rows</b>, one per state and always the same height: its name, its medal now (the row tier, in the
/// look's one kit), a list of "From theme" and every offered set with that state's face, and a reserved note column
/// that says in words what the row's pick misses (a quiet amber dot, never a warning glyph), so nothing shifts.</item>
/// <item><b>A state's list</b> says beside an option what it would add before it is picked ("Ready would stop leading at
/// large sizes", "close to Blocked in a list"); nothing is hidden or disabled. A pick applies at once, with Undo.</item>
/// <item><b>The status line</b>, always present: "Every moon reads apart, and Ready leads (1.35×)." or one calm
/// sentence with Details, Fix it and Reset mix. Details opens the numbers in words below it; Fix it proposes one change
/// that never touches the state just picked (<see cref="MixRules.Fix"/>), applied by Use it with Undo; Reset mix is held
/// (<see cref="GuardedAction.ResetMix"/>), with Undo and the mix's share code in its tooltip.</item>
/// </list>
/// Every number is a lookup in <see cref="MixTable"/> (<see cref="MixRules"/>); nothing renders to judge. High contrast
/// and the Classic theme show one line instead: neither mixes.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string ThemeMixKeywords = "mix match moons state states per state set sets glyph glyphs pick ready blocked completed fix reset warning alike";

    // Spec-1.17 §A2, in logical px.
    private const float MixRowLogical = 46f;
    private const float MixMedalLogical = 32f;
    private const float MixComboLogical = 200f;
    private const float MixComboFaceLogical = 18f;
    private const float MixListFaceLogical = 20f;
    private const float MixListWidthLogical = 380f;
    private const float MixFixMedalLogical = 40f;

    // The medal column draws the row tier: 32 px starts the hero tier (and its badge), so the row tier's largest is 31.
    private const float MixMedalRowTierPx = MedalLayout.RowTierMaxPx - 1f;

    private readonly ConfirmGate mixResetGate = new();
    private readonly ConfirmGate resetAppearanceGate = new();

    // The state the player last picked: Fix it never changes it (spec §A4 step 2).
    private QuestState? mixKeep;
    private bool mixDetailsOpen;
    private bool mixFixOpen;

    // The judged mix, its proposal and every text the section shows, rebuilt when the appearance or the language changes.
    private ResolvedAppearance? mixFor;
    private int mixLanguage = -1;
    private QuestState? mixKeepFor;
    private MixVerdict? mixVerdict;
    private MixFix? mixFix;
    private readonly string[] mixRowNotes = new string[AppearanceStates.Count];
    private string[][] mixOptionNotes = [];
    private string mixStatus = string.Empty;
    private string mixDetails = string.Empty;
    private string mixFixAction = string.Empty;
    private string mixFixResult = string.Empty;
    private string mixFixNone = string.Empty;

    // One look per offered set (every state from it, in the saved look's kit and palette), to draw a set's faces.
    private readonly MixLooks mixLooks = new();

    private static readonly LocText resetMixLabelText = new(static () => Strings.ThemesMixReset + Chrome.HoldIdSuffix);
    private static readonly LocText resetAppearanceHoldLabelText = new(static () => Strings.ThemesResetButton + Chrome.HoldIdSuffix);

    /// <summary>Settings › Themes › Mix moons by state.</summary>
    private void DrawThemeMix()
    {
        Header(Strings.ThemesHeadingMix);
        if (!Row(Strings.ThemesMix, Strings.ThemesMixHint, ThemeMixKeywords))
        {
            return;
        }

        DrawMixBody();

        // The row's foot, as a setting row leaves it.
        ImGui.Dummy(new Vector2(0f, MathF.Max(0f, UiMetrics.Px(RowPadLogical) - ImGui.GetStyle().ItemSpacing.Y)));
    }

    /// <summary>The section under its heading: the lead line, then the rows and the status line, or the one line that says why there is no mix.</summary>
    private void DrawMixBody()
    {
        var saved = settings.Appearance;
        var resolved = GlyphSeam.Appearance;
        var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        using (Typography.Caption())
        {
            var at = ImGui.GetCursorScreenPos();
            var height = TextFlow.Height(Strings.ThemesMixHint, avail);
            TextFlow.DrawClamped(dl, at, Strings.ThemesMixHint, avail, MaxRowLines, Theme.U32(s.TextSecondary));
            ImGui.Dummy(new Vector2(avail, height + UiMetrics.Px(6f)));
        }

        if (!MixRules.Applies(resolved))
        {
            using (Typography.Caption())
            {
                var line = resolved.HighContrast ? Strings.ThemesMixHighContrast : Strings.ThemesMixClassic;
                var at = ImGui.GetCursorScreenPos();
                TextFlow.DrawClamped(dl, at, line, avail, MaxRowLines, Theme.U32(s.TextSecondary));
                ImGui.Dummy(new Vector2(avail, TextFlow.Height(line, avail)));
            }

            return;
        }

        var verdict = RefreshMix(resolved);
        DrawMixRows(saved, resolved, avail);
        DrawMixStatus(saved, verdict, avail);
        if (mixDetailsOpen && !verdict.Ok)
        {
            DrawMixDetails(avail);
        }

        if (mixFixOpen && mixFix is { } fix)
        {
            DrawMixFix(saved, resolved, fix, avail);
        }
    }

    /// <summary>The eight rows: name, medal, list, note; each the same height whatever it says.</summary>
    private void DrawMixRows(AppearanceConfig saved, ResolvedAppearance resolved, float avail)
    {
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var rowHeight = MathF.Max(UiMetrics.Px(MixRowLogical), ImGui.GetFrameHeight() + UiMetrics.Px(10f));
        var medal = MathF.Min(UiMetrics.Px(MixMedalLogical), MixMedalRowTierPx);
        var gap = UiMetrics.Px(12f);
        var states = AppearanceStates.All;
        var nameWidth = 0f;
        for (var i = 0; i < states.Count; i++)
        {
            nameWidth = MathF.Max(nameWidth, ImGui.CalcTextSize(Strings.StateName(states[i])).X);
        }

        nameWidth = MathF.Min(nameWidth + gap, avail * 0.3f);
        var combo = MathF.Min(UiMetrics.Px(MixComboLogical), MathF.Max(UiMetrics.Px(120f), avail - nameWidth - medal - (gap * 3f)));
        var noteX = nameWidth + medal + gap + combo + gap;
        var noteWidth = MathF.Max(0f, avail - noteX);
        var line = ImGui.GetTextLineHeight();
        var top = ImGui.GetCursorScreenPos();
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            var rowMin = top + new Vector2(0f, i * rowHeight);
            var mid = rowMin.Y + (rowHeight * 0.5f);
            if (i > 0)
            {
                dl.AddLine(new Vector2(rowMin.X, rowMin.Y), new Vector2(rowMin.X + avail, rowMin.Y), Theme.WithAlpha(s.Line, 0.5f), UiMetrics.Hairline);
            }

            Chrome.EllipsisTextAt(dl, new Vector2(rowMin.X, mid - (line * 0.5f)), MathF.Max(1f, nameWidth - gap), Strings.StateName(state), Theme.U32(s.Text));

            // The medal now, in the look's kit at the row tier.
            var medalCenter = new Vector2(MathF.Round(rowMin.X + nameWidth + (medal * 0.5f)), MathF.Round(mid));
            GlyphSeam.Draw(dl, medalCenter, RadiusForBox(medal), state, 0);

            // The list.
            ImGui.SetCursorScreenPos(new Vector2(rowMin.X + nameWidth + medal + gap, mid - (ImGui.GetFrameHeight() * 0.5f)));
            using (ImRaii.PushId(i))
            {
                DrawMixCombo(saved, resolved, state, i, combo, avail - nameWidth - medal - gap);
            }

            // The reserved note column: a quiet amber dot and the row's first warning in words, two lines at most.
            if (noteWidth > UiMetrics.Px(24f) && mixRowNotes[i].Length > 0)
            {
                using (Typography.Caption())
                {
                    var caption = ImGui.GetTextLineHeight();
                    var dot = MathF.Max(1.5f, UiMetrics.Px(2.5f));
                    var textX = rowMin.X + noteX + (dot * 2f) + UiMetrics.Px(6f);
                    var width = MathF.Max(1f, rowMin.X + avail - textX);
                    var height = MathF.Min(TextFlow.Height(mixRowNotes[i], width), caption * 2f);
                    var y = mid - (height * 0.5f);
                    var amber = MixAmber(CardGround());
                    dl.AddCircleFilled(new Vector2(rowMin.X + noteX + dot, y + (caption * 0.5f)), dot, Theme.U32(amber), 10);
                    var cut = TextFlow.DrawClamped(dl, new Vector2(textX, y), mixRowNotes[i], width, 2, Theme.U32(amber));
                    if (cut && ImGui.IsMouseHoveringRect(new Vector2(textX, y), new Vector2(textX + width, y + height)))
                    {
                        UiMetrics.Tooltip(mixRowNotes[i]);
                    }
                }
            }
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(avail, states.Count * rowHeight));
    }

    /// <summary>A state's list: "From theme" (naming the theme's set) and every offered set, each with the state's face and what it would add.</summary>
    private void DrawMixCombo(AppearanceConfig saved, ResolvedAppearance resolved, QuestState state, int index, float width, float listRoom)
    {
        var parent = ImGui.GetWindowDrawList();
        var frameMin = ImGui.GetCursorScreenPos();
        var frameHeight = ImGui.GetFrameHeight();
        var picked = PickedSet(saved, state);
        var choices = MixRules.Choices;
        var listWidth = MathF.Max(width, MathF.Min(UiMetrics.Px(MixListWidthLogical), listRoom));

        ImGui.SetNextItemWidth(width);
        ImGui.SetNextWindowSizeConstraints(new Vector2(listWidth, 0f), new Vector2(listWidth, float.MaxValue));
        using (Theme.PushPopup())
        {
            if (ImGui.BeginCombo("##mixState", string.Empty, ImGuiComboFlags.HeightLargest))
            {
                var line = ImGui.GetTextLineHeight();
                var optionHeight = MathF.Max(UiMetrics.Px(30f), (line * 2f) + UiMetrics.Px(4f));
                for (var o = 0; o <= choices.Count; o++)
                {
                    var set = o == 0 ? null : choices[o - 1];
                    var selected = set is null ? picked is null : picked == set.Id;
                    using var id = ImRaii.PushId(o);
                    var min = ImGui.GetCursorScreenPos();
                    if (ImGui.Selectable("##option", selected, ImGuiSelectableFlags.None, new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), optionHeight)))
                    {
                        PickMixSet(saved, state, set);
                    }

                    DrawMixOption(ImGui.GetWindowDrawList(), min, ImGui.GetItemRectMax(), resolved, state, set, mixOptionNotes[index][o]);
                }

                ImGui.EndCombo();
            }
        }

        // The preview over the frame: the state's face at 18 px, then "From theme" or the set's name.
        var face = UiMetrics.Px(MixComboFaceLogical);
        var pad = ImGui.GetStyle().FramePadding.X;
        var faceCenter = new Vector2(MathF.Round(frameMin.X + pad + (face * 0.5f)), MathF.Round(frameMin.Y + (frameHeight * 0.5f)));
        using (GlyphSeam.PushAppearance(MixLook(picked ?? resolved.Theme.Glyphs)))
        {
            GlyphSeam.Draw(parent, faceCenter, RadiusForBox(face), state, 0);
        }

        var label = picked is { } p ? ShareSetName(p) : Strings.ThemesMixFromTheme;
        var textX = faceCenter.X + (face * 0.5f) + UiMetrics.Px(8f);
        var arrow = frameHeight;
        Chrome.EllipsisTextAt(parent, new Vector2(textX, frameMin.Y + ((frameHeight - ImGui.GetTextLineHeight()) * 0.5f)), MathF.Max(1f, frameMin.X + width - arrow - textX - pad), label, Theme.U32(Theme.Surface.Text));
    }

    /// <summary>One option of a state's list: the face at 20 px, the name (the theme's set beside "From theme"), and what it would add.</summary>
    private void DrawMixOption(ImDrawListPtr dl, Vector2 min, Vector2 max, ResolvedAppearance resolved, QuestState state, GlyphSetInfo? set, string note)
    {
        var s = Theme.Surface;
        var face = UiMetrics.Px(MixListFaceLogical);
        var pad = UiMetrics.Px(8f);
        var mid = (min.Y + max.Y) * 0.5f;
        var line = ImGui.GetTextLineHeight();
        var setId = set?.Id ?? resolved.Theme.Glyphs;
        using (GlyphSeam.PushAppearance(MixLook(setId)))
        {
            GlyphSeam.Draw(dl, new Vector2(MathF.Round(min.X + pad + (face * 0.5f)), MathF.Round(mid)), RadiusForBox(face), state, 0);
        }

        var x = min.X + pad + face + UiMetrics.Px(10f);
        var width = max.X - x - pad;
        var noteWidth = note.Length > 0 ? width * 0.5f : 0f;
        var nameWidth = MathF.Max(1f, width - noteWidth - (noteWidth > 0f ? UiMetrics.Px(10f) : 0f));
        var name = set is null ? Strings.ThemesMixFromTheme : ShareSetName(set.Id);
        if (set is null)
        {
            // "From theme" over the theme's set, in the tertiary tone.
            Chrome.EllipsisTextAt(dl, new Vector2(x, mid - line), nameWidth, name, Theme.U32(s.Text));
            using (Typography.Caption())
            {
                Chrome.EllipsisTextAt(dl, new Vector2(x, mid), nameWidth, ShareSetName(resolved.Theme.Glyphs), Theme.U32(s.TextTertiary));
            }
        }
        else
        {
            Chrome.EllipsisTextAt(dl, new Vector2(x, mid - (line * 0.5f)), nameWidth, name, Theme.U32(s.Text));
        }

        if (note.Length == 0)
        {
            return;
        }

        using (Typography.Caption())
        {
            var caption = ImGui.GetTextLineHeight();
            var dot = MathF.Max(1.5f, UiMetrics.Px(2.5f));
            var noteX = max.X - pad - noteWidth;
            var textX = noteX + (dot * 2f) + UiMetrics.Px(5f);
            var textWidth = MathF.Max(1f, max.X - pad - textX);
            var height = MathF.Min(TextFlow.Height(note, textWidth), caption * 2f);
            var y = mid - (height * 0.5f);
            var amber = MixListAmber();
            dl.AddCircleFilled(new Vector2(noteX + dot, y + (caption * 0.5f)), dot, Theme.U32(amber), 10);
            TextFlow.DrawClamped(dl, new Vector2(textX, y), note, textWidth, 2, Theme.U32(amber));
        }
    }

    /// <summary>The status line under the rows: the verdict in one sentence, then Details, Fix it and Reset mix at the right.</summary>
    private void DrawMixStatus(AppearanceConfig saved, MixVerdict verdict, float avail)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var style = ImGui.GetStyle();
        var spacing = style.ItemSpacing.X;
        var padX = style.FramePadding.X * 2f;
        var frame = ImGui.GetFrameHeight();
        ImGui.Dummy(new Vector2(avail, UiMetrics.Px(4f)));
        var min = ImGui.GetCursorScreenPos();
        var right = min.X + avail;

        var resetLabel = resetMixLabelText.Value;
        var resetWidth = Chrome.HoldButtonWidth(resetLabel);
        var warn = !verdict.Ok;
        var fixWidth = ImGui.CalcTextSize(Strings.ThemesMixFix).X + padX;
        var detailsWidth = ImGui.CalcTextSize(Strings.ThemesMixDetails).X + padX;
        var buttons = resetWidth + (warn ? fixWidth + detailsWidth + (spacing * 2f) : 0f);
        var textRight = right - buttons - UiMetrics.Px(12f);

        // The sentence, with an amber dot while it warns.
        var line = ImGui.GetTextLineHeight();
        var textY = min.Y + ((frame - line) * 0.5f);
        var textX = min.X;
        if (warn)
        {
            var dot = MathF.Max(1.5f, UiMetrics.Px(2.5f));
            dl.AddCircleFilled(new Vector2(min.X + dot, textY + (line * 0.5f)), dot, Theme.U32(MixAmber(CardGround())), 10);
            textX += (dot * 2f) + UiMetrics.Px(8f);
        }

        var sentenceWidth = MathF.Max(1f, textRight - textX);
        Chrome.EllipsisTextAt(dl, new Vector2(textX, textY), sentenceWidth, mixStatus, Theme.U32(warn ? s.Text : s.TextSecondary));
        if (ImGui.IsMouseHoveringRect(new Vector2(textX, min.Y), new Vector2(textRight, min.Y + frame)) && ImGui.CalcTextSize(mixStatus).X > sentenceWidth)
        {
            UiMetrics.Tooltip(mixStatus);
        }

        var x = right - buttons;
        if (warn)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, min.Y));
            if (ImGui.Button(Strings.ThemesMixDetails, new Vector2(detailsWidth, frame)))
            {
                mixDetailsOpen = !mixDetailsOpen;
            }

            x += detailsWidth + spacing;
            ImGui.SetCursorScreenPos(new Vector2(x, min.Y));
            using (ImRaii.Disabled(mixFix is null))
            {
                if (ImGui.Button(Strings.ThemesMixFix, new Vector2(fixWidth, frame)))
                {
                    mixFixOpen = !mixFixOpen;
                }
            }

            if (mixFix is null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(mixFixNone);
            }

            x += fixWidth + spacing;
        }

        // Reset mix: held, with Undo and the mix's code in its tooltip so it can always be recovered.
        ImGui.SetCursorScreenPos(new Vector2(x, min.Y));
        var hasMix = AppearanceEdits.HasMix(saved);
        bool reset;
        using (ImRaii.Disabled(!hasMix))
        {
            reset = Chrome.HoldButton(resetLabel, mixResetGate);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (hasMix)
            {
                Safety.Tooltip(string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixResetTooltipFormat, RefreshShareCode(saved)), GuardedAction.ResetMix);
            }
            else
            {
                UiMetrics.Tooltip(Strings.ThemesMixResetNothing);
            }
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(avail, frame));
        if (reset && hasMix)
        {
            var before = saved.Clone();
            AppearanceEdits.ResetMix(saved);
            Save();
            mixKeep = null;
            mixFixOpen = false;
            if (SafetyRules.OffersUndo(GuardedAction.ResetMix))
            {
                UndoToast.Show(Strings.UndoToastMixReset, () => RestoreAppearance(before));
            }
        }
    }

    /// <summary>Details: every warning's numbers in words, and the bar each missed, in a sunken box under the status line.</summary>
    private void DrawMixDetails(float avail)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(10f);
        ImGui.Dummy(new Vector2(avail, UiMetrics.Px(4f)));
        using (Typography.Caption())
        {
            var inner = MathF.Max(1f, avail - (pad * 2f));
            var height = TextFlow.Height(mixDetails, inner) + (pad * 2f);
            var min = ImGui.GetCursorScreenPos();
            dl.AddRectFilled(min, min + new Vector2(avail, height), Theme.U32(s.Sunken), UiMetrics.Px(4f));
            dl.AddRect(min, min + new Vector2(avail, height), Theme.WithAlpha(s.Line, 0.8f), UiMetrics.Px(4f), ImDrawFlags.None, UiMetrics.Hairline);
            TextFlow.DrawClamped(dl, min + new Vector2(pad), mixDetails, inner, 24, Theme.U32(s.TextSecondary));
            ImGui.Dummy(new Vector2(avail, height));
        }
    }

    /// <summary>Fix it's proposal (spec §A4): the medal now and the medal proposed at 40 px, the change in words, Not now and Use it.</summary>
    private void DrawMixFix(AppearanceConfig saved, ResolvedAppearance resolved, MixFix fix, float avail)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(12f);
        var medal = UiMetrics.Px(MixFixMedalLogical);
        var width = MathF.Min(avail, UiMetrics.Px(420f));
        var line = ImGui.GetTextLineHeight();
        var frame = ImGui.GetFrameHeight();
        var textX = (medal * 2f) + UiMetrics.Px(30f);
        var textWidth = MathF.Max(1f, width - (pad * 2f) - textX);
        float caption;
        float resultHeight;
        using (Typography.Caption())
        {
            caption = ImGui.GetTextLineHeight();
            resultHeight = TextFlow.Height(mixFixResult, textWidth);
        }

        var actionHeight = TextFlow.Height(mixFixAction, textWidth);
        var body = MathF.Max(medal, actionHeight + UiMetrics.Px(2f) + resultHeight);
        var height = pad + caption + UiMetrics.Px(8f) + body + UiMetrics.Px(10f) + frame + pad;

        ImGui.Dummy(new Vector2(avail, UiMetrics.Px(6f)));
        var min = new Vector2(ImGui.GetCursorScreenPos().X + avail - width, ImGui.GetCursorScreenPos().Y);
        var max = min + new Vector2(width, height);
        dl.AddRectFilled(min, max, Theme.U32(s.Raised), UiMetrics.Px(6f));
        dl.AddRect(min, max, Theme.U32(s.Line), UiMetrics.Px(6f), ImDrawFlags.None, UiMetrics.Hairline);

        var y = min.Y + pad;
        using (Typography.Caption())
        {
            dl.AddText(new Vector2(min.X + pad, y), Theme.U32(s.TextSecondary), Strings.ThemesMixFixTitle);
        }

        y += caption + UiMetrics.Px(8f);
        var center = new Vector2(MathF.Round(min.X + pad + (medal * 0.5f)), MathF.Round(y + (medal * 0.5f)));
        using (GlyphSeam.PushAppearance(MixLook(fix.From)))
        {
            GlyphSeam.Draw(dl, center, RadiusForBox(medal), fix.State, 0);
        }

        var arrowX = center.X + (medal * 0.5f) + UiMetrics.Px(5f);
        var arrowW = UiMetrics.Px(10f);
        dl.AddLine(new Vector2(arrowX, center.Y), new Vector2(arrowX + arrowW, center.Y), Theme.U32(s.TextTertiary), UiMetrics.Hairline);
        dl.AddLine(new Vector2(arrowX + arrowW - UiMetrics.Px(3f), center.Y - UiMetrics.Px(3f)), new Vector2(arrowX + arrowW, center.Y), Theme.U32(s.TextTertiary), UiMetrics.Hairline);
        dl.AddLine(new Vector2(arrowX + arrowW - UiMetrics.Px(3f), center.Y + UiMetrics.Px(3f)), new Vector2(arrowX + arrowW, center.Y), Theme.U32(s.TextTertiary), UiMetrics.Hairline);
        using (GlyphSeam.PushAppearance(MixLook(fix.To)))
        {
            GlyphSeam.Draw(dl, center + new Vector2(medal + UiMetrics.Px(20f), 0f), RadiusForBox(medal), fix.State, 0);
        }

        var tx = min.X + pad + textX;
        TextFlow.DrawClamped(dl, new Vector2(tx, y), mixFixAction, textWidth, MaxRowLines, Theme.U32(s.Text));
        using (Typography.Caption())
        {
            TextFlow.DrawClamped(dl, new Vector2(tx, y + actionHeight + UiMetrics.Px(2f)), mixFixResult, textWidth, MaxRowLines, Theme.U32(s.TextSecondary));
        }

        // Not now and Use it, at the card's right.
        y += body + UiMetrics.Px(10f);
        var padX = ImGui.GetStyle().FramePadding.X * 2f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var useWidth = ImGui.CalcTextSize(Strings.ThemesMixFixUse).X + padX;
        var notNowWidth = ImGui.CalcTextSize(Strings.ThemesMixFixNotNow).X + padX;
        ImGui.SetCursorScreenPos(new Vector2(max.X - pad - useWidth - spacing - notNowWidth, y));
        if (ImGui.Button(Strings.ThemesMixFixNotNow, new Vector2(notNowWidth, 0f)))
        {
            mixFixOpen = false;
        }

        ImGui.SameLine(0f, spacing);
        if (ImGui.Button(Strings.ThemesMixFixUse, new Vector2(useWidth, 0f)))
        {
            var before = saved.Clone();
            AppearanceEdits.SetGlyph(saved, fix.State, GlyphSets.Get(fix.To));
            Save();
            mixFixOpen = false;
            if (SafetyRules.OffersUndo(GuardedAction.ApplyFix))
            {
                UndoToast.Show(MixPickToast(resolved, fix.State, fix.To), () => RestoreAppearance(before));
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X - (avail - width), min.Y));
        ImGui.Dummy(new Vector2(avail, height));
    }

    /// <summary>A pick from a state's list: applied at once, with Undo ("Ready: Aether Crystal"). Fix it keeps this state from now on.</summary>
    private void PickMixSet(AppearanceConfig saved, QuestState state, GlyphSetInfo? set)
    {
        var before = saved.Clone();
        AppearanceEdits.SetGlyph(saved, state, set);
        mixKeep = state;
        if (before.SameAs(saved))
        {
            return;
        }

        Save();
        var resolved = AppearanceResolver.Resolve(before);
        UndoToast.Show(set is null ? string.Format(CultureInfo.CurrentCulture, Strings.UndoToastMixPickFormat, Strings.StateName(state), Strings.ThemesMixFromTheme) : MixPickToast(resolved, state, set.Id), () => RestoreAppearance(before));
    }

    private static string MixPickToast(ResolvedAppearance resolved, QuestState state, GlyphSetId set) =>
        string.Format(CultureInfo.CurrentCulture, Strings.UndoToastMixPickFormat, Strings.StateName(state), set == resolved.Theme.Glyphs ? Strings.ThemesMixFromTheme : ShareSetName(set));

    /// <summary>The set <paramref name="state"/>'s pick names in the saved look; null for "From theme".</summary>
    private static GlyphSetId? PickedSet(AppearanceConfig saved, QuestState state) =>
        ShareCode.OwnPick(saved, state) is var set && set != 0 ? set : null;

    /// <summary>The keyline radius that makes a medal's box <paramref name="box"/> px (<see cref="MedalGlyph.Box"/>).</summary>
    private static float RadiusForBox(float box) => box * MedalArt.KeylineRadius / 128f;

    /// <summary>The section's amber, held to 4.5 : 1 on <paramref name="ground"/>.</summary>
    private static Vector4 MixAmber(Vector4 ground) => ColorMath.EnsureContrast(ShareAmber, Theme.Surface.Text, ground, SurfaceColors.TextMinContrast);

    /// <summary>
    /// The amber of a list option's note, held to 4.5 : 1 on every ground the option takes (Theme.PushPopup): the popup's
    /// window, hovered, selected and pressed.
    /// </summary>
    private static Vector4 MixListAmber()
    {
        var s = Theme.Surface;
        var window = s.Window with { W = 1f };
        ReadOnlySpan<Vector4> grounds =
        [
            window,
            ColorMath.Over(s.Hover, window),
            ColorMath.Over(Theme.SelectionWash, window),
            ColorMath.Over(Theme.SelectionWashActive, window),
        ];
        return ColorMath.EnsureContrast(ShareAmber, s.Text, grounds, SurfaceColors.TextMinContrast);
    }

    /// <summary>What a settings card shows under its rows: the Raised surface at .55 over the window (CloseCard paints it so).</summary>
    private static Vector4 CardGround() => ColorMath.Over(Theme.Surface.Raised with { W = 0.55f }, Theme.Surface.Window with { W = 1f });

    /// <summary>
    /// A look that draws every state from <paramref name="set"/>, in the saved look's theme, palette and frames (one per
    /// set, built from the saved look and rebuilt when it changes, so a frames pick or a theme card shows at once).
    /// </summary>
    private ResolvedAppearance MixLook(GlyphSetId set) => mixLooks.For(settings.Appearance, set);

    /// <summary>Judges the saved mix and builds every text the section shows, when the appearance, the pick to keep or the language changed.</summary>
    private MixVerdict RefreshMix(ResolvedAppearance resolved)
    {
        if (ReferenceEquals(resolved, mixFor) && mixLanguage == Loc.Version && mixKeepFor == mixKeep && mixVerdict is { } cached)
        {
            return cached;
        }

        if (!ReferenceEquals(resolved, mixFor))
        {
            mixFixOpen = false;
        }

        mixFor = resolved;
        mixLanguage = Loc.Version;
        mixKeepFor = mixKeep;
        var verdict = MixRules.Evaluate(MixRules.Column(resolved));
        mixVerdict = verdict;
        mixFix = MixRules.Fix(verdict, resolved.Theme.Glyphs, mixKeep);

        // Each row's note, and what each option of each list would add.
        var states = AppearanceStates.All;
        var choices = MixRules.Choices;
        if (mixOptionNotes.Length != states.Count || mixOptionNotes[0].Length != choices.Count + 1)
        {
            mixOptionNotes = new string[states.Count][];
            for (var i = 0; i < states.Count; i++)
            {
                mixOptionNotes[i] = new string[choices.Count + 1];
            }
        }

        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            mixRowNotes[i] = verdict.NoteFor(state) is { } note ? RowNote(note, state) : string.Empty;
            for (var o = 0; o <= choices.Count; o++)
            {
                var set = o == 0 ? resolved.Theme.Glyphs : choices[o - 1].Id;
                var added = set == verdict.SetFor(state) ? null : MixRules.Added(verdict, MixRules.WhatIf(verdict.Column, state, set));
                mixOptionNotes[i][o] = added is { } a ? OptionNote(a, state) : string.Empty;
            }
        }

        mixStatus = MixStatus(verdict);
        mixDetails = MixDetails(verdict);
        mixFixNone = mixKeep is { } keep ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixFixNoneKeepFormat, Strings.StateName(keep)) : Strings.ThemesMixFixNone;
        if (mixFix is { } fix)
        {
            var already = false;
            foreach (var set in verdict.Column)
            {
                already |= set == fix.To;
            }

            mixFixAction = !fix.ClearsAll
                ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixFixThemeOwnFormat, Strings.StateName(fix.State))
                : string.Format(CultureInfo.CurrentCulture, already ? Strings.ThemesMixFixUseTooFormat : Strings.ThemesMixFixUseFormat, ShareSetName(fix.To), Strings.StateName(fix.State));
            var leadWarned = false;
            foreach (var w in verdict.Warnings)
            {
                leadWarned |= !w.IsPair;
            }

            var result = !fix.ClearsAll
                ? Strings.ThemesMixFixPartial
                : leadWarned
                    ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixFixLeadFormat, Ratio(fix.After.Lead(MixTier.Hero)), Ratio(fix.After.Lead(MixTier.Row)))
                    : Strings.ThemesMixFixApart;
            mixFixResult = mixKeep is { } kept
                ? result + " " + string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixFixKeepFormat, Strings.StateName(kept))
                : result;
        }

        return verdict;
    }

    private static string Ratio(float value) => string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixRatioFormat, value);

    /// <summary>The status line's sentence.</summary>
    private static string MixStatus(MixVerdict verdict)
    {
        if (!verdict.Measured)
        {
            foreach (var set in verdict.Column)
            {
                if (!MixTable.IsMeasured(set))
                {
                    return string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixUnmeasuredFormat, ShareSetName(set));
                }
            }
        }

        if (!verdict.Mixed)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixOneSetFormat, ShareSetName(verdict.Column[0]));
        }

        if (verdict.Ok)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixOkFormat, Ratio(verdict.LeastLead));
        }

        var w = verdict.Warnings[0];
        var first = w.Kind switch
        {
            MixWarningKind.Hard or MixWarningKind.Close => string.Format(
                CultureInfo.CurrentCulture,
                w.Kind == MixWarningKind.Hard ? Strings.ThemesMixHardFormat : Strings.ThemesMixCloseFormat,
                Strings.StateName(w.A),
                ShareSetName(verdict.SetFor(w.A)),
                Strings.StateName(w.B),
                ShareSetName(verdict.SetFor(w.B))),
            MixWarningKind.ReadyLead => w.Tier == MixTier.Hero ? Strings.ThemesMixLeadLarge : Strings.ThemesMixLeadRow,
            _ => w.Tier == MixTier.Hero ? Strings.ThemesMixCompletedLarge : Strings.ThemesMixCompletedRow,
        };

        // Ready's lead and Completed's share are one complaint: Details tells both.
        var more = 0;
        for (var i = 1; i < verdict.Warnings.Count; i++)
        {
            more += verdict.Warnings[i].Kind == MixWarningKind.CompletedRecedes && w.Kind == MixWarningKind.ReadyLead ? 0 : 1;
        }

        return more == 0 ? first : string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixMoreFormat, first, more);
    }

    /// <summary>Details: one sentence per pair, then Ready's lead and Completed's share at each tier that misses, and where Ready still leads.</summary>
    private static string MixDetails(MixVerdict verdict)
    {
        var parts = new List<string>();
        var leadMissed = new bool[2];
        var completedMissed = new bool[2];
        foreach (var w in verdict.Warnings)
        {
            if (w.IsPair)
            {
                parts.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ThemesMixDetailPairFormat,
                    Strings.StateName(w.A),
                    ShareSetName(verdict.SetFor(w.A)),
                    Strings.StateName(w.B),
                    ShareSetName(verdict.SetFor(w.B)),
                    w.Value.ToString("0.0", CultureInfo.CurrentCulture),
                    ModeName(w.Mode),
                    MixTable.CloseBar(w.Mode).ToString("0", CultureInfo.CurrentCulture)));
            }
        }

        foreach (var tier in (ReadOnlySpan<MixTier>)[MixTier.Hero, MixTier.Row])
        {
            var t = (int)tier;
            leadMissed[t] = MixTable.RatioUnder(verdict.Lead(tier), MixTable.ReadyLeadBar);
            completedMissed[t] = MixTable.RatioOver(verdict.CompletedOfReady(tier), MixTable.CompletedOfReadyBar);
        }

        var anyRatio = leadMissed[0] || leadMissed[1] || completedMissed[0] || completedMissed[1];
        foreach (var tier in (ReadOnlySpan<MixTier>)[MixTier.Hero, MixTier.Row])
        {
            var t = (int)tier;
            var where = tier == MixTier.Hero ? Strings.ThemesMixTierLarge : Strings.ThemesMixTierRow;
            if (leadMissed[t])
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixDetailLeadFormat, where, ShareSetName(verdict.SetFor(QuestState.Ready)), Ratio(verdict.Lead(tier))));
            }

            if (completedMissed[t])
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixDetailCompletedFormat, where, ShareSetName(verdict.SetFor(QuestState.Completed)), Ratio(verdict.CompletedOfReady(tier))));
            }

            if (anyRatio && !leadMissed[t] && !completedMissed[t])
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixDetailStillLeadsFormat, where, Ratio(verdict.Lead(tier))));
            }
        }

        return string.Join("\n", parts);
    }

    /// <summary>A row's note for <paramref name="w"/>, from <paramref name="state"/>'s side.</summary>
    private static string RowNote(MixWarning w, QuestState state) => w.Kind switch
    {
        MixWarningKind.Hard => string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixNoteHardFormat, Strings.StateName(w.A == state ? w.B : w.A)),
        MixWarningKind.Close => string.Format(CultureInfo.CurrentCulture, Strings.ThemesMixNoteCloseFormat, Strings.StateName(w.A == state ? w.B : w.A)),
        MixWarningKind.ReadyLead => string.Format(CultureInfo.CurrentCulture, w.Tier == MixTier.Hero ? Strings.ThemesMixNoteQuieterLargeFormat : Strings.ThemesMixNoteQuieterRowFormat, Strings.StateName(w.B)),
        _ => w.Tier == MixTier.Hero ? Strings.ThemesMixNoteLoudLarge : Strings.ThemesMixNoteLoudRow,
    };

    /// <summary>What an option of <paramref name="state"/>'s list would add, in words.</summary>
    private static string OptionNote(MixWarning w, QuestState state) => w.Kind switch
    {
        MixWarningKind.Hard or MixWarningKind.Close => RowNote(w, w.A == state || w.B == state ? state : w.A),
        MixWarningKind.ReadyLead => w.Tier == MixTier.Hero ? Strings.ThemesMixOptionLeadLarge : Strings.ThemesMixOptionLeadRow,
        _ => w.Tier == MixTier.Hero ? Strings.ThemesMixOptionLoudLarge : Strings.ThemesMixOptionLoudRow,
    };

    /// <summary>A vision mode inside a sentence.</summary>
    private static string ModeName(VisionMode mode) => mode switch
    {
        VisionMode.Deut => Strings.ThemesMixModeDeut,
        VisionMode.MachadoDeut => Strings.ThemesMixModeMachadoDeut,
        VisionMode.MachadoProt => Strings.ThemesMixModeMachadoProt,
        VisionMode.MachadoTrit => Strings.ThemesMixModeMachadoTrit,
        _ => Strings.ThemesMixModeGrey,
    };

    /// <summary>
    /// Settings › Themes › Reset appearance while a mix is set (spec-1.17 §A5): it discards several picks, so it is held
    /// like Reset mix (<see cref="GuardedAction.ResetAppearanceWithMix"/>); Undo follows. Returns true when confirmed.
    /// </summary>
    private bool HoldResetAppearance()
    {
        var label = resetAppearanceHoldLabelText.Value;
        if (!Setting(Strings.ThemesReset, Strings.ThemesResetHint, "reset appearance default theme restore look", Chrome.HoldButtonWidth(label)))
        {
            return false;
        }

        var confirmed = Chrome.HoldButton(label, resetAppearanceGate);
        if (ImGui.IsItemHovered())
        {
            Safety.Tooltip(Strings.ThemesResetHint, GuardedAction.ResetAppearanceWithMix);
        }

        EndSetting();
        return confirmed;
    }
}
