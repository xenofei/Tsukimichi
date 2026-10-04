using System;
using System.Collections.Generic;
using System.Text;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Who's in it, in words and plates (feature plan v7 N10; spec-1.21 N10): "With Thancred, Urianger and a familiar face"
/// for the detail pane's Cast line and the Journal row's hover. Only characters the viewed character has met in the main
/// scenario (<see cref="StoryCast.HasMet"/>) are named and wear a plate with their face (the portrait index's, by the
/// quest's era, under 1.15's spoiler rule) or their initials; everyone else is "a familiar face" with the moon disc,
/// never their face.
/// </summary>
internal static class CastText
{
    /// <summary>The Cast line for one quest: its plates (met characters first, in script order), their names for the plate hover, and the words.</summary>
    internal sealed record Composed(PortraitRequest[] Plates, string[] PlateNames, string Text);

    // The hover line, kept for the last row asked so a held hover composes nothing.
    private static (uint RowId, int Version, int Shield, int Language, StoryCast? Cast) hoverKey;
    private static string hoverText = string.Empty;

    /// <summary>The Cast line for <paramref name="quest"/>; null when it features no recurring story character.</summary>
    public static Composed? Compose(StoryCast cast, QuestRecord quest, SessionState session)
    {
        ArgumentNullException.ThrowIfNull(cast);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(session);
        var line = Line(cast, quest.RowId, session);
        if (line.IsEmpty)
        {
            return null;
        }

        var index = GiverPortraits.Index?.Invoke() ?? PortraitIndex.Empty;
        var spoilers = session.Spoilers;
        var shieldOn = spoilers.Options.HideNames || spoilers.Options.HideArtwork;
        var era = quest.Festival != 0 ? PortraitIndex.SeasonalEra : quest.Expansion;
        var plates = new PortraitRequest[line.Met.Count + line.Familiar];
        var names = new string[plates.Length];
        for (var i = 0; i < line.Met.Count; i++)
        {
            var member = line.Met[i];
            plates[i] = PlateFor(member, index, era, shieldOn, spoilers.ReachExpansion);
            names[i] = member.Name;
        }

        for (var i = line.Met.Count; i < plates.Length; i++)
        {
            plates[i] = PortraitRequest.None;
            names[i] = Strings.CastFamiliarFace;
        }

        return new Composed(plates, names, Words(line));
    }

    /// <summary>"With Thancred and Urianger" for the Journal row's hover; empty for a quest without a cast.</summary>
    public static string Hover(SessionState? session, uint rowId)
    {
        if (session?.Bundle?.Cast is not { } cast || !cast.HasCast(rowId))
        {
            return string.Empty;
        }

        var key = (rowId, session.Version, session.Spoilers.Fingerprint, Localization.Loc.Version, cast);
        if (key != hoverKey)
        {
            hoverKey = key;
            hoverText = Words(Line(cast, rowId, session));
        }

        return hoverText;
    }

    private static CastLine Line(StoryCast cast, uint rowId, SessionState session)
    {
        var states = session.States;
        return cast.Line(rowId, id => states.TryGetValue(id, out var evaluation) && evaluation.State == QuestState.Completed);
    }

    /// <summary>"With A", "With A and B", "With A, B and a familiar face", "With familiar faces"; empty for none.</summary>
    internal static string Words(CastLine line)
    {
        var items = new List<string>(line.Met.Count + 1);
        foreach (var member in line.Met)
        {
            items.Add(member.Name);
        }

        if (line.Familiar == 1)
        {
            items.Add(Strings.CastFamiliarFace);
        }
        else if (line.Familiar > 1)
        {
            items.Add(Strings.CastFamiliarFaces);
        }

        if (items.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder(Strings.CastWith);
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                text.Append(i == items.Count - 1 ? Strings.CastAnd : Strings.CastComma);
            }

            text.Append(items[i]);
        }

        return text.ToString();
    }

    /// <summary>
    /// A met character's plate: the portrait index's face for one of their ENpcResident rows it knows, by the quest's era
    /// and the shield's reach, else their initials.
    /// </summary>
    private static PortraitRequest PlateFor(CastMember member, PortraitIndex index, byte era, bool shieldOn, byte reach)
    {
        foreach (var npc in member.NpcIds)
        {
            if (index.NameOf(npc).Length == 0)
            {
                continue;
            }

            var portrait = index.For(npc, era, 0);
            if (!portrait.HasArt)
            {
                break;
            }

            var allowed = GiverPortraits.Enabled && PortraitPlate.FaceAllowed(portrait.Era, false, shieldOn, reach);
            return new PortraitRequest(portrait, allowed);
        }

        var initials = new PortraitFallback(PortraitFallbackKind.Initials, 0, 0, 0, PortraitNames.Initials(member.Key));
        return new PortraitRequest(new PortraitRef(0, 0, PortraitSource.None, PortraitCrop.Full, era, initials), false);
    }
}
