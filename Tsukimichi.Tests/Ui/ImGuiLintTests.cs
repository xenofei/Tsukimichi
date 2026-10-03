using System.Text;
using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for ImGui labels (feature plan v6 U1): no tree node or selectable draws a "#" the player sees. ImGui hides
/// the text after "##" only in a label that is also the item's id; Dalamud's <c>TreeNodeEx(id, flags, label)</c> draws a
/// separate label verbatim, which is how 1.10's Journal printed "##" beside every leaf row's icon. So: a
/// <c>TreeNodeEx</c> label argument never starts with "#" (an id-only node passes none), an id-as-label never starts with
/// a single "#" (shown whole), and every Journal tree node's id starts with "##" (the id-only rows draw it otherwise).
/// The test project references Core and GameData only, so it reads the plugin's sources from the repository.
/// </summary>
public sealed class ImGuiLintTests
{
    // A call of one of the linted ImGui items; the argument list starts after the match.
    private static readonly Regex Call = new(@"\b(?:ImGui|ImRaii)\.(TreeNodeEx|TreeNode|Selectable)\s*\(", RegexOptions.Compiled);

    // "private const string Name = "…";": a const a call may pass by name.
    private static readonly Regex ConstString = new("""const\s+string\s+(\w+)\s*=\s*(@?\$?@?"(?:[^"\\]|\\.)*")""", RegexOptions.Compiled);

    // A Journal tree node's id argument: new Node(scope, "<id>" …).
    private static readonly Regex NodeId = new("""new\s+Node\(\s*[^,]+,\s*(@?\$?@?"(?:[^"\\]|\\.)*")""", RegexOptions.Compiled);

    public static IEnumerable<object[]> PluginSources() =>
        Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi"), "*.cs", SearchOption.AllDirectories)
            .Where(static f => !IsBuildOutput(f))
            .OrderBy(static f => f, StringComparer.Ordinal)
            .Select(static f => new object[] { Path.GetRelativePath(ResxFiles.RepositoryRoot(), f) });

    [Fact]
    public void The_tree_and_its_calls_are_found()
    {
        var tree = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "TreePane.cs"));
        Assert.Contains(Calls(tree), static c => c.Method == "TreeNodeEx");
        Assert.NotEmpty(NodeId.Matches(tree));
    }

    [Theory]
    [MemberData(nameof(PluginSources))]
    public void No_tree_node_or_selectable_draws_a_hash(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), relativePath));
        var offenders = Lint(source).Select(o => $"{relativePath}: {o}").ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Every_journal_tree_node_id_is_hidden()
    {
        var tree = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "TreePane.cs"));
        foreach (Match match in NodeId.Matches(tree))
        {
            var id = LiteralText(match.Groups[1].Value);
            Assert.True(id.StartsWith("##", StringComparison.Ordinal), $"Journal node id {match.Groups[1].Value} does not start with \"##\": TreeNodeEx would draw it");
        }
    }

    [Fact]
    public void The_lint_catches_the_1_10_label_and_a_single_hash()
    {
        const string old = """
            private const string HiddenLabel = "##";
            open = ImGui.TreeNodeEx(node.Id, flags, HiddenLabel);
            """;
        Assert.Single(Lint(old));
        Assert.Single(Lint("""ImGui.TreeNodeEx("##g1", ImGuiTreeNodeFlags.None, "##");"""));
        Assert.Single(Lint("""ImGui.TreeNodeEx(id, label: $"##{x}");"""));
        Assert.Single(Lint("""ImGui.Selectable("#row", selected);"""));
        Assert.Single(Lint("""using var node = ImRaii.TreeNode("#history");"""));

        Assert.Empty(Lint("""open = ImGui.TreeNodeEx(node.Id, flags);"""));
        Assert.Empty(Lint("""ImGui.Selectable("##row", isSelected, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0f, Fn(a, b)));"""));
        Assert.Empty(Lint("""ImGui.Selectable(here ? zone.HereLabel : zone.Label, isSelected);"""));
        Assert.Empty(Lint("""ImGui.TreeNodeEx("##g1", flags, "Main Scenario");"""));
    }

    /// <summary>Every visible-"#" label in <paramref name="source"/>, one line each.</summary>
    private static List<string> Lint(string source)
    {
        var consts = ConstString.Matches(source).ToDictionary(static m => m.Groups[1].Value, static m => LiteralText(m.Groups[2].Value), StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var (method, args, line) in Calls(source))
        {
            // The label TreeNodeEx draws as given: its third positional argument or the one named label.
            if (method == "TreeNodeEx")
            {
                var label = args.FirstOrDefault(static a => a.StartsWith("label:", StringComparison.Ordinal)) is { } named
                    ? named["label:".Length..].Trim()
                    : args.Count >= 3 && !args[2].Contains(':', StringComparison.Ordinal) ? args[2] : null;
                if (label is not null && Resolve(label, consts) is { } text && text.StartsWith('#'))
                {
                    offenders.Add($"line {line}: TreeNodeEx draws its label \"{text}\" verbatim; pass none (an id-only node)");
                }
            }

            // The first argument is the id and, without a separate label, the text: ImGui hides only what follows "##".
            if (args.Count > 0 && Resolve(args[0], consts) is { } first && first.StartsWith('#') && !first.StartsWith("##", StringComparison.Ordinal))
            {
                offenders.Add($"line {line}: {method} label \"{first}\" starts with a single \"#\", which is drawn");
            }
        }

        return offenders;
    }

    /// <summary>The linted calls in <paramref name="source"/>: the method, its top-level arguments and the line it starts on.</summary>
    private static IEnumerable<(string Method, List<string> Args, int Line)> Calls(string source)
    {
        foreach (Match match in Call.Matches(source))
        {
            var line = 1 + source.AsSpan(0, match.Index).Count('\n');
            yield return (match.Groups[1].Value, Arguments(source, match.Index + match.Length), line);
        }
    }

    /// <summary>The comma-separated arguments from <paramref name="start"/> (just after the open parenthesis) to its close.</summary>
    private static List<string> Arguments(string source, int start)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                // A string literal whole (escapes, verbatim doubled quotes), so its commas and brackets do not count.
                var verbatim = i > 0 && (source[i - 1] == '@' || (source[i - 1] == '$' && i > 1 && source[i - 2] == '@'));
                var end = i + 1;
                while (end < source.Length)
                {
                    if (!verbatim && source[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }

                    if (source[end] == '"')
                    {
                        if (verbatim && end + 1 < source.Length && source[end + 1] == '"')
                        {
                            end += 2;
                            continue;
                        }

                        break;
                    }

                    end++;
                }

                current.Append(source, i, Math.Min(end + 1, source.Length) - i);
                i = end;
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.ToString().Trim() is { Length: > 0 } last)
        {
            args.Add(last);
        }

        return args;
    }

    /// <summary>
    /// The text an argument starts with when it starts with a string literal (plain, verbatim or interpolated, possibly
    /// concatenated) or names a const string of the file; null when it is anything else.
    /// </summary>
    private static string? Resolve(string arg, Dictionary<string, string> consts)
    {
        var trimmed = arg.Trim();
        if (trimmed.TrimStart('@', '$').StartsWith('"'))
        {
            return LiteralText(trimmed);
        }

        return consts.TryGetValue(trimmed, out var value) ? value : null;
    }

    /// <summary>The content of the literal <paramref name="literal"/> starts with, without its prefix and quotes.</summary>
    private static string LiteralText(string literal)
    {
        var body = literal.TrimStart('@', '$');
        if (!body.StartsWith('"'))
        {
            return string.Empty;
        }

        var close = body.IndexOf('"', 1);
        return close < 0 ? body[1..] : body[1..close];
    }

    private static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin", StringComparer.OrdinalIgnoreCase) || parts.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }
}
