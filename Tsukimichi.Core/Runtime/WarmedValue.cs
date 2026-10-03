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

    /// <summary>Whether the build has finished (with a value, or failed); false before <see cref="Start"/> and while it builds.</summary>
    public bool IsDone => Volatile.Read(ref task) is { IsCompleted: true };

    /// <summary>How long the build took, in milliseconds; 0 before it finished.</summary>
    public double BuildMs { get; private set; }

    /// <summary>
    /// The value, starting the build if nobody has and waiting for it to land: for a caller that cannot answer without
    /// it (a worker, or a click). Never builds twice: a build already under way is waited for, not repeated. Null when
    /// the build failed.
    /// </summary>
    public T? Wait()
    {
        if (Value is { } ready)
        {
            return ready;
        }

        Start();

        // Another thread may have won the start and not yet stored its task.
        Task? running;
        var spin = default(SpinWait);
        while ((running = Volatile.Read(ref task)) is null)
        {
            spin.SpinOnce();
        }

        running.Wait();
        return Value;
    }

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
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var built = build();
                BuildMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Volatile.Write(ref value, built);
            }
            catch (Exception ex)
            {
                BuildMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                failed?.Invoke(ex);
            }
        });
        Volatile.Write(ref task, run);
        return run;
    }
}
