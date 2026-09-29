using System.Threading;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Generation counter for a resource that is rebuilt off-thread and swapped in on the framework thread (the catalog).
/// Every build takes a ticket from <see cref="Start"/>; when it finishes, <see cref="IsCurrent"/> says whether a newer
/// build has started since, in which case its result is stale and must be dropped rather than swapped in over the
/// newer one. Safe to call from any thread; the ticket compare is the only state.
/// </summary>
public sealed class BuildGeneration
{
    private int current;

    /// <summary>The ticket of the latest build started; 0 before the first.</summary>
    public int Current => Volatile.Read(ref current);

    /// <summary>Starts a build: every ticket handed out before this one is superseded.</summary>
    public int Start() => Interlocked.Increment(ref current);

    /// <summary>Whether <paramref name="generation"/> is the latest build started, i.e. its result may be published.</summary>
    public bool IsCurrent(int generation) => generation == Volatile.Read(ref current);
}
