using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// Every character the alt lists show, in their one stable order (1.8.0, R7 B, E, H; <see cref="CharacterList"/>): the
/// stored snapshots plus the character logged in here even when it has no file (not tracked, or not saved yet), with
/// its world and data center names, whether it is live here or in another client, hidden or not tracked. Rebuilt on
/// the framework thread when the stored list or the viewed character (<see cref="SessionState.RosterVersion"/>, which
/// moves with <see cref="SessionState.CharactersChanged"/>) or the character settings change; reading <see cref="All"/>
/// costs two compares otherwise. Another client's save never moves a row: the order does not read the capture time. The character switcher, the Characters pane, the collection grid and Settings › Data read it.
/// </summary>
public sealed class CharacterRoster
{
    private readonly SessionState session;
    private readonly CharacterSettingsBook settings;
    private readonly IDataManager? data;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, (string World, string DataCenter)> worlds = [];

    private List<CharacterEntry> all = [];
    private int sessionVersion = -1;
    private int settingsVersion = -1;

    /// <param name="data">Resolves world and data center names from the World sheet; without it the world id is shown and nothing is grouped.</param>
    public CharacterRoster(SessionState session, CharacterSettingsBook settings, IDataManager? data, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.data = data;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Bumps when <see cref="All"/> reads differently (a character added, gone, moved, saved, hidden, logged in elsewhere).</summary>
    public int Version
    {
        get
        {
            Refresh();
            return version;
        }
    }

    private int version;

    /// <summary>Every character in list order, hidden ones included (each says so).</summary>
    public IReadOnlyList<CharacterEntry> All
    {
        get
        {
            Refresh();
            return all;
        }
    }

    /// <summary>The settings every list reads (hidden, not tracked, the Compare target).</summary>
    public CharacterSettingsBook Settings => settings;

    private void Refresh()
    {
        if (sessionVersion == session.RosterVersion && settingsVersion == settings.Version)
        {
            return;
        }

        sessionVersion = session.RosterVersion;
        settingsVersion = settings.Version;
        var stored = session.Characters;
        var entries = new List<CharacterEntry>(stored.Count + 1);
        var live = session.LiveSnapshot;
        var liveListed = false;
        foreach (var c in stored)
        {
            var here = c.ContentId == session.LiveContentId;
            liveListed |= here;
            entries.Add(Entry(c.ContentId, c.Name, c.World, c.TakenUtc, c.CompletedCount, here));
        }

        if (live is not null && !liveListed)
        {
            // Logged in and never saved (not tracked, or the first save still pending): listed all the same.
            entries.Add(Entry(live.ContentId, live.Name, live.World, live.TakenUtc, CountBits(live.CompletedBits), true));
        }

        var sorted = CharacterList.Sort(entries);
        if (sorted.Count == all.Count && System.Linq.Enumerable.SequenceEqual(sorted, all))
        {
            return;
        }

        all = sorted;
        version++;
    }

    private CharacterEntry Entry(ulong id, string name, uint world, DateTime taken, int completed, bool here)
    {
        var (worldName, dataCenter) = Lookup(world);
        return new CharacterEntry(
            id,
            name,
            world,
            worldName,
            dataCenter,
            taken,
            completed,
            here,
            !here && session.IsLiveElsewhere(id),
            settings.IsHidden(id),
            settings.IsTracked(id));
    }

    private (string World, string DataCenter) Lookup(uint world)
    {
        if (worlds.TryGetValue(world, out var known))
        {
            return known;
        }

        var result = (world.ToString(CultureInfo.InvariantCulture), string.Empty);
        if (data is not null)
        {
            try
            {
                if (data.GetExcelSheet<World>().GetRowOrDefault(world) is { } row)
                {
                    var name = row.Name.ExtractText();
                    var center = row.DataCenter.ValueNullable?.Name.ExtractText() ?? string.Empty;
                    result = (name.Length == 0 ? result.Item1 : name, center);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "World sheet lookup for {WorldId} failed", world);
            }
        }

        worlds[world] = result;
        return result;
    }

    private static int CountBits(byte[] bits)
    {
        var count = 0;
        foreach (var b in bits)
        {
            count += System.Numerics.BitOperations.PopCount(b);
        }

        return count;
    }
}
