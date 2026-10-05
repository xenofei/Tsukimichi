namespace Tsukimichi.Core.Umbra;

/// <summary>
/// Umbra, driven through Umbra's own code (the plugin reaches it by reflection, <c>Tsukimichi/Game/UmbraControl.cs</c>;
/// tests use a fake). Every change goes through the path Umbra's own settings use, so Umbra's checks and saving run;
/// Tsukimichi never writes Umbra's files. Each call runs on the framework thread inside the implementation.
/// </summary>
public interface IUmbraControl
{
    /// <summary>
    /// Finds Umbra's running code and every member the steps use, checking their shapes. Changes nothing.
    /// <see cref="UmbraFailure.None"/> when all are there; <see cref="UmbraFailure.NotRunning"/> or
    /// <see cref="UmbraFailure.UnknownUmbra"/> otherwise.
    /// </summary>
    Task<UmbraFailure> Prepare();

    /// <summary>What Umbra holds now.</summary>
    Task<UmbraLook> Look();

    /// <summary>Turns Umbra's custom plugins on or off, as Umbra's own "I agree" switch does.</summary>
    Task SetCustomPlugins(bool on);

    /// <summary>Has Umbra read the add-on's latest release, download and check it, and list it, as Umbra's "Add repository" does.</summary>
    Task<UmbraFailure> AddRepository();

    /// <summary>Removes the add-on repository's entries from Umbra's list, as Umbra's own Remove does; returns how many.</summary>
    Task<int> RemoveRepository();

    /// <summary>Umbra's own Restart (its settings' Restart button): it reloads its toolbar and loads or unloads add-ons.</summary>
    Task<UmbraFailure> Restart();

    /// <summary>Places the Tsukimichi widget on the active toolbar profile, as Umbra's "Add widget" does; returns its instance id, or null.</summary>
    Task<string?> PlaceWidget();

    /// <summary>Removes the widget instance <paramref name="widgetId"/> when it is on the active profile; true when it was.</summary>
    Task<bool> RemoveWidget(string widgetId);
}

/// <summary>What a run did.</summary>
/// <param name="Failure">Why it stopped; <see cref="UmbraFailure.None"/> when every step went through.</param>
/// <param name="Left">What this run changed and has not undone (after a failed Add: what putting back could not undo).</param>
/// <param name="KeptCustomPluginsOn">A Remove left custom plugins on although Tsukimichi turned them on, because other add-ons use them.</param>
public sealed record UmbraRunResult(UmbraFailure Failure, UmbraSetupRecord Left, bool KeptCustomPluginsOn)
{
    /// <summary>Nothing of this run's is left in Umbra.</summary>
    public bool PutBack => Left.IsEmpty;
}

/// <summary>
/// Runs "Add to Umbra" and "Remove from Umbra" (<see cref="UmbraAddonSetup"/>'s plans) against <see cref="IUmbraControl"/>.
/// <list type="bullet">
/// <item>Nothing changes before every member is found (<see cref="IUmbraControl.Prepare"/>).</item>
/// <item>Each step looks at Umbra again first and does nothing when it is already done.</item>
/// <item>Each change is recorded the moment it is made (<c>changed</c>), so a crash mid-run still leaves a record that
/// "Remove from Umbra" can undo.</item>
/// <item>When a step fails or Umbra throws, the run puts back what it changed, newest first, and stops.</item>
/// </list>
/// </summary>
public static class UmbraSetupRunner
{
    /// <summary>"Add to Umbra": the steps <see cref="UmbraAddonSetup.Plan"/> gives, then nothing more.</summary>
    /// <param name="control">Umbra.</param>
    /// <param name="stepping">Called as each step starts.</param>
    /// <param name="undoing">Called as each step of putting back starts, after a failure.</param>
    /// <param name="changed">Called with what this run has changed so far, after every change (and every undo).</param>
    public static async Task<UmbraRunResult> Add(IUmbraControl control, Action<UmbraSetupStep> stepping, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(stepping);
        ArgumentNullException.ThrowIfNull(undoing);
        ArgumentNullException.ThrowIfNull(changed);

        var prepared = await Prepare(control).ConfigureAwait(false);
        if (prepared != UmbraFailure.None)
        {
            return new UmbraRunResult(prepared, UmbraSetupRecord.Empty, false);
        }

        var run = UmbraSetupRecord.Empty;
        UmbraFailure failure;
        try
        {
            failure = UmbraFailure.None;
            foreach (var step in UmbraAddonSetup.Plan(await control.Look().ConfigureAwait(false)))
            {
                stepping(step);
                var look = await control.Look().ConfigureAwait(false);
                switch (step)
                {
                    case UmbraSetupStep.TurnOnCustomPlugins when !look.CustomPluginsOn:
                        await control.SetCustomPlugins(true).ConfigureAwait(false);
                        run = run with { TurnedOnCustomPlugins = true };
                        changed(run);
                        if (!(await control.Look().ConfigureAwait(false)).CustomPluginsOn)
                        {
                            failure = UmbraFailure.Unexpected;
                        }

                        break;
                    case UmbraSetupStep.AddRepository when !look.RepositoryListed:
                        var added = await control.AddRepository().ConfigureAwait(false);
                        var listed = (await control.Look().ConfigureAwait(false)).RepositoryListed;
                        if (listed)
                        {
                            // Listed, even by a step that then failed: it is this run's to put back.
                            run = run with { AddedRepository = true };
                            changed(run);
                        }

                        failure = added != UmbraFailure.None ? added : listed ? UmbraFailure.None : UmbraFailure.ReleaseRefused;
                        break;
                    case UmbraSetupStep.RestartUmbra:
                        failure = await control.Restart().ConfigureAwait(false);
                        if (failure == UmbraFailure.None && !(await control.Look().ConfigureAwait(false)).WidgetRegistered)
                        {
                            failure = UmbraFailure.NotLoaded;
                        }

                        break;
                    case UmbraSetupStep.PlaceWidget when !look.WidgetPlaced:
                        if (await control.PlaceWidget().ConfigureAwait(false) is { } id)
                        {
                            run = run.WithWidget(id);
                            changed(run);
                        }
                        else
                        {
                            failure = UmbraFailure.WidgetNotPlaced;
                        }

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
        }

        if (failure == UmbraFailure.None)
        {
            return new UmbraRunResult(UmbraFailure.None, run, false);
        }

        // Put back what this run changed, and nothing else.
        var undone = await Undo(control, run, undoing, changed).ConfigureAwait(false);
        return new UmbraRunResult(failure, undone.Left, undone.KeptCustomPluginsOn);
    }

    /// <summary>
    /// "Remove from Umbra": undoes <paramref name="record"/>, Tsukimichi's own changes only
    /// (<see cref="UmbraAddonSetup.UndoPlan"/>). What the plan leaves alone is dropped from the record too (a repository
    /// that is no longer listed, custom plugins that other add-ons now use), so the record ends empty on success.
    /// </summary>
    public static async Task<UmbraRunResult> Remove(IUmbraControl control, UmbraSetupRecord record, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(undoing);
        ArgumentNullException.ThrowIfNull(changed);

        var prepared = await Prepare(control).ConfigureAwait(false);
        return prepared != UmbraFailure.None
            ? new UmbraRunResult(prepared, record, false)
            : await Undo(control, record, undoing, changed).ConfigureAwait(false);
    }

    private static async Task<UmbraFailure> Prepare(IUmbraControl control)
    {
        try
        {
            return await control.Prepare().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return UmbraFailure.UnknownUmbra;
        }
    }

    private static async Task<UmbraRunResult> Undo(IUmbraControl control, UmbraSetupRecord record, Action<UmbraUndoStep> undoing, Action<UmbraSetupRecord> changed)
    {
        var left = record;
        var kept = false;
        try
        {
            var look = await control.Look().ConfigureAwait(false);
            kept = UmbraAddonSetup.KeepsCustomPluginsOn(record, look) == true;
            foreach (var step in UmbraAddonSetup.UndoPlan(record, look))
            {
                undoing(step);
                switch (step)
                {
                    case UmbraUndoStep.RemoveWidgets:
                        foreach (var id in record.WidgetIds)
                        {
                            // Gone already (the player removed it, or it is on another toolbar profile) counts as done:
                            // without the add-on that widget never loads again.
                            await control.RemoveWidget(id).ConfigureAwait(false);
                            left = left.WithoutWidget(id);
                            changed(left);
                        }

                        break;
                    case UmbraUndoStep.RemoveRepository:
                        await control.RemoveRepository().ConfigureAwait(false);
                        if ((await control.Look().ConfigureAwait(false)).RepositoryListed)
                        {
                            return new UmbraRunResult(UmbraFailure.Unexpected, left, kept);
                        }

                        left = left with { AddedRepository = false };
                        changed(left);
                        break;
                    case UmbraUndoStep.TurnOffCustomPlugins:
                        await control.SetCustomPlugins(false).ConfigureAwait(false);
                        if ((await control.Look().ConfigureAwait(false)).CustomPluginsOn)
                        {
                            return new UmbraRunResult(UmbraFailure.Unexpected, left, kept);
                        }

                        left = left with { TurnedOnCustomPlugins = false };
                        changed(left);
                        break;
                    case UmbraUndoStep.RestartUmbra:
                        // The changes are saved by now; a failed restart only means Umbra applies them when it next starts.
                        var restarted = await control.Restart().ConfigureAwait(false);
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
    /// What the plan left alone is no longer Tsukimichi's to undo: a repository no longer listed, custom plugins other
    /// add-ons use or that are already off, widgets already gone.
    /// </summary>
    private static void Clear(ref UmbraSetupRecord left, Action<UmbraSetupRecord> changed)
    {
        if (!left.IsEmpty)
        {
            left = UmbraSetupRecord.Empty;
            changed(left);
        }
    }
}
