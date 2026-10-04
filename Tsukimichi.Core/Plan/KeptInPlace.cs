namespace Tsukimichi.Core.Plan;

/// <summary>
/// The My blues rows moved to the other side (set aside, or brought back) since the list was built, for one character
/// (feature plan v7 P4): they keep their place as a quiet line with Undo until the list is rebuilt. The set belongs to
/// the character it was made for; viewing another character drops it (<see cref="Follow"/>), so a row of one character
/// never shows as moved, with an Undo, on another's list. <see cref="Version"/> moves on every change.
/// </summary>
public sealed class KeptInPlace
{
    private readonly HashSet<uint> rows = [];

    /// <summary>The character the rows were moved for; null for none.</summary>
    public ulong? Owner { get; private set; }

    /// <summary>Bumps whenever the set changes.</summary>
    public int Version { get; private set; }

    public int Count => rows.Count;

    /// <summary>The rows, for the list's filter.</summary>
    public IReadOnlySet<uint> Rows => rows;

    public bool Contains(uint rowId) => rows.Contains(rowId);

    /// <summary>The character on view; another one than the rows' drops them. True when they were dropped.</summary>
    public bool Follow(ulong? viewed)
    {
        if (viewed == Owner)
        {
            return false;
        }

        Owner = viewed;
        return Clear();
    }

    /// <summary>A row moved for the character on view (<see cref="Owner"/>).</summary>
    public void Keep(uint rowId)
    {
        if (rows.Add(rowId))
        {
            Version++;
        }
    }

    /// <summary>The list is rebuilt: the rows go. True when there were any.</summary>
    public bool Clear()
    {
        if (rows.Count == 0)
        {
            return false;
        }

        rows.Clear();
        Version++;
        return true;
    }
}
