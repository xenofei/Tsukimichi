using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Triad;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Triple Triad card's lines for the viewed character (feature plan v7, 1.21.0 P6; spec-1.21 P6;
/// <see cref="TriadBoard"/>): the caption ("11 opponents to unlock") and its hover, the chips' counts, the rows of
/// "Plays you" and "Locked behind a quest" with every word composed, and the opponents past the story point. Built when
/// the session version, the catalog, the language, the viewed character, the opponents or the spoiler shield changes;
/// drawing allocates nothing. Every name goes through the spoiler shield (1.20 N6): an opponent past the story point
/// is "An opponent ahead · a zone ahead · after Main scenario quest (Lv 90)". Framework thread only.
/// </summary>
public sealed class TriadBoardSource
{
    private static readonly IReadOnlyList<Row> NoRows = [];

    private readonly SessionState session;
    private readonly Func<TriadOpponents?> opponents;
    private readonly GameLinks links;

    private (int Version, GameData.CatalogBundle? Bundle, int Language, ulong? Viewed, TriadOpponents? Index, int Spoilers) builtKey = (-1, null, -1, null, null, 0);

    private bool visible;
    private TriadBoardModel model = TriadBoardModel.Empty;
    private string caption = string.Empty;
    private string captionTip = string.Empty;
    private string playsChip = string.Empty;
    private string lockedChip = string.Empty;
    private string cardsChip = string.Empty;
    private string firstName = string.Empty;
    private IReadOnlyList<Row> plays = NoRows;
    private IReadOnlyList<Row> named = NoRows;
    private IReadOnlyList<Row> masked = NoRows;

    public TriadBoardSource(SessionState session, Func<TriadOpponents?> opponents, GameLinks links)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.opponents = opponents ?? throw new ArgumentNullException(nameof(opponents));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
    }

    /// <summary>
    /// One opponent as the card draws it: the name (or "An opponent ahead"), the place (or "a zone ahead"), line 2's
    /// runs, the quest it waits on (null when none, or when masked), and the aetheryte nearest it (0 when unknown or
    /// masked: a masked row has no actions).
    /// </summary>
    public sealed record Row(TriadRow Model, string Name, string Place, BoardRun[] Line2, QuestRecord? Quest, uint AetheryteId, string AetheryteName)
    {
        public TriadOpponent Opponent => Model.Opponent;

        public bool Masked => Model.Masked;

        /// <summary>
        /// For a masked row, what its placeholder hides (the shield's hover and right-click): the quest past the story
        /// point it waits on, revealed with its names ("Reveal this name" stands for that quest), or the hidden zone it
        /// stands in. Null for a row the shield does not mask.
        /// </summary>
        public (SpoilerKind Kind, string Name, QuestRecord? Quest, bool StandIn)? Shield { get; init; }
    }

    /// <summary>Whether the card has anything to show: the opponents are read and a character is viewed.</summary>
    public bool Visible
    {
        get
        {
            Refresh();
            return visible;
        }
    }

    /// <summary>The board behind the rows.</summary>
    public TriadBoardModel Model
    {
        get
        {
            Refresh();
            return model;
        }
    }

    /// <summary>"11 opponents to unlock"; empty when none waits on a quest.</summary>
    public string Caption
    {
        get
        {
            Refresh();
            return caption;
        }
    }

    /// <summary>The caption's hover: how many opponents are finished (they leave the list).</summary>
    public string CaptionTip
    {
        get
        {
            Refresh();
            return captionTip;
        }
    }

    /// <summary>"Plays you 2" ("Plays you" before the records are read), "Locked 11", "Cards left 9".</summary>
    public (string PlaysYou, string Locked, string CardsLeft) Chips
    {
        get
        {
            Refresh();
            return (playsChip, lockedChip, cardsChip);
        }
    }

    /// <summary>The viewed character's first name, for "6 more past Kiri's story".</summary>
    public string FirstName
    {
        get
        {
            Refresh();
            return firstName;
        }
    }

    /// <summary>The opponents that play the character and still have something for it; empty before the records are read.</summary>
    public IReadOnlyList<Row> PlaysYou
    {
        get
        {
            Refresh();
            return plays;
        }
    }

    /// <summary>The opponents behind a quest the card names, in order.</summary>
    public IReadOnlyList<Row> Named
    {
        get
        {
            Refresh();
            return named;
        }
    }

    /// <summary>The opponents past the story point, by the level of their quest.</summary>
    public IReadOnlyList<Row> Masked
    {
        get
        {
            Refresh();
            return masked;
        }
    }

    private void Refresh()
    {
        var bundle = session.Bundle;
        var index = opponents();
        var spoilers = session.Spoilers;
        var key = (session.Version, bundle, Localization.Loc.Version, session.ViewedContentId, index, spoilers.Fingerprint);
        if (key == builtKey)
        {
            return;
        }

        builtKey = key;
        visible = false;
        model = TriadBoardModel.Empty;
        caption = captionTip = playsChip = lockedChip = cardsChip = firstName = string.Empty;
        plays = named = masked = NoRows;
        if (bundle is null || index is not { Count: > 0 } || session.ViewedSnapshot is not { } snapshot)
        {
            return;
        }

        visible = true;
        var states = session.States;
        model = TriadBoard.Build(index, bundle.Catalog, states, snapshot.TriadRecords, spoilers.IsMasked, zone => spoilers.IsNameMasked(SpoilerKind.Area, zone));
        firstName = FirstNameOf(snapshot.Name);
        caption = model.ToUnlock switch
        {
            0 => string.Empty,
            1 => Strings.TriadToUnlockOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.TriadToUnlockFormat, model.ToUnlock),
        };
        captionTip = model.Done switch
        {
            0 => Strings.TriadDoneNone,
            1 => Strings.TriadDoneOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.TriadDoneFormat, model.Done),
        };
        playsChip = model.HasRecords ? string.Format(CultureInfo.CurrentCulture, Strings.TriadChipPlaysYouFormat, model.PlaysYou.Count) : Strings.TriadChipPlaysYou;
        lockedChip = string.Format(CultureInfo.CurrentCulture, Strings.TriadChipLockedFormat, model.ToUnlock);
        cardsChip = string.Format(CultureInfo.CurrentCulture, Strings.TriadChipCardsLeftFormat, model.WithCardsLeft);

        var playRows = new List<Row>(model.PlaysYou.Count);
        if (model.HasRecords)
        {
            foreach (var row in model.PlaysYou)
            {
                playRows.Add(Build(row, states));
            }
        }

        var namedRows = new List<Row>();
        var maskedRows = new List<Row>();
        foreach (var row in model.Locked)
        {
            (row.Masked ? maskedRows : namedRows).Add(Build(row, states));
        }

        plays = playRows;
        named = namedRows;
        masked = maskedRows;
    }

    private Row Build(TriadRow row, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        var opponent = row.Opponent;
        if (row.Masked)
        {
            // Name, place and quest all hidden; the quest by its placeholder, which says only its level.
            BoardRun[] runs = row.Quest is { } hidden
                ? [new BoardRun(Strings.TriadAfter), new BoardRun(session.Spoilers.DisplayName(hidden))]
                : [new BoardRun(Strings.TriadWaitsForStory)];
            return new Row(row, Strings.TriadOpponentAhead, Strings.TriadZoneAhead, runs, null, 0, string.Empty) { Shield = ShieldOf(row) };
        }

        // The people and places through the wider shield (1.20 N6), as every other name on the card.
        var spoilers = session.Spoilers;
        var name = Capitalize(spoilers.Name(SpoilerKind.Npc, opponent.Name));
        var place = spoilers.Name(SpoilerKind.Area, opponent.Zone);
        var near = opponent.Spot is { } spot ? links.Aetherytes.Nearest(spot.TerritoryId, spot.X, spot.Z) : null;
        var aetheryteName = near is null ? string.Empty : session.Spoilers.Name(SpoilerKind.Area, near.Name);
        BoardRun[] line2;
        if (row.Group == TriadGroup.PlaysYou)
        {
            line2 = [new BoardRun(PlaysLine(row))];
        }
        else if (row.Quest is { } quest)
        {
            var evaluation = states.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            var reason = evaluation is null ? string.Empty : BlockerText.Reason(evaluation, quest, session.Names, states);
            var stateName = Strings.StateName(state, quest);
            var tail = reason.Length > 0 && state != QuestState.Accepted
                ? string.Format(CultureInfo.CurrentCulture, Strings.TriadStateReasonFormat, stateName, reason)
                : string.Format(CultureInfo.CurrentCulture, Strings.TriadStateFormat, stateName);
            line2 = [new BoardRun(Strings.TriadAfter), new BoardRun(session.Spoilers.DisplayName(quest), state), new BoardRun(tail)];
        }
        else
        {
            line2 = [new BoardRun(Strings.TriadWaitsForStory)];
        }

        return new Row(row, name, place, line2, row.Quest, near?.RowId ?? 0, aetheryteName);
    }

    /// <summary>
    /// What a masked row's placeholder hides: the first quest of its gate the shield masks (its reveal is that quest's,
    /// which unmasks the row), else the zone it stands in when that is the hidden name.
    /// </summary>
    private (SpoilerKind Kind, string Name, QuestRecord? Quest, bool StandIn)? ShieldOf(TriadRow row)
    {
        var spoilers = session.Spoilers;
        var opponent = row.Opponent;
        foreach (var rowId in opponent.Gate.QuestIds)
        {
            if (spoilers.IsMasked(rowId) && session.Bundle?.Catalog.GetByRowId(rowId) is { } quest)
            {
                return (SpoilerKind.Npc, opponent.Name, quest, true);
            }
        }

        return opponent.Zone.Length > 0 && spoilers.IsNameMasked(SpoilerKind.Area, opponent.Zone)
            ? (SpoilerKind.Area, opponent.Zone, row.Quest, false)
            : null;
    }

    /// <summary>"3 cards you don't have", "beaten · 2 cards left", "not beaten yet · every card yours".</summary>
    private static string PlaysLine(TriadRow row)
    {
        var left = Math.Max(0, row.CardsLeft);
        if (row.Beaten == true)
        {
            return left == 1 ? Strings.TriadBeatenCardsOne : string.Format(CultureInfo.CurrentCulture, Strings.TriadBeatenCardsFormat, left);
        }

        return left switch
        {
            0 => Strings.TriadNotBeatenAllCards,
            1 => Strings.TriadCardsOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.TriadCardsFormat, left),
        };
    }

    /// <summary>A role the game writes in lower case ("indolent imperial") starts a row with a capital, as a person's name does.</summary>
    internal static string Capitalize(string name) =>
        name.Length > 0 && char.IsLower(name[0]) ? char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..] : name;

    private static string FirstNameOf(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
