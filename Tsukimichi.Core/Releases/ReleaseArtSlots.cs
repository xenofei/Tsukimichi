namespace Tsukimichi.Core.Releases;

/// <summary>
/// The What's new popup's two picture slots (spec-1.22 W1, "Motion": pages cross-fade in place): the page's picture
/// and the outgoing one. Another page keeps the picture on screen as the outgoing one, drawn fading out while the page
/// cross-fades, until the new page's picture lands or the cross-fade has run; only then is it given back to be retired.
/// At most <see cref="ReleaseArt.HeldSlots"/> pictures are held. Every method returns the picture it lets go (null for
/// none), which the caller retires; the slots never dispose anything. Pure.
/// </summary>
/// <typeparam name="T">The picture (a texture in the plugin).</typeparam>
public sealed class ReleaseArtSlots<T>
    where T : class
{
    /// <summary>The page's picture once it has landed; null while it loads or when the page has none.</summary>
    public T? Current { get; private set; }

    /// <summary>The previous page's picture, kept for the cross-fade; null when none is fading out.</summary>
    public T? Outgoing { get; private set; }

    /// <summary>
    /// Another picture is wanted: the one on screen becomes the outgoing one. The picture that was outgoing before is
    /// let go; with nothing on screen (the page had none, or its picture had not landed) the outgoing one stays.
    /// </summary>
    public T? Replace()
    {
        if (Current is not { } shown)
        {
            return null;
        }

        var dropped = Outgoing;
        Outgoing = shown;
        Current = null;
        return dropped;
    }

    /// <summary>
    /// The wanted picture has landed: it is the page's, and the outgoing one is let go (with any other picture still in
    /// the page's slot, which <see cref="Replace"/> normally empties first).
    /// </summary>
    public (T? Current, T? Outgoing) Land(T picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var old = Current is { } held && !ReferenceEquals(held, picture) ? held : null;
        Current = picture;
        return (old, Retire());
    }

    /// <summary>The cross-fade has run: the outgoing picture is let go.</summary>
    public T? Retire()
    {
        var dropped = Outgoing;
        Outgoing = null;
        return dropped;
    }

    /// <summary>The popup closed: both pictures are let go (the page's first).</summary>
    public (T? Current, T? Outgoing) Clear()
    {
        var both = (Current, Outgoing);
        Current = null;
        Outgoing = null;
        return both;
    }
}
