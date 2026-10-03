using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Localization;
using Tsukimichi.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Themes › Share (1.17, plan v7 T12; docs/design/v7/ui/spec-1.17.md §C2, <c>share.png</c>):
/// <list type="bullet">
/// <item><b>This look's code</b>: the saved appearance's share code (<see cref="ShareCode"/>), selectable, and Copy,
/// after which the button reads "Copied" and the status line "Code copied · TM1-…" for a moment.</item>
/// <item><b>Paste a code</b>: read as it is typed (<see cref="SharePreview"/>). A code that reads shows a preview card
/// before anything changes: every change in words, five sample medals at 36 px drawn in the code's look
/// (<see cref="GlyphSeam.PushAppearance"/>), what this version leaves out in amber words, and Cancel and Apply ("Apply the
/// rest" when something is left out). Apply saves the look at once with an Undo (<see cref="GuardedAction.ApplyShareCode"/>).
/// The player's own high contrast is kept, and the medals are drawn with it, whatever the code carries.
/// A code that does not read says so in Locked out's ink and changes nothing.</item>
/// </list>
/// The status line under the field is always there, so a note or a short error never moves what follows; the preview
/// card opens below the field only when a code is pasted. <c>/tsuki look &lt;code&gt;</c> opens this block with the code
/// pasted (<see cref="OpenShareCode"/>); it never applies on its own.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string ThemeShareKeywords = "share sharing code codes copy paste look looks friend chat send tm1 import export";

    // Spec-1.17 §C2, in logical px.
    private const float SharePreviewWidthLogical = 460f;
    private const float ShareMedalLogical = 36f;
    private const float SharePadLogical = 12f;

    /// <summary>How long Copy reads "Copied" and the status line says so.</summary>
    private const double ShareCopiedSeconds = 3.0;

    /// <summary>The preview's five sample medals (spec §C2).</summary>
    private static readonly QuestState[] ShareSampleStates =
    [
        QuestState.Ready, QuestState.Accepted, QuestState.Blocked, QuestState.Completed, QuestState.Unknown,
    ];

    /// <summary>The quiet amber of a warning in words (spec-1.17 §A2, #C9A866), held to 4.5 : 1 on the window it is drawn on.</summary>
    private static readonly Vector4 ShareAmber = new(0xC9 / 255f, 0xA8 / 255f, 0x66 / 255f, 1f);

    // The paste field's text, and the preview read from it against the saved appearance it was read with.
    private string shareText = string.Empty;
    private string? shareReadText;
    private AppearanceConfig? shareReadAgainst;
    private SharePreview? sharePreview;
    private ResolvedAppearance? sharePreviewLook;
    private string[] sharePreviewLines = [];
    private string? shareLeftOutLine;
    private int sharePreviewLanguage = -1;
    private bool shareFocus;

    // The saved look's code, rebuilt when the saved look changes, and when Copy was last pressed.
    private string shareCode = string.Empty;
    private AppearanceConfig? shareCodeOf;
    private string shareCopiedNote = string.Empty;
    private double shareCopiedUntil = double.NegativeInfinity;

    /// <summary>
    /// <c>/tsuki look &lt;code&gt;</c>: opens Settings › Themes on the Share block with <paramref name="text"/> pasted, so
    /// its preview shows; with no text the field takes the keyboard. Nothing is applied until Apply.
    /// </summary>
    public void OpenShareCode(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        shareText = trimmed.Length > ShareCode.MaxTextLength ? trimmed[..ShareCode.MaxTextLength] : trimmed;
        shareFocus = shareText.Length == 0;
        OpenAt(SettingsSection.Themes, SettingsAnchor.ThemeShare);
    }

    /// <summary>Settings › Themes › Share: the look's code with Copy, and the paste field with its preview.</summary>
    private void DrawThemeShare()
    {
        Header(Strings.ThemesHeadingShare);
        var saved = settings.Appearance;
        DrawShareCodeRow(saved);
        DrawSharePasteRow(saved);
    }

    /// <summary>The look's code, selectable, and Copy.</summary>
    private void DrawShareCodeRow(AppearanceConfig saved)
    {
        if (shareCodeOf is null || !shareCodeOf.SameAs(saved))
        {
            shareCode = ShareCode.Encode(saved);
            shareCodeOf = saved.Clone();
        }

        var padX = ImGui.GetStyle().FramePadding.X * 2f;
        var codeWidth = ImGui.CalcTextSize("TM1-WWWW-WWWW-WWW").X + padX;
        var copyWidth = MathF.Max(ImGui.CalcTextSize(Strings.ThemesShareCopy).X, ImGui.CalcTextSize(Strings.ThemesShareCopied).X) + padX;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        if (!Setting(Strings.ThemesShareCode, Strings.ThemesShareCodeHint, ThemeShareKeywords, codeWidth + gap + copyWidth))
        {
            return;
        }

        // Read-only, so the code can be selected and copied by hand too.
        var code = shareCode;
        ImGui.SetNextItemWidth(MathF.Max(1f, ControlWidth - gap - copyWidth));
        ImGui.InputText("##shareCode", ref code, ShareCode.MaxTextLength, ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine(0f, gap);
        var copied = ImGui.GetTime() < shareCopiedUntil;
        if (ImGui.Button(copied ? Strings.ThemesShareCopied : Strings.ThemesShareCopy, new Vector2(copyWidth, 0f)))
        {
            ImGui.SetClipboardText(shareCode);
            shareCopiedNote = string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareCopiedFormat, shareCode);
            shareCopiedUntil = ImGui.GetTime() + ShareCopiedSeconds;
        }

        EndSetting();
    }

    /// <summary>The paste field, its status line, and the preview card while a pasted code would change the look.</summary>
    private void DrawSharePasteRow(AppearanceConfig saved)
    {
        if (!Setting(Strings.ThemesSharePaste, Strings.ThemesSharePasteHint, ThemeShareKeywords))
        {
            return;
        }

        if (shareFocus)
        {
            ImGui.SetKeyboardFocusHere();
            shareFocus = false;
        }

        ImGui.SetNextItemWidth(ControlWidth);
        ImGui.InputTextWithHint("##sharePaste", Strings.ThemesSharePastePlaceholder, ref shareText, ShareCode.MaxTextLength);
        var editing = ImGui.IsItemActive();

        var preview = SharePreviewFor(saved);
        var verdict = preview.Verdict(editing);

        // The status line: a typo, a newer format, the same look, or the copy note; one line kept even when empty.
        SettingBelow();
        var width = MathF.Max(1f, row.Right - row.Left);
        var s = Theme.Surface;
        var (status, ink) = verdict switch
        {
            ShareVerdict.Mistyped => (Strings.ThemesShareMistyped, Theme.StateText(QuestState.Foreclosed)),
            ShareVerdict.Newer => (Strings.ThemesShareNewer, Theme.StateText(QuestState.Foreclosed)),
            ShareVerdict.NothingToChange => (Strings.ThemesShareSameLook, s.TextSecondary),
            _ when ImGui.GetTime() < shareCopiedUntil => (shareCopiedNote, s.TextSecondary),
            _ => (string.Empty, s.TextSecondary),
        };

        if (verdict == ShareVerdict.Preview)
        {
            DrawSharePreviewCard(preview, width);
        }

        using (Typography.Caption())
        {
            var at = ImGui.GetCursorScreenPos();
            var height = status.Length == 0 ? ImGui.GetTextLineHeight() : TextFlow.Height(status, width);
            if (status.Length > 0)
            {
                TextFlow.DrawClamped(ImGui.GetWindowDrawList(), at, status, width, MaxRowLines, Theme.U32(ink));
            }

            ImGui.Dummy(new Vector2(width, height));
        }

        EndSetting();
    }

    /// <summary>The preview for the field's text against <paramref name="saved"/>, read again only when either changes.</summary>
    private SharePreview SharePreviewFor(AppearanceConfig saved)
    {
        if (sharePreview is not null && string.Equals(shareReadText, shareText, StringComparison.Ordinal) && shareReadAgainst is not null && shareReadAgainst.SameAs(saved))
        {
            return sharePreview;
        }

        sharePreview = SharePreview.Of(shareText, saved);
        shareReadText = shareText;
        shareReadAgainst = saved.Clone();
        sharePreviewLook = sharePreview.Result is { } result ? AppearanceResolver.Resolve(result) : null;
        sharePreviewLanguage = -1;
        return sharePreview;
    }

    /// <summary>
    /// The preview card (spec §C2): "This code would change:", a line per change, what is left out, the five sample
    /// medals in the code's look, then Cancel and Apply. Laid out from its measured height, so it draws in one pass.
    /// </summary>
    private void DrawSharePreviewCard(SharePreview preview, float available)
    {
        RefreshSharePreviewLines(preview);
        var look = sharePreviewLook ?? GlyphSeam.Appearance;
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var width = MathF.Min(available, UiMetrics.Px(SharePreviewWidthLogical));
        var pad = UiMetrics.Px(SharePadLogical);
        var inner = MathF.Max(1f, width - (pad * 2f));
        var bulletIndent = UiMetrics.Px(14f);
        var gap = UiMetrics.Px(10f);
        var medal = UiMetrics.Px(ShareMedalLogical);
        var paneHeight = medal + UiMetrics.Px(12f);
        var buttonHeight = ImGui.GetFrameHeight();
        var line = ImGui.GetTextLineHeight();

        // Measure.
        var title = Strings.ThemesShareWouldChange;
        var height = pad + TextFlow.Height(title, inner) + UiMetrics.Px(4f);
        foreach (var text in sharePreviewLines)
        {
            height += TextFlow.Height(text, inner - bulletIndent) + UiMetrics.Px(2f);
        }

        if (shareLeftOutLine is { } leftOut)
        {
            height += gap + TextFlow.Height(leftOut, inner);
        }

        height += gap + paneHeight + gap + buttonHeight + pad;

        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var rounding = UiMetrics.Px(6f);
        dl.AddRectFilled(min, max, Theme.U32(s.Sunken), rounding);
        dl.AddRect(min, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);

        // The words.
        var x = min.X + pad;
        var y = min.Y + pad;
        TextFlow.DrawClamped(dl, new Vector2(x, y), title, inner, MaxRowLines, Theme.U32(s.Text));
        y += TextFlow.Height(title, inner) + UiMetrics.Px(4f);
        var dot = MathF.Max(1.5f, UiMetrics.Px(2f));
        foreach (var text in sharePreviewLines)
        {
            dl.AddCircleFilled(new Vector2(x + (bulletIndent * 0.4f), y + (line * 0.5f)), dot, Theme.U32(s.TextTertiary), 8);
            TextFlow.DrawClamped(dl, new Vector2(x + bulletIndent, y), text, inner - bulletIndent, MaxRowLines, Theme.U32(s.Text));
            y += TextFlow.Height(text, inner - bulletIndent) + UiMetrics.Px(2f);
        }

        if (shareLeftOutLine is { } note)
        {
            y += gap;
            var amber = ColorMath.EnsureContrast(ShareAmber, s.Text, s.Sunken, SurfaceColors.TextMinContrast);
            TextFlow.DrawClamped(dl, new Vector2(x, y), note, inner, MaxRowLines, Theme.U32(amber));
            y += TextFlow.Height(note, inner);
        }

        // The five sample medals on the code's own window, drawn by its appearance and palette.
        y += gap;
        var paneMin = new Vector2(x, y);
        var paneWidth = MathF.Min(inner, (ShareSampleStates.Length * (medal + UiMetrics.Px(10f))) + UiMetrics.Px(10f));
        var panePalette = PaletteFor(look.Palette, look.HighContrast);
        dl.AddRectFilled(paneMin, paneMin + new Vector2(paneWidth, paneHeight), Theme.U32(panePalette.Surface.Window with { W = 1f }), UiMetrics.Px(4f));
        using (GlyphSeam.PushAppearance(look))
        using (Theme.PushPalette(PaletteFor(look.Palette, highContrast: false), look.GlyphPalette))
        {
            var step = (paneWidth - UiMetrics.Px(10f)) / ShareSampleStates.Length;
            for (var i = 0; i < ShareSampleStates.Length; i++)
            {
                var center = new Vector2(MathF.Round(paneMin.X + UiMetrics.Px(5f) + ((i + 0.5f) * step)), MathF.Round(paneMin.Y + (paneHeight * 0.5f)));
                GlyphSeam.Draw(dl, center, medal * MoonGlyph.InlineRadiusFraction, ShareSampleStates[i], 0);
            }
        }

        // Cancel and Apply, at the card's right.
        y += paneHeight + gap;
        var padX = ImGui.GetStyle().FramePadding.X * 2f;
        var applyLabel = preview.LeftOut.Count > 0 ? Strings.ThemesShareApplyRest : Strings.ThemesShareApply;
        var applyWidth = ImGui.CalcTextSize(applyLabel).X + padX;
        var cancelWidth = ImGui.CalcTextSize(Strings.ThemesShareCancel).X + padX;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorScreenPos(new Vector2(max.X - pad - applyWidth - spacing - cancelWidth, y));
        if (ImGui.Button(Strings.ThemesShareCancel, new Vector2(cancelWidth, 0f)))
        {
            ClearShareText();
        }

        ImGui.SameLine(0f, spacing);
        if (ImGui.Button(applyLabel, new Vector2(applyWidth, 0f)))
        {
            ApplySharePreview(preview);
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(width, UiMetrics.Px(2f)));
    }

    /// <summary>Apply: saves the code's look at once, clears the field, and offers Undo ("Look applied · Undo").</summary>
    private void ApplySharePreview(SharePreview preview)
    {
        if (preview.Result is not { } result)
        {
            return;
        }

        var before = settings.Appearance.Clone();
        settings.Appearance = result.Clone();
        Save();
        ClearShareText();
        if (SafetyRules.OffersUndo(GuardedAction.ApplyShareCode))
        {
            UndoToast.Show(Strings.UndoToastLookApplied, () => RestoreAppearance(before));
        }
    }

    private void ClearShareText()
    {
        shareText = string.Empty;
        sharePreview = null;
        sharePreviewLook = null;
    }

    /// <summary>The preview's lines in words, built once per preview (and again after a language change).</summary>
    private void RefreshSharePreviewLines(SharePreview preview)
    {
        if (sharePreviewLanguage == Loc.Version)
        {
            return;
        }

        sharePreviewLanguage = Loc.Version;
        var lines = new string[preview.Changes.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = ShareChangeLine(preview.Changes[i]);
        }

        sharePreviewLines = lines;
        shareLeftOutLine = preview.LeftOut.Count switch
        {
            0 => null,
            1 => string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareLeftOutOneFormat, LeftOutItem(preview.LeftOut[0])),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareLeftOutManyFormat, preview.LeftOut.Count, LeftOutItems(preview.LeftOut)),
        };
    }

    /// <summary>One change in words: "Theme Menphina's Medallion → Ishgard Glass", "Ready from Aether Crystal", "Completed from theme".</summary>
    private static string ShareChangeLine(ShareChange change) => change.Kind switch
    {
        ShareChangeKind.Theme => Arrow(Strings.ThemesShareTheme, ThemeName((ThemeId)change.From), ThemeName((ThemeId)change.To)),
        ShareChangeKind.Palette => Arrow(Strings.ThemesSharePalette, PaletteName((PaletteId)change.From), PaletteName((PaletteId)change.To)),
        ShareChangeKind.Frames => Arrow(Strings.ThemesShareFrames, KitName((FrameKitId)change.From), KitName((FrameKitId)change.To)),
        _ => change.To == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesSharePickFromThemeFormat, Strings.StateName(change.State))
            : string.Format(CultureInfo.CurrentCulture, Strings.ThemesSharePickFormat, Strings.StateName(change.State), ShareSetName((GlyphSetId)change.To)),
    };

    private static string Arrow(string label, string from, string to) =>
        string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareChangeFormat, label, from, to);

    /// <summary>A glyph set's name: the theme of the same number's (the two tables share their numbers; ThemeRegistryTests pins both).</summary>
    private static string ShareSetName(GlyphSetId id) => ThemeName((ThemeId)(byte)id);

    private static string LeftOutItems(IReadOnlyList<ShareCodeOmission> leftOut)
    {
        var items = new string[leftOut.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = LeftOutItem(leftOut[i]);
        }

        return string.Join(Strings.ThemesShareListSeparator, items);
    }

    /// <summary>One left-out choice: "Ready: a set this version doesn't have", "Theme: Astrologian's Orrery, not offered yet".</summary>
    private static string LeftOutItem(ShareCodeOmission omission)
    {
        var (field, what) = omission.Field switch
        {
            ShareCodeField.Theme => (Strings.ThemesShareTheme, omission.Registered ? NotOffered(ThemeName((ThemeId)omission.Id)) : Strings.ThemesShareUnknownTheme),
            ShareCodeField.Palette => (Strings.ThemesSharePalette, omission.Registered && ShareCode.TryPaletteFromWire(omission.Id, out var palette) ? NotOffered(PaletteName(palette)) : Strings.ThemesShareUnknownPalette),
            ShareCodeField.Frames => (Strings.ThemesShareFrames, omission.Registered ? NotOffered(KitName((FrameKitId)omission.Id)) : Strings.ThemesShareUnknownFrames),
            _ => (Strings.StateName(omission.State), !omission.Registered
                ? Strings.ThemesShareUnknownSet
                : GlyphSets.Get((GlyphSetId)omission.Id).Mixable
                    ? NotOffered(ShareSetName((GlyphSetId)omission.Id))
                    : string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareWholeThemeFormat, ShareSetName((GlyphSetId)omission.Id))),
        };

        return string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareLeftOutItemFormat, field, what);
    }

    private static string NotOffered(string name) => string.Format(CultureInfo.CurrentCulture, Strings.ThemesShareNotOfferedFormat, name);
}
