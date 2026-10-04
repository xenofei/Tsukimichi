using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Advanced › Diagnostics, "The game's own offers" (feature plan v7, C1): how many quests the game showed
/// the logged-in character that Tsukimichi reads as not available, how many it confirmed that Tsukimichi could not
/// check, and how many Ready quests it did not show in their giver's zone; "Copy game checks" copies one line per
/// quest for a bug report. Rebuilt when the offers or the session change.
/// </summary>
public sealed partial class ConfigWindow
{
    private int gameOffersVersion = -1;
    private int gameOffersSession = -1;
    private int gameOffersLanguage = -1;
    private string gameOffersLine = string.Empty;
    private List<GameDisagreement> gameOffersRows = [];

    /// <summary>The block under the refresh timing; nothing without the observer.</summary>
    private void DrawGameOffers()
    {
        if (diagnostics.Offers is not { } offers)
        {
            return;
        }

        if (gameOffersVersion != offers.Version || gameOffersSession != session.Version || gameOffersLanguage != Loc.Version)
        {
            gameOffersVersion = offers.Version;
            gameOffersSession = session.Version;
            gameOffersLanguage = Loc.Version;
            gameOffersRows = offers.Disagreements();
            gameOffersLine = session.LiveContentId is null
                ? Strings.GameOffersNotLive
                : gameOffersRows.Count == 0
                    ? Strings.GameOffersNone
                    : string.Format(CultureInfo.CurrentCulture, Strings.GameOffersCountFormat, Count(GameOfferVerdict.Disagrees), Count(GameOfferVerdict.Confirms), Count(GameOfferVerdict.Unseen));
        }

        using (Typography.Caption())
        using (Theme.PushText(Count(GameOfferVerdict.Disagrees) > 0 ? Theme.Surface.Text : Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(gameOffersLine);
        }

        HintOnHover(Strings.GameOffersTooltip);
        if (gameOffersRows.Count == 0)
        {
            return;
        }

        if (ImGui.SmallButton(Strings.GameOffersCopy))
        {
            DiagnosticBuilder.TryCopy(GameOfferChecks.Report(gameOffersRows, session.LiveNames, session.LiveStates), Plugin.Log);
        }

        HintOnHover(Strings.GameOffersCopyTooltip);
    }

    private int Count(GameOfferVerdict verdict)
    {
        var count = 0;
        foreach (var row in gameOffersRows)
        {
            if (row.Check.Verdict == verdict)
            {
                count++;
            }
        }

        return count;
    }
}
