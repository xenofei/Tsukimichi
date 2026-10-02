using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Game;

/// <summary>
/// The logged-in character's collectible unlock flags (emotes, minions, mounts, orchestrion rolls, ornaments, Triple
/// Triad cards, bardings, hairstyles, aether currents, duties), read through Dalamud's <see cref="IUnlockState"/> rather
/// than ClientStructs offsets, so a patch that moves a field is Dalamud's to fix. Framework thread only, like every
/// client read. Shared by the capture (<see cref="GameStateReader"/>, which saves the answers in the snapshot) and the
/// Moonlit reads for the live character (<see cref="RewardUnlockReader"/>).
/// <para>
/// <see cref="Generation"/> moves on every <see cref="IUnlockState.Unlock"/> event (something was just unlocked), so
/// the capture reads the flags again and the Moonlit pane drops its cached answers at once instead of waiting for the
/// next save. The handler only increments a counter; it is safe on any thread.
/// </para>
/// </summary>
public sealed class CollectibleReader : IDisposable
{
    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    private readonly IUnlockState unlocks;
    private readonly IDataManager data;
    private readonly IPluginLog log;

    private Dictionary<uint, uint>? instanceByCondition;
    private int generation;
    private bool warned;
    private bool disposed;

    public CollectibleReader(IUnlockState unlocks, IDataManager data, IPluginLog log)
    {
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        unlocks.Unlock += OnUnlock;
    }

    /// <summary>Moves whenever the game reports a new unlock; readers compare it to drop what they cached.</summary>
    public int Generation => Volatile.Read(ref generation);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        unlocks.Unlock -= OnUnlock;
    }

    /// <summary>
    /// Whether the live character has the collectible: true, false, or null when the kind is not one of
    /// <see cref="Collectibles.StoredKinds"/>, the row does not exist or the read failed (logged once). Framework thread
    /// and a loaded character only; the caller checks both.
    /// </summary>
    public bool? IsUnlocked(RewardKind kind, uint id, uint itemId = 0)
    {
        try
        {
            return kind switch
            {
                RewardKind.Emote => Row<Emote>(id) is { } emote ? unlocks.IsEmoteUnlocked(emote) : null,
                RewardKind.Minion => Row<Companion>(id) is { } minion ? unlocks.IsCompanionUnlocked(minion) : null,
                RewardKind.Mount => Row<Mount>(id) is { } mount ? unlocks.IsMountUnlocked(mount) : null,
                RewardKind.Orchestrion => Row<Orchestrion>(id) is { } roll ? unlocks.IsOrchestrionUnlocked(roll) : null,
                RewardKind.Ornament => Row<Ornament>(id) is { } ornament ? unlocks.IsOrnamentUnlocked(ornament) : null,
                RewardKind.TripleTriadCard => Row<TripleTriadCard>(id) is { } card ? unlocks.IsTripleTriadCardUnlocked(card) : null,
                RewardKind.Barding => Row<BuddyEquip>(id) is { } barding ? unlocks.IsBuddyEquipUnlocked(barding) : null,
                RewardKind.Hairstyle => Hairstyle(id, itemId),
                RewardKind.AetherCurrent => Row<AetherCurrent>(id) is { } current ? unlocks.IsAetherCurrentUnlocked(current) : null,
                RewardKind.Instance => Row<InstanceContent>(id) is { } instance ? unlocks.IsInstanceContentUnlocked(instance) : null,
                RewardKind.DutyUnlock => InstanceForCondition(id) is { } link && Row<InstanceContent>(link) is { } duty ? unlocks.IsInstanceContentUnlocked(duty) : null,
                _ => null,
            };
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Collectible unlock flags could not be read; owned states show as unknown");
            }

            return null;
        }
    }

    /// <summary>
    /// A hairstyle's reward id is the unlock link its item's action sets (see the generator), which is what the game
    /// checks; the item itself is the fallback for an id past the 16-bit link range.
    /// </summary>
    private bool? Hairstyle(uint unlockLink, uint itemId)
    {
        if (unlockLink is > 0 and <= ushort.MaxValue)
        {
            return unlocks.IsUnlockLinkUnlocked((ushort)unlockLink);
        }

        return itemId != 0 && Row<Item>(itemId) is { } item ? unlocks.IsItemUnlocked(item) : null;
    }

    private T? Row<T>(uint id)
        where T : struct, IExcelRow<T> =>
        id == 0 ? null : data.GetExcelSheet<T>().GetRowOrDefault(id);

    /// <summary>ContentFinderCondition row id to InstanceContent row id, read once from the sheet. Null for other content types.</summary>
    private uint? InstanceForCondition(uint conditionId)
    {
        if (instanceByCondition is null)
        {
            var map = new Dictionary<uint, uint>();
            try
            {
                foreach (var row in data.GetExcelSheet<ContentFinderCondition>())
                {
                    if (row.ContentLinkType == InstanceContentLink && row.Content.RowId != 0)
                    {
                        map[row.RowId] = row.Content.RowId;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "ContentFinderCondition sheet could not be read; duty unlock states show as unknown");
            }

            instanceByCondition = map;
        }

        return instanceByCondition.TryGetValue(conditionId, out var instance) ? instance : null;
    }

    private void OnUnlock(RowRef rowRef) => Interlocked.Increment(ref generation);
}
