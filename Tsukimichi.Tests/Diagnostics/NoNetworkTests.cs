using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// "Trust you can check" (feature plan v7 N2): docs/privacy.md says Tsukimichi has no network code. These keep it true:
/// no source file of the shipped projects (the plugin, Core and GameData; the tools are not shipped) names an HTTP
/// client, a web request, a socket or a DNS lookup, and Core references no networking assembly. The one way out is a
/// page the player clicks to open in their browser, through Dalamud's <c>Util.OpenLink</c>, which the statement lists.
/// </summary>
public class NoNetworkTests
{
    private static readonly string[] Shipped = ["Tsukimichi", "Tsukimichi.Core", "Tsukimichi.GameData"];

    private static readonly Regex Network = new(
        @"\b(HttpClient|HttpMessageHandler|HttpWebRequest|WebRequest|WebClient|WebSocket|ClientWebSocket|TcpClient|TcpListener|UdpClient|Socket|Dns|NetworkStream|SmtpClient|FtpWebRequest)\b|using\s+System\.Net\b|System\.Net\.(Http|Sockets|WebSockets|Mail)\b",
        RegexOptions.Compiled);

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
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i];
                var comment = code.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0)
                {
                    code = code[..comment];
                }

                if (Network.Match(code) is { Success: true } match)
                {
                    offenders.Add($"{Path.GetRelativePath(ResxFiles.RepositoryRoot(), file)}:{i + 1}: {match.Value}");
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

    [Fact]
    public void Core_references_no_networking_assembly()
    {
        var references = typeof(QuestHistory).Assembly.GetReferencedAssemblies().Select(static a => a.Name ?? string.Empty).ToList();
        Assert.DoesNotContain(references, static name => name.StartsWith("System.Net", StringComparison.Ordinal));
    }
}
