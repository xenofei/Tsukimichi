using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The tour's copy (T14, accessibility C1): every step body is at most <see cref="MaxWords"/> words, every body has a
/// title, and no body leaks an implementation detail such as a timing. The copy lives in the English resource file
/// (<c>Tutorial.*</c> keys of <c>Tsukimichi/Localization/Strings.resx</c>, V2-19), which this reads like
/// <see cref="StringsVocabularyTests"/> does. The word limit is English's: translations are not word-counted (Japanese
/// has no spaces to count by), only laid out.
/// </summary>
public class TutorialCopyTests
{
    private const int MaxWords = 35;
    private const string Prefix = "Tutorial.";

    private static Dictionary<string, string> Constants() =>
        ResxFiles.Load(string.Empty)
            .Where(static e => e.Key.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(static e => e.Key[Prefix.Length..], static e => e.Value, StringComparer.Ordinal);

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

    [Fact]
    public void The_tutorial_strings_read_the_resource_file()
    {
        // The source keeps the API (Strings.Tutorial.WelcomeBody) and reads every body from the resx.
        var source = File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", "Strings.Tutorial.cs"));
        Assert.Contains("Loc.Get(\"Tutorial.WelcomeBody\")", source, StringComparison.Ordinal);
    }
}
