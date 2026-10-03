using System;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Alerts (feature plan v6 U7): the chat lines (newly available quests with the main scenario under it, job
/// quests after a level-up, abandoned quests, seasonal events at login, "Before you continue"), the chat extras
/// (<c>ConfigWindow.InGame.cs</c>) and the welcome-back card.
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawNotices()
    {
        Header(Strings.ConfigSectionNotices);
        var notice = settings.ChatNoticeNewlyAvailable;
        if (Toggle(Strings.ConfigChatNotice, Strings.ConfigChatNoticeHint, ref notice, "chat message newly available ready notice"))
        {
            settings.ChatNoticeNewlyAvailable = notice;
            Save();
        }

        var msq = settings.IncludeMsqInNotices;
        if (Toggle(Strings.ConfigIncludeMsq, Strings.ConfigIncludeMsqHint, ref msq, "chat main scenario msq notice", settings.ChatNoticeNewlyAvailable, sub: true, reason: Strings.SettingsNoticeOffReason))
        {
            settings.IncludeMsqInNotices = msq;
            Save();
        }

        var nudge = settings.JobQuestNudge;
        if (Toggle(Strings.JobsConfigNudge, Strings.JobsConfigNudgeHint, ref nudge, "chat job class role quest notice level"))
        {
            settings.JobQuestNudge = nudge;
            Save();
        }

        var abandoned = settings.ChatNoticeAbandoned;
        if (Toggle(Strings.AbandonedConfigNotice, Strings.AbandonedConfigNoticeHint, ref abandoned, "chat notice abandon"))
        {
            settings.ChatNoticeAbandoned = abandoned;
            Save();
        }

        var seasonal = settings.ChatNoticeSeasonal;
        if (Toggle(Strings.SeasonalConfigNotice, Strings.SeasonalConfigNoticeHint, ref seasonal, "chat notice event festival seasonal login"))
        {
            settings.ChatNoticeSeasonal = seasonal;
            Save();
        }

        var payoff = settings.ChatNoticePayoffGates;
        if (Toggle(Strings.PayoffConfigNotice, Strings.PayoffConfigNoticeHint, ref payoff, "chat notice before you continue optional", settings.ShowPayoffGates, reason: Strings.SettingsPayoffOffReason))
        {
            settings.ChatNoticePayoffGates = payoff;
            Save();
        }
    }

    /// <summary>"Welcome-back card after N days" (P7): Off turns the card off; saved once the slider is still.</summary>
    private void DrawWelcomeBack()
    {
        Header(Strings.SettingsWelcomeBackHeading);
        if (!Setting(Strings.WelcomeBackConfigDays, Strings.WelcomeBackConfigHint, "since you were away welcome back break days return"))
        {
            return;
        }

        var days = Math.Clamp(settings.WelcomeBackDays, 0, Core.Return.WelcomeBackTrigger.MaxDays);
        ImGui.SetNextItemWidth(ControlWidth);
        if (ImGui.SliderInt("##welcomeBackDays", ref days, 0, Core.Return.WelcomeBackTrigger.MaxDays, days == 0 ? Strings.WelcomeBackConfigOff : Strings.WelcomeBackConfigDaysFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            settings.WelcomeBackDays = days;
            SaveSoon();
        }

        EndSetting();
    }
}
