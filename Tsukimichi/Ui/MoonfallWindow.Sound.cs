using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's sound hooks (plan v9 G8): the window hands the audio (<see cref="MoonfallAudio"/>) the events it reads,
/// the shot, its buttons and the pause, and shows the Sound quick setting on the pause screen. Sound never feeds the
/// engine. Without <see cref="Audio"/> (no output set up) everything here does nothing.
/// </summary>
public sealed partial class MoonfallWindow
{
    private const float SoundButtonLogical = 20f;

    private Vector2 soundDownMin;
    private Vector2 soundUpMin;
    private Vector2 soundRowAt;
    private float soundButton;
    private bool soundDownHovered;
    private bool soundUpHovered;
    private bool soundShown;
    private string soundValueText = string.Empty;
    private int soundValueFor = -1;
    private int soundLanguageFor = -1;

    /// <summary>The sound; set by the plugin (Plugin.Moonfall.cs), null for none.</summary>
    internal MoonfallAudio? Audio { get; set; }

    private void SoundOpen() => Audio?.Open();

    private void SoundClose() => Audio?.Close();

    /// <summary>Every frame, after the pause is worked out: the level, and silence while paused.</summary>
    private void SoundFrame() => Audio?.Update(pause.Paused, ImGui.GetIO().DeltaTime);

    private void SoundEvent(in MoonfallEvent e) => Audio?.Event(e);

    /// <summary>After the frame's events: its chime, and the count-up's tick.</summary>
    private void SoundFlush(MoonfallGame g) => Audio?.Flush(g.ShownScore, g.Score, ImGui.GetIO().DeltaTime, pause.Paused);

    private void SoundShot() => Audio?.Shot();

    private void SoundClick() => Audio?.Click();

    private void SoundNewLevel() => Audio?.NewLevel();

    /// <summary>
    /// The Sound quick setting's buttons, under the pause screen's hint: taken before the board's own button so a click
    /// on them changes the volume and does not resume. Drawn later, over the scrim (<see cref="DrawSoundSetting"/>).
    /// </summary>
    private void SoundSettingInput(Vector2 origin, Vector2 size, bool room)
    {
        soundShown = room && Audio is not null && pause.Paused;
        if (Audio is not { } audio || !soundShown)
        {
            return;
        }

        RefreshSoundText(audio.Percent);
        soundButton = MathF.Round(UiMetrics.Px(SoundButtonLogical));
        var gap = UiMetrics.Px(10f);
        var label = ImGui.CalcTextSize(Strings.MoonfallSound).X;
        var value = MathF.Max(ImGui.CalcTextSize(Strings.MoonfallSoundOff).X, ImGui.CalcTextSize(string.Format(CultureInfo.CurrentCulture, Strings.MoonfallSoundPercentFormat, 100)).X);
        var width = label + gap + soundButton + gap + value + gap + soundButton;
        var hintY = origin.Y + (size.Y * 0.45f) + UiMetrics.Px(8f);
        var y = MathF.Round(hintY + ImGui.GetTextLineHeight() + UiMetrics.Px(24f));
        var x = MathF.Round(origin.X + ((size.X - width) * 0.5f));
        soundRowAt = new Vector2(x, y);
        soundDownMin = new Vector2(x + label + gap, y);
        soundUpMin = new Vector2(soundDownMin.X + soundButton + gap + value + gap, y);

        var percent = audio.Percent;
        ImGui.SetCursorScreenPos(soundDownMin);
        if (ImGui.InvisibleButton("##moonfallSoundDown", new Vector2(soundButton)) && percent > 0)
        {
            audio.Percent = percent - MoonfallAudio.PercentStep;
        }

        soundDownHovered = ImGui.IsItemHovered();
        if (soundDownHovered)
        {
            UiMetrics.Tooltip(Strings.MoonfallSoundQuieter, Strings.MoonfallSoundTooltip);
        }

        ImGui.SetCursorScreenPos(soundUpMin);
        if (ImGui.InvisibleButton("##moonfallSoundUp", new Vector2(soundButton)) && percent < 100)
        {
            audio.Percent = percent + MoonfallAudio.PercentStep;
        }

        soundUpHovered = ImGui.IsItemHovered();
        if (soundUpHovered)
        {
            UiMetrics.Tooltip(Strings.MoonfallSoundLouder, Strings.MoonfallSoundTooltip);
        }
    }

    /// <summary>"Sound ‹ 70% ›" over the pause scrim: the gilt chevron buttons of the pause mock, dimmed at the ends.</summary>
    private void DrawSoundSetting(ImDrawListPtr dl)
    {
        if (!soundShown || Audio is not { } audio)
        {
            return;
        }

        var percent = audio.Percent;
        RefreshSoundText(percent);
        var textY = soundRowAt.Y + ((soundButton - ImGui.GetTextLineHeight()) * 0.5f);
        dl.AddText(new Vector2(soundRowAt.X, textY), Theme.U32(Theme.Surface.TextSecondary), Strings.MoonfallSound);
        Chevron(dl, soundDownMin, soundButton, -1, percent > 0, soundDownHovered);
        Chevron(dl, soundUpMin, soundButton, 1, percent < 100, soundUpHovered);
        var valueLeft = soundDownMin.X + soundButton;
        var valueWidth = soundUpMin.X - valueLeft;
        var textWidth = ImGui.CalcTextSize(soundValueText).X;
        dl.AddText(new Vector2(MathF.Round(valueLeft + ((valueWidth - textWidth) * 0.5f)), textY), Theme.U32(percent > 0 ? Theme.Gold : Theme.Surface.TextSecondary), soundValueText);
    }

    private static void Chevron(ImDrawListPtr dl, Vector2 min, float size, int direction, bool enabled, bool hovered)
    {
        var centre = min + new Vector2(size * 0.5f);
        var ink = enabled ? (hovered ? Theme.GoldHigh : Theme.Gold) : Theme.Surface.TextSecondary;
        var alpha = enabled ? 1f : 0.45f;
        dl.AddCircle(centre, size * 0.5f, Theme.WithAlpha(ink, alpha), 24, UiMetrics.Hairline);
        var arm = size * 0.18f;
        var tip = new Vector2(centre.X + (direction * arm * 0.6f), centre.Y);
        var back = centre.X - (direction * arm * 0.6f);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.6f));
        dl.AddLine(new Vector2(back, centre.Y - arm), tip, Theme.WithAlpha(ink, alpha), thickness);
        dl.AddLine(tip, new Vector2(back, centre.Y + arm), Theme.WithAlpha(ink, alpha), thickness);
    }

    private void RefreshSoundText(int percent)
    {
        if (percent == soundValueFor && soundLanguageFor == Localization.Loc.Version)
        {
            return;
        }

        soundValueFor = percent;
        soundLanguageFor = Localization.Loc.Version;
        soundValueText = percent == 0 ? Strings.MoonfallSoundOff : string.Format(CultureInfo.CurrentCulture, Strings.MoonfallSoundPercentFormat, percent);
    }
}
