using System;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// The <c>/tsukimichi</c> chat command. Sub-commands are dispatched by the first word: <c>glyphs</c> opens the glyph
/// sheet, <c>search &lt;text&gt;</c> (or any other text) searches and prints matches to chat, <c>zone</c> and
/// <c>which</c> print discovery lists, <c>config</c> and <c>help</c> open those windows; a bare command toggles the
/// main window.
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

    /// <summary>Invoked for <c>/tsukimichi config</c>; set once the config window exists.</summary>
    public Action? ToggleConfigWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi help</c>; set once the help window exists. Falls back to the main window.</summary>
    public Action? ToggleHelpWindow { get; set; }

    /// <summary>Invoked for <c>/tsukimichi zone</c>: quests startable in the current zone. Falls back to a search for "zone".</summary>
    public Action? ListZoneQuests { get; set; }

    /// <summary>Invoked for <c>/tsukimichi which</c>: quests the targeted NPC hands out. Falls back to a search for "which".</summary>
    public Action? ListTargetQuests { get; set; }

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

        commands.AddHandler(Name, new CommandInfo(OnCommand)
        {
            HelpMessage = Strings.CommandHelp,
            ShowInHelp = true,
        });
        commands.AddHandler(Alias, new CommandInfo(OnCommand)
        {
            HelpMessage = Strings.CommandAliasHelp,
            ShowInHelp = false,
        });
    }

    public void Dispose()
    {
        commands.RemoveHandler(Alias);
        commands.RemoveHandler(Name);
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

            default:
                search(args);
                break;
        }
    }
}
