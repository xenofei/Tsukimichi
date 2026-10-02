using System;
using System.Globalization;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Text;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// The <c>/tsukimichi</c> chat command. Sub-commands are dispatched by the first word (<see cref="CommandLine.Parse"/>):
/// <c>search &lt;text&gt;</c> (or any other text) searches and prints matches to chat, suggesting a subcommand when the
/// search finds nothing and the first word was a near miss of one (<see cref="CommandLine.DidYouMean"/>);
/// <c>journal</c>, <c>moonlit</c>, <c>characters</c>, <c>flight</c> and <c>blues</c> open those tabs; <c>tour</c>
/// starts the tour; <c>zone</c> and <c>which</c> print discovery lists; <c>why [quest name]</c> prints what blocks a
/// quest; <c>route [quest name]</c> opens its unlock route; <c>nearby</c> toggles the Nearby quests window; <c>todo</c>
/// toggles the todo overlay; <c>report [quest name]</c> copies a quest's diagnostic block; <c>export [quests|moonlit]
/// [json|csv]</c> writes the export files; <c>settings</c> (or <c>config</c>) and <c>help</c> open those windows;
/// <c>glyphs</c> opens the glyph sheet (not listed to players); a bare command toggles the main window.
/// </summary>
public sealed class TsukimichiCommand : IDisposable
{
    public const string Name = "/tsukimichi";

    /// <summary>Short alias with the same handler; hidden from the help list so the command appears once.</summary>
    public const string Alias = "/tsuki";

    private readonly ICommandManager commands;
    private readonly Action toggleMainWindow;
    private readonly Action toggleGlyphWindow;
    private readonly Func<string, int> search;

    // Kept so a language switch can update the help text Dalamud lists (/xlhelp, the installer).
    private readonly CommandInfo mainInfo;
    private readonly CommandInfo aliasInfo;

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

    /// <param name="commands">Dalamud command manager.</param>
    /// <param name="toggleMainWindow">Invoked for <c>/tsukimichi</c> with no arguments.</param>
    /// <param name="toggleGlyphWindow">Invoked for <c>/tsukimichi glyphs</c>.</param>
    /// <param name="search">
    /// Invoked with the search text for <c>/tsukimichi search &lt;text&gt;</c> and <c>/tsukimichi &lt;text&gt;</c>;
    /// returns how many quests matched (negative when it could not search).
    /// </param>
    public TsukimichiCommand(ICommandManager commands, Action toggleMainWindow, Action toggleGlyphWindow, Func<string, int> search)
    {
        this.commands = commands;
        this.toggleMainWindow = toggleMainWindow;
        this.toggleGlyphWindow = toggleGlyphWindow;
        this.search = search;

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
        Localization.Loc.Changed += OnLanguageChanged;
    }

    public void Dispose()
    {
        Localization.Loc.Changed -= OnLanguageChanged;
        commands.RemoveHandler(Alias);
        commands.RemoveHandler(Name);
    }

    /// <summary>Dalamud reads the help text from the registered <see cref="CommandInfo"/>, so /xlhelp follows a language switch.</summary>
    private void OnLanguageChanged()
    {
        mainInfo.HelpMessage = Strings.CommandHelp;
        aliasInfo.HelpMessage = Strings.CommandAliasHelp;
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
