using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// EXP and gil at the top of the Rewards section (1.9.0, R6 G): "12,345 EXP · 1,200 gil", or a range for a Quest Sync
/// quest ("54,000–57,240 EXP"), from the game's own formula (<see cref="QuestExp"/>). The EXP is left out, never
/// guessed, where the formula does not hold (allied society and seasonal event quests, a catalog without its
/// <c>ParamGrow</c> rows); the tooltip says why. Built once per quest, catalog and language.
/// </summary>
public sealed partial class DetailPane
{
    private uint expRowId = uint.MaxValue;
    private CatalogBundle? expBundle;
    private int expLanguage = -1;
    private string expLine = string.Empty;
    private string expTooltip = string.Empty;

    /// <summary>Whether the quest gives EXP or gil (the line is built once per quest, catalog and language).</summary>
    private bool HasExpAndGil(QuestRecord quest)
    {
        if (model.Bundle is not { } bundle)
        {
            return false;
        }

        if (expRowId != quest.RowId || !ReferenceEquals(expBundle, bundle) || expLanguage != Localization.Loc.Version)
        {
            expRowId = quest.RowId;
            expBundle = bundle;
            expLanguage = Localization.Loc.Version;
            (expLine, expTooltip) = ExpAndGil(quest, QuestExp.For(quest, bundle.ExpTable));
        }

        return expLine.Length > 0;
    }

    /// <summary>The EXP and gil line, when the quest gives either; returns whether it drew one.</summary>
    private bool DrawExpAndGil(QuestRecord quest)
    {
        if (!HasExpAndGil(quest))
        {
            return false;
        }

        TextFlow.Wrapped(expLine, RoomTo(cardRight));
        if (expTooltip.Length > 0 && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(expTooltip);
        }

        ImGui.Spacing();
        return true;
    }

    /// <summary>The line and its tooltip; both empty when the quest gives neither EXP nor gil.</summary>
    private static (string Line, string Tooltip) ExpAndGil(QuestRecord quest, ExpReward exp)
    {
        var culture = CultureInfo.CurrentCulture;
        var expText = exp.Kind switch
        {
            ExpKind.Fixed => string.Format(culture, Strings.PlanningExpFormat, exp.Min.ToString("N0", culture)),
            ExpKind.Range => string.Format(culture, Strings.PlanningExpRangeFormat, exp.Min.ToString("N0", culture), exp.Max.ToString("N0", culture)),
            _ => string.Empty,
        };
        var gilText = quest.Gil > 0 ? string.Format(culture, Strings.PlanningGilFormat, quest.Gil.ToString("N0", culture)) : string.Empty;
        var line = expText.Length > 0 && gilText.Length > 0 ? expText + Strings.StateReasonSeparator + gilText : expText + gilText;
        if (line.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var tooltip = exp.Kind switch
        {
            ExpKind.Fixed => Strings.PlanningExpTooltip,
            ExpKind.Range => string.Format(culture, Strings.PlanningExpRangeTooltipFormat, quest.DisplayLevel, quest.LevelMax),
            ExpKind.Unknown when quest.BeastTribe != 0 || quest.Festival != 0 => Strings.PlanningExpVariesTooltip,
            _ => string.Empty,
        };
        return (line, tooltip);
    }
}
