using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Alerts (feature plan v6 U7): the chat lines (newly available quests with the main scenario under it, job
/// quests after a level-up, abandoned quests, seasonal events at login, "Before you continue"), the chat extras
/// (<c>ConfigWindow.InGame.cs</c>), the "Needs you" alerts while automation runs (1.18.0, A5) and the welcome-back card.
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

    /// <summary>Settings' Test: plays a chat sound effect through the game (1–16); false while game calls are paused. Set by the plugin.</summary>
    public Func<int, bool>? TestNeedsYouSound { get; set; }

    /// <summary>The sound choices: None, then &lt;se.1&gt; to &lt;se.16&gt; (the game's own names for them).</summary>
    private static readonly LocArray NeedsYouSoundOptions = new(static () =>
    {
        var options = new string[Configuration.MaxNeedsYouSound + 1];
        options[0] = Strings.ConfigNeedsYouSoundNone;
        for (var i = 1; i < options.Length; i++)
        {
            options[i] = string.Create(CultureInfo.InvariantCulture, $"<se.{i}>");
        }

        return options;
    });

    /// <summary>
    /// Settings › Alerts › While automation runs (1.18.0, A5; spec-1.18): which "Needs you" alerts come while a hand-off
    /// runs, each with its own chat sound or none (and a Test button), the taskbar flash, and what Tsukimichi stops on a
    /// knock-out or a stall (its own hand-offs by default; Questionable only when the player opts in). Every alert is
    /// also a chat line and the panel over the game. Read each frame by <c>Game.RunWatch</c>.
    /// </summary>
    private void DrawNeedsYou()
    {
        // Only while the automation level shows a hand-off that runs (1.18, A10): at Tracker only and Travel, Tsukimichi
        // starts nothing that could need you.
        if (!AutomationGate.ShowsAny(AutomationGate.RunningHandOffs))
        {
            return;
        }

        Header(Strings.ConfigSectionNeedsYou);
        Note(Strings.ConfigNeedsYouScope, Strings.ConfigNeedsYouScopeHint, "needs you alert automation hand-off run questionable autoduty");

        var death = settings.NeedsYouDeath;
        if (Toggle(Strings.ConfigNeedsYouDeath, Strings.ConfigNeedsYouDeathHint, ref death, "needs you alert death died dead knocked out automation"))
        {
            settings.NeedsYouDeath = death;
            Save();
        }

        var sound = settings.NeedsYouSoundDeath;
        if (SoundRow("death", ref sound, settings.NeedsYouDeath))
        {
            settings.NeedsYouSoundDeath = sound;
            Save();
        }

        var stuck = settings.NeedsYouStuck;
        if (Toggle(Strings.ConfigNeedsYouStuck, Strings.ConfigNeedsYouStuckHint, ref stuck, "needs you alert stuck vnavmesh walking automation"))
        {
            settings.NeedsYouStuck = stuck;
            Save();
        }

        sound = settings.NeedsYouSoundStuck;
        if (SoundRow("stuck", ref sound, settings.NeedsYouStuck))
        {
            settings.NeedsYouSoundStuck = sound;
            Save();
        }

        var pop = settings.NeedsYouDutyPop;
        if (Toggle(Strings.ConfigNeedsYouDutyPop, Strings.ConfigNeedsYouDutyPopHint, ref pop, "needs you alert duty pop ready duty finder queue automation"))
        {
            settings.NeedsYouDutyPop = pop;
            Save();
        }

        sound = settings.NeedsYouSoundDutyPop;
        if (SoundRow("pop", ref sound, settings.NeedsYouDutyPop))
        {
            settings.NeedsYouSoundDutyPop = sound;
            Save();
        }

        var tell = settings.NeedsYouTell;
        if (Toggle(Strings.ConfigNeedsYouTell, Strings.ConfigNeedsYouTellHint, ref tell, "needs you alert tell whisper message automation"))
        {
            settings.NeedsYouTell = tell;
            Save();
        }

        sound = settings.NeedsYouSoundTell;
        if (SoundRow("tell", ref sound, settings.NeedsYouTell))
        {
            settings.NeedsYouSoundTell = sound;
            Save();
        }

        var flash = settings.NeedsYouFlash;
        if (Toggle(Strings.ConfigNeedsYouFlash, Strings.ConfigNeedsYouFlashHint, ref flash, "needs you alert taskbar flash window background automation"))
        {
            settings.NeedsYouFlash = flash;
            Save();
        }

        var stop = settings.NeedsYouStopHandOffs;
        if (Toggle(Strings.ConfigNeedsYouStopHandOffs, Strings.ConfigNeedsYouStopHandOffsHint, ref stop, "needs you stop hand-offs knocked out stuck travel autoduty artisan"))
        {
            settings.NeedsYouStopHandOffs = stop;
            Save();
        }

        var alsoQuestionable = settings.NeedsYouStopQuestionable;
        if (Toggle(Strings.ConfigNeedsYouStopQuestionable, Strings.ConfigNeedsYouStopQuestionableHint, ref alsoQuestionable, "needs you stop questionable knocked out stuck", settings.NeedsYouStopHandOffs, sub: true))
        {
            settings.NeedsYouStopQuestionable = alsoQuestionable;
            Save();
        }
    }

    /// <summary>
    /// A kind's sound, a sub-row under its toggle: None or &lt;se.1&gt;–&lt;se.16&gt;, and Test, which plays the
    /// chosen one through the game. Returns true when the choice changed.
    /// </summary>
    private bool SoundRow(string id, ref int sound, bool enabled)
    {
        var options = NeedsYouSoundOptions.Value;
        var test = Strings.ConfigNeedsYouSoundTest;
        var testWidth = ImGui.CalcTextSize(test).X + (ImGui.GetStyle().FramePadding.X * 2f);
        var comboWidth = UiMetrics.Px(110f);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        if (!Setting(Strings.ConfigNeedsYouSoundRow, Strings.ConfigNeedsYouSoundRowHint, "needs you alert sound se chat sound effect test " + id, comboWidth + gap + testWidth, 0f, enabled, sub: true))
        {
            return false;
        }

        var changed = false;
        var choice = Math.Clamp(sound, 0, options.Length - 1);
        ImGui.PushID(id);
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.Combo("##sound", ref choice, options, options.Length))
        {
            sound = choice;
            changed = true;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(choice == 0 || TestNeedsYouSound is null);
        if (ImGui.Button(test + "##test"))
        {
            TestNeedsYouSound?.Invoke(choice);
        }

        ImGui.EndDisabled();
        ImGui.PopID();
        EndSetting();
        return changed;
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
