using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The one New Game+ line (1.19.0, C4) the status bar, the Tonight card and the Todo overlay share: "New Game+ ·
/// Shadowbringers - Part 2 · quest 87 of 112", or as much as is known. Cached per session version and language.
/// </summary>
public sealed class NewGamePlusText
{
    private (int Version, int Language, CatalogBundle? Bundle, bool Shows) key = (-1, -1, null, false);
    private string line = string.Empty;

    /// <summary>
    /// The line for <paramref name="session"/>'s running New Game+ session; empty when none runs or it belongs to another
    /// character than the one viewed (<see cref="NewGamePlusSession.IsFor"/>).
    /// </summary>
    public string Line(SessionState session, CatalogBundle bundle)
    {
        var replay = session.NewGamePlus;
        var shows = replay.IsFor(session.ViewedContentId);
        var next = (replay.Version, Localization.Loc.Version, bundle, shows);
        if (next != key)
        {
            key = next;
            line = shows ? For(replay, bundle) : string.Empty;
        }

        return line;
    }

    /// <summary>The line for a running session.</summary>
    public static string For(NewGamePlusSession replay, CatalogBundle bundle)
    {
        NewGamePlusPosition? position = null;
        if (replay.QuestId != 0 && bundle.Catalog.TryGetByQuestId(replay.QuestId, out var quest))
        {
            position = bundle.NewGamePlusChapters.Position(quest.RowId, replay.Chapter);
        }

        if (position is null && replay.Replaying.Count > 0)
        {
            var rows = new List<uint>(replay.Replaying.Count);
            foreach (var id in replay.Replaying)
            {
                if (bundle.Catalog.TryGetByQuestId(id, out var replayed))
                {
                    rows.Add(replayed.RowId);
                }
            }

            position = bundle.NewGamePlusChapters.Guess(rows);
        }

        return position is { } p
            ? string.Format(CultureInfo.CurrentCulture, Strings.NewGamePlusStatusFormat, p.Name, p.Index, p.Count)
            : Strings.NewGamePlusStatus;
    }
}
