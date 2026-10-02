namespace Tsukimichi.Core.Companions;

/// <summary>How Tsukimichi learns one of another plugin's settings.</summary>
public enum SetupSource
{
    /// <summary>It cannot: Settings gives the instruction and shows "?".</summary>
    Manual,

    /// <summary>The plugin's own configuration file in Dalamud's <c>pluginConfigs</c> folder, read only.</summary>
    ConfigFile,

    /// <summary>A gate of the plugin's own IPC (<see cref="SetupRequirement.Path"/> is the key it takes).</summary>
    Ipc,
}

/// <summary>Where one recommended setting stands.</summary>
public enum SetupCheck
{
    /// <summary>It could not be read (or the plugin is not loaded): "?".</summary>
    Unknown,

    /// <summary>Set as recommended: "✓".</summary>
    Ok,

    /// <summary>Set otherwise: "✕".</summary>
    NeedsChange,
}

/// <summary>What happens to a hand-off when a setting is not as recommended.</summary>
public enum SetupImpact
{
    /// <summary>The hand-off stalls or does not happen: the button that hands work over says so.</summary>
    Blocking,

    /// <summary>The hand-off works but asks more of the player, or does more than the quest needs: listed in Setup only.</summary>
    Recommended,
}

/// <summary>Whether a rule names the values that are fine or the ones that are not.</summary>
public enum SetupMatch
{
    /// <summary>The value must be one of <see cref="SetupRequirement.Values"/>.</summary>
    AnyOf,

    /// <summary>The value must be none of <see cref="SetupRequirement.Values"/>.</summary>
    NoneOf,
}

/// <summary>A setting's value as read: whether it could be read at all, and its text (null when the source lacks it).</summary>
/// <param name="Read">The source answered (the file parsed, the gate replied).</param>
/// <param name="Value">The value as text; null when the source has no such setting, so the plugin's default applies.</param>
public readonly record struct SetupReading(bool Read, string? Value)
{
    /// <summary>The source could not be read: unknown.</summary>
    public static SetupReading Unread => default;

    /// <summary>The source was read but has no such setting: the plugin's default applies.</summary>
    public static SetupReading Missing => new(true, null);

    /// <summary>The source holds <paramref name="value"/>.</summary>
    public static SetupReading Of(string? value) => new(true, value);
}

/// <summary>
/// One setting of a companion plugin that matters to a Tsukimichi hand-off, with the value Tsukimichi recommends
/// (companion setup, 1.10). The words shown for it (its label in that plugin's window, why it matters, what to change)
/// are looked up by <see cref="Id"/> in the UI strings.
/// </summary>
/// <param name="Id">Stable id, also the strings key ("questionable.combat-module").</param>
/// <param name="Plugin">Whose setting it is.</param>
/// <param name="HandOffs">The companions whose hand-off it affects (a TextAdvance setting affects Questionable).</param>
/// <param name="Source">How it is read.</param>
/// <param name="Path">The setting's JSON path in the configuration file, or the key the IPC getter takes; null for <see cref="SetupSource.Manual"/>.</param>
/// <param name="Match">Whether <paramref name="Values"/> are the fine values or the bad ones.</param>
/// <param name="Values">Values compared as text, ignoring case; an enum lists both its number and its name.</param>
/// <param name="Default">The plugin's own default, used when the file lacks the setting; null when unknown.</param>
/// <param name="ApplyValue">The value Tsukimichi sets through the plugin's own IPC on "Apply recommended settings"; null when it cannot.</param>
/// <param name="Impact">What a wrong value does to the hand-off.</param>
/// <param name="Variant">The internal name of the one build it belongs to (GatherBuddy Reborn, Boss Mod Reborn); null for every build.</param>
/// <param name="ApplyKey">The key the IPC setter takes, when it differs from <paramref name="Path"/>.</param>
/// <param name="File">The configuration file, relative to Dalamud's <c>pluginConfigs</c> folder, when it is not <c>&lt;InternalName&gt;.json</c>.</param>
/// <param name="CoveredBy">
/// The id of another requirement that makes this one moot when it is met (Questionable sets TextAdvance up itself while
/// "Automatically configure TextAdvance" is on); this one then reads "✓".
/// </param>
public sealed record SetupRequirement(
    string Id,
    CompanionPlugin Plugin,
    IReadOnlyList<CompanionPlugin> HandOffs,
    SetupSource Source,
    string? Path,
    SetupMatch Match,
    IReadOnlyList<string> Values,
    string? Default,
    string? ApplyValue,
    SetupImpact Impact,
    string? Variant = null,
    string? ApplyKey = null,
    string? File = null,
    string? CoveredBy = null)
{
    /// <summary>Tsukimichi can set it through the plugin's own IPC.</summary>
    public bool CanApply => ApplyValue is not null;

    /// <summary>The key the IPC setter takes.</summary>
    public string? SetterKey => ApplyKey ?? Path;

    /// <summary>The value is a fine one.</summary>
    public bool Accepts(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var listed = false;
        foreach (var candidate in Values)
        {
            if (string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                listed = true;
                break;
            }
        }

        return Match == SetupMatch.AnyOf ? listed : !listed;
    }

    /// <summary>It applies to the build found: loaded, and of its <see cref="Variant"/> when it names one.</summary>
    public bool AppliesTo(CompanionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.Plugin == Plugin
            && status.IsLoaded
            && (Variant is null || string.Equals(Variant, status.Variant.InternalName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One requirement and where it stands.</summary>
/// <param name="Requirement">The recommended setting.</param>
/// <param name="Check">"✓", "✕" or "?".</param>
/// <param name="Value">The value read (or the default it fell back to); null when unknown.</param>
/// <param name="Covered">Read "✓" because the requirement it names in <see cref="SetupRequirement.CoveredBy"/> is met.</param>
public sealed record SetupResult(SetupRequirement Requirement, SetupCheck Check, string? Value, bool Covered = false);

/// <summary>Where a companion plugin's setup stands as a whole.</summary>
public enum PluginSetupState
{
    /// <summary>Not loaded: nothing to check.</summary>
    NotLoaded,

    /// <summary>Nothing recommended for it.</summary>
    NothingToSet,

    /// <summary>Every readable setting is as recommended (some may be unknown).</summary>
    Ready,

    /// <summary>At least one setting is not as recommended.</summary>
    NeedsSetup,
}

/// <summary>A companion's setup: its results in catalog order and the state they add up to.</summary>
public sealed record PluginSetup(CompanionStatus Status, IReadOnlyList<SetupResult> Results, PluginSetupState State)
{
    public CompanionPlugin Plugin => Status.Plugin;

    /// <summary>How many settings read "?".</summary>
    public int UnknownCount => Results.Count(static r => r.Check == SetupCheck.Unknown);

    /// <summary>How many settings read "✕".</summary>
    public int NeedsChangeCount => Results.Count(static r => r.Check == SetupCheck.NeedsChange);

    /// <summary>The settings "Apply recommended settings" would change: not as recommended, and settable.</summary>
    public IReadOnlyList<SetupResult> Applicable => Results.Where(static r => r.Check == SetupCheck.NeedsChange && r.Requirement.CanApply).ToList();

    /// <summary>
    /// The settings an Apply the player confirmed may change: those of <paramref name="confirmed"/> (requirement ids, the
    /// list the confirmation showed) still <see cref="Applicable"/> as read now. One set meanwhile (by hand, or a plugin
    /// reloaded) is left alone, and nothing the confirmation did not list is ever added.
    /// </summary>
    public IReadOnlyList<SetupResult> ApplicableOf(IReadOnlyCollection<string> confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        return Applicable.Where(r => confirmed.Contains(r.Requirement.Id)).ToList();
    }
}

/// <summary>The line at the top of Companion plugins: whether everything full automation needs is loaded and set up.</summary>
/// <param name="NeedSetup">Loaded companions with a setting not as recommended, in catalog order.</param>
/// <param name="NotLoaded">Companions full automation needs that are missing, turned off or outdated, in catalog order.</param>
/// <param name="Unknown">Settings that could not be read across the loaded companions.</param>
public sealed record CompanionSetupSummary(IReadOnlyList<CompanionPlugin> NeedSetup, IReadOnlyList<CompanionPlugin> NotLoaded, int Unknown)
{
    /// <summary>Everything full automation needs is loaded and set as recommended.</summary>
    public bool Ready => NeedSetup.Count == 0 && NotLoaded.Count == 0;
}

/// <summary>
/// Evaluates the recommended settings (<see cref="CompanionSetupCatalog"/>) against what was read. Pure: the plugin
/// reads the files and gates, this decides "✓", "✕" or "?", the per-plugin state, the summary line and the reason a
/// hand-off button gives.
/// </summary>
public static class CompanionSetupEvaluator
{
    /// <summary>"✓", "✕" or "?" for one requirement given what was read. A setting the source lacks takes the plugin's default.</summary>
    public static SetupCheck Evaluate(SetupRequirement requirement, SetupReading reading)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirement.Source == SetupSource.Manual || !reading.Read)
        {
            return SetupCheck.Unknown;
        }

        var value = reading.Value ?? requirement.Default;
        if (value is null)
        {
            return SetupCheck.Unknown;
        }

        return requirement.Accepts(value) ? SetupCheck.Ok : SetupCheck.NeedsChange;
    }

    /// <summary>
    /// A companion's setup: each requirement of its build (<see cref="SetupRequirement.AppliesTo"/>) read through
    /// <paramref name="read"/>. A companion that is not loaded is <see cref="PluginSetupState.NotLoaded"/> with no results.
    /// </summary>
    public static PluginSetup Evaluate(CompanionStatus status, IReadOnlyList<SetupRequirement> requirements, Func<SetupRequirement, SetupReading> read)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(read);
        if (!status.IsLoaded)
        {
            return new PluginSetup(status, [], PluginSetupState.NotLoaded);
        }

        var results = new List<SetupResult>();
        foreach (var requirement in requirements)
        {
            if (!requirement.AppliesTo(status))
            {
                continue;
            }

            var reading = requirement.Source == SetupSource.Manual ? SetupReading.Unread : read(requirement);
            results.Add(new SetupResult(requirement, Evaluate(requirement, reading), reading.Value ?? (reading.Read ? requirement.Default : null)));
        }

        return new PluginSetup(status, results, StateOf(results));
    }

    /// <summary>
    /// Every companion's setup with <see cref="SetupRequirement.CoveredBy"/> applied: a requirement whose covering
    /// requirement reads "✓" (in any companion's setup) reads "✓" too, marked <see cref="SetupResult.Covered"/>, and the
    /// states are summed again. A covering requirement that is unknown or not as recommended covers nothing.
    /// </summary>
    public static IReadOnlyList<PluginSetup> ApplyCoverage(IReadOnlyList<PluginSetup> setups)
    {
        ArgumentNullException.ThrowIfNull(setups);
        var met = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setup in setups)
        {
            foreach (var result in setup.Results)
            {
                if (result.Check == SetupCheck.Ok && !result.Covered)
                {
                    met.Add(result.Requirement.Id);
                }
            }
        }

        var output = new PluginSetup[setups.Count];
        for (var i = 0; i < setups.Count; i++)
        {
            var setup = setups[i];
            if (!setup.Results.Any(r => r.Requirement.CoveredBy is { } by && met.Contains(by) && r.Check != SetupCheck.Ok))
            {
                output[i] = setup;
                continue;
            }

            var results = setup.Results
                .Select(r => r.Requirement.CoveredBy is { } by && met.Contains(by) && r.Check != SetupCheck.Ok ? r with { Check = SetupCheck.Ok, Covered = true } : r)
                .ToList();
            output[i] = setup with { Results = results, State = StateOf(results) };
        }

        return output;
    }

    private static PluginSetupState StateOf(IReadOnlyList<SetupResult> results) =>
        results.Count == 0
            ? PluginSetupState.NothingToSet
            : results.Any(static r => r.Check == SetupCheck.NeedsChange) ? PluginSetupState.NeedsSetup : PluginSetupState.Ready;

    /// <summary>The summary line's counts over every companion's setup, in catalog order.</summary>
    public static CompanionSetupSummary Summarize(IReadOnlyList<PluginSetup> setups)
    {
        ArgumentNullException.ThrowIfNull(setups);
        var needSetup = new List<CompanionPlugin>();
        var notLoaded = new List<CompanionPlugin>();
        var unknown = 0;
        foreach (var setup in setups)
        {
            switch (setup.State)
            {
                case PluginSetupState.NeedsSetup:
                    needSetup.Add(setup.Plugin);
                    break;
                case PluginSetupState.NotLoaded when setup.Status.Definition.ForAutomation:
                    notLoaded.Add(setup.Plugin);
                    break;
            }

            unknown += setup.UnknownCount;
        }

        return new CompanionSetupSummary(needSetup, notLoaded, unknown);
    }

    /// <summary>
    /// The first blocking setting not as recommended that affects <paramref name="handOff"/>, across every companion's
    /// setup; null when none. An unknown setting never blocks.
    /// </summary>
    public static SetupResult? BlockingFor(CompanionPlugin handOff, IReadOnlyList<PluginSetup> setups)
    {
        ArgumentNullException.ThrowIfNull(setups);
        foreach (var setup in setups)
        {
            foreach (var result in setup.Results)
            {
                if (result.Check == SetupCheck.NeedsChange
                    && result.Requirement.Impact == SetupImpact.Blocking
                    && result.Requirement.HandOffs.Contains(handOff))
                {
                    return result;
                }
            }
        }

        return null;
    }
}
