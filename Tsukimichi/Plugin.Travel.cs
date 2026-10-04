using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Tsukimichi;

/// <summary>
/// Travel wiring for getting there faster (travel review, 1.10): the mounts the character owns, for Settings ›
/// Integrations › Travel › Mount. Read through Dalamud's <c>IUnlockState</c> (no game call of Tsukimichi's own) and kept
/// for <see cref="OwnedMountsRefreshMs"/>, so the settings window reads the sheet at most every few seconds. Also the
/// travel preflight (feature plan v7 A9), which reads the game's movement type through <see cref="GameConfig"/>.
/// </summary>
public sealed partial class Plugin
{
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    /// <summary>The travel preflight (Settings › Automation › Travel › Before a walk); null until the plugin built it.</summary>
    private Game.TravelPreflightService? travelPreflight;

    /// <summary>How long the owned-mount list is reused before it is read again (a mount may be learnt meanwhile).</summary>
    private const long OwnedMountsRefreshMs = 10_000;

    private IReadOnlyList<(uint Id, string Name)> ownedMounts = [];
    private long ownedMountsAt = long.MinValue;

    /// <summary>The character's own mounts (Mount sheet row and name), sorted by name; empty while logged out.</summary>
    private IReadOnlyList<(uint Id, string Name)> OwnedMounts()
    {
        var now = Environment.TickCount64;
        if (ownedMountsAt != long.MinValue && now - ownedMountsAt < OwnedMountsRefreshMs)
        {
            return ownedMounts;
        }

        ownedMountsAt = now;
        var list = new List<(uint Id, string Name)>();
        try
        {
            if (ClientState.IsLoggedIn)
            {
                var text = CultureInfo.CurrentCulture.TextInfo;
                foreach (var mount in DataManager.GetExcelSheet<Mount>())
                {
                    if (mount.RowId == 0 || !UnlockState.IsMountUnlocked(mount))
                    {
                        continue;
                    }

                    var name = mount.Singular.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // English mount names are lower case in the sheet ("company chocobo").
                    list.Add((mount.RowId, text.ToUpper(name[0]) + name[1..]));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Owned mounts unavailable");
        }

        list.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        ownedMounts = list;
        return ownedMounts;
    }
}
