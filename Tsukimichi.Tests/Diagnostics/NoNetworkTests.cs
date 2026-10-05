using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// "Trust you can check" (feature plan v7 N2): docs/privacy.md says Tsukimichi sends nothing and never goes online. The
/// quest data and the giver photos ship inside the plugin (the photos were a download from 1.20 to 1.22; no more), so
/// these keep the plugin with no network code at all:
/// <list type="bullet">
/// <item>no source of the shipped projects (the plugin, Core and GameData; the tools are not shipped) names an HTTP
/// client, a web request, a socket or a DNS lookup, nor any API that fetches a URL it is given without a network type in
/// its name (the XML readers and resolvers, such as <c>XDocument.Load("https://…")</c> and <c>XmlReader.Create(url)</c>;
/// <c>DataSet.ReadXml</c>; remote named pipes; the WinINet, WinHTTP, Winsock and URL Moniker DLLs; COM by ProgID or
/// CLSID, such as <c>MSXML2.XMLHTTP</c>);</item>
/// <item>no shipped assembly, the plugin included, references a networking, XML, data or pipes assembly;</item>
/// <item>nothing is left of the pack's download: no transport, no <c>StartDownload</c>, no <c>portrait_pack.json</c>.</item>
/// </list>
/// <see cref="Offenders"/> fails on any file that names network code (proved by
/// <see cref="Any_network_code_anywhere_fails"/>). The one way out is a page the player clicks to open in their browser,
/// through Dalamud's <c>Util.OpenLink</c>, which the statement lists.
/// <para>
/// What a scan of names cannot catch, so review must: a file path that is a network share (<c>\\server\share</c>, which
/// Windows reaches over SMB through ordinary file APIs; no shipped path is built from anything but the plugin's own
/// folders and the game install); another program started with a URL (the one <c>Process.Start</c> opens the export
/// folder in Explorer); a type or DLL named by a string built at run time ("Http" + "Client"); and Dalamud or another
/// plugin fetching on the plugin's behalf through an API of theirs.
/// </para>
/// </summary>
public class NoNetworkTests
{
    private static readonly string[] Shipped = ["Tsukimichi", "Tsukimichi.Core", "Tsukimichi.GameData"];

    /// <summary>
    /// Assemblies no shipped assembly references: networking, and those whose APIs fetch a URL they are given (the XML
    /// readers and resolvers, <c>DataSet.ReadXml</c>, remote named pipes).
    /// </summary>
    private static readonly string[] BlockedAssemblyPrefixes = ["System.Net", "System.Xml", "System.Private.Xml", "System.Data", "System.IO.Pipes"];

    // Any identifier that contains a network type's name (HttpClientHandler, SocketsHttpHandler, IHttpClientFactory,
    // DnsEndPoint, ...), or a System.Net namespace; and the APIs that fetch a URL without a network type in their name:
    // the XML readers, documents and resolvers (XDocument.Load(url), XmlReader.Create(url), an XmlUrlResolver),
    // DataSet.ReadXml, named pipes (a remote one goes over the network), the System.Xml and System.IO.Pipes namespaces,
    // the WinINet, WinHTTP, Winsock and URL Moniker DLLs (URLDownloadToFile), and COM by ProgID or CLSID (MSXML2.XMLHTTP).
    private static readonly Regex Network = new(
        @"\w*(HttpClient|HttpMessageHandler|HttpHandler|HttpWebRequest|WebRequest|WebClient|WebSocket|TcpClient|TcpListener|UdpClient|Socket|Dns|NetworkStream|SmtpClient|FtpWebRequest|HttpListener|XDocument|XElement|XStreamingElement|XmlReader|XmlTextReader|XmlDocument|XmlDataDocument|XPathDocument|XslCompiledTransform|XslTransform|Xml\w*Resolver|XmlSchemaSet|XmlSerializer|ReadXml|NamedPipe|URLDownload|InternetOpen|WinHttp|GetTypeFromProgID|GetTypeFromCLSID)\w*|using\s+(static\s+)?System\.(Net|Xml|IO\.Pipes)\b|System\.Net\.(Http|Sockets|WebSockets|Mail|NetworkInformation|Security|Quic)\b|System\.Xml\.|System\.IO\.Pipes\.|(?i:\b(wininet|winhttp|ws2_32|urlmon|msxml\d*)(\.dll)?\b)",
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
        var files = SourceFiles().Select(file => (Path.GetRelativePath(ResxFiles.RepositoryRoot(), file), File.ReadAllText(file)));
        var offenders = Offenders(files);
        Assert.True(offenders.Count == 0, "docs/privacy.md says Tsukimichi has no network code; update it before adding any:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Every use of network code in <paramref name="files"/>, in any file.</summary>
    internal static List<string> Offenders(IEnumerable<(string Path, string Source)> files)
    {
        var offenders = new List<string>();
        foreach (var (path, source) in files)
        {
            var lines = StripComments(source).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in Network.Matches(lines[i]))
                {
                    if (!NotNetwork.Contains(match.Value))
                    {
                        offenders.Add($"{path}:{i + 1}: {match.Value}");
                    }
                }
            }
        }

        return offenders;
    }

    [Fact]
    public void Any_network_code_anywhere_fails()
    {
        var game = Path.Combine("Tsukimichi", "Game", "Telemetry.cs");

        // Network code in any file is refused, whatever it fetches, GitHub's releases included.
        Assert.NotEmpty(Offenders([(game, "var c = new HttpClient(); c.GetAsync(\"https://github.com/xenofei/Tsukimichi/releases/download/x\");")]));
        Assert.NotEmpty(Offenders([(game, "using System.Net.Sockets;")]));
        Assert.NotEmpty(Offenders([(Path.Combine("Tsukimichi", "Game", "PortraitPackHttp.cs"), "var h = new HttpClient(new SocketsHttpHandler());")]));

        // A link stays a link, not network code (opened in the browser by Dalamud).
        Assert.Empty(Offenders([(game, "Util.OpenLink(\"https://na.finalfantasyxiv.com/lodestone/\");")]));
    }

    [Fact]
    public void Nothing_of_the_portrait_packs_download_is_left()
    {
        // The photos ship with the plugin: no transport, nothing that starts a download, no offer naming a release.
        var root = ResxFiles.RepositoryRoot();
        Assert.False(File.Exists(Path.Combine(root, "Tsukimichi", "Game", "PortraitPackHttp.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Tsukimichi", "Data", "portrait_pack.json")));
        var found = new List<string>();
        foreach (var file in SourceFiles())
        {
            var source = StripComments(File.ReadAllText(file));
            foreach (var name in (string[])["StartDownload(", "PortraitPackHttp", "PortraitPackOffer", "IPortraitPackTransport", "releases/download"])
            {
                // A whole name: the config's obsolete PortraitPackOfferAnswered (read only to drop it) is not the offer.
                if (Regex.IsMatch(source, Regex.Escape(name) + "(?![A-Za-z0-9_])"))
                {
                    found.Add($"{Path.GetRelativePath(root, file)}: {name}");
                }
            }
        }

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
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
    [InlineData("var doc = XDocument.Load(\"https://example.com/feed.xml\");", true)]
    [InlineData("var e = XElement.Load(url);", true)]
    [InlineData("using var r = XmlReader.Create(url);", true)]
    [InlineData("var d = new XmlDocument(); d.Load(url);", true)]
    [InlineData("var p = new XPathDocument(url);", true)]
    [InlineData("settings.XmlResolver = new XmlUrlResolver();", true)]
    [InlineData("var t = new XslCompiledTransform(); t.Load(url);", true)]
    [InlineData("new DataSet().ReadXml(url);", true)]
    [InlineData("using System.Xml.Linq;", true)]
    [InlineData("var x = System.Xml.Linq.XName.Get(\"a\");", true)]
    [InlineData("using var pipe = new NamedPipeClientStream(\"server\", \"pipe\");", true)]
    [InlineData("[LibraryImport(\"urlmon.dll\")] private static partial int Fetch();", true)]
    [InlineData("[DllImport(\"wininet.dll\")] static extern IntPtr Open();", true)]
    [InlineData("NativeLibrary.Load(\"WinHttp\");", true)]
    [InlineData("var t = Type.GetTypeFromProgID(\"MSXML2.XMLHTTP\");", true)]
    [InlineData("var json = JsonNode.Parse(File.ReadAllBytes(path));", false)]
    [InlineData("var name = \"Xmlrpc\"; var count = 1;", false)]
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
                // No shipped assembly, the plugin included, references a networking assembly at all.
                if (BlockedAssemblyPrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)))
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
