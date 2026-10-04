using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.Ui;

/// <summary>
/// What the detail pane's hand-offs are doing, for the status bar (feature plan v6, U4: the window never moves under
/// the player). Before 1.12 a trip, a Questionable run and an AutoDuty run each added a live line under the action
/// pills, and Craft, Gather, Copy, Run with AutoDuty, Add to Questionable and Open in Quest Map left a note in their
/// card for a few seconds; each pushed the pane down while the player read it. Now they all go to one
/// <see cref="CompanionActivityFeed"/> that the main window's fixed status bar draws as a single gold segment with a
/// small Stop (MainWindow.Activity.cs). The running hand-off's pill still turns into a labelled Stop, so the pane loses
/// nothing.
/// </summary>
public sealed partial class DetailPane
{
    // The travel line is composed once per step and language.
    private GoToGiverStep travelLineStep = GoToGiverStep.Idle;
    private int travelLineLanguage = -1;
    private string travelLine = string.Empty;

    // "Artisan: crafting Maple Lumber" (1.18, A7), composed when Craft hands the run over; null before any.
    private string? artisanCraftingLine;

    /// <summary>
    /// HOOK for richer travel status (mounting, flying, "step 2 of 4"): when set and it answers, its text replaces the
    /// line built from <see cref="TravelService.JourneyStep"/> while a trip runs. Read once per frame while travelling,
    /// so it should return a cached string.
    /// </summary>
    public Func<string?>? TravelStatusText { get; set; }

    /// <summary>The hand-offs under way and the last press's note, as the status bar shows them.</summary>
    public CompanionActivityFeed Activity { get; } = new();

    /// <summary>
    /// Reads each hand-off's live state into <see cref="Activity"/>: the trip, Questionable (polled at most once a
    /// second inside its IPC), AutoDuty, and an Artisan craft Tsukimichi started. Called once per frame by the status
    /// bar, whatever the tab or the selection.
    /// </summary>
    public void PollActivity()
    {
        var traveling = links.IsTraveling;
        Activity.Report(StopTarget.Travel, traveling ? TravelStatusText?.Invoke() ?? TravelLine(links.Travel?.JourneyStep ?? GoToGiverStep.Walking) : null, canStop: true);

        var questionable = QuestionableActions;
        Activity.Report(StopTarget.Questionable, questionable?.PollStatusText(), canStop: questionable?.CanStop == true);

        var autoDuty = AutoDuty is { Available: true, IsStopped: false };
        Activity.Report(StopTarget.AutoDuty, autoDuty ? Strings.AutoDutyRunning : null, canStop: true);

        // Only a craft Tsukimichi handed off (the claim is kept current by /tsuki stop's frame hook): Stop ends that run.
        var crafting = Artisan is { HandOffClaimed: true, IsBusy: true };
        Activity.Report(StopTarget.Artisan, crafting ? artisanCraftingLine ?? Strings.ArtisanCraftingStatus : null, canStop: true);
    }

    /// <summary>The Stop tooltip of the hand-off <paramref name="kind"/>; Questionable draws its own button.</summary>
    public string ActivityStopTooltip(StopTarget kind) => kind switch
    {
        StopTarget.Travel => links.StopTooltip(),
        StopTarget.AutoDuty => Strings.AutoDutyStopTooltip,
        _ => Strings.ArtisanStopTooltip,
    };

    /// <summary>Stops the hand-off <paramref name="kind"/>, as its pill's Stop does; a stop that cannot reach its plugin leaves a note.</summary>
    public void StopActivity(StopTarget kind)
    {
        switch (kind)
        {
            case StopTarget.Travel:
                links.StopTravel();
                break;
            case StopTarget.Questionable:
                QuestionableActions?.Stop(MainWindow.QuestionableHost);
                break;
            case StopTarget.AutoDuty when AutoDuty is { } autoDuty:
                if (!autoDuty.Stop())
                {
                    ShowCompanionNote(Strings.AutoDutyUnreachable);
                }

                break;
            case StopTarget.Artisan when Artisan is { } artisan:
                if (!artisan.Stop())
                {
                    ShowCompanionNote(Strings.ArtisanStopFailed);
                }

                break;
        }
    }

    /// <summary>Leaves <paramref name="note"/> in the status bar for a few seconds: the answer to a hand-off button just pressed.</summary>
    private void ShowCompanionNote(string note) => Activity.Note(note, ImGui.GetTime());

    /// <summary>"Going to giver · Teleporting…", composed once per step and language.</summary>
    private string TravelLine(GoToGiverStep step)
    {
        if (step != travelLineStep || travelLineLanguage != Localization.Loc.Version || travelLine.Length == 0)
        {
            travelLineStep = step;
            travelLineLanguage = Localization.Loc.Version;
            var text = step switch
            {
                GoToGiverStep.Teleporting => Strings.ActionTravelStepTeleporting,
                GoToGiverStep.Hopping => Strings.ActionTravelStepHopping,
                GoToGiverStep.PreparingPath => Strings.ActionTravelStepPreparing,
                _ => Strings.ActionTravelStepWalking,
            };
            travelLine = string.Format(CultureInfo.CurrentCulture, Strings.ActionTravelStatusFormat, text);
        }

        return travelLine;
    }
}
