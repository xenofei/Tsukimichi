using System;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › In game › Panels beside game windows (1.7.0; placed by the 1.13.0 rebuild, v6 U7): "Worth it?" beside
/// quest offers, "What this opened" beside quest completions, the Journal companion and the Duty Finder unlock hint,
/// each on by default. They read the game's windows, so they share the addon kill switch's pause: while it holds, a
/// line at the top of the card says so (Advanced holds the override).
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Called with the new value after <c>Configuration.QuestOfferPanelEnabled</c> is toggled and saved.</summary>
    public Action<bool>? QuestOfferPanelToggled { get; set; }

    /// <summary>Called with the new value after <c>Configuration.QuestResultPanelEnabled</c> is toggled and saved.</summary>
    public Action<bool>? QuestResultPanelToggled { get; set; }

    /// <summary>Called with the new value after <c>Configuration.JournalCompanionEnabled</c> is toggled and saved.</summary>
    public Action<bool>? JournalCompanionToggled { get; set; }

    private void DrawGamePanels()
    {
        Header(Strings.GamePanelSettingsHeading);
        if (HookGate is { IsPaused: true })
        {
            Note(Strings.SettingsHooksPaused, Strings.SettingsHooksPausedHint, "hooks paused patch game panels");
        }

        var offer = settings.QuestOfferPanelEnabled;
        if (Toggle(Strings.GamePanelSettingOffer, Strings.GamePanelSettingOfferHint, ref offer, "quest offer accept worth moonlit unlock panel"))
        {
            settings.QuestOfferPanelEnabled = offer;
            Save();
            QuestOfferPanelToggled?.Invoke(offer);
        }

        var result = settings.QuestResultPanelEnabled;
        if (Toggle(Strings.GamePanelSettingResult, Strings.GamePanelSettingResultHint, ref result, "quest complete result opened unlocked next chain panel"))
        {
            settings.QuestResultPanelEnabled = result;
            Save();
            QuestResultPanelToggled?.Invoke(result);
        }

        var journal = settings.JournalCompanionEnabled;
        if (Toggle(Strings.GamePanelSettingJournal, Strings.GamePanelSettingJournalHint, ref journal, "journal companion verdict route panel"))
        {
            settings.JournalCompanionEnabled = journal;
            Save();
            JournalCompanionToggled?.Invoke(journal);
        }

        var dutyHint = settings.DutyFinderHintEnabled;
        if (Toggle(Strings.DutyHintSetting, Strings.DutyHintSettingHint, ref dutyHint, "duty finder raid padlock unlock hint"))
        {
            settings.DutyFinderHintEnabled = dutyHint;
            Save();
            DutyFinderHintToggled?.Invoke(dutyHint);
        }
    }
}
