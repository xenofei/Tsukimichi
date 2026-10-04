using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Text;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// The <c>/tsukimichi</c> chat command. Sub-commands are dispatched by the first word (<see cref="CommandLine.Parse"/>):
/// <c>search &lt;text&gt;</c> (or any other text) searches and prints matches to chat, suggesting a subcommand when the
/// search finds nothing and the first word was a near miss of one (<see cref="CommandLine.DidYouMean"/>);
/// <c>journal</c>, <c>moonlit</c>, <c>characters</c>, <c>flight</c> and <c>blues</c> open those tabs; <c>tour</c>
/// starts the tour; <c>zone</c> and <c>which</c> print discovery lists; <c>why [quest name]</c> prints what blocks a
/// quest; <c>route [quest name]</c> opens its unlock route; <c>recap [quest name]</c> opens the story recap; <c>nearby</c> toggles the Nearby quests window; <c>todo</c>
/// toggles the todo overlay; <c>icon</c> shows or hides the moon icon (1.22, alone: with text after it the line searches);
/// <c>report [quest name]</c> copies a quest's diagnostic block; <c>export [quests|moonlit]
/// [json|csv]</c> writes the export files; <c>stop</c> stops every hand-off Tsukimichi started (<see cref="StopCommand"/>);
/// <c>look &lt;code&gt;</c> opens Settings › Themes with a share code pasted and previewed, never applying it;
/// <c>msq</c> and <c>next</c> print plain sentences for text-to-speech and <c>go [quest name]</c> travels to the current
/// step (1.21, <see cref="GuidanceCommand"/>; with text after them that would shadow a quest name they search instead,
/// <see cref="CommandLine.RunsGuidance"/>);
/// <c>settings</c> (or <c>config</c>) and <c>help</c> open those windows; <c>glyphs</c> opens the glyph sheet and
/// <c>ipc</c> the IPC developer window (neither listed to players); a bare command toggles the main window.
/// <para>
/// Aliases (1.11.0, A12): <c>/ts</c>, <c>/moon</c> and the player's own from Settings answer exactly as <c>/tsuki</c>
/// does, subcommands included (<see cref="CommandAliases"/>). <c>/tsuki</c> stays the primary command. An alias that
/// Dalamud, the game or another plugin already answers to is skipped and named in Settings, never taken over;
/// <see cref="ApplyAliases"/> registers and unregisters them at runtime and never throws.
/// </para>
/// </summary>
public sealed class TsukimichiCommand : IDisposable
{
    public const string Name = CommandAliases.FullName;

    /// <summary>The primary short command, with the same handler; hidden from the help list so the command appears once.</summary>
    public const string Alias = CommandAliases.Primary;

    private readonly ICommandManager commands;
    private readonly Func<string, bool>? isGameCommand;
    private readonly IPluginLog? log;

    // The aliases registered now, each with its own CommandInfo so a language switch can update its help text.
    private readonly Dictionary<string, CommandInfo> aliases = new(StringComparer.Ordinal);
    private readonly Action toggleMainWindow;
    private readonly Action toggleGlyphWindow;
    private readonly Func<string, int> search;

    // Kept so a language switch can update the help text Dalamud lists (/xlhelp, the installer).
    private readonly CommandInfo mainInfo;
    private readonly CommandInfo aliasInfo;

    /// <summary>Invoked for <c>/tsukimichi ipc</c> (not listed to players): the IPC developer window; set once it exists.</summary>
    public Action? ToggleIpcWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi config</c>; set once the config window exists.</summary>
    public Action? ToggleConfigWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi help</c>; set once the help window exists. Falls back to the main window.</summary>
    public Action? ToggleHelpWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi tour</c>: opens the main window and starts the tour. Falls back to the main window.</summary>
    public Action? StartTour { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi journal|moonlit|characters|flight|blues</c>: opens the main window on that tab. Falls
    /// back to the main window.
    /// </summary>
    public Action<NavTab>? ShowTab { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi route [quest name]</c> with the rest of the line (empty for the selected quest): opens
    /// the unlock route to the quest. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Route { get; set; }

    /// <summary>Prints one line to chat (the "did you mean" line); null prints nothing.</summary>
    public Action<string>? Print { get; set; }

    /// <summary>Invoked for <c>/tsukimichi zone</c>: quests startable in the current zone. Falls back to a search for "zone".</summary>
    public Action? ListZoneQuests { get; set; }

    /// <summary>Invoked for <c>/tsukimichi which</c>: quests the targeted NPC hands out. Falls back to a search for "which".</summary>
    public Action? ListTargetQuests { get; set; }

    /// <summary>Invoked for <c>/tsukimichi nearby</c>: toggles the Nearby quests window. Falls back to the main window.</summary>
    public Action? ToggleNearbyWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi todo</c>: toggles the todo overlay setting. Falls back to the config window, then the main window.</summary>
    public Action? ToggleTodoOverlay { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi icon</c> (1.22, H1): shows or hides the moon icon. With text after <c>icon</c>, or
    /// while it is not wired, the whole line searches (<see cref="CommandLine.RunsIcon"/>).
    /// </summary>
    public Action? ToggleMoonIcon { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi report [quest name]</c> with the rest of the line (empty for the selected quest):
    /// copies the quest's diagnostic block to the clipboard. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Report { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi why [quest name]</c> with the rest of the line (empty for the selected quest):
    /// prints why the quest is not offered. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Why { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi export [quests|moonlit] [json|csv]</c> with the rest of the line: writes the export
    /// files and prints where. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Export { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi recap [quest name]</c> with the rest of the line (empty for the main scenario): opens
    /// the story recap. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Recap { get; set; }

    /// <summary>Invoked for <c>/tsukimichi stop</c>: stops every hand-off and prints one line. Says "Nothing to stop." until wired.</summary>
    public Action? Stop { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi look &lt;code&gt;</c> with the rest of the line (empty opens the Share section): opens
    /// Settings › Themes with the code pasted and its preview showing. It never applies the look. A code-shaped text that
    /// does not read prints one chat line instead and opens nothing, and text that is not a code searches the whole line
    /// (<see cref="SharePreview.CommandRoute"/>). Falls back to <see cref="OpenConfigWindow"/>.
    /// </summary>
    public Action<string>? Look { get; set; }

    /// <summary>Opens Settings (never closes it), for <c>/tsukimichi look</c> while <see cref="Look"/> is not wired.</summary>
    public Action? OpenConfigWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi msq</c> (1.21, P8): the main scenario line. Falls back to a search for the text.</summary>
    public Action? Msq { get; set; }

    /// <summary>Invoked for <c>/tsukimichi next</c> (1.21, P8): what to do next. Falls back to a search for the text.</summary>
    public Action? Next { get; set; }

    /// <summary>
    /// Invoked for <c>/tsukimichi go [quest name]</c> (1.21, P2) with the rest of the line: travel to the current step of
    /// the named or selected quest. Falls back to a search for the text.
    /// </summary>
    public Action<string>? Go { get; set; }

    /// <summary>
    /// Whether a quest's name (as the player sees it) begins with the given text, case-insensitively: a <c>go</c> line
    /// that begins a quest's own name ("go west", "go with the flow") stays a search (<see cref="CommandLine.RunsGuidance"/>).
    /// </summary>
    public Func<string, bool>? BeginsQuestName { get; set; }

    /// <summary>The aliases registered now: the built-in ones, then the player's, in order.</summary>
    public IReadOnlyList<string> ActiveAliases { get; private set; } = [];

    /// <summary>Wanted aliases that Dalamud, the game or another plugin already answers to, so they were skipped.</summary>
    public IReadOnlyList<string> SkippedAliases { get; private set; } = [];

    /// <summary>Words of the Settings field that are not an alias (no slash, a space or another character), as typed.</summary>
    public IReadOnlyList<string> InvalidAliases { get; private set; } = [];

    /// <summary>Bumped whenever the aliases change, so Settings rebuilds its lines only then.</summary>
    public int AliasesVersion { get; private set; }

    /// <param name="commands">Dalamud command manager.</param>
    /// <param name="toggleMainWindow">Invoked for <c>/tsukimichi</c> with no arguments.</param>
    /// <param name="toggleGlyphWindow">Invoked for <c>/tsukimichi glyphs</c>.</param>
    /// <param name="search">
    /// Invoked with the search text for <c>/tsukimichi search &lt;text&gt;</c> and <c>/tsukimichi &lt;text&gt;</c>;
    /// returns how many quests matched (negative when it could not search).
    /// </param>
    /// <param name="userAliases">The Settings field of extra aliases (<see cref="CommandAliases.Parse"/>).</param>
    /// <param name="isGameCommand">True for a chat command of the game's own, which an alias never shadows; null checks Dalamud's commands only.</param>
    /// <param name="log">The plugin log, for aliases that could not be registered; null logs nothing.</param>
    public TsukimichiCommand(
        ICommandManager commands,
        Action toggleMainWindow,
        Action toggleGlyphWindow,
        Func<string, int> search,
        string? userAliases = null,
        Func<string, bool>? isGameCommand = null,
        IPluginLog? log = null)
    {
        this.commands = commands;
        this.toggleMainWindow = toggleMainWindow;
        this.toggleGlyphWindow = toggleGlyphWindow;
        this.search = search;
        this.isGameCommand = isGameCommand;
        this.log = log;

        mainInfo = new CommandInfo(OnCommand)
        {
            HelpMessage = Strings.CommandHelp,
            ShowInHelp = true,
        };
        aliasInfo = new CommandInfo(OnCommand)
        {
            HelpMessage = Strings.CommandAliasHelp,
            ShowInHelp = false,
        };
        commands.AddHandler(Name, mainInfo);
        commands.AddHandler(Alias, aliasInfo);
        ApplyAliases(userAliases);
        Localization.Loc.Changed += OnLanguageChanged;
    }

    public void Dispose()
    {
        Localization.Loc.Changed -= OnLanguageChanged;
        foreach (var alias in aliases.Keys)
        {
            RemoveAlias(alias);
        }

        aliases.Clear();
        commands.RemoveHandler(Alias);
        commands.RemoveHandler(Name);
    }

    /// <summary>
    /// Registers <c>/ts</c>, <c>/moon</c> and the valid aliases of <paramref name="userAliases"/>, and unregisters those
    /// no longer wanted. An alias Dalamud, the game or another plugin answers to, or one Dalamud refuses, is skipped and
    /// listed in <see cref="SkippedAliases"/>. Never throws.
    /// </summary>
    public void ApplyAliases(string? userAliases)
    {
        try
        {
            InvalidAliases = CommandAliases.Parse(userAliases).Invalid;
            var plan = CommandAliases.Plan(aliases.Keys, CommandAliases.Wanted(userAliases), TakenElsewhere);
            foreach (var alias in plan.Remove)
            {
                RemoveAlias(alias);
                aliases.Remove(alias);
            }

            var skipped = new List<string>(plan.Skipped);
            foreach (var alias in plan.Add)
            {
                var info = new CommandInfo(OnCommand)
                {
                    HelpMessage = Strings.CommandAliasHelp,
                    ShowInHelp = false,
                };
                if (TryAdd(alias, info))
                {
                    aliases[alias] = info;
                }
                else
                {
                    skipped.Add(alias);
                }
            }

            foreach (var alias in skipped)
            {
                log?.Information("Command alias {Alias} skipped: Dalamud, the game or another plugin already uses it", alias);
            }

            // In the order the player sees them: the built-in ones first, then the Settings field's.
            var wanted = CommandAliases.Wanted(userAliases);
            ActiveAliases = [.. wanted.Where(aliases.ContainsKey)];
            SkippedAliases = [.. wanted.Where(skipped.Contains)];
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Command aliases could not be applied");
        }

        AliasesVersion++;
        mainInfo.HelpMessage = HelpText();
    }

    /// <summary>True when something other than Tsukimichi answers to <paramref name="alias"/>; an error reads as taken, so nothing is ever taken over.</summary>
    private bool TakenElsewhere(string alias)
    {
        try
        {
            return commands.Commands.Keys.Any(key => string.Equals(key, alias, StringComparison.OrdinalIgnoreCase))
                || isGameCommand?.Invoke(alias) == true;
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Could not check whether {Alias} is taken; skipped", alias);
            return true;
        }
    }

    private bool TryAdd(string alias, CommandInfo info)
    {
        try
        {
            return commands.AddHandler(alias, info);
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Command alias {Alias} could not be registered", alias);
            return false;
        }
    }

    private void RemoveAlias(string alias)
    {
        try
        {
            commands.RemoveHandler(alias);
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Command alias {Alias} could not be unregistered", alias);
        }
    }

    /// <summary>The primary command's help: what it does, then every other name it answers to.</summary>
    private string HelpText()
    {
        var names = new List<string>(ActiveAliases.Count + 1) { Alias };
        names.AddRange(ActiveAliases);
        return Strings.CommandHelp + " " + string.Format(CultureInfo.CurrentCulture, Strings.CommandAlsoFormat, string.Join(Strings.CommandListSeparator, names));
    }

    /// <summary>Dalamud reads the help text from the registered <see cref="CommandInfo"/>, so /xlhelp follows a language switch.</summary>
    private void OnLanguageChanged()
    {
        mainInfo.HelpMessage = HelpText();
        aliasInfo.HelpMessage = Strings.CommandAliasHelp;
        foreach (var info in aliases.Values)
        {
            info.HelpMessage = Strings.CommandAliasHelp;
        }
    }

    private void OnCommand(string command, string arguments)
    {
        var parsed = CommandLine.Parse(arguments);
        var rest = parsed.Rest;
        var args = parsed.Arguments;

        switch (parsed.Kind)
        {
            case Subcommand.Toggle:
                toggleMainWindow();
                break;

            case Subcommand.Glyphs:
                toggleGlyphWindow();
                break;

            case Subcommand.Ipc:
                Run(ToggleIpcWindow);
                break;

            case Subcommand.Search:
                Search(parsed);
                break;

            case Subcommand.Settings:
                Run(ToggleConfigWindow);
                break;

            case Subcommand.Help:
                Run(ToggleHelpWindow);
                break;

            case Subcommand.Tour:
                Run(StartTour);
                break;

            case Subcommand.Journal:
                OpenTab(NavTab.Journal);
                break;

            case Subcommand.Moonlit:
                OpenTab(NavTab.Moonlit);
                break;

            case Subcommand.Characters:
                OpenTab(NavTab.Characters);
                break;

            case Subcommand.Flight:
                OpenTab(NavTab.Flight);
                break;

            case Subcommand.Blues:
                OpenTab(NavTab.Plan);
                break;

            case Subcommand.Zone:
                RunOrSearch(ListZoneQuests, args);
                break;

            case Subcommand.Which:
                RunOrSearch(ListTargetQuests, args);
                break;

            case Subcommand.Nearby:
                Run(ToggleNearbyWindow);
                break;

            case Subcommand.Report:
                RunOrSearch(Report, rest, args);
                break;

            case Subcommand.Why:
                RunOrSearch(Why, rest, args);
                break;

            case Subcommand.Route:
                RunOrSearch(Route, rest, args);
                break;

            case Subcommand.Export:
                RunOrSearch(Export, rest, args);
                break;

            case Subcommand.Recap:
                RunOrSearch(Recap, rest, args);
                break;

            case Subcommand.Stop:
                if (Stop is { } stop)
                {
                    stop();
                }
                else
                {
                    Print?.Invoke(Strings.StopNothing);
                }

                break;

            case Subcommand.Look:
                // spec-1.17 §C2: a code-shaped text that does not read says so in chat and opens nothing; text that is not
                // a code at all ("look to the stars") is the quest search it was before "look" was a subcommand.
                var route = SharePreview.CommandRoute(rest);
                if (route == LookCommandRoute.Search)
                {
                    search(args);
                }
                else if (route == LookCommandRoute.Unreadable)
                {
                    Print?.Invoke(Strings.CommandLookUnreadable);
                }
                else if (Look is { } look)
                {
                    look(rest);
                }
                else
                {
                    OpenConfigWindow?.Invoke();
                }

                break;

            case Subcommand.Msq or Subcommand.Next or Subcommand.Go:
                RunGuidance(parsed);
                break;

            case Subcommand.Todo:
                if (ToggleTodoOverlay is { } toggleTodo)
                {
                    toggleTodo();
                }
                else
                {
                    Run(ToggleConfigWindow);
                }

                break;

            case Subcommand.Icon:
                if (CommandLine.RunsIcon(parsed) && ToggleMoonIcon is { } toggleIcon)
                {
                    toggleIcon();
                }
                else
                {
                    search(args);
                }

                break;
        }
    }

    /// <summary>
    /// <c>search &lt;text&gt;</c> or an unknown first word: searches; when nothing matched and the first word was a near
    /// miss of a subcommand, says which one was probably meant.
    /// </summary>
    private void Search(ParsedCommand parsed)
    {
        var text = parsed.SearchText;
        if (text.Length == 0)
        {
            toggleMainWindow();
            return;
        }

        var count = search(text);
        if (count == 0 && !string.Equals(parsed.Word, "search", StringComparison.OrdinalIgnoreCase)
            && CommandLine.DidYouMean(parsed.Word) is { } meant && Print is { } print)
        {
            print(string.Format(CultureInfo.CurrentCulture, Strings.CommandDidYouMeanFormat, meant));
        }
    }

    /// <summary>
    /// <c>msq</c>, <c>next</c> and <c>go</c> (1.21): the line runs when <see cref="CommandLine.RunsGuidance"/> says so and
    /// the handler is wired; otherwise it is the quest search it was before, so no quest name is shadowed.
    /// </summary>
    private void RunGuidance(ParsedCommand parsed)
    {
        Action? run = null;
        if (CommandLine.RunsGuidance(parsed, BeginsQuestName))
        {
            run = parsed.Kind switch
            {
                Subcommand.Msq => Msq,
                Subcommand.Next => Next,
                _ => Go is { } go ? () => go(parsed.Rest) : null,
            };
        }

        if (run is not null)
        {
            run();
        }
        else
        {
            search(parsed.Arguments);
        }
    }

    /// <summary>Runs <paramref name="action"/>, or toggles the main window while it is not wired yet.</summary>
    private void Run(Action? action)
    {
        if (action is not null)
        {
            action();
        }
        else
        {
            toggleMainWindow();
        }
    }

    private void OpenTab(NavTab tab)
    {
        if (ShowTab is { } show)
        {
            show(tab);
        }
        else
        {
            toggleMainWindow();
        }
    }

    private void RunOrSearch(Action? action, string args)
    {
        if (action is not null)
        {
            action();
        }
        else
        {
            search(args);
        }
    }

    private void RunOrSearch(Action<string>? action, string rest, string args)
    {
        if (action is not null)
        {
            action(rest);
        }
        else
        {
            search(args);
        }
    }
}
