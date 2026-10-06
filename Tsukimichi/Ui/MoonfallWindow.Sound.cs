using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's sound hooks (plan v9 G8): the window hands the audio (<see cref="MoonfallAudio"/>) the events it reads,
/// the shot, its buttons and the pause, and keeps the Sound setting (the pause menu's and Options' stepper). Sound never
/// feeds the engine. Without <see cref="Audio"/> (no output set up) everything here does nothing.
/// </summary>
public sealed partial class MoonfallWindow
{
    private string soundValueText = string.Empty;
    private int soundValueFor = -1;
    private int soundLanguageFor = -1;

    /// <summary>The sound; set by the plugin (Plugin.Moonfall.cs), null for none.</summary>
    internal MoonfallAudio? Audio { get; set; }

    private void SoundOpen() => Audio?.Open();

    private void SoundClose() => Audio?.Close();

    /// <summary>Every frame the window draws: the level, and silence while the board is paused.</summary>
    private void SoundFrame(bool paused) => Audio?.Update(paused, ImGui.GetIO().DeltaTime);

    /// <summary>
    /// A frame the window did not draw (collapsed): the sound holds as on a paused board, every voice in its place, and
    /// the board waits for the player when it shows again.
    /// </summary>
    private void HoldWhileHidden()
    {
        Audio?.Update(true, ImGui.GetIO().DeltaTime);
        if (flow.Current == MoonfallScreen.Play)
        {
            pause.Pause(MoonfallPauseReason.Unfocused);
        }
    }

    private void SoundEvent(in MoonfallEvent e) => Audio?.Event(e);

    /// <summary>After the frame's events: its chime, and the count-up's tick.</summary>
    private void SoundFlush(MoonfallGame g) => Audio?.Flush(g.ShownScore, g.Score, ImGui.GetIO().DeltaTime, pause.Paused);

    private void SoundShot() => Audio?.Shot();

    private void SoundClick() => Audio?.Click();

    private void SoundNewLevel() => Audio?.NewLevel();

    /// <summary>Whether there is a sound setting to show (Moonfall's options; the audio reads the same setting).</summary>
    private bool HasSound => options is not null;

    /// <summary>The Sound setting, 0 (off) to 100.</summary>
    private int SoundPercent => options?.SoundPercent ?? 0;

    /// <summary>The Sound setting a step quieter (<paramref name="direction"/> −1) or louder (+1); the sound follows it on its next frame.</summary>
    private void SoundStep(int direction)
    {
        if (options is null)
        {
            return;
        }

        options.SoundPercent = Math.Clamp(options.SoundPercent + (direction * MoonfallAudio.PercentStep), 0, 100);
        options.Save();
        Audio?.Click();
    }

    /// <summary>"70%", or "Off" at 0; made when it changes.</summary>
    private string SoundValueText()
    {
        var percent = SoundPercent;
        if (percent != soundValueFor || soundLanguageFor != Localization.Loc.Version)
        {
            soundValueFor = percent;
            soundLanguageFor = Localization.Loc.Version;
            soundValueText = percent == 0 ? Strings.MoonfallSoundOff : string.Format(CultureInfo.CurrentCulture, Strings.MoonfallSoundPercentFormat, percent);
        }

        return soundValueText;
    }
}
