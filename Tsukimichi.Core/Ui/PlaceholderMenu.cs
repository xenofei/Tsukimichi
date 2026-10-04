namespace Tsukimichi.Core.Ui;

/// <summary>
/// The state of the spoiler shield's one right-click menu (spec-1.20 N6, "The right-click menu on any placeholder"),
/// kept apart from the placeholders that open it. A placeholder only asks (<see cref="Request"/>); a window that hosts
/// placeholders draws the menu once a frame from a stable place, its end (<see cref="TakeOpen"/>, <see cref="Owns"/>),
/// so the menu stays while the placeholder that opened it scrolls out of a clipped list or is skipped for a frame.
/// A right-click opens it on release, as the game's own context menus do: the press closes a menu already open, so one
/// right-click on a second placeholder moves the menu there (<see cref="Opens"/>).
/// </summary>
/// <typeparam name="T">What the menu acts on: the placeholder's hidden name, its quest and links.</typeparam>
public sealed class PlaceholderMenu<T>
    where T : class
{
    private bool pending;

    /// <summary>The placeholder the menu acts on: the last one asked for, until the menu closes.</summary>
    public T? Target { get; private set; }

    /// <summary>The window that drew the menu since it opened; null while none has.</summary>
    public string? Owner { get; private set; }

    /// <summary>
    /// Whether a placeholder's menu opens this frame: the pointer is on it and the right button was released. Whether
    /// a menu is open already does not matter: the press closed it, so the release opens the new one.
    /// </summary>
    /// <param name="hovered">The pointer is on the placeholder.</param>
    /// <param name="rightReleased">The right mouse button was released this frame.</param>
    public static bool Opens(bool hovered, bool rightReleased) => hovered && rightReleased;

    /// <summary>A placeholder asks for the menu on <paramref name="target"/>: the next host to draw opens it there.</summary>
    public void Request(T target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
        pending = true;
    }

    /// <summary>
    /// Whether <paramref name="host"/> opens the menu now: once per request, by the first host that draws after it,
    /// which then owns it.
    /// </summary>
    public bool TakeOpen(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!pending)
        {
            return false;
        }

        pending = false;
        Owner = host;
        return true;
    }

    /// <summary>Whether <paramref name="host"/> draws the menu: the one that opened it.</summary>
    public bool Owns(string host) => Target is not null && string.Equals(Owner, host, StringComparison.Ordinal);

    /// <summary>
    /// The owner found its menu closed: the target is forgotten, unless another placeholder asked for the menu
    /// meanwhile (its request still opens).
    /// </summary>
    public void Closed(string host)
    {
        if (!string.Equals(Owner, host, StringComparison.Ordinal))
        {
            return;
        }

        Owner = null;
        if (!pending)
        {
            Target = null;
        }
    }
}
