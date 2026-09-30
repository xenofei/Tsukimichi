using System.Text;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Text.Expressions;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace Tsukimichi.GameData;

/// <summary>
/// Renders a quest text line without the game's evaluator (P9): for a stored character, whose name is known but whose
/// gender, race and job the plugin did not keep, and for the search index. The journal's macros are resolved as
/// follows: line breaks and hyphens become text; colour, italics and icons are dropped; a fork on a player parameter
/// (<c>&lt;If(gnum4)&gt;he&lt;Else/&gt;she&lt;/If&gt;</c>, the morning/evening and race switches) shows its branches
/// side by side ("he/she"), or the first when a switch has more than <see cref="MaxSwitchBranches"/> different ones;
/// the player's name (<c>String(gstr1)</c>, <c>Split(gstr1, " ", 1)</c>) becomes the stored name, or
/// <see cref="NameFallback"/> when none is given; a sheet reference (<c>Sheet</c>, <c>EnNoun</c>) is looked up when an
/// Excel module is given. For the index (<see cref="Words"/>) every branch is kept and names are left out.
/// </summary>
public static class QuestTextNeutral
{
    /// <summary>A switch with more different branches than this shows only its first.</summary>
    public const int MaxSwitchBranches = 3;

    /// <summary>What stands for the player's name when no name is known.</summary>
    public const string NameFallback = "you";

    private const int MaxDepth = 6;

    /// <summary>The line as neutral text: see the class summary. Never throws on odd macros; unknown ones are dropped.</summary>
    /// <param name="playerName">The stored character's full name ("Michiru Tsukikage"); null uses <see cref="NameFallback"/>.</param>
    /// <param name="excel">Resolves <c>Sheet</c> and <c>EnNoun</c> references; null drops them.</param>
    public static string Render(ReadOnlySeString text, string? playerName = null, ExcelModule? excel = null, Language? language = null)
    {
        var context = new Context(playerName, excel, language, ForIndex: false);
        var sb = new StringBuilder(text.ByteLength);
        Append(sb, text.AsSpan(), context, 0);
        return Tidy(sb);
    }

    /// <summary>
    /// Every word a search could look for in the line: the text of every branch of every fork, no names. Appended to
    /// <paramref name="into"/> with a space after.
    /// </summary>
    public static void Words(ReadOnlySeString text, StringBuilder into)
    {
        ArgumentNullException.ThrowIfNull(into);
        Append(into, text.AsSpan(), new Context(null, null, null, ForIndex: true), 0);
        into.Append(' ');
    }

    private sealed record Context(string? PlayerName, ExcelModule? Excel, Language? Language, bool ForIndex);

    private static void Append(StringBuilder sb, ReadOnlySeStringSpan text, Context context, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (var payload in text)
        {
            switch (payload.Type)
            {
                case ReadOnlySePayloadType.Text:
                    sb.Append(Encoding.UTF8.GetString(payload.Body));
                    break;
                case ReadOnlySePayloadType.Macro:
                    AppendMacro(sb, payload, context, depth);
                    break;
            }
        }
    }

    private static void AppendMacro(StringBuilder sb, ReadOnlySePayloadSpan payload, Context context, int depth)
    {
        switch (payload.MacroCode)
        {
            case MacroCode.NewLine:
                sb.Append('\n');
                break;
            case MacroCode.Hyphen:
                sb.Append('-');
                break;
            case MacroCode.NonBreakingSpace:
                sb.Append(' ');
                break;
            case MacroCode.If:
            case MacroCode.IfPcGender:
            case MacroCode.IfPcName:
            case MacroCode.IfSelf:
                AppendFork(sb, payload, context, depth, skipFirst: 1, maxBranches: int.MaxValue);
                break;
            case MacroCode.Switch:
                AppendFork(sb, payload, context, depth, skipFirst: 1, maxBranches: MaxSwitchBranches);
                break;
            case MacroCode.String:
            case MacroCode.PcName:
                AppendString(sb, payload, context, depth, split: false);
                break;
            case MacroCode.Split:
                AppendString(sb, payload, context, depth, split: true);
                break;
            case MacroCode.Head:
            case MacroCode.HeadAll:
            case MacroCode.Caps:
            case MacroCode.Lower:
            case MacroCode.LowerHead:
                AppendCased(sb, payload, context, depth);
                break;
            case MacroCode.Sheet:
            case MacroCode.EnNoun:
            case MacroCode.DeNoun:
            case MacroCode.FrNoun:
            case MacroCode.JaNoun:
            case MacroCode.ChNoun:
                AppendSheet(sb, payload, context, depth);
                break;
            case MacroCode.Num:
            case MacroCode.Digit:
            case MacroCode.Kilo:
                if (payload.TryGetExpression(out var number) && number.TryGetUInt(out var value))
                {
                    sb.Append(value);
                }

                break;
            // Colour, italics, icons, links, sounds, soft hyphens and the rest have no text of their own.
        }
    }

    /// <summary>A fork: the distinct non-empty branches joined with "/", or the first when there are too many.</summary>
    private static void AppendFork(StringBuilder sb, ReadOnlySePayloadSpan payload, Context context, int depth, int skipFirst, int maxBranches)
    {
        var branches = new List<string>(2);
        var i = 0;
        foreach (var expression in payload)
        {
            if (i++ < skipFirst)
            {
                continue;
            }

            var branch = new StringBuilder();
            AppendExpression(branch, expression, context, depth + 1);
            var textOf = branch.ToString();
            if (textOf.Trim().Length > 0 && !branches.Contains(textOf))
            {
                branches.Add(textOf);
            }
        }

        if (branches.Count == 0)
        {
            return;
        }

        if (context.ForIndex)
        {
            foreach (var branch in branches)
            {
                sb.Append(branch).Append(' ');
            }

            return;
        }

        if (branches.Count > maxBranches)
        {
            sb.Append(branches[0]);
            return;
        }

        sb.Append(string.Join('/', branches));
    }

    private static void AppendExpression(StringBuilder sb, ReadOnlySeExpressionSpan expression, Context context, int depth)
    {
        if (expression.TryGetString(out var text))
        {
            Append(sb, text, context, depth);
        }
        else if (expression.TryGetUInt(out var number))
        {
            sb.Append(number);
        }
        else if (IsPlayerName(expression))
        {
            AppendName(sb, context);
        }

        // Any other parameter (a companion's name, a local number) is not known here and is left out.
    }

    /// <summary><c>gstr1</c>: the player's name.</summary>
    private static bool IsPlayerName(ReadOnlySeExpressionSpan expression) =>
        expression.TryGetParameterExpression(out var kind, out var operand)
        && kind == (byte)ExpressionType.GlobalString
        && operand.TryGetUInt(out var index)
        && index == 1;

    /// <summary>
    /// <c>String(x)</c> renders its argument; <c>Split(x, separator, n)</c> renders its argument and keeps the n-th part
    /// (1-based), which is how the journal says a forename (<c>&lt;split(&lt;string(gstr1)&gt;, ,1)&gt;</c>).
    /// </summary>
    private static void AppendString(StringBuilder sb, ReadOnlySePayloadSpan payload, Context context, int depth, bool split)
    {
        if (payload.MacroCode == MacroCode.PcName)
        {
            AppendName(sb, context);
            return;
        }

        var inner = new StringBuilder();
        var separator = " ";
        var part = 1u;
        var i = 0;
        foreach (var expression in payload)
        {
            switch (i++)
            {
                case 0:
                    AppendExpression(inner, expression, context, depth + 1);
                    break;
                case 1 when split && expression.TryGetString(out var text):
                    var value = text.ExtractText();
                    separator = value.Length > 0 ? value : separator;
                    break;
                case 2 when split:
                    expression.TryGetUInt(out part);
                    break;
            }
        }

        var rendered = inner.ToString();
        if (!split || rendered.Length == 0)
        {
            sb.Append(rendered);
            return;
        }

        var parts = rendered.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        sb.Append(parts.Length == 0 ? rendered : parts[Math.Clamp((int)part - 1, 0, parts.Length - 1)]);
    }

    private static void AppendName(StringBuilder sb, Context context)
    {
        if (context.ForIndex)
        {
            return;
        }

        sb.Append(context.PlayerName is { Length: > 0 } known ? known : NameFallback);
    }

    private static void AppendCased(StringBuilder sb, ReadOnlySePayloadSpan payload, Context context, int depth)
    {
        if (!payload.TryGetExpression(out var inner))
        {
            return;
        }

        var part = new StringBuilder();
        AppendExpression(part, inner, context, depth + 1);
        var text = part.ToString();
        if (text.Length == 0)
        {
            return;
        }

        switch (payload.MacroCode)
        {
            case MacroCode.Head:
            case MacroCode.HeadAll:
                sb.Append(char.ToUpperInvariant(text[0])).Append(text, 1, text.Length - 1);
                break;
            case MacroCode.Caps:
                sb.Append(text.ToUpperInvariant());
                break;
            case MacroCode.Lower:
                sb.Append(text.ToLowerInvariant());
                break;
            default:
                sb.Append(char.ToLowerInvariant(text[0])).Append(text, 1, text.Length - 1);
                break;
        }
    }

    /// <summary>
    /// <c>Sheet(name, row, column)</c> and the noun macros (<c>EnNoun(name, article, row, …)</c>): the named sheet's
    /// string at that row (column 0 for a noun: its singular). Needs <see cref="Context.Excel"/>; dropped without it.
    /// </summary>
    private static void AppendSheet(StringBuilder sb, ReadOnlySePayloadSpan payload, Context context, int depth)
    {
        if (context.Excel is not { } excel)
        {
            return;
        }

        var isNoun = payload.MacroCode != MacroCode.Sheet;
        string? sheetName = null;
        uint row = 0;
        uint column = 0;
        var i = 0;
        foreach (var expression in payload)
        {
            switch (i++)
            {
                case 0:
                    if (expression.TryGetString(out var name))
                    {
                        sheetName = name.ExtractText();
                    }

                    break;
                case 1 when !isNoun:
                case 2 when isNoun:
                    expression.TryGetUInt(out row);
                    break;
                case 2 when !isNoun:
                    expression.TryGetUInt(out column);
                    break;
            }
        }

        if (string.IsNullOrEmpty(sheetName))
        {
            return;
        }

        try
        {
            var sheet = excel.GetRawSheet(sheetName, context.Language);
            if (!sheet.HasRow(row))
            {
                return;
            }

            var raw = excel.GetSheet<RawRow>(context.Language, sheetName).GetRow(row);
            var index = (int)Math.Min(column, (uint)Math.Max(0, raw.Columns.Count - 1));
            if (raw.Columns[index].Type != Lumina.Data.Structs.Excel.ExcelColumnDataType.String)
            {
                return;
            }

            Append(sb, raw.ReadStringColumn(index).AsSpan(), context, depth + 1);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or ArgumentOutOfRangeException or Lumina.Excel.Exceptions.SheetNotFoundException or Lumina.Excel.Exceptions.UnsupportedLanguageException)
        {
            // An unknown sheet or row: the reference is dropped.
        }
    }

    /// <summary>Collapses runs of spaces left by dropped macros and trims each line.</summary>
    private static string Tidy(StringBuilder sb)
    {
        // The game font's own glyphs (item-link and high-quality marks) sit in the private use area; ImGui has none.
        for (var i = sb.Length - 1; i >= 0; i--)
        {
            if (sb[i] is >= '' and <= '')
            {
                sb.Remove(i, 1);
            }
        }

        var text = sb.ToString();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            while (line.Contains("  ", StringComparison.Ordinal))
            {
                line = line.Replace("  ", " ", StringComparison.Ordinal);
            }

            lines[i] = line.Replace(" .", ".", StringComparison.Ordinal).Replace(" ,", ",", StringComparison.Ordinal).Trim();
        }

        return string.Join('\n', lines).Trim();
    }
}
