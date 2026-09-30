using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// Reads the plugin's resource files (<c>Tsukimichi/Localization/Strings*.resx</c>) straight from the repository: the
/// test project references Core and GameData only, like <see cref="Ui.StringsVocabularyTests"/> reading the sources.
/// </summary>
public static class ResxFiles
{
    /// <summary>The languages that ship a translation beside English.</summary>
    public static readonly string[] Translations = ["ja", "de", "fr"];

    /// <summary>
    /// Composite format items ("{0}", "{1:N0}"), printf specifiers ("%d", "%.2f"), ImGui ids ("###TsukimichiHelp") and
    /// the chat link slot: what every language must keep exactly.
    /// </summary>
    private static readonly Regex Placeholder = new(@"\{\d+(?:,[-\d]+)?(?::[^{}]*)?\}|%[-+#0]*\d*(?:\.\d+)?[dfsuxXi]|#{2,3}[A-Za-z][\w-]*$", RegexOptions.Compiled);

    public static string LocalizationDir() => Path.Combine(RepositoryRoot(), "Tsukimichi", "Localization");

    /// <summary>The resx for a language: "" (English), "ja", "de", "fr".</summary>
    public static string PathFor(string language) =>
        Path.Combine(LocalizationDir(), language.Length == 0 ? "Strings.resx" : $"Strings.{language}.resx");

    /// <summary>Key to value, in file order.</summary>
    public static Dictionary<string, string> Load(string language)
    {
        var doc = XDocument.Load(PathFor(language), LoadOptions.PreserveWhitespace);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in doc.Root!.Elements("data"))
        {
            var name = (string)data.Attribute("name")!;
            var value = data.Element("value")?.Value ?? string.Empty;
            if (!result.TryAdd(name, value))
            {
                throw new InvalidOperationException($"{PathFor(language)} lists {name} twice");
            }
        }

        return result;
    }

    /// <summary>The placeholders of a value, sorted, so two languages compare regardless of word order.</summary>
    public static IReadOnlyList<string> Placeholders(string value) =>
        Placeholder.Matches(value).Select(static m => m.Value).Order(StringComparer.Ordinal).ToList();

    /// <summary>Walks up from the test assembly to the folder that holds the solution file.</summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Tsukimichi.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Tsukimichi.sln not found above " + AppContext.BaseDirectory);
    }
}
