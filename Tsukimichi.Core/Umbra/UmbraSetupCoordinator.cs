namespace Tsukimichi.Core.Umbra;

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

/// <summary>What the card and Settings draw from (<see cref="UmbraSetupCoordinator.View"/>).</summary>
/// <param name="Activity">What runs now.</param>
/// <param name="Step">The add step running now.</param>
/// <param name="UndoStep">The undo step running now.</param>
/// <param name="Plan">The steps the last "Agree and add" planned (for the progress list).</param>
/// <param name="Outcome">How the last "Agree and add" ended; null before one ends.</param>
/// <param name="Failure">Why it failed.</param>
/// <param name="RemoveDone">The last "Remove from Umbra" ended (<see cref="RemoveFailure"/> says how).</param>
/// <param name="RemoveFailure">Why the last Remove stopped, or None.</param>
/// <param name="KeptCustomPluginsOn">The last run left custom plugins on because the player added add-ons since.</param>
public readonly record struct UmbraSetupView(
    UmbraSetupActivity Activity,
    UmbraSetupStep? Step,
    UmbraUndoStep? UndoStep,
    IReadOnlyList<UmbraSetupStep> Plan,
    UmbraSetupOutcome? Outcome,
    UmbraFailure Failure,
    bool RemoveDone,
    UmbraFailure RemoveFailure,
    bool KeptCustomPluginsOn)
{
    /// <summary>Nothing has run.</summary>
    public static UmbraSetupView Idle { get; } = new(UmbraSetupActivity.Idle, null, null, [], null, UmbraFailure.None, false, UmbraFailure.None, false);
}

/// <summary>A look at Umbra before the player agrees: what would change, or why nothing can.</summary>
/// <param name="Ready">The look finished.</param>
/// <param name="Failure">Why Umbra can't be set up from here (nothing would change); None when it can.</param>
/// <param name="Plan">What "Agree and add" would change.</param>
/// <param name="Profile">The Umbra configuration profile it looked at; null when it could not look.</param>
public readonly record struct UmbraSetupPreview(bool Ready, UmbraFailure Failure, IReadOnlyList<UmbraSetupStep> Plan, string? Profile)
{
    /// <summary>Not looked yet.</summary>
    public static UmbraSetupPreview None { get; } = new(false, UmbraFailure.None, [], null);
}

/// <summary>
/// Adding Tsukimichi for Umbra and removing it again (the plugin's <c>Game/UmbraAddonSetupService.cs</c> wraps it):
/// one run at a time, on a worker; the record book kept current after every change (<see cref="Book"/>, saved through
/// <c>save</c>); the wait for the add-on's IPC answer (<see cref="Tick"/>). <see cref="AddToUmbra"/> is reached only from
/// the card's "Agree and add" and <see cref="RemoveFromUmbra"/> only from Settings' Remove confirmation (source-linted).
/// </summary>
public sealed class UmbraSetupCoordinator
{
    private readonly IUmbraControl control;
    private readonly Action<UmbraSetupBook> save;
    private readonly Action<string> log;
    private readonly object gate = new();

    private UmbraSetupBook book;
    private UmbraSetupView view = UmbraSetupView.Idle;
    private UmbraSetupPreview preview = UmbraSetupPreview.None;
    private bool previewing;
    private bool stopped;
    private double? waitStarted;

    /// <param name="control">Umbra.</param>
    /// <param name="book">The saved records.</param>
    /// <param name="save">Saves the book (called from a worker, in order, after every change).</param>
    /// <param name="log">A line for the plugin's log.</param>
    public UmbraSetupCoordinator(IUmbraControl control, UmbraSetupBook book, Action<UmbraSetupBook> save, Action<string> log)
    {
        this.control = control ?? throw new ArgumentNullException(nameof(control));
        this.book = book ?? throw new ArgumentNullException(nameof(book));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>What Tsukimichi changed in Umbra and has not undone, per Umbra profile.</summary>
    public UmbraSetupBook Book
    {
        get
        {
            lock (gate)
            {
                return book;
            }
        }
    }

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

    /// <summary>Something runs (adding, putting back, waiting for the add-on, or removing).</summary>
    public bool Busy => View.Activity != UmbraSetupActivity.Idle;

    /// <summary>Looks at Umbra (changes nothing) so the confirmation can say what would change, or why nothing can.</summary>
    public Task RefreshPreview()
    {
        lock (gate)
        {
            if (previewing || stopped)
            {
                return Task.CompletedTask;
            }

            previewing = true;
            preview = UmbraSetupPreview.None;
        }

        return Task.Run(async () =>
        {
            UmbraSetupPreview next;
            try
            {
                var opened = await control.Open().ConfigureAwait(false);
                if (opened is { Failure: UmbraFailure.None, Session: { } session })
                {
                    var look = await session.Look().ConfigureAwait(false);
                    next = new UmbraSetupPreview(true, UmbraFailure.None, UmbraAddonSetup.Plan(look), look.Profile);
                }
                else
                {
                    next = new UmbraSetupPreview(true, opened.Failure == UmbraFailure.None ? UmbraFailure.UnknownUmbra : opened.Failure, [], null);
                }
            }
            catch (Exception ex)
            {
                log($"Umbra setup: could not look at Umbra ({ex.GetType().Name}: {ex.Message})");
                next = new UmbraSetupPreview(true, UmbraFailure.UnknownUmbra, [], null);
            }

            lock (gate)
            {
                preview = next;
                previewing = false;
            }
        });
    }

    /// <summary>
    /// "Agree and add": the player's click is their agreement. Runs the plan on a worker and returns that run; null when
    /// something runs already (one run at a time) or the coordinator stopped.
    /// </summary>
    public Task? AddToUmbra()
    {
        IReadOnlyList<UmbraSetupStep> plan;
        UmbraSetupBook start;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Idle || stopped)
            {
                return null;
            }

            plan = preview is { Ready: true, Failure: UmbraFailure.None } ? preview.Plan : [];
            view = view with { Activity = UmbraSetupActivity.Adding, Step = null, UndoStep = null, Plan = plan, Outcome = null, Failure = UmbraFailure.None, KeptCustomPluginsOn = false };
            waitStarted = null;
            start = book;
        }

        log($"Umbra setup: the player agreed; adding Tsukimichi for Umbra ({string.Join(", ", plan)})");
        return Task.Run(async () =>
        {
            UmbraRunResult result;
            try
            {
                result = await UmbraSetupRunner.Add(
                    control,
                    step => Update(v => v with { Step = step, Plan = v.Plan.Contains(step) ? v.Plan : [.. v.Plan, step] }),
                    undo => Update(v => v with { Activity = UmbraSetupActivity.PuttingBack, UndoStep = undo }),
                    run => Keep(b => b.With(start.For(run.Profile).Merge(run)))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log($"Umbra setup: adding failed ({ex.GetType().Name}: {ex.Message})");
                result = new UmbraRunResult(UmbraFailure.Unexpected, UmbraSetupRecord.Empty, false);
            }

            if (result.Failure == UmbraFailure.None)
            {
                log("Umbra setup: every step went through; waiting for Tsukimichi for Umbra to answer");
                Update(v => v with { Activity = UmbraSetupActivity.Waiting, Step = null, UndoStep = null });
            }
            else
            {
                log($"Umbra setup: stopped ({result.Failure}); Umbra put back: {result.PutBack}");
                Update(v => v with
                {
                    Activity = UmbraSetupActivity.Idle,
                    Step = null,
                    UndoStep = null,
                    Outcome = UmbraAddonSetup.OutcomeOf(result.Failure, result.PutBack, UmbraHelloWait.Waiting),
                    Failure = result.Failure,
                    KeptCustomPluginsOn = result.KeptCustomPluginsOn,
                });
            }
        });
    }

    /// <summary>
    /// "Remove from Umbra": undoes Tsukimichi's changes on the Umbra profile Umbra runs now. Returns that run; null when
    /// something runs already, there is nothing to undo, or the coordinator stopped.
    /// </summary>
    public Task? RemoveFromUmbra()
    {
        UmbraSetupBook start;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Idle || stopped || book.IsEmpty)
            {
                return null;
            }

            start = book;
            view = view with { Activity = UmbraSetupActivity.Removing, UndoStep = null, RemoveDone = false, RemoveFailure = UmbraFailure.None, KeptCustomPluginsOn = false, Outcome = null };
        }

        log("Umbra setup: removing what Tsukimichi changed in Umbra");
        return Task.Run(async () =>
        {
            UmbraRunResult result;
            try
            {
                result = await UmbraSetupRunner.Remove(control, start, undo => Update(v => v with { UndoStep = undo }), left => Keep(b => b.With(left))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log($"Umbra setup: removing failed ({ex.GetType().Name}: {ex.Message})");
                result = new UmbraRunResult(UmbraFailure.Unexpected, UmbraSetupRecord.Empty, false);
            }

            log($"Umbra setup: remove ended ({result.Failure}); left: {(result.Left.IsEmpty ? "nothing" : "some")}");
            Update(v => v with
            {
                Activity = UmbraSetupActivity.Idle,
                UndoStep = null,
                RemoveDone = true,
                RemoveFailure = result.Failure,
                KeptCustomPluginsOn = result.KeptCustomPluginsOn,
            });
        });
    }

    /// <summary>
    /// Once per frame: while waiting for the add-on, Added once it answered, Not confirmed after
    /// <see cref="UmbraAddonSetup.HelloTimeoutSeconds"/> from the first tick of the wait.
    /// </summary>
    /// <param name="answered">The add-on said hello over IPC.</param>
    /// <param name="nowSeconds">A steady clock, in seconds.</param>
    public void Tick(bool answered, double nowSeconds)
    {
        UmbraHelloWait wait;
        lock (gate)
        {
            if (view.Activity != UmbraSetupActivity.Waiting)
            {
                return;
            }

            waitStarted ??= nowSeconds;
            wait = UmbraAddonSetup.HelloWait(answered, nowSeconds - waitStarted.Value);
            if (wait == UmbraHelloWait.Waiting)
            {
                return;
            }

            view = view with { Activity = UmbraSetupActivity.Idle, Outcome = UmbraAddonSetup.OutcomeOf(UmbraFailure.None, true, wait), Failure = UmbraFailure.None };
        }

        log($"Umbra setup: Tsukimichi for Umbra {(wait == UmbraHelloWait.Answered ? "answered" : "did not answer in time")}");
    }

    /// <summary>No new run starts (Tsukimichi unloads); a running one ends at its next Umbra call, which the control refuses.</summary>
    public void Stop()
    {
        lock (gate)
        {
            stopped = true;
        }
    }

    private void Update(Func<UmbraSetupView, UmbraSetupView> change)
    {
        lock (gate)
        {
            view = change(view);
        }
    }

    /// <summary>The book after <paramref name="change"/>, kept and saved (also while stopping: a change made in Umbra is never forgotten).</summary>
    private void Keep(Func<UmbraSetupBook, UmbraSetupBook> change)
    {
        UmbraSetupBook next;
        lock (gate)
        {
            book = change(book);
            next = book;
        }

        save(next);
    }
}
