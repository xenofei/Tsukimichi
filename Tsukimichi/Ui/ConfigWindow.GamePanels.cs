using System;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations, the panels beside game windows (1.7.0): "Worth it?" beside quest offers, "What this
/// opened" beside quest completions and the Journal companion, each on by default. Like the Duty Finder hint they read
/// the game's windows, so they share the addon kill switch's pause (the notice under Integrations says when).
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
        if (Row(Strings.GamePanelSettingOffer, Strings.GamePanelSettingOfferHint, "quest offer accept worth moonlit unlock panel"))
        {
            var offer = settings.QuestOfferPanelEnabled;
            if (ImGui.Checkbox(Strings.GamePanelSettingOffer, ref offer))
            {
                settings.QuestOfferPanelEnabled = offer;
                Save();
                QuestOfferPanelToggled?.Invoke(offer);
            }

            HintOnHover(Strings.GamePanelSettingOfferHint);
        }

        if (Row(Strings.GamePanelSettingResult, Strings.GamePanelSettingResultHint, "quest complete result opened unlocked next chain panel"))
        {
            var result = settings.QuestResultPanelEnabled;
            if (ImGui.Checkbox(Strings.GamePanelSettingResult, ref result))
            {
                settings.QuestResultPanelEnabled = result;
                Save();
                QuestResultPanelToggled?.Invoke(result);
            }

            HintOnHover(Strings.GamePanelSettingResultHint);
        }

        if (Row(Strings.GamePanelSettingJournal, Strings.GamePanelSettingJournalHint, "journal companion verdict route panel"))
        {
            var journal = settings.JournalCompanionEnabled;
            if (ImGui.Checkbox(Strings.GamePanelSettingJournal, ref journal))
            {
                settings.JournalCompanionEnabled = journal;
                Save();
                JournalCompanionToggled?.Invoke(journal);
            }

            HintOnHover(Strings.GamePanelSettingJournalHint);
        }
    }
}
