using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// EXP and gil at the top of the Rewards section (1.9.0, R6 G): "12,345 EXP" and "1,200 gil", each after its game icon
/// (1.14.0, UI-5a), or a range for a Quest Sync quest ("54,000–57,240 EXP"), from the game's own formula
/// (<see cref="QuestExp"/>). The EXP is left out, never guessed, where the formula does not hold (allied society and
/// seasonal event quests, a catalog without its <c>ParamGrow</c> rows); the tooltip says why. Built once per quest,
/// catalog and language.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>The EXP and gil icons' side over the text line's height: a touch taller, as the game's reward lists draw them.</summary>
    private const float AmountIconScale = 1.2f;

    private uint expRowId = uint.MaxValue;
    private CatalogBundle? expBundle;
    private int expLanguage = -1;
    private string expText = string.Empty;
    private string gilText = string.Empty;
    private string expTooltip = string.Empty;

    /// <summary>Whether the quest gives EXP or gil (the texts are built once per quest, catalog and language).</summary>
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
            (expText, gilText, expTooltip) = ExpAndGil(quest, QuestExp.For(quest, bundle.ExpTable));
        }

        return expText.Length > 0 || gilText.Length > 0;
    }

    /// <summary>
    /// The EXP and gil line, each amount after its game icon (the EXP laurel, the gil coin), the gil under the EXP when
    /// the card is too narrow for both; returns whether it drew one.
    /// </summary>
    private bool DrawExpAndGil(QuestRecord quest)
    {
        if (!HasExpAndGil(quest))
        {
            return false;
        }

        ImGui.BeginGroup();
        var drew = DrawAmount(RewardIcons.Exp, expText, after: false);
        DrawAmount(RewardIcons.Gil, gilText, after: drew);
        ImGui.EndGroup();
        if (expTooltip.Length > 0 && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(expTooltip);
        }

        ImGui.Spacing();
        return true;
    }

    /// <summary>One amount: its icon centred on the text line, then the text; nothing when the text is empty.</summary>
    private bool DrawAmount(uint icon, string text, bool after)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var side = MathF.Round(lineHeight * AmountIconScale);
        var gap = UiMetrics.Px(5f);
        var width = side + gap + ImGui.CalcTextSize(text).X;
        if (after)
        {
            ImGui.SameLine(0f, UiMetrics.Px(16f));
            if (ImGui.GetCursorScreenPos().X + width > cardRight)
            {
                ImGui.NewLine();
            }
        }

        var min = ImGui.GetCursorScreenPos();
        var height = MathF.Max(side, lineHeight);
        ImGui.Dummy(new Vector2(width, height));
        var dl = ImGui.GetWindowDrawList();
        var iconMin = new Vector2(min.X, min.Y + MathF.Round((height - side) * 0.5f));
        GameIcon.DrawAt(dl, textures, icon, iconMin, iconMin + new Vector2(side));
        dl.AddText(new Vector2(min.X + side + gap, min.Y + MathF.Round((height - lineHeight) * 0.5f)), ImGui.GetColorU32(ImGuiCol.Text), text);
        return true;
    }

    /// <summary>The EXP text, the gil text and the tooltip; both texts empty when the quest gives neither.</summary>
    private static (string Exp, string Gil, string Tooltip) ExpAndGil(QuestRecord quest, ExpReward exp)
    {
        var culture = CultureInfo.CurrentCulture;
        var expText = exp.Kind switch
        {
            ExpKind.Fixed => string.Format(culture, Strings.PlanningExpFormat, exp.Min.ToString("N0", culture)),
            ExpKind.Range => string.Format(culture, Strings.PlanningExpRangeFormat, exp.Min.ToString("N0", culture), exp.Max.ToString("N0", culture)),
            _ => string.Empty,
        };
        var gilText = quest.Gil > 0 ? string.Format(culture, Strings.PlanningGilFormat, quest.Gil.ToString("N0", culture)) : string.Empty;
        if (expText.Length == 0 && gilText.Length == 0)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var tooltip = exp.Kind switch
        {
            ExpKind.Fixed => Strings.PlanningExpTooltip,
            ExpKind.Range => string.Format(culture, Strings.PlanningExpRangeTooltipFormat, quest.DisplayLevel, quest.LevelMax),
            ExpKind.Unknown when quest.BeastTribe != 0 || quest.Festival != 0 => Strings.PlanningExpVariesTooltip,
            _ => string.Empty,
        };
        return (expText, gilText, tooltip);
    }
}
