using System;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Commands;

/// <summary>
/// The <c>/tsukimichi</c> chat command. Sub-commands are dispatched by the first word; later tasks add cases
/// (config, search text) to <see cref="OnCommand"/>.
/// </summary>
public sealed class TsukimichiCommand : IDisposable
{
    public const string Name = "/tsukimichi";

    private readonly ICommandManager commands;
    private readonly Action toggleMainWindow;
    private readonly Action toggleGlyphWindow;

    /// <param name="commands">Dalamud command manager.</param>
    /// <param name="toggleMainWindow">Invoked for <c>/tsukimichi</c> with no arguments.</param>
    /// <param name="toggleGlyphWindow">Invoked for <c>/tsukimichi glyphs</c>.</param>
    public TsukimichiCommand(ICommandManager commands, Action toggleMainWindow, Action toggleGlyphWindow)
    {
        this.commands = commands;
        this.toggleMainWindow = toggleMainWindow;
        this.toggleGlyphWindow = toggleGlyphWindow;

        commands.AddHandler(Name, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Tsukimichi. /tsukimichi glyphs shows the moon-phase glyph sheet.",
            ShowInHelp = true,
        });
    }

    public void Dispose()
    {
        commands.RemoveHandler(Name);
    }

    private void OnCommand(string command, string arguments)
    {
        var args = arguments.Trim();
        var split = args.IndexOf(' ');
        var sub = split < 0 ? args : args[..split];
        // var rest = split < 0 ? string.Empty : args[(split + 1)..].Trim();   // for the search sub-command later

        switch (sub.ToLowerInvariant())
        {
            case "glyphs":
                toggleGlyphWindow();
                break;

            // Later tasks: "config" opens the config window; anything else searches and prints matches to chat.
            case "":
            default:
                toggleMainWindow();
                break;
        }
    }
}
