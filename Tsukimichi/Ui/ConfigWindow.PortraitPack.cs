using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › General › Look › Portrait pack (feature plan v7 F4, decision 8), under Giver portraits: what the optional
/// pack is (how many givers, its size, the GitHub release it comes from), and Download, Update or Remove. Download and
/// Update ask first, naming the size, the release and the exact address; nothing goes online before that click. Remove
/// asks too. A running download shows its progress with Cancel; each run leaves one line saying how it ended. Nothing
/// here downloads on its own: a newer pack in a plugin update is offered, never fetched.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The portrait pack service; set by the plugin. Null hides the row.</summary>
    public PortraitPackService? PortraitPack { get; set; }

    private bool openPackDownloadConfirm;
    private bool openPackRemoveConfirm;

    // The hint and the progress line, composed only when what they say changes.
    private (PortraitPackState State, PortraitPack? Installed, int Language) packHintKey;
    private string packHint = string.Empty;
    private (long Tenths, int Language) packProgressKey = (-1, -1);
    private string packProgress = string.Empty;

    /// <summary>The Portrait pack row and its confirmations.</summary>
    private void DrawPortraitPack()
    {
        if (PortraitPack is not { } service)
        {
            return;
        }

        var state = service.State;
        var phase = service.Phase;
        var hint = PackHint(service, state);
        var download = state switch
        {
            PortraitPackState.Available or PortraitPackState.Damaged when service.Offer is not null => Strings.PortraitPackDownload,
            PortraitPackState.UpdateAvailable => Strings.PortraitPackUpdate,
            _ => null,
        };
        var remove = state is PortraitPackState.Installed or PortraitPackState.UpdateAvailable or PortraitPackState.Damaged ? Strings.PortraitPackRemove : null;
        var busy = phase != PortraitPackPhase.Idle;

        var style = ImGui.GetStyle();
        float ButtonWidth(string? label) => label is null ? 0f : ImGui.CalcTextSize(label, true).X + (style.FramePadding.X * 2f);
        var controlWidth = busy
            ? ButtonWidth(Strings.PortraitPackCancel)
            : ButtonWidth(download) + ButtonWidth(remove) + (download is not null && remove is not null ? style.ItemSpacing.X : 0f);

        if (Setting(Strings.PortraitPackLabel, hint, "portrait pack download photos garland faces givers remove update github celes", controlWidth, sub: true))
        {
            if (busy)
            {
                using (ImRaii.Disabled(phase != PortraitPackPhase.Downloading))
                {
                    if (ImGui.Button(Strings.PortraitPackCancel))
                    {
                        service.Cancel();
                    }
                }

                SettingBelow();
                var (fraction, line) = phase switch
                {
                    PortraitPackPhase.Downloading => (service.Progress.Fraction, PackProgressLine(service.Progress)),
                    PortraitPackPhase.Installing => (1f, Strings.PortraitPackInstalling),
                    _ => (1f, Strings.PortraitPackRemoving),
                };
                ImGui.ProgressBar(fraction, new Vector2(-1f, 0f), line);
            }
            else
            {
                if (download is not null && ImGui.Button(download))
                {
                    // Opened outside the row, where the confirmation is drawn, so a search that hides the row keeps it.
                    openPackDownloadConfirm = true;
                }

                if (download is not null && remove is not null)
                {
                    ImGui.SameLine();
                }

                if (remove is not null)
                {
                    using (Theme.PushDestructiveButton())
                    {
                        if (ImGui.Button(remove))
                        {
                            openPackRemoveConfirm = true;
                        }
                    }
                }

                DrawPackNotes(service, state);
            }

            EndSetting();
        }

        DrawPackConfirms(service);
    }

    /// <summary>The lines under the row: the last run's result, the older-game note, "not shown", and the credit.</summary>
    private void DrawPackNotes(PortraitPackService service, PortraitPackState state)
    {
        if (service.LastFinishedUtc != default && DateTime.UtcNow - service.LastFinishedUtc < TimeSpan.FromMinutes(10))
        {
            var failed = service.LastResult != PortraitPackFailure.None;
            var line = service.LastWasRemoval
                ? failed ? Strings.PortraitPackRemoveIncomplete : Strings.PortraitPackRemoved
                : failed ? Strings.PortraitPackFailed(service.LastResult) : Strings.PortraitPackInstalled;
            SettingNote(line, failed && service.LastResult != PortraitPackFailure.Cancelled ? Theme.DangerText : null);
        }

        if (service.Installed is { } pack)
        {
            if (service.ForOlderGame)
            {
                SettingNote(string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackOlderGame, Core.Diagnostics.DataStamp.ShortGameVersion(pack.GameVersion), Core.Diagnostics.DataStamp.ShortGameVersion(service.ClientGameVersion)));
            }

            if (settings.GiverPortraits != GiverPortraitMode.GameArtAndPack)
            {
                SettingNote(Strings.PortraitPackNotInUse);
            }
        }

        if (state != PortraitPackState.NotOffered)
        {
            SettingNote(Strings.PortraitPackCredit, Theme.Surface.TextTertiary);
        }
    }

    /// <summary>The download and remove confirmations: modal, each with its own Cancel. Only their buttons act.</summary>
    private void DrawPackConfirms(PortraitPackService service)
    {
        if (openPackDownloadConfirm)
        {
            openPackDownloadConfirm = false;
            ImGui.OpenPopup(Strings.PortraitPackConfirmTitle);
        }

        using (var confirm = ImRaii.PopupModal(Strings.PortraitPackConfirmTitle, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (confirm)
            {
                UiMetrics.ApplyFontScale();
                if (service.Offer is { } offer)
                {
                    ImGui.PushTextWrapPos(UiMetrics.Px(460f));
                    ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackConfirmText, PortraitPackOffer.SizeText(offer.Size), offer.ReleaseName, offer.DownloadUri));
                    ImGui.PopTextWrapPos();
                    ImGui.Spacing();
                    if (ImGui.Button(Strings.PortraitPackConfirmButton))
                    {
                        // The one place a download starts: the player's click on this button.
                        service.StartDownload();
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();
                }

                if (ImGui.Button(Strings.ConfigCancel))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (openPackRemoveConfirm)
        {
            openPackRemoveConfirm = false;
            ImGui.OpenPopup(Strings.PortraitPackRemoveTitle);
        }

        using var remove = ImRaii.PopupModal(Strings.PortraitPackRemoveTitle, ImGuiWindowFlags.AlwaysAutoResize);
        if (!remove)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        ImGui.PushTextWrapPos(UiMetrics.Px(420f));
        ImGui.TextUnformatted(Strings.PortraitPackRemoveText);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.PortraitPackRemoveButton))
            {
                if (service.StartRemove() && settings.GiverPortraits == GiverPortraitMode.GameArtAndPack)
                {
                    settings.GiverPortraits = GiverPortraitMode.GameArt;
                    Save();
                }

                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private string PackHint(PortraitPackService service, PortraitPackState state)
    {
        var installed = service.Installed;
        var key = (state, installed, Localization.Loc.Version);
        if (key == packHintKey && packHint.Length > 0)
        {
            return packHint;
        }

        packHintKey = key;
        var offer = service.Offer;
        packHint = state switch
        {
            PortraitPackState.Available when offer is not null => string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackHintAvailable, offer.Givers.ToString("N0", CultureInfo.CurrentCulture), PortraitPackOffer.SizeText(offer.Size), offer.ReleaseName),
            PortraitPackState.Installed when installed is not null => string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackHintInstalled, installed.Givers.ToString("N0", CultureInfo.CurrentCulture), installed.Tag),
            PortraitPackState.UpdateAvailable when offer is not null => string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackHintUpdate, PortraitPackOffer.SizeText(offer.Size), offer.ReleaseName),
            PortraitPackState.Damaged => Strings.PortraitPackHintDamaged,
            _ => Strings.PortraitPackHintNotOffered,
        };
        return packHint;
    }

    private string PackProgressLine(PortraitPackProgress progress)
    {
        var key = (progress.Received / 104_858, Localization.Loc.Version);
        if (key != packProgressKey)
        {
            packProgressKey = key;
            packProgress = string.Format(CultureInfo.CurrentCulture, Strings.PortraitPackProgress, PortraitPackOffer.SizeText(progress.Received), PortraitPackOffer.SizeText(progress.Total));
        }

        return packProgress;
    }
}
