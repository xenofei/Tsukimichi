using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// The test-call box of the <c>/tsuki ipc</c> developer window: reads the arguments typed for a gate and prints its
/// answer. Pure, so the window stays a thin shell over Dalamud's subscribers.
/// </summary>
public static class IpcConsole
{
    private static readonly char[] Separators = [' ', ',', ';', '\t'];

    /// <summary>The words of an argument line: split at spaces, commas and semicolons, empty pieces dropped.</summary>
    public static string[] Words(string? line) =>
        (line ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A whole number in decimal or <c>0x</c> hexadecimal; false for anything else.</summary>
    public static bool TryUInt(string? word, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        var text = word.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>true/false, yes/no, on/off or 1/0; false for anything else.</summary>
    public static bool TryBool(string? word, out bool value)
    {
        switch (word?.Trim().ToLowerInvariant())
        {
            case "true" or "yes" or "on" or "1":
                value = true;
                return true;
            case "false" or "no" or "off" or "0":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    /// <summary>Every word as a number; false when any word is not one (an empty line is an empty array).</summary>
    public static bool TryUInts(string? line, out uint[] values)
    {
        var words = Words(line);
        values = new uint[words.Length];
        for (var i = 0; i < words.Length; i++)
        {
            if (!TryUInt(words[i], out values[i]))
            {
                values = [];
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// An answer as one line: strings quoted, arrays in brackets, tuples in parentheses, booleans in lower case,
    /// <c>null</c> for nothing. Arrays longer than <paramref name="maxItems"/> end with "… (N in all)".
    /// </summary>
    public static string Format(object? value, int maxItems = 50)
    {
        var sb = new StringBuilder();
        Append(sb, value, maxItems);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, object? value, int maxItems)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case string s:
                sb.Append('"').Append(s).Append('"');
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case ITuple tuple:
                sb.Append('(');
                for (var i = 0; i < tuple.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    Append(sb, tuple[i], maxItems);
                }

                sb.Append(')');
                break;
            case IEnumerable items:
                sb.Append('[');
                var count = 0;
                foreach (var item in items)
                {
                    if (count < maxItems)
                    {
                        if (count > 0)
                        {
                            sb.Append(", ");
                        }

                        Append(sb, item, maxItems);
                    }

                    count++;
                }

                if (count > maxItems)
                {
                    sb.Append(", … (").Append(count.ToString(CultureInfo.InvariantCulture)).Append(" in all)");
                }

                sb.Append(']');
                break;
            case IFormattable formattable:
                sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append(value);
                break;
        }
    }
}
