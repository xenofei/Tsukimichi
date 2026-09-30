namespace Tsukimichi.Core.Storage;

/// <summary>What a read of a file another game client may own found (D11). Only <see cref="Loaded"/> carries a value.</summary>
public enum SharedLoad
{
    /// <summary>Read and parsed.</summary>
    Loaded,

    /// <summary>No such file.</summary>
    Missing,

    /// <summary>The file exists but could not be read right now (locked, permissions, disk); it is left in place.</summary>
    Unreadable,

    /// <summary>The file was read but could not be parsed; it is left in place for its owner to deal with.</summary>
    Invalid,

    /// <summary>A newer version of the plugin wrote the file (a higher schema version); it is valid, and left alone.</summary>
    Newer,
}

/// <summary>
/// The result of a read that never quarantines or rewrites anything: a multibox scan of another client's snapshot, or
/// a merge of a shared user file into memory. <paramref name="Problem"/> says what went wrong when nothing was loaded.
/// </summary>
public readonly record struct SharedRead<T>(SharedLoad Status, T? Value, string? Problem)
    where T : class
{
    public static SharedRead<T> Missing => new(SharedLoad.Missing, null, null);

    public static SharedRead<T> Of(T value) => new(SharedLoad.Loaded, value, null);

    public bool IsLoaded => Status == SharedLoad.Loaded && Value is not null;
}
