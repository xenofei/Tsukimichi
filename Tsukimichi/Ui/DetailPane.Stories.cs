using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// 1.21's storyline lines in the detail pane (feature plan v7 P5, N10; spec-1.21): the chain line's curated name with
/// "Caught up · continues in a later patch" for an ongoing series done so far and "A side story ahead" for one past the
/// story point, and the Cast line under the hero: 20 px plates, then "With Tataru" (only characters the viewed character
/// has met in the main scenario are named; anyone else is "a familiar face" with the moon disc, never their face).
/// </summary>
public sealed partial class DetailPane
{
    // The Cast line, composed when the quest, the session or the shield change (never per frame).
    private (uint RowId, int Version, int Shield, int Language, StoryCast? Cast) castKey;
    private CastText.Composed? castLine;

    /// <summary>
    /// The chain line through the shield: a curated line nothing of which is done whose first quest lies past the story
    /// point reads "A side story ahead", a next quest past it "Sidequest (Lv 90)", and an ongoing series with every
    /// released quest done "Caught up · continues in a later patch" instead of "Chain complete".
    /// </summary>
    private void ShieldChainLine(SessionState session, CatalogBundle bundle, Chain chain)
    {
        var spoilers = session.Spoilers;
        if (chain.Ongoing && model.ChainKind == ChainNextKind.Complete)
        {
            model.ChainLabel = Strings.StoriesCaughtUpLine;
        }

        if ((chain.IsCurated || chain.IsStory) && chain.RowIds.Count > 0 && spoilers.IsAhead(chain.RowIds[0])
            && ChainCatalog.Progress(chain, session.States).Done == 0)
        {
            model.ChainText = Strings.StoriesAhead;
        }

        if (model.ChainNextName is not null && model.ChainNextRowId != 0 && spoilers.IsAhead(model.ChainNextRowId)
            && bundle.Catalog.GetByRowId(model.ChainNextRowId) is { } next && !spoilers.IsMasked(next))
        {
            model.ChainNextName = string.Format(CultureInfo.CurrentCulture, Strings.StoriesSideAheadFormat, next.DisplayLevel);
        }
    }

    /// <summary>
    /// The Cast line under the hero (22 px, only for a quest with a cast): a 20 px plate per character, met ones with
    /// their face where the portrait index has one (else initials), familiar faces with the moon disc; then "With
    /// Thancred, Urianger and a familiar face". The words wrap under the plates in a narrow pane.
    /// </summary>
    private void DrawCast(SessionState session, QuestRecord quest)
    {
        var cast = session.Bundle?.Cast;
        var key = (quest.RowId, session.Version, session.Spoilers.Fingerprint, Localization.Loc.Version, cast);
        if (key != castKey)
        {
            castKey = key;
            castLine = cast is null ? null : CastText.Compose(cast, quest, session);
        }

        // A quest the shield masks says nothing of who is in it.
        if (castLine is not { } line || model.NameMasked)
        {
            return;
        }

        var plate = UiMetrics.Px(20f);
        var height = MathF.Max(UiMetrics.Px(22f), ImGui.GetTextLineHeight());
        var start = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var x = start.X;
        var gap = UiMetrics.Px(3f);
        var hovered = -1;
        for (var i = 0; i < line.Plates.Length; i++)
        {
            var min = new Vector2(x, start.Y + MathF.Round((height - plate) * 0.5f));
            Chrome.Portrait(dl, min, plate, line.Plates[i]);
            if (ImGui.IsMouseHoveringRect(min, min + new Vector2(plate)) && ImGui.IsWindowHovered())
            {
                hovered = i;
            }

            x += plate + gap;
        }

        ImGui.Dummy(new Vector2(MathF.Max(0f, x - start.X), height));
        if (hovered >= 0)
        {
            UiMetrics.Tooltip(line.PlateNames[hovered], Strings.CastTooltip);
        }

        var words = line.Text;
        if (ImGui.GetItemRectMax().X + UiMetrics.Px(6f) + ImGui.CalcTextSize(words).X <= bodyRight)
        {
            ImGui.SameLine(0f, UiMetrics.Px(6f));
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, start.Y + MathF.Round((height - ImGui.GetTextLineHeight()) * 0.5f)));
        }

        TextFlow.Wrapped(words, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextSecondary));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.CastTooltip);
        }
    }
}
