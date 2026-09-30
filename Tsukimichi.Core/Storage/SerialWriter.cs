using System.Collections.Concurrent;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// One background queue for the plugin's file saves (snapshots and their sidecars, pins, overrides), so the framework
/// thread never waits on the disk, a slow reader in another game client, or the cross-client lock (D11). Work runs on
/// the thread pool one item at a time, in the order it was queued, so two saves of one file never race each other.
/// Each item may name a completion; completions are collected and run by whoever calls <see cref="DrainCompletions"/>
/// (the plugin does so on the framework thread), never on the worker.
/// </summary>
public sealed class SerialWriter : IDisposable
{
    /// <summary>How long <see cref="Dispose"/> waits for queued work by default.</summary>
    public static readonly TimeSpan DefaultDrainWait = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly ConcurrentQueue<Action> completions = new();
    private readonly TimeSpan drainWait;
    private Task tail = Task.CompletedTask;
    private int pending;
    private bool closed;

    public SerialWriter(TimeSpan? drainWait = null)
    {
        this.drainWait = drainWait ?? DefaultDrainWait;
    }

    /// <summary>Items queued and not finished yet.</summary>
    public int Pending => Volatile.Read(ref pending);

    /// <summary>
    /// Queues <paramref name="work"/> after everything queued before it. <paramref name="done"/> receives its result,
    /// or the exception it threw, on the next <see cref="DrainCompletions"/>. After <see cref="Dispose"/> the work runs
    /// at once on the caller's thread (a last save at unload is never dropped) and <paramref name="done"/> does not run.
    /// </summary>
    public void Enqueue<T>(Func<T> work, Action<T?, Exception?>? done = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (gate)
        {
            if (!closed)
            {
                Interlocked.Increment(ref pending);
                tail = tail.ContinueWith(
                    _ => Run(work, done),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                return;
            }
        }

        // Closed: nobody drains completions any more, so the outcome is only the work itself.
        try
        {
            work();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort at unload.
        }
    }

    /// <summary>
    /// <see cref="Enqueue{T}"/> on <paramref name="writer"/>, or, without one (a test, a surface built before the
    /// writer), the work and its completion at once on the calling thread.
    /// </summary>
    public static void Submit<T>(SerialWriter? writer, Func<T> work, Action<T?, Exception?> done)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(done);
        if (writer is not null)
        {
            writer.Enqueue(work, done);
            return;
        }

        T? result = default;
        Exception? error = null;
        try
        {
            result = work();
        }
        catch (Exception ex)
        {
            error = ex;
        }

        done(result, error);
    }

    /// <summary><see cref="Enqueue{T}"/> for work with no result.</summary>
    public void Enqueue(Action work, Action<Exception?>? done = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        Enqueue<bool>(
            () =>
            {
                work();
                return true;
            },
            done is null ? null : (_, error) => done(error));
    }

    /// <summary>Runs the completions of finished work on the calling thread, oldest first; returns how many ran.</summary>
    public int DrainCompletions()
    {
        var ran = 0;
        while (completions.TryDequeue(out var completion))
        {
            completion();
            ran++;
        }

        return ran;
    }

    /// <summary>Waits until everything queued so far has run; false when <paramref name="timeout"/> ran out first.</summary>
    public bool WaitIdle(TimeSpan timeout)
    {
        Task last;
        lock (gate)
        {
            last = tail;
        }

        try
        {
            return last.Wait(timeout);
        }
        catch (AggregateException)
        {
            // Every item catches its own failure; nothing else can fault the chain.
            return true;
        }
    }

    /// <summary>Stops queueing (later work runs inline) and waits up to the drain wait for what is queued to land.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (closed)
            {
                return;
            }

            closed = true;
        }

        WaitIdle(drainWait);
    }

    private void Run<T>(Func<T> work, Action<T?, Exception?>? done)
    {
        T? result = default;
        Exception? error = null;
        try
        {
            result = work();
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            Interlocked.Decrement(ref pending);
        }

        if (done is not null)
        {
            completions.Enqueue(() => done(result, error));
        }
    }
}
