using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// "You have N" for the detail pane's Hand in section (feature plan v5, research C4 #2): how many of an item the
/// logged-in character holds, read from the game, plus what its retainers hold, through Allagan Tools when it is
/// loaded and the player allows it.
/// <para>
/// The game read is <c>InventoryManager.GetInventoryItemCount</c> three ways (the four bags alone, with what is worn,
/// with the armoury chest) and <c>GetItemCountInContainer</c> for the saddlebag pages (the game fills those once the
/// saddlebag was opened this session), each for NQ and HQ, kept apart so an item a quest wants high quality counts only
/// its HQ (<see cref="HandInCount"/>; Allagan Tools' counts cannot be split). Since 1.19 (N5) the count is the inventory
/// alone, what the game takes at the hand-in; the saddlebag and the armoury chest are kept apart so the pane can say
/// where the rest is. It calls into the game, so it follows the shared <see cref="HookGate"/> as the other
/// game reads do: while the gate pauses them, only Allagan Tools' numbers show. It runs on the framework thread only
/// (the UI draws there) and answers are cached for <see cref="CacheMs"/>, so a pane listing six items costs a few dozen
/// game calls a second at most.
/// </para>
/// </summary>
public sealed class HandInStock
{
    /// <summary>How long a count is reused before the game is asked again.</summary>
    public const long CacheMs = 1000;

    private static readonly InventoryType[] SaddleBags =
    [
        InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
    ];

    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, (GameRead? Count, long At)> game = [];
    private bool warned;

    public HandInStock(IClientState clientState, IFramework framework, HookGate gate, IPluginLog log)
    {
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Allagan Tools' counts; null leaves retainers uncounted.</summary>
    public AllaganToolsIpc? Allagan { get; set; }

    /// <summary>Reads Settings › Integrations › "Count retainers with Allagan Tools"; null reads as on.</summary>
    public Func<bool>? AllaganEnabled { get; set; }

    /// <summary>Whether Allagan Tools is loaded, ready and allowed.</summary>
    public bool AllaganActive => Allagan is { Available: true } && AllaganEnabled?.Invoke() != false;

    /// <summary>
    /// The logged-in character's stock of <paramref name="itemId"/>: the game's count with its HQ part, and Allagan
    /// Tools' retainer count (its character count stands in, without an HQ part, while the game read is paused).
    /// Framework thread; nothing is read while logged out.
    /// </summary>
    public HandInCount For(uint itemId)
    {
        if (itemId == 0 || !clientState.IsLoggedIn || !framework.IsInFrameworkUpdateThread)
        {
            return default;
        }

        var read = GameCount(itemId);
        int? held = read?.Inventory;
        int? retainers = null;
        if (AllaganActive && Allagan!.Counts(itemId) is { } counts)
        {
            retainers = (int)Math.Min(counts.Retainers, int.MaxValue);
            held ??= (int)Math.Min(counts.Character, int.MaxValue);
        }

        return new HandInCount(held, read?.InventoryHq, retainers, read?.Saddlebag, read?.Armoury, read?.Equipped);
    }

    /// <summary>One game read: the inventory (what a hand-in counts, 1.19 N5) with its HQ part, and the other containers apart.</summary>
    private readonly record struct GameRead(int Inventory, int InventoryHq, int Saddlebag, int Armoury, int Equipped);

    private GameRead? GameCount(uint itemId)
    {
        if (!gate.HooksAllowed)
        {
            return null;
        }

        var now = Environment.TickCount64;
        if (game.TryGetValue(itemId, out var cached) && now - cached.At < CacheMs)
        {
            return cached.Count;
        }

        var count = ReadGame(itemId);
        game[itemId] = (count, now);
        return count;
    }

    private unsafe GameRead? ReadGame(uint itemId)
    {
        try
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
            {
                return null;
            }

            int bags = 0, bagsHq = 0, saddlebag = 0, armoury = 0, equipped = 0;
            foreach (var hq in (ReadOnlySpan<bool>)[false, true])
            {
                // The four bags alone, then with what is worn, then with the armoury chest: the differences are each part.
                var inBags = Math.Max(0, inventory->GetInventoryItemCount(itemId, hq, false, false));
                var withWorn = Math.Max(inBags, inventory->GetInventoryItemCount(itemId, hq, true, false));
                var withArmoury = Math.Max(withWorn, inventory->GetInventoryItemCount(itemId, hq, true, true));
                bags += inBags;
                bagsHq += hq ? inBags : 0;
                equipped += withWorn - inBags;
                armoury += withArmoury - withWorn;
                foreach (var bag in SaddleBags)
                {
                    saddlebag += Math.Max(0, inventory->GetItemCountInContainer(itemId, bag, hq));
                }
            }

            return new GameRead(bags, bagsHq, saddlebag, armoury, equipped);
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Item counts could not be read from the game; the Hand in section shows Allagan Tools' counts only");
            }

            return null;
        }
    }
}
