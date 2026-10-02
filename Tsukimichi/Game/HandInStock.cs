using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// "You have N" for the detail pane's Hand in section (feature plan v5, research C4 #2): how many of an item the
/// logged-in character holds, read from the game, plus what its retainers hold, through Allagan Tools when it is
/// loaded and the player allows it.
/// <para>
/// The game read is <c>InventoryManager.GetInventoryItemCount</c> (the four bags, the armoury and what is equipped)
/// and <c>GetItemCountInContainer</c> for the saddlebag pages (the game fills those once the saddlebag was opened this
/// session), each for NQ and HQ. It calls into the game, so it follows the shared <see cref="HookGate"/> as the other
/// game reads do: while the gate pauses them, only Allagan Tools' numbers show. It runs on the framework thread only
/// (the UI draws there) and answers are cached for <see cref="CacheMs"/>, so a pane listing six items costs a dozen
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
    private readonly Dictionary<uint, (int? Count, long At)> game = [];
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

    /// <summary>What the logged-in character holds.</summary>
    /// <param name="Held">In its bags, armoury, equipped and saddlebag (the game), or what Allagan Tools counts on the character itself while the game read is paused; null when neither can tell.</param>
    /// <param name="Retainers">On its retainers (Allagan Tools); null without it.</param>
    public readonly record struct Stock(int? Held, int? Retainers)
    {
        /// <summary>Both counts together; null when neither is known.</summary>
        public int? Total => Held is null && Retainers is null ? null : (Held ?? 0) + (Retainers ?? 0);
    }

    /// <summary>The logged-in character's stock of <paramref name="itemId"/>. Framework thread; nothing is read while logged out.</summary>
    public Stock For(uint itemId)
    {
        if (itemId == 0 || !clientState.IsLoggedIn || !framework.IsInFrameworkUpdateThread)
        {
            return default;
        }

        var held = GameCount(itemId);
        int? retainers = null;
        if (AllaganActive && Allagan!.Counts(itemId) is { } counts)
        {
            retainers = (int)Math.Min(counts.Retainers, int.MaxValue);
            held ??= (int)Math.Min(counts.Character, int.MaxValue);
        }

        return new Stock(held, retainers);
    }

    private int? GameCount(uint itemId)
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

    private unsafe int? ReadGame(uint itemId)
    {
        try
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
            {
                return null;
            }

            var total = 0;
            foreach (var hq in (ReadOnlySpan<bool>)[false, true])
            {
                total += Math.Max(0, inventory->GetInventoryItemCount(itemId, hq, true, true));
                foreach (var bag in SaddleBags)
                {
                    total += Math.Max(0, inventory->GetItemCountInContainer(itemId, bag, hq));
                }
            }

            return total;
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
