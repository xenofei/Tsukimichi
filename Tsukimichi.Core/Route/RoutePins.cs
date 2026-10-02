namespace Tsukimichi.Core.Route;

/// <summary>Where "Pin all" puts its pins; the plugin adapts its pin list (QueryRunner's, saved in <c>user/pins.json</c>) to this.</summary>
public interface IRoutePinStore
{
    /// <summary>False when there is nobody to pin for (browse mode).</summary>
    bool CanPin { get; }

    /// <summary>The character whose pins the store reads and writes right now (its content id); null in browse mode.</summary>
    ulong? Owner { get; }

    bool IsPinned(uint rowId);

    /// <summary>Pins an unpinned quest (after every pin already held: the pins keep the order they were added in) or unpins a pinned one; false when nothing changed.</summary>
    bool TogglePin(uint rowId);
}

/// <summary>What one "Pin all" pinned, and for whom: <see cref="RoutePins.Undo"/> takes it back only from that character.</summary>
public sealed record RoutePinBatch(ulong? Owner, IReadOnlyList<uint> Added)
{
    public static readonly RoutePinBatch Empty = new(null, []);
}

/// <summary>
/// "Pin all" on the route card, and its Undo. Feature plan v3 decision 10 (pins per character or per account) is still
/// open, so this is the one place a route's steps become pins: today it pins through the store it is handed, which is
/// the plugin's pin list for the character being viewed (keyed by content id in <c>user/pins.json</c>), so pinning a
/// stored alt's route pins on that alt. When decision 10 is answered, only this class and the store behind it change.
/// </summary>
public static class RoutePins
{
    /// <summary>
    /// Pins every step of <paramref name="route"/> that is not pinned yet, in route order after the pins already held
    /// (the store keeps the order pins were added in, and the Todo overlay lists them in that order, so the steps read
    /// in the order to do them; a step pinned before keeps its place). Returns the quests it pinned and the character it pinned them for, for
    /// <see cref="Undo"/>; no quests when the store cannot pin or every step was pinned already.
    /// </summary>
    public static RoutePinBatch PinAll(UnlockRoute route, IRoutePinStore store)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(store);
        if (!store.CanPin)
        {
            return RoutePinBatch.Empty;
        }

        var added = new List<uint>();
        foreach (var step in route.Steps)
        {
            if (!store.IsPinned(step.RowId) && store.TogglePin(step.RowId))
            {
                added.Add(step.RowId);
            }
        }

        return new RoutePinBatch(store.Owner, added);
    }

    /// <summary>How many steps of <paramref name="route"/> "Pin all" would pin now (the steps not pinned yet); 0 when the store cannot pin.</summary>
    public static int CountUnpinned(UnlockRoute route, IRoutePinStore store)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(store);
        if (!store.CanPin)
        {
            return 0;
        }

        var count = 0;
        foreach (var step in route.Steps)
        {
            if (!store.IsPinned(step.RowId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>True while <paramref name="batch"/> can still be undone: it pinned something, and the store still holds the pins of the character it pinned for.</summary>
    public static bool CanUndo(RoutePinBatch batch, IRoutePinStore store)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(store);
        return batch.Added.Count > 0 && batch.Owner is { } owner && store.Owner == owner;
    }

    /// <summary>
    /// Unpins what <see cref="PinAll"/> pinned and is still pinned; pins the player removed meanwhile stay removed.
    /// Removes nothing once the store holds another character's pins (the view switched since). Returns how many it unpinned.
    /// </summary>
    public static int Undo(RoutePinBatch batch, IRoutePinStore store)
    {
        if (!CanUndo(batch, store))
        {
            return 0;
        }

        var removed = 0;
        foreach (var rowId in batch.Added)
        {
            if (store.IsPinned(rowId) && store.TogglePin(rowId))
            {
                removed++;
            }
        }

        return removed;
    }
}
