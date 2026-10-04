namespace Tsukimichi.Core.Updates;

/// <summary>The plain notes as the hover shows them: Dalamud's changelog text, with Markdown list marks made plain.</summary>
public static class UpdateNotes
{
    /// <summary>"- point" and "* point" become "• point"; blank runs fold to one; the text is cut at 1,200 characters.</summary>
    public static string Plain(string notes)
    {
        var lines = notes.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new System.Text.StringBuilder(notes.Length);
        var blank = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                blank = builder.Length > 0;
                continue;
            }

            if (blank)
            {
                builder.Append('\n');
                blank = false;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                line = "• " + trimmed[2..];
            }

            builder.Append(line).Append('\n');
        }

        var text = builder.ToString().TrimEnd();
        return text.Length > 1200 ? text[..1200] + "…" : text;
    }
}
