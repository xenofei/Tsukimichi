using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// "Trust you can check" (feature plan v7 N2): docs/privacy.md says Tsukimichi sends nothing and goes online for one
/// thing only, the optional portrait pack the player confirms (F4, decision 8). The owner allowed one narrow exception
/// for it (spec-1.20 F4, open question 1), and these keep it exactly that narrow:
/// <list type="bullet">
/// <item>one named file: no source of the shipped projects (the plugin, Core and GameData; the tools are not shipped)
/// names an HTTP client, a web request, a socket or a DNS lookup, except the pack's downloader (<see cref="NetworkFile"/>);
/// nor any API that fetches a URL it is given without a network type in its name (the XML readers and resolvers, such as
/// <c>XDocument.Load("https://…")</c> and <c>XmlReader.Create(url)</c>; <c>DataSet.ReadXml</c>; remote named pipes; the
/// WinINet, WinHTTP, Winsock and URL Moniker DLLs; COM by ProgID or CLSID, such as <c>MSXML2.XMLHTTP</c>);</item>
/// <item>one host and path prefix: every address the downloader or the pack's offer names starts with
/// <see cref="AllowedPrefix"/>, and the offer allows nothing else to be asked for first. GitHub redirects a release
/// download; the hosts it may go to are pinned to exactly its two asset hosts (<see cref="GitHubAssetHosts"/>);</item>
/// <item>one pinned URL per release: the shipped <c>portrait_pack.json</c> names one release asset under that prefix.</item>
/// </list>
/// <see cref="Offenders"/> fails on any other file, host or address pattern (proved by
/// <see cref="Any_other_network_file_host_or_address_fails"/>). Core and GameData reference no networking, XML, data or
/// pipes assembly, and the plugin only <c>System.Net.Http</c> (and <c>System.Net.Primitives</c>, for the status code);
/// the transport is made in one place and a download starts only from the Settings confirmation. The other way out is a
/// page the player clicks to open in their browser, through Dalamud's <c>Util.OpenLink</c>, which the statement lists.
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

    /// <summary>The portrait pack's transport (docs/privacy.md, "The portrait pack"): the one file that may name network code.</summary>
    private static readonly string NetworkFile = Path.Combine("Tsukimichi", "Game", "PortraitPackHttp.cs");

    /// <summary>The one host and path prefix the exception covers.</summary>
    private const string AllowedPrefix = "https://github.com/xenofei/Tsukimichi/releases/download/";

    /// <summary>The hosts a release download may be redirected to: GitHub's own asset hosts, exactly these two.</summary>
    private static readonly string[] GitHubAssetHosts = ["objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    /// <summary>The files whose addresses must all sit under <see cref="AllowedPrefix"/>: the downloader and the offer that pins its URL.</summary>
    private static readonly string[] PinnedFiles = [NetworkFile, Path.Combine("Tsukimichi.Core", "Portraits", "PortraitPackOffer.cs")];

    // An http(s) address in a string literal.
    private static readonly Regex Address = new(@"https?://[^\s""']*", RegexOptions.Compiled);

    /// <summary>What the plugin's assembly alone may reference for that file: HttpClient and its status code.</summary>
    private static readonly HashSet<string> PluginNetworkAssemblies = new(StringComparer.Ordinal) { "System.Net.Http", "System.Net.Primitives" };

    /// <summary>
    /// Assemblies no shipped assembly references: networking (but the plugin's two above), and those whose APIs fetch a
    /// URL they are given (the XML readers and resolvers, <c>DataSet.ReadXml</c>, remote named pipes).
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
        Assert.True(offenders.Count == 0, "docs/privacy.md says the portrait pack's downloader is the only network code, and only for Tsukimichi's GitHub releases; update it before adding any:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// The exception's three limits, checked on sources: network code in any file but <see cref="NetworkFile"/>, and any
    /// address in the downloader or the offer that is not under <see cref="AllowedPrefix"/>.
    /// </summary>
    internal static List<string> Offenders(IEnumerable<(string Path, string Source)> files)
    {
        var offenders = new List<string>();
        foreach (var (path, source) in files)
        {
            var lines = StripComments(source).Split('\n');
            var networkFile = path == NetworkFile;
            var pinned = PinnedFiles.Contains(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!networkFile)
                {
                    foreach (Match match in Network.Matches(lines[i]))
                    {
                        if (!NotNetwork.Contains(match.Value))
                        {
                            offenders.Add($"{path}:{i + 1}: {match.Value}");
                        }
                    }
                }

                if (pinned)
                {
                    foreach (Match match in Address.Matches(lines[i]))
                    {
                        if (!match.Value.StartsWith(AllowedPrefix, StringComparison.Ordinal))
                        {
                            offenders.Add($"{path}:{i + 1}: {match.Value} is not under {AllowedPrefix}");
                        }
                    }
                }
            }
        }

        return offenders;
    }

    [Fact]
    public void Any_other_network_file_host_or_address_fails()
    {
        var other = Path.Combine("Tsukimichi", "Game", "Telemetry.cs");

        // Another file with network code: refused, whatever it fetches.
        Assert.NotEmpty(Offenders([(other, "var c = new HttpClient(); c.GetAsync(\"https://github.com/xenofei/Tsukimichi/releases/download/x\");")]));
        Assert.NotEmpty(Offenders([(other, "using System.Net.Sockets;")]));

        // The downloader naming another host, or another path on GitHub: refused.
        Assert.NotEmpty(Offenders([(NetworkFile, "var u = new Uri(\"https://evil.example/pack.zip\");")]));
        Assert.NotEmpty(Offenders([(NetworkFile, "var u = new Uri(\"https://github.com/xenofei/Tsukimichi/archive/main.zip\");")]));
        Assert.NotEmpty(Offenders([(NetworkFile, "var u = new Uri(\"http://github.com/xenofei/Tsukimichi/releases/download/v1/x.zip\");")]));
        Assert.NotEmpty(Offenders([(NetworkFile, "var u = new Uri(\"https://github.com/someone/Tsukimichi/releases/download/x\");")]));

        // The offer pinning a URL elsewhere: refused.
        Assert.NotEmpty(Offenders([(PinnedFiles[1], "public const string ReleaseBase = \"https://example.com/releases/download/\";")]));

        // What the exception covers: the downloader's own network code, and the pinned prefix.
        Assert.Empty(Offenders([(NetworkFile, "var h = new HttpClient(new SocketsHttpHandler());")]));
        Assert.Empty(Offenders([(PinnedFiles[1], "public const string ReleaseBase = \"https://github.com/xenofei/Tsukimichi/releases/download/\";")]));

        // And a link elsewhere in the plugin stays a link, not network code (opened in the browser by Dalamud).
        Assert.Empty(Offenders([(other, "Util.OpenLink(\"https://na.finalfantasyxiv.com/lodestone/\");")]));
    }

    [Fact]
    public void A_download_may_be_redirected_only_to_githubs_two_asset_hosts()
    {
        // The hosts list is the exception's one other address: pinned, so a third host fails here, not in review.
        Assert.Equal(GitHubAssetHosts, Core.Portraits.PortraitPackOffer.AssetHosts);
        var offer = new Core.Portraits.PortraitPackOffer("portraits-1", "Tsukimichi-portraits.zip", 1, new string('a', 64), 1, string.Empty, 1);
        foreach (var host in GitHubAssetHosts)
        {
            Assert.True(offer.Allows(new Uri("https://" + host + "/x")));
        }

        Assert.False(offer.Allows(new Uri("https://githubusercontent.com/x")));
        Assert.False(offer.Allows(new Uri("https://raw.githubusercontent.com/x")));
        Assert.False(offer.Allows(new Uri("https://github.com/x")));
    }

    [Fact]
    public void The_shipped_offer_names_one_release_asset_under_the_prefix()
    {
        Assert.Equal(AllowedPrefix, Core.Portraits.PortraitPackOffer.ReleaseBase);
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Data", Core.Portraits.PortraitPackOffer.FileName);
        Assert.True(File.Exists(path), "Tsukimichi/Data/portrait_pack.json ships with every build");
        var offer = Core.Portraits.PortraitPackOffer.Parse(File.ReadAllBytes(path), out var warning);
        Assert.Null(warning);
        if (offer is not null)
        {
            Assert.StartsWith(AllowedPrefix, offer.DownloadUri.AbsoluteUri, StringComparison.Ordinal);
            Assert.True(offer.Allows(offer.DownloadUri));
            Assert.False(offer.Allows(new Uri(AllowedPrefix + "portraits-999/other.zip")));
        }
    }

    [Fact]
    public void The_one_network_file_is_the_portrait_pack_transport_and_follows_no_redirect_itself()
    {
        var source = StripComments(File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), NetworkFile)));
        Assert.Contains("class PortraitPackHttp : IPortraitPackTransport", source, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect = false", source, StringComparison.Ordinal);
        Assert.Contains("UseCookies = false", source, StringComparison.Ordinal);
        Assert.Contains("if (!offer.Allows(uri))", source, StringComparison.Ordinal);

        // It names no address of its own: every one comes from the offer, checked hop by hop (PortraitPackOffer.Allows).
        Assert.DoesNotContain("://", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pack_is_fetched_only_from_the_pinned_release_after_the_settings_confirmation()
    {
        Assert.Equal("https://github.com/xenofei/Tsukimichi/releases/download/", Core.Portraits.PortraitPackOffer.ReleaseBase);

        // The transport is made in one place (the plugin's wiring), and a download starts only from the confirmation's button.
        var plugin = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi");
        var made = new List<string>();
        var started = new List<string>();
        foreach (var file in Directory.GetFiles(plugin, "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}obj{sep}", StringComparison.Ordinal) || file.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            {
                continue;
            }

            var source = StripComments(File.ReadAllText(file));
            var name = Path.GetRelativePath(ResxFiles.RepositoryRoot(), file);
            if (source.Contains("new Game.PortraitPackHttp(", StringComparison.Ordinal) || source.Contains("new PortraitPackHttp(", StringComparison.Ordinal))
            {
                made.Add(name);
            }

            if (source.Contains(".StartDownload(", StringComparison.Ordinal))
            {
                started.Add(name);
            }
        }

        Assert.Equal([Path.Combine("Tsukimichi", "Plugin.cs")], made);
        Assert.Equal([Path.Combine("Tsukimichi", "Ui", "ConfigWindow.PortraitPack.cs")], started);
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
            var isPlugin = Path.GetFileName(path) == "Tsukimichi.dll";
            foreach (var reference in References(path))
            {
                // Core and GameData reference no networking assembly at all; the plugin only what the portrait pack's
                // transport needs (HttpClient, and its status code from System.Net.Primitives).
                if (BlockedAssemblyPrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)) && !(isPlugin && PluginNetworkAssemblies.Contains(reference)))
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
