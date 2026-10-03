using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Tsukimichi.Core.Diagnostics;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Advanced › Privacy &amp; trust (feature plan v7 N2, "Trust you can check"): what Tsukimichi reads and
/// sends in a few plain lines (the whole statement is <c>docs/privacy.md</c>, a click away in the browser), the plugin's
/// version and the SHA-256 of the <c>Tsukimichi.dll</c> running now, worked out on this PC and copyable, so a player can
/// compare it with the hashes the release workflow publishes beside each release.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The whole statement, on the repository's main branch.</summary>
    private const string PrivacyStatementUrl = "https://github.com/xenofei/Tsukimichi/blob/main/docs/privacy.md";

    private const double FingerprintCopiedSeconds = 3.0;

    // The loaded plugin file's SHA-256, read once off the draw thread the first time the block shows.
    private Task<string>? fingerprint;
    private double fingerprintCopiedUntil;

    // The Copy button's two labels and the hash's line, composed once (per language) instead of every frame.
    private readonly Localization.LocCache<string> copyFingerprintLabel = new(static () => Strings.ConfigPrivacyCopy + "##copyFingerprint");
    private readonly Localization.LocCache<string> copiedFingerprintLabel = new(static () => Strings.ConfigPrivacyCopied + "##copyFingerprint");
    private string? fingerprintNote;
    private int fingerprintNoteLanguage = -1;

    /// <summary>Settings › Advanced › Privacy &amp; trust: the statement's summary, a link to all of it, and this build's fingerprint.</summary>
    private void DrawPrivacy()
    {
        Header(Strings.ConfigPrivacyHeading);
        if (Setting(Strings.ConfigPrivacyStatement, Strings.ConfigPrivacyStatementHint, "privacy trust network internet upload send data reads files", 0f))
        {
            SettingNote(Strings.ConfigPrivacyReads);
            SettingNote(Strings.ConfigPrivacyKeeps);
            SettingNote(Strings.ConfigPrivacySends);
            EndSetting();
        }

        if (ButtonRow(Strings.ConfigPrivacyFull, Strings.ConfigPrivacyFullHint, Strings.ConfigPrivacyOpen, "privacy statement github browser link"))
        {
            OpenPrivacyStatement();
        }

        DrawFingerprint();
    }

    /// <summary>
    /// "This build's fingerprint": Copy (disabled until the hash is known), then the version and the hash under the row.
    /// The button keeps its width while it says Copied, so nothing moves.
    /// </summary>
    private void DrawFingerprint()
    {
        var padX = ImGui.GetStyle().FramePadding.X * 2f;
        var width = MathF.Max(ImGui.CalcTextSize(Strings.ConfigPrivacyCopy).X, ImGui.CalcTextSize(Strings.ConfigPrivacyCopied).X) + padX;
        if (!Setting(Strings.ConfigPrivacyFingerprint, Strings.ConfigPrivacyFingerprintHint, "privacy trust sha256 sha-256 hash checksum verify fingerprint build version release dll", width))
        {
            return;
        }

        var task = fingerprint ??= StartFingerprint();
        var hash = task.IsCompletedSuccessfully ? task.Result : null;
        var copied = ImGui.GetTime() < fingerprintCopiedUntil;
        using (ImRaii.Disabled(hash is null))
        {
            if (ImGui.Button(copied ? copiedFingerprintLabel.Value : copyFingerprintLabel.Value, new Vector2(width, 0f)) && hash is not null)
            {
                ImGui.SetClipboardText(hash);
                fingerprintCopiedUntil = ImGui.GetTime() + FingerprintCopiedSeconds;
            }
        }

        SettingNote(pluginVersionLine.Value);
        if (hash is not null)
        {
            SettingNote(FingerprintNote(task), Theme.Surface.Text);
        }
        else if (task.IsFaulted)
        {
            SettingNote(FingerprintNote(task), Theme.DangerText);
        }
        else
        {
            SettingNote(Strings.ConfigPrivacyHashing);
        }

        EndSetting();
    }

    /// <summary>
    /// The line under the fingerprint once <paramref name="task"/> is done: the hash, or why it could not be read.
    /// Composed once per language rather than every frame the block shows.
    /// </summary>
    private string FingerprintNote(Task<string> task)
    {
        if (fingerprintNote is null || fingerprintNoteLanguage != Localization.Loc.Version)
        {
            fingerprintNoteLanguage = Localization.Loc.Version;
            fingerprintNote = task.IsCompletedSuccessfully
                ? string.Format(CultureInfo.InvariantCulture, Strings.ConfigPrivacyHashFormat, task.Result)
                : string.Format(CultureInfo.CurrentCulture, Strings.ConfigPrivacyHashFailedFormat, task.Exception?.GetBaseException().Message ?? string.Empty);
        }

        return fingerprintNote;
    }

    /// <summary>Reads the loaded plugin file and hashes it on the thread pool; the path is taken here, on the draw thread.</summary>
    private Task<string> StartFingerprint()
    {
        var path = pluginInterface.AssemblyLocation.FullName;
        return Task.Run(() => FileFingerprint.Sha256(path));
    }

    /// <summary>Opens the whole statement in the browser: only on this click, like every page Tsukimichi opens.</summary>
    private static void OpenPrivacyStatement()
    {
        try
        {
            Util.OpenLink(PrivacyStatementUrl);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not open {Url} in the browser", PrivacyStatementUrl);
        }
    }
}
