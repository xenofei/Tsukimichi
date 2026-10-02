using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.STD;
using FFXIVClientStructs.STD.Helper;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// Today's allied society daily offer for the logged-in character (decision 5 of feature plan v5), from the game's own
/// calculation: <c>EventFramework.DailyQuests</c> maps each quest giver the client has loaded (keyed by the giver's
/// ENpc id) to its society, rank bounds and quest lists, and <c>DailyQuestMap.CalculateAvailableQuests</c> picks the
/// day's quests from them with <c>QuestManager.DailyQuestSeed</c>, the rank and the ranked-up-today mark, as the giver
/// does when spoken to. Each answer goes into a <see cref="DailyOfferBook"/> that keeps it for the rest of the day, so
/// societies whose givers were loaded once today stay known after the character leaves their zone.
/// <para>
/// Safety: nothing runs while the shared <see cref="HookGate"/> holds the game hooks (an untested game version), when
/// the function's signature did not resolve, or for a giver whose loaded quest list does not hold every daily the
/// catalog gives it at the character's rank; the offer then stays unknown for that society and its dailies read as
/// before (not held back). An answer naming a quest the catalog does not give that giver is dropped, and the book
/// drops a society whose computed offer misses a daily the character took today. Framework thread only.
/// </para>
/// </summary>
public sealed unsafe class DailyOfferReader
{
    /// <summary>Prefix of the log lines that name each giver's computed offer, for comparing with the game in person.</summary>
    public const string LogPrefix = "[daily offer]";

    /// <summary>Results the game function writes at most (one giver offers up to three dailies).</summary>
    private const int MaxResults = 3;

    private readonly IPluginLog log;
    private readonly DailyOfferBook book = new();
    private readonly HashSet<uint> incompleteLogged = [];
    private bool unresolvedLogged;
    private bool failureLogged;

    public DailyOfferReader(IPluginLog log)
    {
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The addon kill switch; null (not set yet) reads no offer.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>Forgets every giver's answer (a logout or another character).</summary>
    public void Clear() => book.Clear();

    /// <summary>
    /// Reads the givers the client has loaded that the book does not know yet and returns the offer the book can
    /// assemble for <paramref name="snapshot"/>'s character; <see cref="DailyOffer.None"/> while the gate holds the
    /// hooks or nothing is known. Never throws for a game-side failure: it is logged once and the offer stays unknown.
    /// </summary>
    public DailyOffer Read(QuestCatalog catalog, CharacterSnapshot snapshot, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Gate is not { HooksAllowed: true })
        {
            return DailyOffer.None;
        }

        try
        {
            ReadGivers(catalog, snapshot, nowUtc);
        }
        catch (Exception ex)
        {
            if (!failureLogged)
            {
                failureLogged = true;
                log.Warning(ex, "{Prefix} reading the allied society offer failed; dailies are not held back", LogPrefix);
            }
        }

        var offer = book.Offer(catalog, snapshot, nowUtc);
        foreach (var tribe in book.Mismatched)
        {
            log.Warning("{Prefix} the computed offer for allied society {Tribe} misses a daily taken today; its dailies are not held back", LogPrefix, tribe);
        }

        return offer;
    }

    private void ReadGivers(QuestCatalog catalog, CharacterSnapshot snapshot, DateTime nowUtc)
    {
        if (DailyQuestMap.Addresses.CalculateAvailableQuests.Value == 0)
        {
            if (!unresolvedLogged)
            {
                unresolvedLogged = true;
                log.Warning("{Prefix} DailyQuestMap.CalculateAvailableQuests did not resolve; the daily offer stays unknown", LogPrefix);
            }

            return;
        }

        var ef = EventFramework.Instance();
        var qm = QuestManager.Instance();
        var control = Control.Instance();
        if (ef == null || qm == null || control == null || control->LocalPlayer == null)
        {
            return;
        }

        var map = &ef->DailyQuests;
        var player = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)control->LocalPlayer;
        var seed = qm->DailyQuestSeed;
        var head = map->Entries.WithOps.Tree.Head;
        if (head == null)
        {
            return;
        }

        var results = stackalloc QuestEventHandler*[MaxResults];
        for (var node = head->_Left; node != null && node != head && !node->_Isnil; node = node->Next())
        {
            var giver = node->_Myval.Item1;
            var entry = &node->_Myval.Item2;
            var tribe = entry->TribeId;
            var standing = snapshot.Tribes.GetValueOrDefault(tribe);
            if (tribe == 0 || standing.Rank == 0 || book.Knows(snapshot.ContentId, nowUtc, giver, standing))
            {
                continue;
            }

            var dailies = DailyOfferBook.DailiesOf(catalog, giver);
            if (dailies.Count == 0 || !Covers(entry, dailies, tribe, standing.Rank))
            {
                if (dailies.Count > 0 && incompleteLogged.Add(giver))
                {
                    log.Debug("{Prefix} giver {Giver} (allied society {Tribe}) has not loaded every daily of its rank yet; skipped", LogPrefix, giver, tribe);
                }

                continue;
            }

            for (var i = 0; i < MaxResults; i++)
            {
                results[i] = null;
            }

            // rankInRange: the character outranks every rank the giver's quests ask for (ClientStructs: "rank greater
            // than the largest min rank requirement"), which merges the exclusive pool into the normal one.
            var rankInRange = standing.Rank > entry->RankRequirementMax;
            var found = map->CalculateAvailableQuests(player, node, seed, rankInRange, standing.Rank, standing.RankedUpToday, results);
            if (found is < 0 or > MaxResults)
            {
                continue;
            }

            var quests = new List<ushort>((int)found);
            var valid = true;
            for (var i = 0; i < found; i++)
            {
                var handler = results[i];
                if (handler == null || !Gives(dailies, handler->QuestId))
                {
                    valid = false;
                    break;
                }

                quests.Add(handler->QuestId);
            }

            if (!valid)
            {
                log.Debug("{Prefix} giver {Giver} returned a quest it does not give; answer dropped", LogPrefix, giver);
                continue;
            }

            book.Record(snapshot.ContentId, nowUtc, giver, tribe, standing, quests);
            log.Information(
                "{Prefix} society {Tribe} giver {Giver} at rank {Rank}{RankedUp}, seed {Seed}: {Quests}",
                LogPrefix,
                tribe,
                giver,
                standing.Rank,
                standing.RankedUpToday ? " (ranked up today)" : string.Empty,
                seed,
                quests.Count == 0 ? "none" : string.Join(", ", quests));
        }
    }

    /// <summary>
    /// Whether the giver's loaded quest lists hold every daily the catalog gives it at or below <paramref name="rank"/>,
    /// for the society the catalog names: the game picks among the loaded handlers, so a missing one would make the
    /// answer differ from the giver's.
    /// </summary>
    private static bool Covers(DailyQuestMap.Entry* entry, IReadOnlyList<QuestRecord> dailies, byte tribe, byte rank)
    {
        var loaded = new HashSet<ushort>();
        Collect(ref entry->HandlersNormal, loaded);
        Collect(ref entry->HandlersExclusive, loaded);
        foreach (var quest in dailies)
        {
            if (quest.BeastTribe != tribe)
            {
                return false;
            }

            if (quest.BeastRank <= rank && !loaded.Contains(quest.QuestId))
            {
                return false;
            }
        }

        return true;
    }

    private static void Collect(ref StdVector<FFXIVClientStructs.Interop.Pointer<QuestEventHandler>> handlers, HashSet<ushort> into)
    {
        var count = handlers.Count;
        for (var i = 0; i < count; i++)
        {
            var handler = handlers[i].Value;
            if (handler != null)
            {
                into.Add(handler->QuestId);
            }
        }
    }

    private static bool Gives(IReadOnlyList<QuestRecord> dailies, ushort questId)
    {
        foreach (var quest in dailies)
        {
            if (quest.QuestId == questId)
            {
                return true;
            }
        }

        return false;
    }
}
