using System;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// The <c>/tsukimichi</c> chat command. Sub-commands are dispatched by the first word: <c>glyphs</c> opens the glyph
/// sheet, <c>search &lt;text&gt;</c> (or any other text) searches and prints matches to chat, <c>zone</c> and
/// <c>which</c> print discovery lists, <c>why [quest name]</c> prints what blocks a quest, <c>nearby</c> toggles the
/// Nearby quests window, <c>todo</c> toggles the todo overlay, <c>report [quest name]</c> copies a quest's diagnostic
/// block, <c>export [quests|moonlit] [json|csv]</c> writes the export files, <c>settings</c> (or <c>config</c>) and
/// <c>help</c> open those windows; a bare command toggles the main window.
/// </summary>
public sealed class TsukimichiCommand : IDisposable
{
    public const string Name = "/tsukimichi";

    /// <summary>Short alias with the same handler; hidden from the help list so the command appears once.</summary>
    public const string Alias = "/tsuki";

    private readonly ICommandManager commands;
    private readonly Action toggleMainWindow;
    private readonly Action toggleGlyphWindow;
    private readonly Action<string> search;

    // Kept so a language switch can update the help text Dalamud lists (/xlhelp, the installer).
    private readonly CommandInfo mainInfo;
    private readonly CommandInfo aliasInfo;

    /// <summary>Invoked for <c>/tsukimichi config</c>; set once the config window exists.</summary>
    public Action? ToggleConfigWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi help</c>; set once the help window exists. Falls back to the main window.</summary>
    public Action? ToggleHelpWindow { get; set; }

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
    /// <param name="search">Invoked with the search text for <c>/tsukimichi search &lt;text&gt;</c> and <c>/tsukimichi &lt;text&gt;</c>.</param>
    public TsukimichiCommand(ICommandManager commands, Action toggleMainWindow, Action toggleGlyphWindow, Action<string> search)
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
        var args = arguments.Trim();
        var split = args.IndexOf(' ');
        var sub = split < 0 ? args : args[..split];
        var rest = split < 0 ? string.Empty : args[(split + 1)..].Trim();

        switch (sub.ToLowerInvariant())
        {
            case "":
                toggleMainWindow();
                break;

            case "glyphs":
                toggleGlyphWindow();
                break;

            case "search":
                if (rest.Length == 0)
                {
                    toggleMainWindow();
                }
                else
                {
                    search(rest);
                }

                break;

            case "config":
            case "settings":
                if (ToggleConfigWindow is { } toggleConfig)
                {
                    toggleConfig();
                }
                else
                {
                    toggleMainWindow();
                }

                break;

            case "help":
                if (ToggleHelpWindow is { } toggleHelp)
                {
                    toggleHelp();
                }
                else
                {
                    toggleMainWindow();
                }

                break;

            case "zone":
                if (ListZoneQuests is { } zone)
                {
                    zone();
                }
                else
                {
                    search(args);
                }

                break;

            case "which":
                if (ListTargetQuests is { } which)
                {
                    which();
                }
                else
                {
                    search(args);
                }

                break;

            case "nearby":
                if (ToggleNearbyWindow is { } toggleNearby)
                {
                    toggleNearby();
                }
                else
                {
                    toggleMainWindow();
                }

                break;

            case "report":
                if (Report is { } report)
                {
                    report(rest);
                }
                else
                {
                    search(args);
                }

                break;

            case "why":
                if (Why is { } why)
                {
                    why(rest);
                }
                else
                {
                    search(args);
                }

                break;

            case "export":
                if (Export is { } export)
                {
                    export(rest);
                }
                else
                {
                    search(args);
                }

                break;

            case "todo":
                if (ToggleTodoOverlay is { } toggleTodo)
                {
                    toggleTodo();
                }
                else if (ToggleConfigWindow is { } toggleConfigForTodo)
                {
                    toggleConfigForTodo();
                }
                else
                {
                    toggleMainWindow();
                }

                break;

            default:
                search(args);
                break;
        }
    }
}
