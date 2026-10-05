namespace Tsukimichi.Core.Umbra;

/// <summary>
/// Umbra, driven through Umbra's own code (the plugin reaches it by reflection, <c>Tsukimichi/Game/UmbraControl.cs</c>;
/// tests use a fake). Every change goes through the path Umbra's own settings use, so Umbra's checks and saving run;
/// Tsukimichi never writes Umbra's files.
/// </summary>
public interface IUmbraControl
{
    /// <summary>
    /// Finds Umbra's running code and every member the steps use, checking their shapes, and opens a session on it for
    /// one run. Changes nothing. Refused (<see cref="UmbraFailure.NotRunning"/>) while an earlier restart is still running.
    /// </summary>
    Task<UmbraOpened> Open();
}

/// <summary>What <see cref="IUmbraControl.Open"/> found: a session, or why there is none.</summary>
/// <param name="Failure"><see cref="UmbraFailure.None"/> with a session; otherwise why Umbra can't be driven.</param>
/// <param name="Session">The run's own view of Umbra; null on failure.</param>
public sealed record UmbraOpened(UmbraFailure Failure, IUmbraSession? Session);

/// <summary>One run's handle on Umbra, with the members it resolved (never shared with another run). Each call runs on the framework thread.</summary>
public interface IUmbraSession
{
    /// <summary>What Umbra holds now.</summary>
    Task<UmbraLook> Look();

    /// <summary>Turns Umbra's custom plugins on or off, as Umbra's own "I agree" switch does.</summary>
    Task SetCustomPlugins(bool on);

    /// <summary>Has Umbra read the add-on's latest release, download and check it, and list it, as Umbra's "Add repository" does.</summary>
    Task<UmbraFailure> AddRepository();

    /// <summary>Removes the add-on repository's entries from Umbra's list, as Umbra's own Remove does; returns how many.</summary>
    Task<int> RemoveRepository();

    /// <summary>
    /// Umbra's own Restart (its settings' Restart button): it reloads its toolbar and loads or unloads add-ons. Refused
    /// (<see cref="UmbraFailure.NotRunning"/>) when Umbra or the player went away; <see cref="UmbraFailure.RestartTimedOut"/>
    /// when it is still starting after the limit.
    /// </summary>
    Task<UmbraFailure> Restart();

    /// <summary>Places a Tsukimichi widget with instance id <paramref name="widgetId"/> on the active toolbar profile, as Umbra's "Add widget" does; true when it is there.</summary>
    Task<bool> PlaceWidget(string widgetId);

    /// <summary>Removes the widget instance <paramref name="widgetId"/> when it is on the active profile; true when it was.</summary>
    Task<bool> RemoveWidget(string widgetId);
}

/// <summary>What a run did.</summary>
/// <param name="Failure">Why it stopped; <see cref="UmbraFailure.None"/> when every step went through.</param>
/// <param name="Left">What this run changed and has not undone (after a failed Add: what putting back could not undo).</param>
/// <param name="KeptCustomPluginsOn">Custom plugins Tsukimichi turned on stayed on, because the player added add-ons since.</param>
public sealed record UmbraRunResult(UmbraFailure Failure, UmbraSetupRecord Left, bool KeptCustomPluginsOn)
{
    /// <summary>Nothing of this run's is left in Umbra.</summary>
    public bool PutBack => Left.IsEmpty;
}

/// <summary>
/// Runs "Add to Umbra" and "Remove from Umbra" (<see cref="UmbraAddonSetup"/>'s plans) against <see cref="IUmbraControl"/>.
/// <list type="bullet">
/// <item>Nothing changes before every member is found (<see cref="IUmbraControl.Open"/>).</item>
/// <item>Each step looks at Umbra again first and does nothing when it is already done.</item>
/// <item>Each change is recorded the moment it is made (<c>changed</c>), so a crash mid-run still leaves a record that
/// "Remove from Umbra" can undo; when a step throws, Umbra is looked at again and whatever it changed is recorded too.</item>
/// <item>When a step fails or Umbra throws, the run puts back what it changed, newest first, including the switch it
/// turned on. After a restart that timed out it changes nothing more: Umbra may still be starting.</item>
/// <item>Remove undoes only the record of the Umbra profile Umbra runs now.</item>
/// </list>
/// </summary>
public static class UmbraSetupRunner
{
    /// <summary>"Add to Umbra": the steps <see cref="UmbraAddonSetup.Plan"/> gives, then nothing more.</summary>
    /// <param name="control">Umbra.</param>
    /// <param name="stepping">Called as each step starts.</param>
    /// <param name="undoing">Called as each step of putting back starts, after a failure.</param>
    /// <param name="changed">Called with what this run has changed so far (for its profile), after every change and undo.</param>
    public static async Task<UmbraRunResult> Add(IUmbraControl control, Action<UmbraSetupStep> stepping, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(stepping);
        ArgumentNullException.ThrowIfNull(undoing);
        ArgumentNullException.ThrowIfNull(changed);

        var (failure, session) = await Open(control).ConfigureAwait(false);
        if (session is null)
        {
            return new UmbraRunResult(failure, UmbraSetupRecord.Empty, false);
        }

        UmbraLook start;
        try
        {
            start = await session.Look().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new UmbraRunResult(UmbraFailure.Unexpected, UmbraSetupRecord.Empty, false);
        }

        var run = UmbraSetupRecord.For(start.Profile, start.CharacterId);
        string? placing = null;
        try
        {
            foreach (var step in UmbraAddonSetup.Plan(start))
            {
                stepping(step);
                var look = await session.Look().ConfigureAwait(false);
                switch (step)
                {
                    case UmbraSetupStep.TurnOnCustomPlugins when !look.CustomPluginsOn:
                        await session.SetCustomPlugins(true).ConfigureAwait(false);
                        var on = await session.Look().ConfigureAwait(false);
                        if (on.CustomPluginsOn)
                        {
                            // The add-on count as Umbra reads its stored list now: a later Remove compares against it.
                            run = run with { TurnedOnCustomPlugins = true, OtherAddonsAtTurnOn = on.OtherAddons };
                            changed(run);
                        }
                        else
                        {
                            failure = UmbraFailure.Unexpected;
                        }

                        break;
                    case UmbraSetupStep.AddRepository when !look.RepositoryListed:
                        var added = await session.AddRepository().ConfigureAwait(false);
                        var listed = (await session.Look().ConfigureAwait(false)).RepositoryListed;
                        if (listed)
                        {
                            // Listed, even by a step that then failed: it is this run's to put back.
                            run = run with { AddedRepository = true };
                            changed(run);
                        }

                        failure = added != UmbraFailure.None ? added : listed ? UmbraFailure.None : UmbraFailure.ReleaseRefused;
                        break;
                    case UmbraSetupStep.RestartUmbra:
                        failure = await session.Restart().ConfigureAwait(false);
                        if (failure == UmbraFailure.None && !(await session.Look().ConfigureAwait(false)).WidgetRegistered)
                        {
                            failure = UmbraFailure.NotLoaded;
                        }

                        break;
                    case UmbraSetupStep.PlaceWidget when !look.WidgetPlaced:
                        placing = Guid.NewGuid().ToString();
                        if (await session.PlaceWidget(placing).ConfigureAwait(false))
                        {
                            run = run.WithWidget(placing);
                            changed(run);
                        }
                        else
                        {
                            failure = UmbraFailure.WidgetNotPlaced;
                        }

                        placing = null;
                        break;
                }

                if (failure != UmbraFailure.None)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            failure = UmbraFailure.Unexpected;
            run = await Salvage(session, start, run, placing, changed).ConfigureAwait(false);
        }

        if (failure == UmbraFailure.None)
        {
            return new UmbraRunResult(UmbraFailure.None, run, false);
        }

        if (failure == UmbraFailure.RestartTimedOut)
        {
            // Umbra may still be starting: undoing now would race it. What changed stays recorded for Remove from Umbra.
            return new UmbraRunResult(failure, run, false);
        }

        // Put back what this run changed, and nothing else; the switch it turned on goes back off.
        var undone = await Undo(session, run, undoing, changed, sameRun: true).ConfigureAwait(false);
        return new UmbraRunResult(failure, undone.Left, undone.KeptCustomPluginsOn);
    }

    /// <summary>
    /// "Remove from Umbra": undoes the record of the Umbra profile Umbra runs now (<see cref="UmbraAddonSetup.UndoPlan"/>).
    /// A book whose changes are all on other profiles changes nothing (<see cref="UmbraFailure.OtherProfile"/>). What the
    /// plan leaves alone is dropped from the record too (a repository no longer listed, custom plugins the player's later
    /// add-ons use), so the record ends empty on success.
    /// </summary>
    /// <param name="changed">Called with that profile's record after every undo.</param>
    public static async Task<UmbraRunResult> Remove(IUmbraControl control, UmbraSetupBook book, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(undoing);
        ArgumentNullException.ThrowIfNull(changed);

        var (failure, session) = await Open(control).ConfigureAwait(false);
        if (session is null)
        {
            return new UmbraRunResult(failure, UmbraSetupRecord.Empty, false);
        }

        string profile;
        try
        {
            profile = (await session.Look().ConfigureAwait(false)).Profile;
        }
        catch (Exception)
        {
            return new UmbraRunResult(UmbraFailure.Unexpected, UmbraSetupRecord.Empty, false);
        }

        var record = book.For(profile);
        if (record.IsEmpty)
        {
            return new UmbraRunResult(book.IsEmpty ? UmbraFailure.None : UmbraFailure.OtherProfile, record, false);
        }

        return await Undo(session, record, undoing, changed, sameRun: false).ConfigureAwait(false);
    }

    private static async Task<(UmbraFailure Failure, IUmbraSession? Session)> Open(IUmbraControl control)
    {
        try
        {
            var opened = await control.Open().ConfigureAwait(false);
            return opened.Session is { } session && opened.Failure == UmbraFailure.None
                ? (UmbraFailure.None, session)
                : (opened.Failure == UmbraFailure.None ? UmbraFailure.UnknownUmbra : opened.Failure, null);
        }
        catch (Exception)
        {
            return (UmbraFailure.UnknownUmbra, null);
        }
    }

    /// <summary>
    /// A step threw: it may have changed Umbra before throwing (an Umbra event handler failing after the change). Looks
    /// again and records whatever is on, listed or placed now that was not at the start, so it is put back too.
    /// </summary>
    private static async Task<UmbraSetupRecord> Salvage(IUmbraSession session, UmbraLook start, UmbraSetupRecord run, string? placing, Action<UmbraSetupRecord> changed)
    {
        try
        {
            var now = await session.Look().ConfigureAwait(false);
            var salvaged = run;
            if (!start.CustomPluginsOn && now.CustomPluginsOn && !salvaged.TurnedOnCustomPlugins)
            {
                salvaged = salvaged with { TurnedOnCustomPlugins = true, OtherAddonsAtTurnOn = now.OtherAddons };
            }

            if (!start.RepositoryListed && now.RepositoryListed)
            {
                salvaged = salvaged with { AddedRepository = true };
            }

            if (placing is not null && now.AddonWidgetIds?.Contains(placing, StringComparer.Ordinal) == true)
            {
                salvaged = salvaged.WithWidget(placing);
            }

            if (salvaged != run)
            {
                changed(salvaged);
            }

            return salvaged;
        }
        catch (Exception)
        {
            return run;
        }
    }

    private static async Task<UmbraRunResult> Undo(IUmbraSession session, UmbraSetupRecord record, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed, bool sameRun)
    {
        var left = record;
        var kept = false;
        try
        {
            var look = await session.Look().ConfigureAwait(false);
            kept = UmbraAddonSetup.KeepsCustomPluginsOn(record, look, sameRun) == true;
            foreach (var step in UmbraAddonSetup.UndoPlan(record, look, sameRun))
            {
                undoing(step);
                switch (step)
                {
                    case UmbraUndoStep.RemoveWidgets:
                        foreach (var id in record.WidgetIds)
                        {
                            // Gone already (the player removed it, or it is on another toolbar profile) counts as done:
                            // without the add-on that widget never loads again.
                            await session.RemoveWidget(id).ConfigureAwait(false);
                            left = left.WithoutWidget(id);
                            changed(left);
                        }

                        break;
                    case UmbraUndoStep.RemoveRepository:
                        await session.RemoveRepository().ConfigureAwait(false);
                        if ((await session.Look().ConfigureAwait(false)).RepositoryListed)
                        {
                            return new UmbraRunResult(UmbraFailure.Unexpected, left, kept);
                        }

                        left = left with { AddedRepository = false };
                        changed(left);
                        break;
                    case UmbraUndoStep.TurnOffCustomPlugins:
                        await session.SetCustomPlugins(false).ConfigureAwait(false);
                        if ((await session.Look().ConfigureAwait(false)).CustomPluginsOn)
                        {
                            return new UmbraRunResult(UmbraFailure.Unexpected, left, kept);
                        }

                        left = left with { TurnedOnCustomPlugins = false };
                        changed(left);
                        break;
                    case UmbraUndoStep.RestartUmbra:
                        // The changes are saved by now; a failed restart only means Umbra applies them when it next starts.
                        var restarted = await session.Restart().ConfigureAwait(false);
                        if (restarted != UmbraFailure.None)
                        {
                            Clear(ref left, changed);
                            return new UmbraRunResult(restarted, left, kept);
                        }

                        break;
                }
            }
        }
        catch (Exception)
        {
            return new UmbraRunResult(UmbraFailure.Unexpected, left, kept);
        }

        Clear(ref left, changed);
        return new UmbraRunResult(UmbraFailure.None, left, kept);
    }

    /// <summary>
    /// What the plan left alone is no longer Tsukimichi's to undo: a repository no longer listed, custom plugins the
    /// player's later add-ons use or that are already off, widgets already gone.
    /// </summary>
    private static void Clear(ref UmbraSetupRecord left, Action<UmbraSetupRecord> changed)
    {
        if (!left.IsEmpty)
        {
            left = UmbraSetupRecord.For(left.Profile, left.CharacterId);
            changed(left);
        }
    }
}
