using System.Text;

namespace Tsukimichi.Core.Text;

/// <summary>
/// Splits text meant for a chat message (Discord caps a message at 2,000 characters) into parts that each fit, at line
/// breaks. A part that starts inside a Markdown section repeats that section's heading line (a line starting with
/// <c>#</c>) so every part reads on its own; a single line longer than the limit is cut at the last space that fits
/// (or hard at the limit). Lengths count UTF-16 code units, as Discord does; a cut never splits a surrogate pair.
/// </summary>
public static class MessageSplitter
{
    /// <summary>Discord's message limit for a user without Nitro.</summary>
    public const int DiscordLimit = 2000;

    /// <summary>
    /// The parts of <paramref name="text"/>, none longer than <paramref name="limit"/>; one part when it fits (the text
    /// itself, line endings normalised to <c>\n</c> and trailing blank lines dropped); none for empty text.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, int limit = DiscordLimit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 16);

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
        if (normalized.Length == 0)
        {
            return [];
        }

        if (normalized.Length <= limit)
        {
            return [normalized];
        }

        var parts = new List<string>();
        var part = new StringBuilder(limit);
        string? heading = null;

        // Where the part's last line starts when that line is a heading, else -1: a heading never ends a part, it
        // moves on with its section.
        var trailingHeading = -1;

        void Flush()
        {
            if (trailingHeading >= 0)
            {
                part.Length = trailingHeading;
            }

            var done = part.ToString().TrimEnd('\n');
            if (done.Length > 0)
            {
                parts.Add(done);
            }

            part.Clear();
            trailingHeading = -1;
        }

        foreach (var raw in normalized.Split('\n'))
        {
            var isHeading = raw.StartsWith('#');
            foreach (var line in Pieces(raw, limit))
            {
                if (part.Length + (part.Length == 0 ? 0 : 1) + line.Length > limit)
                {
                    Flush();

                    // The new part carries on the section: its heading first, when it and the line fit together.
                    if (!isHeading && heading is not null && heading.Length + 1 + line.Length <= limit)
                    {
                        part.Append(heading);
                    }
                }

                if (part.Length > 0)
                {
                    part.Append('\n');
                }

                trailingHeading = isHeading ? part.Length : -1;
                part.Append(line);
            }

            if (isHeading)
            {
                heading = raw.Length <= limit ? raw : null;
            }
        }

        trailingHeading = -1;
        Flush();
        return parts;
    }

    /// <summary>A line as is when it fits, else cut at the last space that fits (or at the limit).</summary>
    private static IEnumerable<string> Pieces(string line, int limit)
    {
        var rest = line;
        while (rest.Length > limit)
        {
            var cut = rest.LastIndexOf(' ', limit - 1, limit);
            if (cut <= 0)
            {
                cut = limit;
                if (char.IsHighSurrogate(rest[cut - 1]))
                {
                    cut--;
                }
            }

            yield return rest[..cut].TrimEnd();
            rest = rest[cut..].TrimStart();
        }

        yield return rest;
    }
}
