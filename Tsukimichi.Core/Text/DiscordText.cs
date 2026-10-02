using System.Text;
using Tsukimichi.Core.Links;

namespace Tsukimichi.Core.Text;

/// <summary>
/// Copy for Discord (1.8.0, research C9 #3): text that reads well pasted into a Discord message. Plain bullets
/// (<c>- </c>), never task boxes (<c>- [ ]</c>, which Discord prints literally); an optional masked link per name,
/// <c>[name](&lt;url&gt;)</c>, whose angle brackets keep Discord from adding a preview card under the message; and the
/// whole split into parts of at most 2,000 characters (<see cref="MessageSplitter"/>), a part that starts inside a
/// section repeating its heading. Pure.
/// </summary>
public static class DiscordText
{
    /// <summary>The bullet every list line starts with.</summary>
    public const string Bullet = "- ";

    /// <summary>
    /// Backslash before the characters Discord reads as formatting (emphasis, strike, spoiler bars, code, links,
    /// quotes, headings, masks), so a name prints as written.
    /// </summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.AsSpan().IndexOfAny("\\*_~`|[]<>#") < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '\\' or '*' or '_' or '~' or '`' or '|' or '[' or ']' or '<' or '>' or '#')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// <c>[text](&lt;url&gt;)</c> with <paramref name="text"/> escaped; the escaped text alone when there is no URL.
    /// The URL is made safe for a link target (<see cref="ExternalLinks.ForMarkdown"/>).
    /// </summary>
    public static string Link(string text, string? url)
    {
        var escaped = Escape(text ?? string.Empty);
        return string.IsNullOrEmpty(url) ? escaped : "[" + escaped + "](<" + ExternalLinks.ForMarkdown(url) + ">)";
    }

    /// <summary>A bold title line and one bullet per line, each line escaped: the Compare list's Discord form.</summary>
    public static string List(string title, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(title))
        {
            sb.Append("**").Append(Escape(title)).Append("**\n");
        }

        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                sb.Append(Bullet).Append(Escape(line.Trim())).Append('\n');
            }
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>The message parts to paste one after another (<see cref="MessageSplitter.Split"/> at Discord's 2,000).</summary>
    public static IReadOnlyList<string> Parts(string text) => MessageSplitter.Split(text ?? string.Empty);
}
