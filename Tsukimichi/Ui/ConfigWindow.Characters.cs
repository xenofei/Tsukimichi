using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Data › Characters (1.8.0, R7 B, E, H): group the Characters list by data center, show hidden characters
/// in the lists, the characters hidden or not tracked with Show and Track buttons, and "Forget characters not seen in
/// N days" behind a confirmation that names them. A character live here or in another game client is never forgotten,
/// and one that logs in elsewhere while the question is open is kept and counted in the result.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string ForgetBulkPopupId = "##forgetCharacters";

    // The characters the confirmation names, picked when it opened; the result line under the button.
    private List<CharacterEntry> forgetPicked = [];
    private string forgetQuestionText = string.Empty;
    private string? charactersToast;
    private DateTime charactersToastUntilUtc;

    // The button's label and the hidden list's header, rebuilt when the roster, the days or the language change.
    private (int Roster, int Days, int Language, long Minute) charactersKey = (-1, -1, -1, -1);
    private List<CharacterEntry> forgetCandidates = [];
    private List<CharacterEntry> setAside = [];
    private string forgetButton = string.Empty;
    private string setAsideHeader = string.Empty;

    /// <summary>Every character in the alt lists' order, with the per-character settings; set by the plugin. Null hides the block.</summary>
    public CharacterRoster? Roster { get; set; }

    private void DrawCharacters()
    {
        if (Roster is not { } roster)
        {
            return;
        }

        Header(Strings.AltsSettingsHeading);
        RefreshCharacters(roster);

        if (Row(Strings.AltsSettingsByDataCenter, Strings.AltsSettingsByDataCenterHint, "data center dc group world alts list"))
        {
            var byCenter = settings.CharacterListByDataCenter;
            if (ImGui.Checkbox(Strings.AltsSettingsByDataCenter, ref byCenter))
            {
                settings.CharacterListByDataCenter = byCenter;
                Save();
            }

            HintOnHover(Strings.AltsSettingsByDataCenterHint);
        }

        if (Row(Strings.AltsSettingsShowHidden, Strings.AltsSettingsShowHiddenHint, "hidden characters alts switcher list"))
        {
            var showHidden = settings.ShowHiddenCharacters;
            if (ImGui.Checkbox(Strings.AltsSettingsShowHidden, ref showHidden))
            {
                settings.ShowHiddenCharacters = showHidden;
                Save();
            }

            HintOnHover(Strings.AltsSettingsShowHiddenHint);
        }

        if (Row(Strings.AltsSettingsHiddenFormat, Strings.AltsSettingsHiddenHint, "hidden untracked not tracked don't track characters alts"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(setAsideHeader);
            HintOnHover(Strings.AltsSettingsHiddenHint);
            DrawSetAside(roster.Settings);
        }

        if (Row(Strings.AltsSettingsForget, Strings.AltsSettingsForgetHint, "forget delete old characters alts prune days not seen"))
        {
            ImGui.Spacing();
            DrawForgetNotSeen();
        }

        DrawForgetBulkConfirm();
    }

    private void RefreshCharacters(CharacterRoster roster)
    {
        var key = (roster.Version, settings.ForgetNotSeenDays, Localization.Loc.Version, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute);
        if (key == charactersKey)
        {
            return;
        }

        charactersKey = key;
        forgetCandidates = CharacterList.NotSeenFor(roster.All, settings.ForgetNotSeenDays, DateTime.UtcNow);
        forgetButton = string.Format(CultureInfo.CurrentCulture, Strings.AltsSettingsForgetButtonFormat, forgetCandidates.Count);
        setAside = [];
        foreach (var entry in roster.All)
        {
            if (entry.Hidden || !entry.Tracked)
            {
                setAside.Add(entry);
            }
        }

        setAsideHeader = string.Format(CultureInfo.CurrentCulture, Strings.AltsSettingsHiddenFormat, setAside.Count);
    }

    /// <summary>The characters hidden or not tracked, each with Show and Track where they apply.</summary>
    private void DrawSetAside(CharacterSettingsBook book)
    {
        if (setAside.Count == 0)
        {
            Chrome.Hint(Strings.AltsSettingsHiddenNone);
            return;
        }

        using var indent = ImRaii.PushIndent();
        for (var i = 0; i < setAside.Count; i++)
        {
            var entry = setAside[i];
            using var id = ImRaii.PushId(i);
            if (entry.Hidden)
            {
                if (ImGui.SmallButton(Strings.AltsSettingsShowButton))
                {
                    book.Edit(CharacterSettingChange.Hide(entry.ContentId, false));
                }

                ImGui.SameLine();
            }

            if (!entry.Tracked)
            {
                if (ImGui.SmallButton(Strings.AltsSettingsTrackButton))
                {
                    book.Edit(CharacterSettingChange.Track(entry.ContentId, true));
                }

                ImGui.SameLine();
            }

            var badges = (entry.Hidden ? Strings.AltsHiddenBadge : string.Empty)
                         + (entry.Hidden && !entry.Tracked ? " · " : string.Empty)
                         + (!entry.Tracked ? Strings.AltsUntrackedBadge : string.Empty);
            Chrome.FitText(string.Format(CultureInfo.CurrentCulture, Strings.CharacterNameFormat, entry.Name, entry.WorldName) + " · " + badges, ImGui.GetColorU32(ImGuiCol.Text));
        }
    }

    /// <summary>"Forget characters not seen in [N] days", the button with how many that picks, and the result line.</summary>
    private void DrawForgetNotSeen()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Strings.AltsSettingsForget);
        HintOnHover(Strings.AltsSettingsForgetHint);
        ImGui.SameLine();
        var days = settings.ForgetNotSeenDays;
        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(110f)));
        if (ImGui.InputInt("##forgetDays", ref days, 1, 30))
        {
            settings.ForgetNotSeenDays = Math.Clamp(days, Configuration.MinForgetDays, Configuration.MaxForgetDays);
            Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(Strings.AltsSettingsForgetDays);
        Chrome.Hint(Strings.AltsSettingsForgetHint);

        if (forgetCandidates.Count == 0)
        {
            Chrome.Hint(Strings.AltsSettingsForgetNone);
        }
        else
        {
            using (Theme.PushDestructiveButton())
            {
                if (ImGui.Button(forgetButton))
                {
                    forgetPicked = [.. forgetCandidates];
                    forgetQuestionText = string.Format(CultureInfo.CurrentCulture, Strings.AltsForgetBulkQuestionFormat, forgetPicked.Count);
                    ImGui.OpenPopup(Strings.AltsForgetBulkPopup);
                }
            }
        }

        if (charactersToast is not null)
        {
            if (DateTime.UtcNow >= charactersToastUntilUtc)
            {
                charactersToast = null;
            }
            else
            {
                using (Theme.PushText(Theme.Silver))
                {
                    ImGui.TextWrapped(charactersToast);
                }
            }
        }
    }

    /// <summary>The confirmation: the question, the characters it picked (name, world, age), Forget them and Cancel.</summary>
    private void DrawForgetBulkConfirm()
    {
        using var modal = ImRaii.PopupModal(Strings.AltsForgetBulkPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            return;
        }

        // A popup is its own window: it scales itself.
        UiMetrics.ApplyFontScale();
        ImGui.TextWrapped(forgetQuestionText);
        ImGui.Spacing();
        var now = DateTime.UtcNow;
        foreach (var entry in forgetPicked)
        {
            ImGui.BulletText(string.Format(CultureInfo.CurrentCulture, Strings.CharacterEntryFormat, entry.Name, entry.WorldName, UiFormat.Age(entry.TakenUtc, now)));
        }

        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.AltsForgetBulkConfirm))
            {
                ForgetPicked();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>Forgets the picked characters, except one that logged in here or elsewhere since; says how many went.</summary>
    private void ForgetPicked()
    {
        var forgotten = 0;
        var kept = 0;
        foreach (var entry in forgetPicked)
        {
            if (session.IsLiveElsewhere(entry.ContentId) || entry.ContentId == session.LiveContentId)
            {
                kept++;
                continue;
            }

            session.ForgetCharacter(entry.ContentId);
            forgotten++;
        }

        forgetPicked = [];
        charactersKey = (-1, -1, -1, -1);
        charactersToast = kept == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.AltsForgetBulkDoneFormat, forgotten)
            : string.Format(CultureInfo.CurrentCulture, Strings.AltsForgetBulkSkippedFormat, forgotten, kept);
        charactersToastUntilUtc = DateTime.UtcNow + ToastDuration;
    }
}
