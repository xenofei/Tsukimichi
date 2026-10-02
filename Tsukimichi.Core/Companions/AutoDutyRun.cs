namespace Tsukimichi.Core.Companions;

/// <summary>What became of a "Run with AutoDuty" press.</summary>
public enum AutoDutyStart
{
    /// <summary>AutoDuty took the run and is no longer stopped.</summary>
    Started,

    /// <summary>AutoDuty is not loaded, a gate is missing, or a call failed.</summary>
    Unavailable,

    /// <summary>AutoDuty refused the duty mode, so nothing was run (it would have queued in whatever mode it had).</summary>
    ModeRefused,

    /// <summary>AutoDuty took the call but stayed stopped (it does not know the duty, or another plugin holds it).</summary>
    NotStarted,
}

/// <summary>
/// The order of a "Run with AutoDuty" press, kept apart from the IPC so it can be tested: push the temporary settings,
/// call Run, then ask whether AutoDuty left its stopped state. AutoDuty restores pushed settings itself when a run stops;
/// a run that never started has no stop, so whenever the settings were pushed and AutoDuty is still stopped afterwards
/// (Run refused, Run threw, or AutoDuty cannot be asked) they are popped here. Otherwise AutoDuty would keep Tsukimichi's
/// run mode, queue and loop count, and save them with its configuration.
/// </summary>
public static class AutoDutyRunSteps
{
    /// <summary>
    /// Runs the steps. <paramref name="push"/> answers whether AutoDuty took the settings; <paramref name="isStopped"/>
    /// answers true when AutoDuty is stopped or cannot be asked. Every exception is handed to <paramref name="failed"/>
    /// and never escapes.
    /// </summary>
    public static AutoDutyStart Run(Func<bool> push, Action run, Func<bool> isStopped, Action pop, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(push);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(isStopped);
        ArgumentNullException.ThrowIfNull(pop);
        ArgumentNullException.ThrowIfNull(failed);

        bool pushed;
        try
        {
            pushed = push();
        }
        catch (Exception ex)
        {
            // AutoDuty's push reverts what it applied when it fails, so nothing is left to pop.
            failed(ex);
            return AutoDutyStart.Unavailable;
        }

        if (!pushed)
        {
            return AutoDutyStart.ModeRefused;
        }

        var runFailed = false;
        try
        {
            run();
        }
        catch (Exception ex)
        {
            runFailed = true;
            failed(ex);
        }

        bool stopped;
        try
        {
            stopped = isStopped();
        }
        catch (Exception ex)
        {
            failed(ex);
            stopped = true;
        }

        if (!stopped)
        {
            // Running: AutoDuty pops the settings when it stops.
            return AutoDutyStart.Started;
        }

        try
        {
            pop();
        }
        catch (Exception ex)
        {
            failed(ex);
        }

        return runFailed ? AutoDutyStart.Unavailable : AutoDutyStart.NotStarted;
    }
}
