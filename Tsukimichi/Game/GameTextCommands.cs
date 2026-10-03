using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Tsukimichi.Game;

/// <summary>
/// The game's own chat commands (the TextCommand sheet: each command, its short form and their aliases, in the client's
/// language and in English), so a Tsukimichi alias never shadows one such as <c>/say</c> (1.11.0, A12). Read once, on
/// first use; a sheet that cannot be read counts as no commands, and is logged.
/// </summary>
public sealed class GameTextCommands(IDataManager data, IPluginLog log)
{
    private HashSet<string>? names;

    /// <summary>True when the game answers to <paramref name="command"/> (with its slash), in any case.</summary>
    public bool Contains(string command) => (names ??= Read()).Contains(command);

    private HashSet<string> Read()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            AddAll(set, data.GetExcelSheet<TextCommand>());
            AddAll(set, data.GetExcelSheet<TextCommand>(ClientLanguage.English));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The game's chat commands could not be read; aliases are checked against Dalamud's commands only");
        }

        return set;
    }

    private static void AddAll(HashSet<string> set, Lumina.Excel.ExcelSheet<TextCommand> sheet)
    {
        foreach (var row in sheet)
        {
            Add(set, row.Command);
            Add(set, row.ShortCommand);
            Add(set, row.Alias);
            Add(set, row.ShortAlias);
        }
    }

    private static void Add(HashSet<string> set, ReadOnlySeString text)
    {
        var name = text.ExtractText().Trim();
        if (name.StartsWith('/'))
        {
            set.Add(name);
        }
    }
}
