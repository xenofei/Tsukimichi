using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Query;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Spoilers (T19): hide main scenario names ahead of the character, how many quests ahead keep their names
/// (saved once the slider is still), whether the places, duties, rewards and people the story ahead introduces hide too
/// (1.20.0 N6), hide journal artwork until a quest is in the journal, whether "Before you continue" notes show (P5),
/// and an override for the character shown. Every change bumps the session so each surface
/// re-reads the mask at once.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray SpoilerCharacterOptions = new(static () => [Strings.SpoilerCharacterDefault, Strings.SpoilerCharacterOn, Strings.SpoilerCharacterOff]);

    // The "N names hidden" line, rebuilt once per session version.
    private int spoilerCountVersion = -1;
    private string spoilerCountLine = string.Empty;

    // The "For {name}" label, rebuilt when the viewed character or its name changes.
    private ulong spoilerLabelContentId;
    private string? spoilerLabelName;
    private string spoilerLabel = string.Empty;

    private void DrawSpoilers()
    {
        Header(Strings.SettingsSpoilers);
        var hideNames = settings.SpoilerHideMsqNames;
        if (Toggle(Strings.SpoilerHideNames, Strings.SpoilerHideNamesHelp, ref hideNames, "spoiler shield main scenario msq names"))
        {
            settings.SpoilerHideMsqNames = hideNames;
            SpoilersChanged();
        }

        // The slider applies whenever the viewed character's effective options hide names: the global setting, or that
        // character's override (Always shield hides them even with the global setting off). Mirrors
        // Configuration.SpoilerOptionsFor without building the options record each frame.
        var effectiveHide = session.ViewedContentId is { } viewedId && Roster?.Settings.SpoilerShield(viewedId) is { } shielded
            ? shielded
            : settings.SpoilerHideMsqNames;
        if (Setting(Strings.SpoilerAhead, Strings.SpoilerAheadHelp, "spoiler shield names ahead reveal", enabled: effectiveHide, sub: true, reason: Strings.SettingsSpoilerAheadOffReason))
        {
            var ahead = Math.Clamp(settings.SpoilerRevealAhead, 0, SpoilerOptions.MaxAhead);
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderInt("##ahead", ref ahead, 0, SpoilerOptions.MaxAhead, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.SpoilerRevealAhead = ahead;
                SaveSoon();
                session.RefreshSpoilers();
            }

            EndSetting();
        }

        // The wider shield (1.20.0 N6) hangs off the same switch: it hides what the masked quests introduce.
        // Its hint says what hides; the placeholders themselves follow on a line of their own (spec-1.20 N6).
        if (ToggleSetting(Strings.SpoilerHideRelated, Strings.SpoilerHideRelatedHelp, "spoiler shield zone area aetheryte duty reward npc giver people places character", effectiveHide, sub: true, reason: Strings.SettingsSpoilerAheadOffReason))
        {
            var hideRelated = settings.HideOtherNames;
            if (RowToggle(ref hideRelated))
            {
                settings.HideOtherNames = hideRelated;
                SpoilersChanged();
            }

            if (effectiveHide)
            {
                SettingNote(Strings.SpoilerHideRelatedExamples);
            }

            EndSetting();
        }

        var hideArtwork = settings.SpoilerHideArtwork;
        if (Toggle(Strings.SpoilerHideArtwork, Strings.SpoilerHideArtworkHelp, ref hideArtwork, "spoiler shield images pictures banner art"))
        {
            settings.SpoilerHideArtwork = hideArtwork;
            SpoilersChanged();
        }

        var payoffNotes = settings.ShowPayoffGates;
        if (Toggle(Strings.PayoffConfigShow, Strings.PayoffConfigShowHint, ref payoffNotes, "spoiler before you continue notes optional"))
        {
            settings.ShowPayoffGates = payoffNotes;
            Save();
        }

        DrawSpoilerOverride();
    }

    /// <summary>The viewed character's own shield: follow the settings above, always shield, or show everything.</summary>
    private void DrawSpoilerOverride()
    {
        if (session.ViewedContentId is not { } contentId || Roster?.Settings is not { } characters)
        {
            Note(Strings.SpoilerCharacterLabel, Strings.SpoilerCharacterNone, "spoiler shield character alt override");
            return;
        }

        var name = session.ViewedSnapshot?.Name;
        if (spoilerLabel.Length == 0 || spoilerLabelContentId != contentId || !string.Equals(spoilerLabelName, name, StringComparison.Ordinal))
        {
            spoilerLabelContentId = contentId;
            spoilerLabelName = name;
            spoilerLabel = string.IsNullOrEmpty(name) ? Strings.SpoilerCharacterLabel : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerCharacterFormat, name);
        }

        var options = SpoilerCharacterOptions.Value;
        if (!Setting(Strings.SpoilerCharacterLabel, Strings.SpoilerCharacterHelp, "spoiler shield character alt override", Chrome.SegmentedWidth(options), UiMetrics.MinTarget, shown: spoilerLabel))
        {
            return;
        }

        var current = characters.SpoilerShield(contentId) is { } shielded ? (shielded ? 1 : 2) : 0;
        var choice = current;
        if (Chrome.Segmented("##spoilerCharacter", ref choice, options, ControlWidth) && choice != current)
        {
            // Saved to user/characters.json, shared by every game client; the masks rebuild when it lands (Plugin).
            characters.Edit(Core.Storage.CharacterSettingChange.Spoiler(contentId, choice == 0 ? null : choice == 1));
        }

        if (spoilerCountVersion != session.Version)
        {
            spoilerCountVersion = session.Version;
            spoilerCountLine = session.Bundle is null ? string.Empty : SpoilerCountLine(session.Spoilers, name);
        }

        if (spoilerCountLine.Length > 0)
        {
            SettingNote(spoilerCountLine);
        }

        EndSetting();
    }

    /// <summary>
    /// "212 story names and 486 other names hidden for Michiru." (spec-1.20 N6): the quest names and the wider shield's
    /// names. Without the wider shield it counts the quest names alone, as before 1.20.
    /// </summary>
    internal static string SpoilerCountLine(SpoilerMask spoilers, string? characterName) =>
        !spoilers.Options.HideRelated || !spoilers.Options.HideNames
            ? string.Format(CultureInfo.CurrentCulture, Strings.SpoilerMaskedCountFormat, spoilers.MaskedCount)
            : string.IsNullOrEmpty(characterName)
                ? string.Format(CultureInfo.CurrentCulture, Strings.SpoilerHiddenCountUnnamed, spoilers.MaskedCount, spoilers.MaskedNameCount)
                : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerHiddenCountFormat, spoilers.MaskedCount, spoilers.MaskedNameCount, characterName);

    /// <summary>Saves a spoiler setting and makes every surface re-read the mask.</summary>
    private void SpoilersChanged()
    {
        Save();
        session.RefreshSpoilers();
    }
}
