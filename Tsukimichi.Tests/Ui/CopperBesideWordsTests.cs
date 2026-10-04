using Tsukimichi.Core.Companions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The supervisor's ruling for 1.18 (spec-1.18, "copper is never the only carrier"): every copper bar or dot sits beside
/// words that say the same. Each stop reason's title and each "Needs you" title and the eyebrow have English words in
/// the resource file, and every plugin source that draws with <c>Theme.Copper</c> also draws those words: the card its
/// title, the panel its eyebrow and title, the preflight its status word. A later change that drops the words fails here.
/// </summary>
public class CopperBesideWordsTests
{
    private static readonly NeedsYouKind[] Kinds = [NeedsYouKind.Death, NeedsYouKind.Stuck, NeedsYouKind.DutyPop, NeedsYouKind.Tell];

    /// <summary>The sources allowed to draw copper, and the words each must draw beside it.</summary>
    private static readonly (string File, string[] Words)[] CopperSources =
    [
        ("StopCardView.cs", ["card.Title", "card.Why"]),
        ("NeedsYouOverlay.cs", ["Strings.NeedsYouEyebrow", "alert.Title", "alert.Line"]),
        ("ConfigWindow.TravelPreflight.cs", ["SemiboldTextWrapped(head", "DrawPreflightDot"]),
        ("MainWindow.RunStops.cs", ["card.Title"]),
        // 1.19.0: the allied board's carried-over daily (C5, the logged-in character's only: a stored alt's has no dot,
        // AlliedCarryover.NeedsYou) and the ending-soon card (C10).
        ("CharactersPane.Planning.cs", ["line.NeedsYou", "line.Line"]),
        ("TonightCard.Events.cs", ["card.Title", "card.Why"]),
        // 1.19.0, C9 (spec-1.19 "Copper is a dot, with words in Text"): a full journal in the row, the status bar, the
        // hero's Make room link and the Todo overlay's count.
        ("TablePane.cs", ["Strings.JournalFullWords"]),
        ("MainWindow.Journal.cs", ["Strings.JournalBarFullFormat"]),
        ("DetailPane.GameAnswers.cs", ["Strings.MakeRoomToAccept"]),
        ("TodoOverlay.cs", ["Strings.JournalBarFullFormat"]),
        // 1.22.0, H1 (spec-1.22 "Colour language": "Each dot also has its words: in the quick card"): the quick card draws
        // the Needs you title beside its copper dot, and the icon's dot is said in words by that card, which its hover opens.
        ("MoonIconCard.cs", ["NeedsYou.Current?.Title", "Theme.Copper, needsYou"]),
        ("MoonIconWindow.cs", ["card.Draw(", "NeedsYou.Current?.Title"]),
        ("Theme.cs", []),
    ];

    [Fact]
    public void Every_reason_and_alert_has_words_in_English()
    {
        var english = ResxFiles.Load(string.Empty);
        foreach (var reason in RunStopClassifier.All)
        {
            var key = RunStopClassifier.TitleKey(reason);
            Assert.True(english.TryGetValue(key, out var title) && title.Any(char.IsLetter), $"{key} has no words");
        }

        foreach (var kind in Kinds)
        {
            var key = NeedsYouAlert.TitleKey(kind);
            Assert.True(english.TryGetValue(key, out var title) && title.Any(char.IsLetter), $"{key} has no words");
        }

        Assert.True(english.TryGetValue(NeedsYouAlert.EyebrowKey, out var eyebrow) && eyebrow.Any(char.IsLetter));
    }

    [Fact]
    public void Copper_is_drawn_only_beside_its_words()
    {
        var ui = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui");
        var allowed = CopperSources.ToDictionary(static s => s.File, static s => s.Words, StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("Theme.Copper", StringComparison.Ordinal) && !source.Contains("Palette.Copper", StringComparison.Ordinal))
            {
                continue;
            }

            var name = Path.GetFileName(file);
            if (!allowed.TryGetValue(name, out var words))
            {
                offenders.Add($"{name} draws copper but is not a surface that draws its words beside it");
                continue;
            }

            foreach (var word in words)
            {
                if (!source.Contains(word, StringComparison.Ordinal))
                {
                    offenders.Add($"{name} draws copper without {word}");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
