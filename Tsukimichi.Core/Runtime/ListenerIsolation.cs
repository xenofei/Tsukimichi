namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Raises an event one listener at a time, so a listener that throws cannot keep the ones after it from running. A
/// plain <c>Changed?.Invoke()</c> stops at the first throw: one broken surface (the chat notices, the todo overlay)
/// would leave every later one (Nearby, Since you were away, the IPC gates) on stale data until the next change.
/// Failures go to the reporter, at most once per listener per <see cref="QuietPeriod"/>: a listener that throws on
/// every session change is reported once, then again with the count of failures held back since.
/// </summary>
public sealed class ListenerIsolation
{
    public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMinutes(1);

    private readonly object gate = new();
    private readonly Action<string, Exception, int> report;
    private readonly Func<DateTime> nowUtc;
    private readonly Dictionary<string, Reported> reported = new(StringComparer.Ordinal);

    /// <param name="report">
    /// Called after a listener threw, unless that listener was reported within the quiet period: the listener's name
    /// (<see cref="Describe"/>), its exception, and how many of its failures were held back since it was last reported.
    /// A reporter that throws is ignored.
    /// </param>
    /// <param name="quietPeriod">How long a reported listener's further failures are only counted; <see cref="DefaultQuietPeriod"/> when null.</param>
    /// <param name="nowUtc">The clock; <see cref="DateTime.UtcNow"/> when null.</param>
    public ListenerIsolation(Action<string, Exception, int> report, TimeSpan? quietPeriod = null, Func<DateTime>? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        var quiet = quietPeriod ?? DefaultQuietPeriod;
        if (quiet < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(quietPeriod), quiet, "Quiet period cannot be negative.");
        }

        this.report = report;
        QuietPeriod = quiet;
        this.nowUtc = nowUtc ?? (static () => DateTime.UtcNow);
    }

    public TimeSpan QuietPeriod { get; }

    /// <summary>Calls every listener of <paramref name="handlers"/> in subscription order; returns how many threw.</summary>
    public int Raise(Action? handlers)
    {
        if (handlers is null)
        {
            return 0;
        }

        var failed = 0;
        foreach (var listener in Delegate.EnumerateInvocationList(handlers))
        {
            try
            {
                listener();
            }
            catch (Exception ex)
            {
                failed++;
                Fail(listener, ex);
            }
        }

        return failed;
    }

    /// <summary>Calls every listener of <paramref name="handlers"/> with <paramref name="argument"/> in subscription order; returns how many threw.</summary>
    public int Raise<T>(Action<T>? handlers, T argument)
    {
        if (handlers is null)
        {
            return 0;
        }

        var failed = 0;
        foreach (var listener in Delegate.EnumerateInvocationList(handlers))
        {
            try
            {
                listener(argument);
            }
            catch (Exception ex)
            {
                failed++;
                Fail(listener, ex);
            }
        }

        return failed;
    }

    /// <summary>A listener's name for the log: its declaring type and method, e.g. <c>ChatNotifier.OnSessionChanged</c>.</summary>
    public static string Describe(Delegate listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        var method = listener.Method;
        return method.DeclaringType is { } type ? $"{type.FullName ?? type.Name}.{method.Name}" : method.Name;
    }

    private void Fail(Delegate listener, Exception error)
    {
        var name = Describe(listener);
        int held;
        lock (gate)
        {
            var now = nowUtc();
            if (reported.TryGetValue(name, out var last) && now - last.AtUtc < QuietPeriod)
            {
                reported[name] = last with { Held = last.Held + 1 };
                return;
            }

            held = last?.Held ?? 0;
            reported[name] = new Reported(now, 0);
        }

        try
        {
            report(name, error, held);
        }
        catch
        {
            // The reporter is a log call; a failing log must not stop the remaining listeners either.
        }
    }

    private sealed record Reported(DateTime AtUtc, int Held);
}
