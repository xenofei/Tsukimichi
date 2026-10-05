using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Game;

/// <summary>What the Umbra setup is doing now.</summary>
public enum UmbraSetupActivity : byte
{
    /// <summary>Nothing runs.</summary>
    Idle,

    /// <summary>"Agree and add" runs its steps (<see cref="UmbraSetupView.Step"/>).</summary>
    Adding,

    /// <summary>A step failed: what the run changed is being put back (<see cref="UmbraSetupView.UndoStep"/>).</summary>
    PuttingBack,

    /// <summary>Every step went through: waiting for the add-on's IPC answer.</summary>
    Waiting,

    /// <summary>"Remove from Umbra" runs (<see cref="UmbraSetupView.UndoStep"/>).</summary>
    Removing,
}

/// <summary>What the card and Settings draw from (<see cref="UmbraAddonSetupService.View"/>).</summary>
/// <param name="Activity">What runs now.</param>
/// <param name="Step">The add step running now.</param>
/// <param name="UndoStep">The undo step running now.</param>
/// <param name="Plan">The steps the last "Agree and add" planned (for the progress list).</param>
/// <param name="Outcome">How the last "Agree and add" ended; null before one ends.</param>
/// <param name="Failure">Why it failed.</param>
/// <param name="RemoveDone">The last "Remove from Umbra" ended (<see cref="RemoveFailure"/> says how).</param>
/// <param name="RemoveFailure">Why the last Remove stopped, or None.</param>
/// <param name="KeptCustomPluginsOn">The last Remove left custom plugins on for the player's other add-ons.</param>
public readonly record struct UmbraSetupView(
    UmbraSetupActivity Activity,
    UmbraSetupStep? Step,
    UmbraUndoStep? UndoStep,
    IReadOnlyList<UmbraSetupStep> Plan,
    UmbraSetupOutcome? Outcome,
    UmbraFailure Failure,
    bool RemoveDone,
    UmbraFailure RemoveFailure,
    bool KeptCustomPluginsOn);

/// <summary>A look at Umbra before the player agrees: what would change, or why nothing can.</summary>
/// <param name="Ready">The look finished.</param>
/// <param name="Failure">Why Umbra can't be set up from here (nothing would change); None when it can.</param>
/// <param name="Plan">What "Agree and add" would change.</param>
public readonly record struct UmbraSetupPreview(bool Ready, UmbraFailure Failure, IReadOnlyList<UmbraSetupStep> Plan);

/// <summary>
/// Adding Tsukimichi for Umbra to Umbra, and removing it again, for the card (<see cref="Ui.UmbraAddonCard"/>) and
/// Settings › About › Umbra. The steps run on a worker against <see cref="IUmbraControl"/> (each Umbra call on the
/// framework thread); the card and Settings read <see cref="View"/>. Each change is recorded in Tsukimichi's settings the
/// moment it is made (<see cref="Record"/>), so "Remove from Umbra" undoes exactly Tsukimichi's own changes, even after a
/// crash mid-run. <see cref="AddToUmbra"/> is called only from the "Agree and add" button and <see cref="RemoveFromUmbra"/> only
/// from Settings' Remove confirmation (a source lint holds both to that).
/// </summary>
public sealed class UmbraAddonSetupService : IDisposable
{
    private readonly IUmbraControl control;
    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly UmbraProbe umbra;
    private readonly IPluginLog log;
    private readonly object gate = new();

    private UmbraSetupView view = new(UmbraSetupActivity.Idle, null, null, [], null, UmbraFailure.None, false, UmbraFailure.None, false);
    private UmbraSetupPreview preview = new(false, UmbraFailure.None, []);
    private bool previewing;
    private long waitStarted;
    private bool disposed;

    public UmbraAddonSetupService(IUmbraControl control, Configuration settings, IDalamudPluginInterface pluginInterface, IFramework framework, UmbraProbe umbra, IPluginLog log)
    {
        this.control = control ?? throw new ArgumentNullException(nameof(control));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.umbra = umbra ?? throw new ArgumentNullException(nameof(umbra));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        Record = new UmbraSetupRecord(settings.UmbraSetupTurnedOnCustomPlugins, settings.UmbraSetupAddedRepository, [.. settings.UmbraSetupWidgetIds]);
        framework.Update += Tick;
    }

    /// <summary>What Tsukimichi changed in Umbra and has not undone (kept in its settings).</summary>
    public UmbraSetupRecord Record { get; private set; }

    /// <summary>What runs now and how the last runs ended.</summary>
    public UmbraSetupView View
    {
        get
        {
            lock (gate)
            {
                return view;
            }
        }
    }

    /// <summary>Something runs (adding, putting back, waiting for the add-on, or removing).</summary>
    public bool Busy => View.Activity != UmbraSetupActivity.Idle;

    /// <summary>The last look at Umbra for the confirmation (<see cref="RefreshPreview"/>).</summary>
    public UmbraSetupPreview Preview
    {
        get
        {
            lock (gate)
            {
                return preview;
            }
        }
    }

    /// <summary>
    /// Looks at Umbra (on a worker; changes nothing) so the confirmation can say what would change, or why nothing can.
    /// </summary>
    public void RefreshPreview()
    {
        lock (gate)
        {
            if (previewing || disposed)
            {
                return;
            }

            previewing = true;
            preview = new UmbraSetupPreview(false, UmbraFailure.None, []);
        }

        _ = Task.Run(async () =>
        {
            UmbraSetupPreview next;
            try
            {
                var failure = await control.Prepare().ConfigureAwait(false);
                next = failure != UmbraFailure.None
                    ? new UmbraSetupPreview(true, failure, [])
                    : new UmbraSetupPreview(true, UmbraFailure.None, UmbraAddonSetup.Plan(await control.Look().ConfigureAwait(false)));
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Umbra setup: could not look at Umbra");
                next = new UmbraSetupPreview(true, UmbraFailure.UnknownUmbra, []);
            }

            lock (gate)
            {
                preview = next;
                previewing = false;
            }
        });
    }

    /// <summary>
    /// "Agree and add": the player's click is their agreement. Runs the plan on a worker; false when something runs
    /// already. Call only from the Agree button (source-linted).
    /// </summary>
    public bool AddToUmbra()
    {
        IReadOnlyList<UmbraSetupStep> plan;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Idle || disposed)
            {
                return false;
            }

            plan = preview is { Ready: true, Failure: UmbraFailure.None } ? preview.Plan : [];
            view = view with { Activity = UmbraSetupActivity.Adding, Step = null, UndoStep = null, Plan = plan, Outcome = null, Failure = UmbraFailure.None };
        }

        var baseline = Record;
        log.Information("Umbra setup: the player agreed; adding Tsukimichi for Umbra ({Steps})", string.Join(", ", plan));
        _ = Task.Run(async () =>
        {
            UmbraRunResult result;
            try
            {
                result = await UmbraSetupRunner.Add(
                    control,
                    step => Update(v => v with { Step = step, Plan = v.Plan.Contains(step) ? v.Plan : [.. v.Plan, step] }),
                    undo => Update(v => v with { Activity = UmbraSetupActivity.PuttingBack, UndoStep = undo }),
                    run => Keep(baseline.Merge(run))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Umbra setup: adding failed");
                result = new UmbraRunResult(UmbraFailure.Unexpected, UmbraSetupRecord.Empty, false);
            }

            if (result.Failure == UmbraFailure.None)
            {
                log.Information("Umbra setup: every step went through; waiting for Tsukimichi for Umbra to answer");
                lock (gate)
                {
                    waitStarted = Stopwatch.GetTimestamp();
                    view = view with { Activity = UmbraSetupActivity.Waiting, Step = null, UndoStep = null };
                }
            }
            else
            {
                log.Warning("Umbra setup: stopped ({Failure}); Umbra put back: {PutBack}", result.Failure, result.PutBack);
                Update(v => v with
                {
                    Activity = UmbraSetupActivity.Idle,
                    Step = null,
                    UndoStep = null,
                    Outcome = UmbraAddonSetup.OutcomeOf(result.Failure, result.PutBack, UmbraHelloWait.Waiting),
                    Failure = result.Failure,
                });
            }
        });

        return true;
    }

    /// <summary>
    /// "Remove from Umbra": undoes Tsukimichi's own changes (<see cref="Record"/>). False when something runs already or
    /// there is nothing to undo. Call only from Settings' Remove confirmation (source-linted).
    /// </summary>
    public bool RemoveFromUmbra()
    {
        var record = Record;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Idle || disposed || record.IsEmpty)
            {
                return false;
            }

            view = view with { Activity = UmbraSetupActivity.Removing, UndoStep = null, RemoveDone = false, RemoveFailure = UmbraFailure.None, KeptCustomPluginsOn = false };
        }

        log.Information("Umbra setup: removing what Tsukimichi changed in Umbra");
        _ = Task.Run(async () =>
        {
            UmbraRunResult result;
            try
            {
                result = await UmbraSetupRunner.Remove(control, record, undo => Update(v => v with { UndoStep = undo }), Keep).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Umbra setup: removing failed");
                result = new UmbraRunResult(UmbraFailure.Unexpected, record, false);
            }

            log.Information("Umbra setup: remove ended ({Failure}); left: {Left}", result.Failure, result.Left.IsEmpty ? "nothing" : "some");
            Update(v => v with
            {
                Activity = UmbraSetupActivity.Idle,
                UndoStep = null,
                RemoveDone = true,
                RemoveFailure = result.Failure,
                KeptCustomPluginsOn = result.KeptCustomPluginsOn,
                Outcome = null,
            });
        });

        return true;
    }

    /// <summary>Opens Umbra's own settings window through Dalamud; false when Umbra isn't loaded or has none.</summary>
    public bool OpenUmbraSettings()
    {
        try
        {
            var plugin = pluginInterface.InstalledPlugins.FirstOrDefault(p => p.IsLoaded && string.Equals(p.InternalName, UmbraSettings.InternalName, StringComparison.Ordinal));
            if (plugin is not { HasConfigUi: true })
            {
                return false;
            }

            plugin.OpenConfigUi();
            return true;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Umbra's settings could not be opened");
            return false;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
        }

        framework.Update -= Tick;
    }

    /// <summary>Once per frame while waiting: Added once the add-on answers, Not confirmed after the timeout.</summary>
    private void Tick(IFramework fw)
    {
        long started;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Waiting)
            {
                return;
            }

            started = waitStarted;
        }

        var wait = UmbraAddonSetup.HelloWait(umbra.AddonVersion is not null, Stopwatch.GetElapsedTime(started).TotalSeconds);
        if (wait == UmbraHelloWait.Waiting)
        {
            return;
        }

        log.Information("Umbra setup: Tsukimichi for Umbra {Answer}", wait == UmbraHelloWait.Answered ? "answered" : "did not answer in time");
        Update(v => v with { Activity = UmbraSetupActivity.Idle, Outcome = UmbraAddonSetup.OutcomeOf(UmbraFailure.None, true, wait), Failure = UmbraFailure.None });
    }

    private void Update(Func<UmbraSetupView, UmbraSetupView> change)
    {
        lock (gate)
        {
            view = change(view);
        }
    }

    /// <summary>
    /// Keeps <paramref name="record"/> as Tsukimichi's record of its changes and saves it, on the framework thread; also
    /// while Tsukimichi unloads, so a change already made in Umbra is never forgotten.
    /// </summary>
    private void Keep(UmbraSetupRecord record)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            Record = record;
            settings.UmbraSetupTurnedOnCustomPlugins = record.TurnedOnCustomPlugins;
            settings.UmbraSetupAddedRepository = record.AddedRepository;
            settings.UmbraSetupWidgetIds = [.. record.WidgetIds];
            try
            {
                settings.Save(pluginInterface);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Umbra setup: could not save what was changed in Umbra");
            }
        });
    }
}
