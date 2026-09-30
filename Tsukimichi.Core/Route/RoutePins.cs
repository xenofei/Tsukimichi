namespace Tsukimichi.Core.Route;

/// <summary>Where "Pin all" puts its pins; the plugin adapts its pin list (QueryRunner's, saved in <c>user/pins.json</c>) to this.</summary>
public interface IRoutePinStore
{
    /// <summary>False when there is nobody to pin for (browse mode).</summary>
    bool CanPin { get; }

    bool IsPinned(uint rowId);

    /// <summary>Pins an unpinned quest or unpins a pinned one; false when nothing changed.</summary>
    bool TogglePin(uint rowId);
}

/// <summary>
/// "Pin all" on the route card, and its Undo. Feature plan v3 decision 10 (pins per character or per account) is still
/// open, so this is the one place a route's steps become pins: today it pins through the store it is handed, which is
/// the plugin's pin list for the character being viewed (keyed by content id in <c>user/pins.json</c>), so pinning a
/// stored alt's route pins on that alt. When decision 10 is answered, only this method and the store behind it change.
/// </summary>
public static class RoutePins
{
    /// <summary>
    /// Pins every step of <paramref name="route"/> that is not pinned yet, in route order (so the Todo overlay lists
    /// them in the order to do them). Returns the quests it pinned, for <see cref="Undo"/>; empty when the store cannot
    /// pin or every step was pinned already.
    /// </summary>
    public static IReadOnlyList<uint> PinAll(UnlockRoute route, IRoutePinStore store)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(store);
        if (!store.CanPin)
        {
            return [];
        }

        var added = new List<uint>();
        foreach (var step in route.Steps)
        {
            if (!store.IsPinned(step.RowId) && store.TogglePin(step.RowId))
            {
                added.Add(step.RowId);
            }
        }

        return added;
    }

    /// <summary>Unpins what <see cref="PinAll"/> pinned and is still pinned; pins the player removed meanwhile stay removed. Returns how many it unpinned.</summary>
    public static int Undo(IReadOnlyList<uint> added, IRoutePinStore store)
    {
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(store);
        var removed = 0;
        foreach (var rowId in added)
        {
            if (store.IsPinned(rowId) && store.TogglePin(rowId))
            {
                removed++;
            }
        }

        return removed;
    }
}
