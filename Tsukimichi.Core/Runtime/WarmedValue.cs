using System.Threading;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// A value built once on a worker and then read for free: <see cref="Start"/> runs the build on the thread pool (at most
/// once), and <see cref="Value"/> is null until it lands, then the result. A draw that reads it never builds and never
/// waits; it shows its "not yet" state for the frames the build takes. A build that throws leaves the value null and
/// hands the exception to the failure callback. Safe to read from any thread.
/// </summary>
/// <typeparam name="T">What is built (the duty index).</typeparam>
public sealed class WarmedValue<T>
    where T : class
{
    private readonly Func<T> build;
    private readonly Action<Exception>? failed;
    private T? value;
    private Task? task;
    private int started;

    /// <param name="build">The build; runs on a worker.</param>
    /// <param name="failed">Told when the build throws (logged there); the value then stays null.</param>
    public WarmedValue(Func<T> build, Action<Exception>? failed = null)
    {
        this.build = build ?? throw new ArgumentNullException(nameof(build));
        this.failed = failed;
    }

    /// <summary>The built value; null before <see cref="Start"/>, while it builds, and after a failed build.</summary>
    public T? Value => Volatile.Read(ref value);

    /// <summary>
    /// Starts the build on the thread pool; later calls return the same task without building again. The task always
    /// completes successfully: a failure goes to the callback.
    /// </summary>
    public Task Start()
    {
        if (Interlocked.Exchange(ref started, 1) == 1)
        {
            return Volatile.Read(ref task) ?? Task.CompletedTask;
        }

        var run = Task.Run(() =>
        {
            try
            {
                Volatile.Write(ref value, build());
            }
            catch (Exception ex)
            {
                failed?.Invoke(ex);
            }
        });
        Volatile.Write(ref task, run);
        return run;
    }
}
