using System.Text.RegularExpressions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The tour's copy in <c>Tsukimichi/Ui/Strings.Tutorial.cs</c> (T14, accessibility C1): every step body is at most
/// <see cref="MaxWords"/> words, every body has a title, and no body leaks an implementation detail such as a timing.
/// The test project references Core and GameData only, so it reads the plugin source like
/// <see cref="StringsVocabularyTests"/> does.
/// </summary>
public class TutorialCopyTests
{
    private const int MaxWords = 35;

    private static readonly Regex Constant = new("public const string (\\w+) = \"((?:[^\"\\\\]|\\\\.)*)\";", RegexOptions.Compiled);

    private static Dictionary<string, string> Constants()
    {
        var path = Path.Combine(RepositoryRoot(), "Tsukimichi", "Ui", "Strings.Tutorial.cs");
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Constant.Matches(File.ReadAllText(path)))
        {
            constants[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return constants;
    }

    private static IEnumerable<KeyValuePair<string, string>> Bodies() =>
        Constants().Where(static c => c.Key.EndsWith("Body", StringComparison.Ordinal));

    [Fact]
    public void The_tour_has_steps_in_three_chapters()
    {
        var constants = Constants();
        Assert.True(Bodies().Count() >= 15, "expected the full tour's bodies");
        Assert.Equal("Find", constants["ChapterFind"]);
        Assert.Equal("Read", constants["ChapterRead"]);
        Assert.Equal("Beyond", constants["ChapterBeyond"]);
    }

    [Fact]
    public void Every_step_body_is_at_most_35_words()
    {
        var offenders = Bodies()
            .Select(static b => (b.Key, Words: b.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length))
            .Where(static b => b.Words > MaxWords)
            .Select(static b => $"{b.Key}: {b.Words} words")
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Every_step_body_has_a_title()
    {
        var constants = Constants();
        foreach (var body in Bodies())
        {
            var title = body.Key[..^"Body".Length] + "Title";
            Assert.True(constants.ContainsKey(title), $"{body.Key} has no {title}");
        }
    }

    [Fact]
    public void No_step_body_quotes_a_timing()
    {
        foreach (var body in Bodies())
        {
            Assert.DoesNotMatch(new Regex("\\d+\\s?ms\\b"), body.Value);
        }
    }

    [Fact]
    public void Later_and_dont_offer_again_are_separate_choices()
    {
        var constants = Constants();
        Assert.Equal("Later", constants["Later"]);
        Assert.Equal("Don't offer again", constants["DontOffer"]);
    }

    /// <summary>Walks up from the test assembly to the folder that holds the solution file.</summary>
    private static string RepositoryRoot()
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
