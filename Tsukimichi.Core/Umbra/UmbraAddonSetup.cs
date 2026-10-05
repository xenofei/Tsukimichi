using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Umbra;

/// <summary>The add-on Tsukimichi offers to set up in Umbra, as Umbra names it.</summary>
public static class UmbraAddon
{
    /// <summary>The GitHub owner of the add-on's repository, as Umbra's Plugins settings take it.</summary>
    public const string RepositoryOwner = "xenofei";

    /// <summary>The add-on's repository name.</summary>
    public const string RepositoryName = "Tsukimichi.Umbra";

    /// <summary>"xenofei/Tsukimichi.Umbra".</summary>
    public const string Repository = RepositoryOwner + "/" + RepositoryName;

    /// <summary>The add-on's main widget id (its <c>[ToolbarWidget("Tsukimichi", …)]</c>): the one Tsukimichi places.</summary>
    public const string WidgetId = "Tsukimichi";

    /// <summary>The toolbar panel the widget goes in: Umbra's own "Left", "Center" or "Right" (Toolbar.Nodes.cs).</summary>
    public const string WidgetPanel = "Right";

    /// <summary>The Umbra version whose source this was written against (3.1.18.0); others are driven only when every member matches.</summary>
    public const string TestedUmbraVersion = "3.1.18.0";
}

/// <summary>
/// What Umbra holds now, read through Umbra's own code (<see cref="IUmbraControl.Look"/>).
/// </summary>
/// <param name="CustomPluginsOn">Umbra's custom plugins are on (<c>PluginManager.CustomPluginsEnabled</c>).</param>
/// <param name="RepositoryListed">Umbra's plugin list has an entry from the add-on's repository.</param>
/// <param name="WidgetRegistered">Umbra knows the add-on's widget: the add-on is loaded in Umbra.</param>
/// <param name="WidgetPlaced">The active toolbar profile already has a Tsukimichi widget.</param>
/// <param name="OtherAddons">How many entries in Umbra's plugin list are not the add-on's.</param>
public readonly record struct UmbraLook(bool CustomPluginsOn, bool RepositoryListed, bool WidgetRegistered, bool WidgetPlaced, int OtherAddons);

/// <summary>One change "Add to Umbra" makes, in the order it makes them.</summary>
public enum UmbraSetupStep : byte
{
    /// <summary>Turn on Umbra's custom plugins (Umbra's own "I agree" switch).</summary>
    TurnOnCustomPlugins,

    /// <summary>Umbra adds the repository: it reads the latest release on GitHub, downloads it, checks it and lists it.</summary>
    AddRepository,

    /// <summary>Umbra restarts its toolbar (its own Restart), which is how Umbra loads a newly listed add-on.</summary>
    RestartUmbra,

    /// <summary>Umbra places the Tsukimichi widget on the active toolbar profile.</summary>
    PlaceWidget,
}

/// <summary>One change "Remove from Umbra" makes, in the order it makes them.</summary>
public enum UmbraUndoStep : byte
{
    /// <summary>Remove the widgets Tsukimichi placed (only those).</summary>
    RemoveWidgets,

    /// <summary>Remove the repository entry Tsukimichi added (only when it added it).</summary>
    RemoveRepository,

    /// <summary>Turn custom plugins off again (only when Tsukimichi turned them on and no other add-on uses them).</summary>
    TurnOffCustomPlugins,

    /// <summary>Umbra restarts its toolbar, so the removed add-on unloads.</summary>
    RestartUmbra,
}

/// <summary>Why setting up (or removing) the add-on stopped. <see cref="None"/> is success.</summary>
public enum UmbraFailure : byte
{
    /// <summary>Nothing went wrong.</summary>
    None,

    /// <summary>Umbra isn't running, or is between starts (it starts after login).</summary>
    NotRunning,

    /// <summary>A member Tsukimichi drives is missing or changed: this Umbra version is not one it knows. Nothing was changed.</summary>
    UnknownUmbra,

    /// <summary>Umbra couldn't read the add-on's latest release on GitHub.</summary>
    ReleaseUnreachable,

    /// <summary>Umbra downloaded the release but wouldn't take it (made for another Umbra version, or no add-on in it).</summary>
    ReleaseRefused,

    /// <summary>Umbra restarted but did not load the add-on.</summary>
    NotLoaded,

    /// <summary>Umbra did not place the widget.</summary>
    WidgetNotPlaced,

    /// <summary>A step took too long (a download or a restart).</summary>
    TimedOut,

    /// <summary>Umbra threw while doing a step.</summary>
    Unexpected,
}

/// <summary>What the player sees after "Agree and add" (<see cref="UmbraAddonSetup.OutcomeOf"/>).</summary>
public enum UmbraSetupOutcome : byte
{
    /// <summary>The add-on answered over IPC: it runs, and the widget is on the bar.</summary>
    Added,

    /// <summary>Every step went through, but the add-on did not answer in time.</summary>
    NotConfirmed,

    /// <summary>A step failed and everything this run changed was put back.</summary>
    Failed,

    /// <summary>A step failed and putting things back failed too: some changes may remain (Remove from Umbra undoes them).</summary>
    FailedPartly,
}

/// <summary>
/// What Tsukimichi changed in Umbra and has not undone: the record "Remove from Umbra" reverses, kept in Tsukimichi's
/// configuration. Only Tsukimichi's own changes are in it: a switch the player had on, a repository they had added, or a
/// widget they placed themselves never is.
/// </summary>
/// <param name="TurnedOnCustomPlugins">Tsukimichi turned Umbra's custom plugins on (they were off).</param>
/// <param name="AddedRepository">Tsukimichi added the repository entry (it was not listed).</param>
/// <param name="WidgetIds">The instance ids of the widgets Tsukimichi placed.</param>
public sealed record UmbraSetupRecord(bool TurnedOnCustomPlugins, bool AddedRepository, IReadOnlyList<string> WidgetIds)
{
    /// <summary>Nothing changed.</summary>
    public static UmbraSetupRecord Empty { get; } = new(false, false, []);

    /// <summary>Nothing to undo.</summary>
    public bool IsEmpty => !TurnedOnCustomPlugins && !AddedRepository && WidgetIds.Count == 0;

    /// <summary>This record and <paramref name="run"/>'s changes together (a later run adds to what an earlier one did).</summary>
    public UmbraSetupRecord Merge(UmbraSetupRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new UmbraSetupRecord(
            TurnedOnCustomPlugins || run.TurnedOnCustomPlugins,
            AddedRepository || run.AddedRepository,
            [.. WidgetIds.Concat(run.WidgetIds).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>This record with <paramref name="widgetId"/> gone.</summary>
    public UmbraSetupRecord WithoutWidget(string widgetId) =>
        this with { WidgetIds = [.. WidgetIds.Where(id => !string.Equals(id, widgetId, StringComparison.Ordinal))] };

    /// <summary>This record with <paramref name="widgetId"/> added.</summary>
    public UmbraSetupRecord WithWidget(string widgetId) =>
        WidgetIds.Contains(widgetId, StringComparer.Ordinal) ? this : this with { WidgetIds = [.. WidgetIds, widgetId] };
}

/// <summary>What the "Add Tsukimichi to your Umbra bar?" card does this frame (<see cref="UmbraAddonSetup.Next"/>).</summary>
public enum UmbraOfferStep : byte
{
    /// <summary>Nothing: the player answered it before.</summary>
    Nothing,

    /// <summary>Not yet: Umbra or its settings aren't read, the add-on may still answer, another popup goes first, or it is not a quiet moment.</summary>
    Wait,

    /// <summary>Show the card now.</summary>
    Show,

    /// <summary>Never show it, and record it as answered: the add-on already runs (it said hello).</summary>
    Retire,
}

/// <summary>Where the card stands, as far as <see cref="UmbraAddonSetup.Next"/> cares.</summary>
/// <param name="Answered">The player answered the card before (Not now, Agree and add, or closed it): persisted.</param>
/// <param name="UmbraLoaded">Umbra is installed and loaded in Dalamud.</param>
/// <param name="SettingsRead">Umbra's settings have been read since it loaded, so "not listed" is known, not a guess.</param>
/// <param name="AddonHello">The add-on said hello over IPC this session: it runs.</param>
/// <param name="AddonPresent">The add-on is listed in Umbra with custom plugins on, or said hello.</param>
/// <param name="Busy">Setting up or removing runs (started from Settings).</param>
/// <param name="OtherFirst">What's new or the tour (and its offer) is due or on screen.</param>
public readonly record struct UmbraOfferState(bool Answered, bool UmbraLoaded, bool SettingsRead, bool AddonHello, bool AddonPresent, bool Busy, bool OtherFirst);

/// <summary>Where the wait for the add-on's IPC answer stands after the steps went through.</summary>
public enum UmbraHelloWait : byte
{
    /// <summary>Still waiting (Umbra may still be loading it).</summary>
    Waiting,

    /// <summary>It answered.</summary>
    Answered,

    /// <summary>It did not answer within <see cref="UmbraAddonSetup.HelloTimeoutSeconds"/>.</summary>
    TimedOut,
}

/// <summary>
/// Setting up Tsukimichi for Umbra from Tsukimichi (the owner's request: "automate, as far as possible, adding the
/// add-on to Umbra and enabling any settings needed so it displays"), as pure rules so they are tested. The plugin drives
/// Umbra through Umbra's own code (<see cref="IUmbraControl"/>), only after the player's click on "Agree and add".
/// <list type="bullet">
/// <item><b>The card</b> shows once, at the first quiet moment, to a player who runs Umbra without the add-on, after
/// What's new and the tour; it steps aside in a fight, a duty, a cutscene, Group Pose, a loading
/// screen, outside the world and while anything that goes first is on screen (<see cref="Next"/>, <see cref="Visible"/>).</item>
/// <item><b>The plan</b> is only what is missing (<see cref="Plan"/>): custom plugins that are already on stay as they
/// are, a listed repository is not added again, and a placed widget is not placed twice.</item>
/// <item><b>Undo</b> reverses only what Tsukimichi did (<see cref="UndoPlan"/>, <see cref="UmbraSetupRecord"/>).</item>
/// <item><b>A failed step</b> puts back what that run changed; the outcome says whether that worked
/// (<see cref="OutcomeOf"/>), and the manual steps show whenever the add-on is not confirmed.</item>
/// </list>
/// </summary>
public static class UmbraAddonSetup
{
    /// <summary>How long the add-on has to answer over IPC once every step went through, in seconds.</summary>
    public const double HelloTimeoutSeconds = 30.0;

    /// <summary>What the card does this frame.</summary>
    public static UmbraOfferStep Next(in UmbraOfferState state, in WhatsNewMoment moment)
    {
        if (state.Answered)
        {
            return UmbraOfferStep.Nothing;
        }

        if (state.AddonHello)
        {
            // The add-on runs: the player has it, so the card is never needed.
            return UmbraOfferStep.Retire;
        }

        if (!state.UmbraLoaded || !state.SettingsRead || state.AddonPresent || state.Busy)
        {
            // Not known yet, listed and about to answer, or already being set up from Settings.
            return UmbraOfferStep.Wait;
        }

        return state.OtherFirst || !WhatsNew.IsQuietMoment(moment) ? UmbraOfferStep.Wait : UmbraOfferStep.Show;
    }

    /// <summary>
    /// Whether the open card draws this frame: in the world, not in a fight, a duty, a cutscene, Group Pose or a loading
    /// screen, and with nothing that goes first on screen. Hidden, it stays open (a running setup keeps going) and comes back.
    /// </summary>
    public static bool Visible(in WhatsNewMoment moment, bool otherFirst) =>
        moment.InWorld && !moment.InCombat && !moment.InDuty && !moment.InCutscene && !moment.GroupPose && !moment.Loading && !otherFirst;

    /// <summary>
    /// The changes "Add to Umbra" makes, given what Umbra holds: only what is missing. A restart follows any change that
    /// Umbra loads only at start (custom plugins, a new repository), and is needed whenever the add-on isn't loaded yet.
    /// Nothing at all when the add-on is loaded and its widget is on the bar.
    /// </summary>
    public static IReadOnlyList<UmbraSetupStep> Plan(in UmbraLook look)
    {
        var steps = new List<UmbraSetupStep>(4);
        if (!look.CustomPluginsOn)
        {
            steps.Add(UmbraSetupStep.TurnOnCustomPlugins);
        }

        if (!look.RepositoryListed)
        {
            steps.Add(UmbraSetupStep.AddRepository);
        }

        if (steps.Count > 0 || !look.WidgetRegistered)
        {
            steps.Add(UmbraSetupStep.RestartUmbra);
        }

        if (!look.WidgetPlaced)
        {
            steps.Add(UmbraSetupStep.PlaceWidget);
        }

        return steps;
    }

    /// <summary>
    /// The changes "Remove from Umbra" makes: only Tsukimichi's own. Its widgets come off; the repository goes only if
    /// Tsukimichi added it and it is still listed; custom plugins turn off only if Tsukimichi turned them on, they are
    /// still on, and no other add-on is listed (turning them off would unload the player's own). A restart follows when
    /// the repository went or custom plugins turned off, so the add-on unloads.
    /// </summary>
    public static IReadOnlyList<UmbraUndoStep> UndoPlan(UmbraSetupRecord record, in UmbraLook look)
    {
        ArgumentNullException.ThrowIfNull(record);
        var steps = new List<UmbraUndoStep>(4);
        if (record.WidgetIds.Count > 0)
        {
            steps.Add(UmbraUndoStep.RemoveWidgets);
        }

        if (record.AddedRepository && look.RepositoryListed)
        {
            steps.Add(UmbraUndoStep.RemoveRepository);
        }

        if (KeepsCustomPluginsOn(record, look) is false)
        {
            steps.Add(UmbraUndoStep.TurnOffCustomPlugins);
        }

        if (steps.Contains(UmbraUndoStep.RemoveRepository) || steps.Contains(UmbraUndoStep.TurnOffCustomPlugins))
        {
            steps.Add(UmbraUndoStep.RestartUmbra);
        }

        return steps;
    }

    /// <summary>
    /// Whether custom plugins stay on although Tsukimichi turned them on: another add-on is listed now. Null when
    /// Tsukimichi did not turn them on (there is nothing of its own to keep or undo), or they are already off.
    /// </summary>
    public static bool? KeepsCustomPluginsOn(UmbraSetupRecord record, in UmbraLook look)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.TurnedOnCustomPlugins || !look.CustomPluginsOn)
        {
            return null;
        }

        return look.OtherAddons > 0;
    }

    /// <summary>Where the wait for the add-on's answer stands, <paramref name="seconds"/> after the last step went through.</summary>
    public static UmbraHelloWait HelloWait(bool answered, double seconds) =>
        answered ? UmbraHelloWait.Answered : seconds >= HelloTimeoutSeconds ? UmbraHelloWait.TimedOut : UmbraHelloWait.Waiting;

    /// <summary>
    /// What the player sees after the steps ran: Added once the add-on answered, Not confirmed when every step went
    /// through but it did not answer in time, Failed when a step failed and the run's changes were put back, Failed
    /// partly when putting them back failed too.
    /// </summary>
    /// <param name="failure">Why the steps stopped; <see cref="UmbraFailure.None"/> when they all went through.</param>
    /// <param name="putBack">After a failure: everything the run changed was undone.</param>
    /// <param name="hello">After success: where the wait for the add-on's answer stands.</param>
    public static UmbraSetupOutcome? OutcomeOf(UmbraFailure failure, bool putBack, UmbraHelloWait hello)
    {
        if (failure != UmbraFailure.None)
        {
            return putBack ? UmbraSetupOutcome.Failed : UmbraSetupOutcome.FailedPartly;
        }

        return hello switch
        {
            UmbraHelloWait.Answered => UmbraSetupOutcome.Added,
            UmbraHelloWait.TimedOut => UmbraSetupOutcome.NotConfirmed,
            _ => null,
        };
    }

    /// <summary>Whether the outcome shows the manual steps: whenever the add-on is not confirmed.</summary>
    public static bool ShowsManualSteps(UmbraSetupOutcome outcome) => outcome != UmbraSetupOutcome.Added;

    /// <summary>Whether "Try again" is offered: a failure that may pass (the network, a slow restart, Umbra not running yet).</summary>
    public static bool CanTryAgain(UmbraFailure failure) =>
        failure is UmbraFailure.ReleaseUnreachable or UmbraFailure.TimedOut or UmbraFailure.NotRunning;
}
