using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Updates;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › About › Updates (plan v8 U1; spec-1.22 W3 item 2, about-history-1.22.png): the status line ("Up to date ·
/// Dalamud last looked 6 min ago", or "Tsukimichi 1.23.0 is ready · Dalamud has it" with What's in it and Update), "Tell
/// me when a new version is ready" (on by default) and "Also say it in chat" (off, decision 6). A dismissed version
/// still shows here with Update. Saved at once.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string UpdatesKeywords = "update updates new version ready dalamud installer check chat changelog";

    /// <summary>The update watcher (U1); set by the plugin. Null hides the block.</summary>
    public UpdateWatcher? Updates { get; set; }

    private bool updateNotesOpen;

    // The ready line and the plain notes, built when the version, the notes or the language change; the labels once per language.
    private (string? Version, string? Notes, int Language) updateTextKey;
    private string updateReadyLine = string.Empty;
    private string updatePlainNotes = string.Empty;
    private readonly LocText updateButtonLabel = new(static () => Strings.UpdateButton + "##aboutUpdate");
    private readonly LocText updateNotesLabel = new(static () => Strings.UpdateWhatsInIt + "##aboutNotes");
    private readonly LocText updateNotesOpenLabel = new(static () => Strings.UpdateWhatsInItOpen + "##aboutNotes");

    private void DrawUpdates()
    {
        if (Updates is not { } updates)
        {
            return;
        }

        Header(Strings.SettingsUpdatesHeading);
        var state = updates.Current;
        if (state is { HasUpdate: true, Available: { } version })
        {
            RefreshUpdateTexts(version, state.Notes);
            if (Setting(updateReadyLine, Strings.UpdateSettingsReadyHint, UpdatesKeywords, ImGui.CalcTextSize(Strings.UpdateButton).X + (ImGui.GetStyle().FramePadding.X * 2f)))
            {
                if (ImGui.Button(updateButtonLabel.Value))
                {
                    updates.OpenInstaller();
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.UpdateButtonTooltip);
                }

                SettingBelow();
                if (ImGui.SmallButton(updateNotesOpen ? updateNotesOpenLabel.Value : updateNotesLabel.Value))
                {
                    updateNotesOpen = !updateNotesOpen;
                }

                if (updateNotesOpen)
                {
                    using (Typography.Caption())
                    using (Theme.PushText(Theme.Surface.TextSecondary))
                    {
                        ImGui.TextWrapped(updatePlainNotes);
                        ImGui.TextWrapped(Strings.UpdateHoverFoot);
                    }
                }

                EndSetting();
            }
        }
        else
        {
            Note(UpToDateLine(updates), null, UpdatesKeywords);
        }

        var check = settings.UpdateCheck;
        if (Toggle(Strings.UpdateCheckLabel, Strings.UpdateCheckHint, ref check, UpdatesKeywords + " network online"))
        {
            settings.UpdateCheck = check;
            Save();
            updates.SettingChanged();
        }

        var chat = settings.UpdateChatLine;
        if (Toggle(Strings.UpdateChatLabel, Strings.UpdateChatHint, ref chat, UpdatesKeywords + " echo", settings.UpdateCheck, sub: true, reason: Strings.UpdateChatOffReason))
        {
            settings.UpdateChatLine = chat;
            Save();
        }

        // W's switch, drawn here so the Updates block holds every update setting once (spec-1.22 W3 item 2).
        var show = settings.ShowWhatsNewAfterUpdate;
        if (Toggle(Strings.WhatsNew.ShowAfterUpdate, Strings.WhatsNew.ShowAfterUpdateHint, ref show, "whats new changelog release notes update popup"))
        {
            settings.ShowWhatsNewAfterUpdate = show;
            Save();
        }
    }

    /// <summary>"Tsukimichi 1.23.0 is ready · Dalamud has it" and the plain notes, rebuilt only when what they say changes.</summary>
    private void RefreshUpdateTexts(string version, string notes)
    {
        var key = (version, notes, Localization.Loc.Version);
        if (key == updateTextKey)
        {
            return;
        }

        updateTextKey = key;
        updateReadyLine = string.Format(CultureInfo.CurrentCulture, Strings.UpdateSettingsReadyFormat, version);
        updatePlainNotes = notes.Length > 0 ? UpdateNotes.Plain(notes) : Strings.UpdateNoNotes;
    }

    // The status line, rebuilt when the minute, the answer, the switch or the language moves.
    private (long Minute, DateTime? Asked, bool On, int Language) upToDateKey;
    private string upToDateLine = string.Empty;

    /// <summary>"Up to date · Dalamud last looked 6 min ago", "Asking Dalamud…" before the first answer, or the switch is off.</summary>
    private string UpToDateLine(UpdateWatcher updates)
    {
        var now = DateTime.UtcNow;
        var key = (now.Ticks / TimeSpan.TicksPerMinute, updates.LastAskedUtc, settings.UpdateCheck, Localization.Loc.Version);
        if (key == upToDateKey && upToDateLine.Length > 0)
        {
            return upToDateLine;
        }

        upToDateKey = key;
        upToDateLine = !settings.UpdateCheck
            ? Strings.UpdateSettingsOff
            : updates.LastAskedUtc is not { } asked
                ? Strings.UpdateSettingsWaiting
                : string.Format(CultureInfo.CurrentCulture, Strings.UpdateSettingsUpToDateFormat, UiFormat.Age(asked, now));
        return upToDateLine;
    }
}
