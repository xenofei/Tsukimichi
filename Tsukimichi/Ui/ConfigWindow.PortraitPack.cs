using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Utility;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › General › Look › Portrait pack (feature plan v7 F4, decision 8; docs/design/v7/ui/spec-1.20.md F4): one
/// sub-row under Giver portraits that keeps one fixed height in every state (a status line, two Secondary lines, one slot
/// for the bar or a Tertiary line) with at most two actions in a 210 px column. Download, Update and Download again open
/// the confirmation, a window of its own kept in front of Settings with Tsukimichi's own scrim over Settings only, focus
/// on Cancel, and Enter and Esc cancelling: nothing goes online before its Download button. Remove pack is a popover at
/// the Hold tier with no Undo. A newer pack is only ever named by the plugin's own <c>portrait_pack.json</c>; nothing
/// here checks online.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The portrait pack service; set by the plugin. Null hides the row.</summary>
    public PortraitPackService? PortraitPack { get; set; }

    private const float PackActionsLogical = 210f;
    private const float PackDialogWidthLogical = 470f;
    private const float PackNarrowLogical = 400f;
    private const string PackRemovePopup = "##portraitPackRemove";

    private bool packDialogOpen;
    private bool packDialogFocusCancel;
    private double packDialogOpenedAt;
    private int packDialogOpenedFrame;

    // The confirmation was opened by picking Game art + pack: once the pack is in, Giver portraits switches to it.
    private bool packAskedForPack;
    private bool openPackRemove;
    private Vector2 packRemoveAnchor;
    private float packBar;
    private readonly ConfirmGate packRemoveGate = new();

    private static string PackRemoveHoldLabel => packRemoveHoldText.Value;

    private static readonly Localization.LocText packRemoveHoldText = new(static () => Strings.PackRemoveHold + Chrome.HoldIdSuffix);

    /// <summary>The 1.17 Settings hint's dot: amber, and a darker one on Ishgard Snow for 3:1 (spec-1.20 F4, "--hint").</summary>
    private static Vector4 PackHintDot => Theme.IsLight ? new Vector4(0x8E / 255f, 0x6A / 255f, 0x1E / 255f, 1f) : new Vector4(0xC9 / 255f, 0xA8 / 255f, 0x66 / 255f, 1f);

    /// <summary>
    /// Opens the download confirmation (Download…, Update…, Download again…, or Game art + pack without the pack:
    /// <paramref name="askedForPack"/>, so Giver portraits switches to it once the pack is in).
    /// </summary>
    private void OpenPackDialog(bool askedForPack = false)
    {
        if (PortraitPack?.Offer is null)
        {
            return;
        }

        packDialogOpen = true;
        packDialogFocusCancel = true;
        packDialogOpenedAt = ImGui.GetTime();
        packDialogOpenedFrame = ImGui.GetFrameCount();
        packAskedForPack = askedForPack;
    }

    /// <summary>The Portrait pack row: status, lines and actions at one fixed height, then the remove popover.</summary>
    private void DrawPortraitPack()
    {
        if (PortraitPack is not { } service)
        {
            return;
        }

        var view = PackView(service);
        if (!Setting(Strings.PackLabel, null, "portrait pack download photos garland celes faces givers remove update github", float.MaxValue, sub: true))
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ControlWidth;
        var narrow = width < UiMetrics.Px(PackNarrowLogical);
        var actions = UiMetrics.Px(PackActionsLogical);
        var gap = UiMetrics.Px(12f);
        var wordsWidth = narrow ? width : MathF.Max(UiMetrics.Px(80f), width - actions - gap);
        var line = ImGui.GetTextLineHeight();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;

        // The words: status (with the hint dot on an error), two Secondary lines, and the slot.
        var y = origin.Y;
        var x = origin.X;
        if (view.Error)
        {
            var dot = UiMetrics.Px(6f);
            dl.AddCircleFilled(new Vector2(x + (dot * 0.5f), y + (line * 0.5f)), dot * 0.5f, Theme.U32(PackHintDot));
            x += dot + UiMetrics.Px(6f);
        }

        PackLine(dl, new Vector2(x, y), view.Status, wordsWidth - (x - origin.X), 1, s.Text);
        y += line + UiMetrics.Px(4f);
        PackLine(dl, new Vector2(origin.X, y), view.Line, wordsWidth, view.Bar is null ? 2 : 1, s.TextSecondary);
        var slotY = y + (line * 2f) + UiMetrics.Px(6f);
        if (view.Bar is { } target)
        {
            // The bar eases to each update over 0.12 s; under Reduce motion it steps.
            packBar = UiMetrics.ReduceMotion ? target : packBar + ((target - packBar) * MathF.Min(1f, ImGui.GetIO().DeltaTime / 0.12f));
            var barY = y + line + UiMetrics.Px(8f);
            var barHeight = UiMetrics.Px(4f);
            var barWidth = MathF.Min(wordsWidth, UiMetrics.Px(348f));
            dl.AddRectFilled(new Vector2(origin.X, barY), new Vector2(origin.X + barWidth, barY + barHeight), Theme.U32(s.Line), barHeight * 0.5f);
            dl.AddRectFilled(new Vector2(origin.X, barY), new Vector2(origin.X + (barWidth * Math.Clamp(packBar, 0f, 1f)), barY + barHeight), Theme.U32(s.TextSecondary), barHeight * 0.5f);
        }
        else
        {
            packBar = 0f;
        }

        if (view.Slot is { Length: > 0 } slot)
        {
            using (Typography.Caption())
            {
                PackLine(dl, new Vector2(origin.X, slotY), slot, wordsWidth, 1, s.TextTertiary);
            }
        }

        var wordsBottom = slotY + line;

        // The actions: at most two, side by side, right-aligned in their column (under the words in a narrow card).
        var buttonsY = narrow ? wordsBottom + UiMetrics.Px(8f) : origin.Y - UiMetrics.Px(2f);
        var style = ImGui.GetStyle();
        float Width(string? label) => label is null ? 0f : ImGui.CalcTextSize(label, true).X + (style.FramePadding.X * 2f);
        var both = Width(view.First) + Width(view.Second) + (view.First is not null && view.Second is not null ? style.ItemSpacing.X : 0f);
        var right = origin.X + width;
        ImGui.SetCursorScreenPos(new Vector2(narrow ? origin.X : right - both, buttonsY));
        if (view.First is { } first && ImGui.Button(first))
        {
            Act(service, view.FirstAction);
        }

        if (view.Second is { } second)
        {
            if (view.First is not null)
            {
                ImGui.SameLine();
            }

            if (ImGui.Button(second))
            {
                Act(service, view.SecondAction);
            }
        }

        // One fixed height in every state (spec-1.20 F4): the block is always as tall as status, two lines and the slot.
        var height = (wordsBottom - origin.Y) + (narrow ? UiMetrics.Px(8f) + ImGui.GetFrameHeight() : 0f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        EndSetting();

        DrawPackRemovePopover(service);
    }

    /// <summary>One clamped line (or two) of the row; the whole text on hover when it was cut.</summary>
    private static void PackLine(ImDrawListPtr dl, Vector2 at, string text, float width, int lines, Vector4 ink)
    {
        if (text.Length == 0)
        {
            return;
        }

        var cut = TextFlow.DrawClamped(dl, at, text, width, lines, Theme.U32(ink));
        if (cut && ImGui.IsMouseHoveringRect(at, at + new Vector2(width, ImGui.GetTextLineHeight() * lines)))
        {
            UiMetrics.Tooltip(text);
        }
    }

    private enum PackAct : byte
    {
        None,
        Confirm,
        Retry,
        Cancel,
        Remove,
        CopyReport,
    }

    private readonly record struct PackRow(string Status, string Line, string? Slot, float? Bar, bool Error, string? First, PackAct FirstAction, string? Second, PackAct SecondAction);

    private void Act(PortraitPackService service, PackAct action)
    {
        switch (action)
        {
            case PackAct.Confirm:
                OpenPackDialog();
                break;
            case PackAct.Retry:
                // Try again after a failed download: the player clicks it, as they clicked Download before.
                service.StartDownload(packAskedForPack);
                break;
            case PackAct.Cancel:
                service.Cancel();
                break;
            case PackAct.Remove:
                openPackRemove = true;
                packRemoveAnchor = new Vector2(ImGui.GetItemRectMax().X, ImGui.GetItemRectMax().Y + UiMetrics.Px(4f));
                break;
            case PackAct.CopyReport:
                ImGui.SetClipboardText(PackReport(service));
                break;
        }
    }

    /// <summary>What the row says and offers in the service's state (spec-1.20 F4's state and error tables).</summary>
    private PackRow PackView(PortraitPackService service)
    {
        var offer = service.Offer;
        var installed = service.Installed;
        var c = CultureInfo.CurrentCulture;
        string Size(long bytes) => PortraitPackOffer.SizeText(bytes);

        switch (service.Phase)
        {
            case PortraitPackPhase.Downloading:
                var progress = service.Progress;
                var line = service.SecondsLeft is { } left
                    ? string.Format(c, Strings.PackLineProgress, Size(progress.Received), Size(progress.Total), left.ToString(c))
                    : string.Format(c, Strings.PackLineProgressNoEta, Size(progress.Received), Size(progress.Total));
                return new PackRow(Strings.PackStatusDownloading, line, Strings.PackKeepsGoing, progress.Fraction, false, Strings.PackCancel, PackAct.Cancel, null, PackAct.None);
            case PortraitPackPhase.Installing:
                return new PackRow(Strings.PackStatusChecking, string.Format(c, Strings.PackLineChecking, diagnostics.PluginVersion), null, 1f, false, null, PackAct.None, null, PackAct.None);
            case PortraitPackPhase.Removing:
                return new PackRow(Strings.PackStatusRemoving, string.Empty, null, null, false, null, PackAct.None, null, PackAct.None);
        }

        // A run that just failed says why, with one way forward (it stays until the next run or a restart): a download,
        // an update (the installed pack stays in use, and the row says so) or a removal (the pack stays installed).
        var failure = service.LastResult;
        var failed = PortraitPackStatus.FailureRowOf(service.State, offer is not null, failure, service.LastWasRemoval, service.LastFinishedUtc != default);
        if (failed == PortraitPackFailureRow.Removal)
        {
            return new PackRow(Strings.PackStatusRemoveFailed, Strings.PackLineRemoveFailed, null, null, true, null, PackAct.None, Strings.PackRemove, PackAct.Remove);
        }

        if (failed != PortraitPackFailureRow.None && offer is not null)
        {
            var keeps = failed == PortraitPackFailureRow.Update && installed is { } kept
                ? string.Format(c, Strings.PackUpdateKeepsWorking, kept.PackNumber.ToString(c))
                : null;
            var update = keeps is not null;
            if (failure == PortraitPackFailure.Cancelled)
            {
                return update
                    ? new PackRow(Strings.PackStatusCancelled, Strings.PackLineCancelled, keeps, null, false, Strings.PackUpdate, PackAct.Confirm, Strings.PackRemove, PackAct.Remove)
                    : new PackRow(Strings.PackStatusCancelled, Strings.PackLineCancelled, null, null, false, Strings.PackDownload, PackAct.Confirm, null, PackAct.None);
            }

            var reason = failure switch
            {
                PortraitPackFailure.Offline => Strings.PackFailedOffline,
                PortraitPackFailure.HashMismatch => Strings.PackFailedHash,
                PortraitPackFailure.DiskFull => string.Format(c, Strings.PackFailedDiskFull, Size(offer.Size)),
                PortraitPackFailure.NotFound => Strings.PackFailedNotFound,
                PortraitPackFailure.Redirected => Strings.PackFailedRedirected,
                PortraitPackFailure.TooLarge or PortraitPackFailure.SizeMismatch => Strings.PackFailedSize,
                PortraitPackFailure.BadArchive or PortraitPackFailure.UnsafeEntry or PortraitPackFailure.BadManifest or PortraitPackFailure.BadImage => Strings.PackFailedChecks,
                PortraitPackFailure.DiskError => Strings.PackFailedDisk,
                _ => Strings.PackFailedHttp,
            };
            var copy = failure is PortraitPackFailure.HashMismatch or PortraitPackFailure.SizeMismatch or PortraitPackFailure.TooLarge;
            return new PackRow(update ? Strings.PackStatusUpdateFailed : Strings.PackStatusFailed, reason, keeps, null, true, Strings.PackTryAgain, PackAct.Retry, copy ? Strings.PackCopyReport : null, copy ? PackAct.CopyReport : PackAct.None);
        }

        switch (service.State)
        {
            case PortraitPackState.Installed when installed is not null:
            {
                var date = installed.InstalledUtc == default ? string.Empty : installed.InstalledUtc.ToLocalTime().ToString("d MMM", c);
                var detail = string.Format(c, Strings.PackLineInstalled, installed.Faces.ToString("N0", c), Size(installed.BytesOnDisk), date);
                return new PackRow(string.Format(c, Strings.PackStatusInstalled, installed.PackNumber.ToString(c)), detail, InstalledNote(service, installed), null, false, null, PackAct.None, Strings.PackRemove, PackAct.Remove);
            }

            case PortraitPackState.UpdateAvailable when installed is not null && offer is not null:
            {
                var detail = string.Format(c, Strings.PackLineUpdate, offer.Givers.ToString("N0", c), Size(offer.Size), installed.PackNumber.ToString(c));
                return new PackRow(string.Format(c, Strings.PackStatusUpdate, offer.PackNumber.ToString(c)), detail, Strings.PackUpdateNote, null, false, Strings.PackUpdate, PackAct.Confirm, Strings.PackRemove, PackAct.Remove);
            }

            case PortraitPackState.Damaged:
                return offer is not null
                    ? new PackRow(Strings.PackStatusDamaged, Strings.PackLineDamaged, null, null, true, Strings.PackDownloadAgain, PackAct.Confirm, null, PackAct.None)
                    : new PackRow(Strings.PackStatusDamaged, Strings.PackLineDamaged, null, null, true, null, PackAct.None, Strings.PackRemove, PackAct.Remove);

            case PortraitPackState.Available when offer is not null:
                return new PackRow(Strings.PackStatusNotDownloaded, string.Format(c, Strings.PackLineAvailable, offer.Givers.ToString("N0", c), Size(offer.Size)), null, null, false, Strings.PackDownload, PackAct.Confirm, null, PackAct.None);

            default:
                return new PackRow(Strings.PackStatusNotOffered, Strings.PackLineNotOffered, null, null, false, null, PackAct.None, null, PackAct.None);
        }
    }

    /// <summary>The installed row's Tertiary slot: not in use, or built for an older game (it still works).</summary>
    private string? InstalledNote(PortraitPackService service, PortraitPack installed)
    {
        if (settings.GiverPortraits != GiverPortraitMode.GameArtAndPack)
        {
            return Strings.PackNotInUse;
        }

        return service.ForOlderGame
            ? string.Format(CultureInfo.CurrentCulture, Strings.PackOlderGame, DataStamp.ShortGameVersion(installed.GameVersion), DataStamp.ShortGameVersion(service.ClientGameVersion))
            : null;
    }

    /// <summary>Copy report after a fingerprint mismatch: the version, the expected and received hashes and byte counts; no character data.</summary>
    private string PackReport(PortraitPackService service)
    {
        var offer = service.Offer;
        var receipt = service.LastReceipt;
        return string.Format(
            CultureInfo.InvariantCulture,
            Strings.PackReportFormat,
            diagnostics.PluginVersion,
            offer?.ReleaseName ?? "-",
            offer?.Sha256 ?? "-",
            offer?.Size ?? 0,
            receipt is { Sha256.Length: > 0 } ? receipt.Sha256 : "-",
            receipt?.Bytes ?? 0);
    }

    /// <summary>
    /// The confirmation (spec-1.20 F4): a 470 px window centred on Settings every frame and kept in front of it, Tsukimichi's
    /// own scrim over the Settings window only, focus on Cancel, Enter and Esc cancel. It states the size, the source,
    /// the Garland Tools credit and the hash check. Only its Download button starts the download.
    /// </summary>
    private void DrawPackDialog(Vector2 settingsMin, Vector2 settingsMax, bool settingsFocused)
    {
        if (PortraitPack is not { Offer: { } offer } service)
        {
            packDialogOpen = false;
            return;
        }

        var installed = service.Installed;
        var update = installed is not null;
        var c = CultureInfo.CurrentCulture;
        var size = PortraitPackOffer.SizeText(offer.Size);

        // Called inside Settings' own Begin: the viewport Settings is on (the game window, or a platform window of its
        // own when dragged out), whose foreground the scrim is drawn on.
        var settingsViewport = ImGui.GetWindowViewport();

        // Rise (0.16 s, 4 px), instant under Reduce motion.
        var t = UiMetrics.ReduceMotion ? 1f : (float)Math.Clamp((ImGui.GetTime() - packDialogOpenedAt) / MotionTokens.Rise, 0d, 1d);
        var ease = 1f - ((1f - t) * (1f - t) * (1f - t));
        var centre = (settingsMin + settingsMax) * 0.5f + new Vector2(0f, UiMetrics.Px(4f) * (1f - ease));
        ImGui.SetNextWindowPos(centre, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(UiMetrics.Px(PackDialogWidthLogical), 0f), ImGuiCond.Always);
        if (settingsFocused || packDialogFocusCancel)
        {
            // A click on Settings brings the dialog back to the front.
            ImGui.SetNextWindowFocus();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ease);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(18f), UiMetrics.Px(16f)));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking;
        var open = ImGui.Begin("##portraitPackConfirm", flags);
        try
        {
            if (!open)
            {
                return;
            }

            UiMetrics.ApplyFontScale();
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var max = min + ImGui.GetWindowSize();

            // The scrim: over the Settings window only, around (never over) the dialog; the only dimming. Drawn on the
            // foreground of Settings' own viewport, so it covers Settings whether or not the dialog shares its platform
            // window (the dialog's draw list is clipped to the dialog's). It stays a cut-out around the dialog's rect.
            var scrim = Theme.IsLight ? new Vector4(26 / 255f, 33 / 255f, 54 / 255f, 0.30f * ease) : new Vector4(5 / 255f, 7 / 255f, 14 / 255f, 0.55f * ease);
            var ink = Theme.U32(scrim);
            var cover = ImGui.GetForegroundDrawList(settingsViewport);
            var top = Math.Clamp(min.Y, settingsMin.Y, settingsMax.Y);
            var bottom = Math.Clamp(max.Y, settingsMin.Y, settingsMax.Y);
            var leftEdge = Math.Clamp(min.X, settingsMin.X, settingsMax.X);
            var rightEdge = Math.Clamp(max.X, settingsMin.X, settingsMax.X);
            cover.PushClipRect(settingsMin, settingsMax, false);
            cover.AddRectFilled(settingsMin, new Vector2(settingsMax.X, top), ink);
            cover.AddRectFilled(new Vector2(settingsMin.X, bottom), settingsMax, ink);
            cover.AddRectFilled(new Vector2(settingsMin.X, top), new Vector2(leftEdge, bottom), ink);
            cover.AddRectFilled(new Vector2(rightEdge, top), new Vector2(settingsMax.X, bottom), ink);
            cover.PopClipRect();

            var s = Theme.Surface;
            var wrap = UiMetrics.Px(PackDialogWidthLogical - 36f);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
            using (Typography.Title(Strings.PackConfirmTitle))
            {
                ImGui.TextUnformatted(update ? string.Format(c, Strings.PackConfirmTitleUpdate, offer.PackNumber.ToString(c)) : Strings.PackConfirmTitle);
            }

            ImGui.Spacing();
            ImGui.TextWrapped(update
                ? string.Format(c, Strings.PackConfirmWhatUpdate, offer.Givers.ToString("N0", c), (installed?.PackNumber ?? 0).ToString(c))
                : string.Format(c, Strings.PackConfirmWhat, offer.Givers.ToString("N0", c)));
            ImGui.Spacing();

            // The facts: Tertiary keys, Text values, Secondary asides.
            var keyWidth = UiMetrics.Px(78f);
            Fact(Strings.PackFactSize, size, Strings.PackFactSizeAside, keyWidth, s);
            Fact(Strings.PackFactFrom, "github.com/xenofei/Tsukimichi", string.Format(c, Strings.PackFactFromAside, offer.ReleaseName), keyWidth, s);
            Fact(Strings.PackFactPhotos, Strings.PackFactPhotosValue, Strings.PackFactPhotosAside, keyWidth, s);
            Fact(Strings.PackFactChecked, string.Format(c, Strings.PackFactCheckedValue, diagnostics.PluginVersion), string.Empty, keyWidth, s);
            ImGui.Spacing();

            // The promise, in a sunk inset with a line outline.
            var insetMin = ImGui.GetCursorScreenPos();
            var pad = UiMetrics.Px(10f);
            dl.ChannelsSplit(2);
            dl.ChannelsSetCurrent(1);
            ImGui.SetCursorScreenPos(insetMin + new Vector2(pad));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap - (pad * 2f));
            using (Typography.Title(Strings.PackPromiseLead))
            {
                ImGui.TextUnformatted(Strings.PackPromiseLead);
            }

            ImGui.TextWrapped(Strings.PackPromise);
            ImGui.PopTextWrapPos();
            var insetMax = new Vector2(insetMin.X + wrap, ImGui.GetCursorScreenPos().Y + pad - ImGui.GetStyle().ItemSpacing.Y);
            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(insetMin, insetMax, Theme.U32(s.Sunken), UiMetrics.Px(6f));
            dl.AddRect(insetMin, insetMax, Theme.U32(s.Line), UiMetrics.Px(6f), ImDrawFlags.None, UiMetrics.Hairline);
            dl.ChannelsMerge();
            ImGui.SetCursorScreenPos(new Vector2(insetMin.X, insetMax.Y + UiMetrics.Px(10f)));

            using (Theme.PushText(s.TextSecondary))
            {
                ImGui.TextWrapped(Strings.PackSpoilers);
            }

            ImGui.PopTextWrapPos();
            ImGui.Spacing();

            // Enter and Esc cancel, before any button sees them: Enter never downloads by accident. Not on the frame the
            // dialog opened (the Enter that pressed "Download…" would cancel it at once), and never on a key's repeat.
            var cancel = ImGui.GetFrameCount() > packDialogOpenedFrame && ImGui.IsWindowFocused()
                && (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false) || ImGui.IsKeyPressed(ImGuiKey.Escape, false));

            // "What Tsukimichi sends": the whole statement in the browser.
            using (Theme.PushText(s.TextSecondary))
            {
                ImGui.TextUnformatted(Strings.PackWhatItSends);
            }

            var linkMin = ImGui.GetItemRectMin();
            var linkMax = ImGui.GetItemRectMax();
            dl.AddLine(new Vector2(linkMin.X, linkMax.Y), linkMax, Theme.U32(s.TextSecondary), UiMetrics.Hairline);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (ImGui.IsItemClicked())
            {
                Util.OpenLink(PrivacyStatementUrl);
            }

            ImGui.SameLine(0f, UiMetrics.Px(14f));
            var download = string.Format(c, Strings.PackConfirmDownload, size);
            if (Chrome.ActionPill("##packDownload", FontAwesomeIcon.Download.ToIconString(), download, PillTone.Primary, !service.Busy, null, PillLayout.Frame) && !cancel)
            {
                // The one place a download starts from the player's first click: this button.
                service.StartDownload(packAskedForPack);
                packDialogOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button(Strings.PackCancel) || cancel)
            {
                packDialogOpen = false;
            }

            if (packDialogFocusCancel)
            {
                // Keyboard focus starts on Cancel.
                ImGui.SetItemDefaultFocus();
                ImGui.SetKeyboardFocusHere(-1);
                packDialogFocusCancel = false;
            }
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }

    /// <summary>One fact of the confirmation: the key in Tertiary, the value in Text, the aside in Secondary.</summary>
    private static void Fact(string key, string value, string aside, float keyWidth, SurfaceColors s)
    {
        var start = ImGui.GetCursorPosX();
        using (Theme.PushText(s.TextTertiary))
        {
            ImGui.TextUnformatted(key);
        }

        ImGui.SameLine(start + keyWidth);
        ImGui.TextUnformatted(value);
        if (aside.Length > 0)
        {
            ImGui.SameLine(0f, 0f);
            using (Theme.PushText(s.TextSecondary))
            {
                ImGui.TextUnformatted(aside);
            }
        }
    }

    /// <summary>
    /// Remove pack's popover (spec-1.20 F4, the 1.18 confirm popover): the question, what goes, the shipped Hold button
    /// (<see cref="GuardedAction.RemovePortraitPack"/>, no Undo: getting the pack back is a download) and Keep it.
    /// </summary>
    private void DrawPackRemovePopover(PortraitPackService service)
    {
        if (openPackRemove)
        {
            openPackRemove = false;
            packRemoveGate.Cancel();
            ImGui.OpenPopup(PackRemovePopup);
        }

        ImGui.SetNextWindowPos(packRemoveAnchor, ImGuiCond.Appearing, new Vector2(1f, 0f));
        if (!ImGui.BeginPopup(PackRemovePopup, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            return;
        }

        try
        {
            UiMetrics.ApplyFontScale();
            var s = Theme.Surface;
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiMetrics.Px(300f));
            using (Typography.Title(Strings.PackRemoveTitle))
            {
                ImGui.TextUnformatted(Strings.PackRemoveTitle);
            }

            var bytes = service.Installed?.BytesOnDisk ?? service.Offer?.Size ?? 0;
            ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.PackRemoveBody, PortraitPackOffer.SizeText(bytes)));
            ImGui.PopTextWrapPos();
            ImGui.Spacing();

            var confirmed = Chrome.HoldButton(PackRemoveHoldLabel, packRemoveGate);
            if (ImGui.IsItemHovered())
            {
                Safety.Tooltip(Strings.PackRemoveHoldTooltip, GuardedAction.RemovePortraitPack);
            }

            if (confirmed)
            {
                // No Undo (the files are gone). Once they are, Giver portraits goes back to Game art (the plugin's
                // pack-changed callback, PortraitPackStatus.ModeAfter); a removal that fails changes nothing.
                service.StartRemove();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button(Strings.PackKeepIt))
            {
                ImGui.CloseCurrentPopup();
            }

            using (Typography.Caption())
            using (Theme.PushText(s.TextTertiary))
            {
                ImGui.TextUnformatted(Strings.PackHoldHint);
            }
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
            ImGui.EndPopup();
        }
    }
}
