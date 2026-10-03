using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// "Trust you can check" (feature plan v7 N2): docs/privacy.md says Tsukimichi has no network code. These keep it true:
/// no source file of the shipped projects (the plugin, Core and GameData; the tools are not shipped) names an HTTP
/// client, a web request, a socket or a DNS lookup, and none of the three assemblies references a networking assembly.
/// The one way out is a page the player clicks to open in their browser, through Dalamud's <c>Util.OpenLink</c>, which
/// the statement lists.
/// </summary>
public class NoNetworkTests
{
    private static readonly string[] Shipped = ["Tsukimichi", "Tsukimichi.Core", "Tsukimichi.GameData"];

    // Any identifier that contains a network type's name (HttpClientHandler, SocketsHttpHandler, IHttpClientFactory,
    // DnsEndPoint, ...), or a System.Net namespace.
    private static readonly Regex Network = new(
        @"\w*(HttpClient|HttpMessageHandler|HttpHandler|HttpWebRequest|WebRequest|WebClient|WebSocket|TcpClient|TcpListener|UdpClient|Socket|Dns|NetworkStream|SmtpClient|FtpWebRequest|HttpListener)\w*|using\s+System\.Net\b|System\.Net\.(Http|Sockets|WebSockets|Mail|NetworkInformation|Security|Quic)\b",
        RegexOptions.Compiled);

    // Names that contain a network word but are not network code: the medal's Dalamud-blue gem socket (art).
    private static readonly HashSet<string> NotNetwork = new(StringComparer.Ordinal) { "DalamudSocket", "DalamudSocketHex" };

    private static IEnumerable<string> SourceFiles()
    {
        var sep = Path.DirectorySeparatorChar;
        foreach (var project in Shipped)
        {
            var dir = Path.Combine(ResxFiles.RepositoryRoot(), project);
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (!file.Contains($"{sep}obj{sep}", StringComparison.Ordinal) && !file.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }
    }

    [Fact]
    public void No_shipped_source_names_network_code()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            var lines = StripComments(File.ReadAllText(file)).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in Network.Matches(lines[i]))
                {
                    if (!NotNetwork.Contains(match.Value))
                    {
                        offenders.Add($"{Path.GetRelativePath(ResxFiles.RepositoryRoot(), file)}:{i + 1}: {match.Value}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, "docs/privacy.md says there is no network code; update it before adding any:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_files_are_found()
    {
        Assert.True(SourceFiles().Count() > 200);
    }

    [Theory]
    [InlineData("var url = \"https://example.com\"; var c = new HttpClient();", true)]
    [InlineData("var h = new SocketsHttpHandler();", true)]
    [InlineData("IHttpClientFactory factory;", true)]
    [InlineData("var e = new DnsEndPoint(host, 80);", true)]
    [InlineData("var handler = new HttpClientHandler(); // a comment", true)]
    [InlineData("var text = \"a // b\"; var s = new Socket();", true)]
    [InlineData("var text = @\"C:\\path \"\" // x\"; var s = new TcpClient();", true)]
    [InlineData("var text = \"\"\"a \" // b\"\"\"; var s = new TcpListener();", true)]
    [InlineData("var c = '\"'; var s = new UdpClient();", true)]
    [InlineData("/* HttpClient */ var x = 1;", false)]
    [InlineData("var x = 1; // new HttpClient()", false)]
    [InlineData("var link = \"https://github.com\"; Util.OpenLink(link);", false)]
    public void The_scan_reads_past_strings_and_sees_wrapped_names(string line, bool flagged)
    {
        Assert.Equal(flagged, Network.Matches(StripComments(line)).Any(match => !NotNetwork.Contains(match.Value)));
    }

    [Fact]
    public void No_shipped_assembly_references_a_networking_assembly()
    {
        var assemblies = new List<string>
        {
            typeof(QuestHistory).Assembly.Location,
            typeof(CatalogMapper).Assembly.Location,
        };

        // The plugin cannot be loaded here (it needs Dalamud), so its references are read from the file the solution
        // build puts next to the tests' configuration. A test run that did not build the plugin skips it; the gates
        // build the solution first.
        if (PluginAssembly() is { } plugin)
        {
            Assert.Contains("Tsukimichi.Core", References(plugin));
            assemblies.Add(plugin);
        }

        var offenders = new List<string>();
        foreach (var path in assemblies)
        {
            foreach (var reference in References(path))
            {
                // System.Net.Primitives and System.Private.Uri hold types like Uri and WebUtility that any assembly may
                // reference without opening a connection; nothing of ours does today, so none are allowed yet.
                if (reference.StartsWith("System.Net", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(path)} references {reference}");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// <paramref name="source"/> with its comments blanked (line breaks kept, so line numbers hold). String and character
    /// literals are kept as they are and read through, so a <c>//</c> inside one (a URL) does not hide the rest of its line.
    /// </summary>
    private static string StripComments(string source)
    {
        var text = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                foreach (var ch in source.AsSpan(i, end - i))
                {
                    if (ch == '\n')
                    {
                        text.Append('\n');
                    }
                }

                i = end;
                continue;
            }

            if (c is '"' or '\'')
            {
                var end = LiteralEnd(source, i);
                text.Append(source, i, end - i);
                i = end;
                continue;
            }

            text.Append(c);
            i++;
        }

        return text.ToString();
    }

    /// <summary>The index just past the string or character literal that opens at <paramref name="start"/>.</summary>
    private static int LiteralEnd(string source, int start)
    {
        var quote = source[start];
        if (quote == '"')
        {
            // A raw string literal: three quotes or more, closed by as many.
            var run = 0;
            while (start + run < source.Length && source[start + run] == '"')
            {
                run++;
            }

            if (run >= 3)
            {
                var close = source.IndexOf(new string('"', run), start + run, StringComparison.Ordinal);
                return close < 0 ? source.Length : close + run;
            }
        }

        // A verbatim string (@"..." or $@"..."): no escapes, "" is a quote, and it may span lines.
        var verbatim = quote == '"' && start > 0 && (source[start - 1] == '@' || (start > 1 && source[start - 1] == '$' && source[start - 2] == '@'));
        for (var i = start + 1; i < source.Length; i++)
        {
            var c = source[i];
            if (verbatim)
            {
                if (c == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    return i + 1;
                }

                continue;
            }

            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c == quote || c == '\n')
            {
                return i + 1;
            }
        }

        return source.Length;
    }

    /// <summary>The names of the assemblies the file at <paramref name="path"/> references, read without loading it.</summary>
    private static List<string> References(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return [.. metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))];
    }

    /// <summary>The plugin's built Tsukimichi.dll for the configuration the tests run in, the newest; null when not built.</summary>
    internal static string? PluginAssembly()
    {
        var bin = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "bin");
        if (!Directory.Exists(bin))
        {
            return null;
        }

        var sep = Path.DirectorySeparatorChar;
        var configuration = typeof(NoNetworkTests).Assembly.Location.Contains($"{sep}Debug{sep}", StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";
        return Directory.GetFiles(bin, "Tsukimichi.dll", SearchOption.AllDirectories)
            .Where(file => file.Contains($"{sep}{configuration}{sep}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
