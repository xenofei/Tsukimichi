using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Evaluation;

/// <summary>How many quests of a node lie on other paths, by the kind of path that closed them.</summary>
public readonly record struct PathTally(int StartCity, int StartClass, int GrandCompany, int Choice)
{
    public int Total => StartCity + StartClass + GrandCompany + Choice;

    public int this[PathKind kind] => kind switch
    {
        PathKind.StartCity => StartCity,
        PathKind.StartClass => StartClass,
        PathKind.GrandCompany => GrandCompany,
        _ => Choice,
    };

    /// <summary>This tally with one more quest of <paramref name="kind"/>.</summary>
    public PathTally Add(PathKind kind) => kind switch
    {
        PathKind.StartCity => this with { StartCity = StartCity + 1 },
        PathKind.StartClass => this with { StartClass = StartClass + 1 },
        PathKind.GrandCompany => this with { GrandCompany = GrandCompany + 1 },
        _ => this with { Choice = Choice + 1 },
    };
}

/// <summary>
/// The words for paths not taken (feature plan v4 D1): the reason a Locked-out row gives ("Another city's start
/// (Ul'dah)"), the detail pane's line ("Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer"),
/// the "Choose one of 3" tag of a choice not made yet and the tree tooltip's tally. English here; each phrase is a
/// <see cref="CoreText"/> key (<c>Core.Path.*</c>).
/// </summary>
public static class PathText
{
    private static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);

    /// <summary>"Choose one of 3": an option of a choice the character has not made yet.</summary>
    public static string ChooseOne(int options) => F("Core.Path.ChooseOne", "Choose one of {0}", options);

    /// <summary>The kind as the tree tally names it: "another city's start".</summary>
    public static string KindName(PathKind kind) => kind switch
    {
        PathKind.StartCity => CoreText.T("Core.Path.KindCity", "another city's start"),
        PathKind.StartClass => CoreText.T("Core.Path.KindClass", "another starting class"),
        PathKind.GrandCompany => CoreText.T("Core.Path.KindGrandCompany", "another Grand Company"),
        _ => CoreText.T("Core.Path.KindChoice", "another choice"),
    };

    /// <summary>The Other paths node's own name: "Other paths".</summary>
    public static string NodeName => CoreText.T("Core.Path.NodeName", "Other paths");

    /// <summary>
    /// The reason after "Locked out · ": "Another city's start (Ul'dah)", "Another starting class (Gladiator)",
    /// "Another Grand Company (Maelstrom)", "Another choice (If I Had a Glamour)" (the option the character took).
    /// </summary>
    public static string Reason(OtherPathRequirement path, Func<byte, string> grandCompanyName)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(grandCompanyName);
        if (path.Decisive is not { } facet)
        {
            return KindName(path.Path);
        }

        return facet.Kind switch
        {
            PathKind.StartCity => F("Core.Path.City", "Another city's start ({0})", Options(facet, grandCompanyName)),
            PathKind.StartClass => F("Core.Path.Class", "Another starting class ({0})", Options(facet, grandCompanyName)),
            PathKind.GrandCompany => F("Core.Path.GrandCompany", "Another Grand Company ({0})", Options(facet, grandCompanyName)),
            _ => facet.Chosen is { } chosen
                ? F("Core.Path.Choice", "Another choice ({0})", Label(chosen, grandCompanyName))
                : KindName(PathKind.Choice),
        };
    }

    /// <summary>
    /// The detail pane's line: who the quest is for, then the character's own path. "Only for Ul'dah Gladiator
    /// starters · you started in Gridania as a Lancer"; "Not for Lancer starters · you started as a Lancer"; "For the
    /// Maelstrom · you chose the Order of the Twin Adder"; "Only one of these can be done · you did If I Had a Glamour".
    /// </summary>
    public static string Detail(OtherPathRequirement path, Func<byte, string> grandCompanyName)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(grandCompanyName);
        var sentences = new List<string>(2);
        PathFacet? city = null;
        PathFacet? cls = null;
        foreach (var facet in path.Facets)
        {
            switch (facet.Kind)
            {
                case PathKind.StartCity:
                    city ??= facet;
                    break;
                case PathKind.StartClass:
                    cls ??= facet;
                    break;
            }
        }

        if (city is { Excludes: true } || cls is { Excludes: true })
        {
            sentences.Add(Start(city, cls, grandCompanyName));
        }

        foreach (var facet in path.Facets)
        {
            if (!facet.Excludes || facet.Kind is PathKind.StartCity or PathKind.StartClass)
            {
                continue;
            }

            var chosen = facet.Chosen is { } c ? Label(c, grandCompanyName) : string.Empty;
            sentences.Add(facet.Kind == PathKind.GrandCompany
                ? F("Core.Path.GrandCompanyDetail", "For the {0} · you chose the {1}", Options(facet, grandCompanyName), chosen)
                : F("Core.Path.ChoiceDetail", "Only one of these can be done · you did {0}", chosen));
        }

        return sentences.Count == 0 ? KindName(path.Path) : string.Join("; ", sentences);
    }

    /// <summary>"53 on other paths: another city's start 49, another starting class 4"; empty for none.</summary>
    public static string Tally(PathTally tally)
    {
        if (tally.Total == 0)
        {
            return string.Empty;
        }

        var parts = new StringBuilder();
        foreach (var kind in Enum.GetValues<PathKind>())
        {
            if (tally[kind] == 0)
            {
                continue;
            }

            if (parts.Length > 0)
            {
                parts.Append(", ");
            }

            parts.Append(F("Core.Path.TallyItem", "{0} {1}", KindName(kind), tally[kind]));
        }

        return F("Core.Path.Tally", "{0} on other paths: {1}", tally.Total, parts.ToString());
    }

    /// <summary>The quest's options: "Ul'dah", "Gridania or Limsa Lominsa", "not Lancer".</summary>
    public static string Options(PathFacet facet, Func<byte, string> grandCompanyName)
    {
        ArgumentNullException.ThrowIfNull(facet);
        if (facet.Except is { } except)
        {
            return F("Core.Path.Not", "not {0}", Label(except, grandCompanyName));
        }

        var text = string.Empty;
        foreach (var option in facet.Options)
        {
            text = text.Length == 0 ? Label(option, grandCompanyName) : F("Core.Path.Or", "{0} or {1}", text, Label(option, grandCompanyName));
        }

        return text;
    }

    private static string Label(PathLabel label, Func<byte, string> grandCompanyName) =>
        label.GrandCompany != 0 && grandCompanyName(label.GrandCompany) is { Length: > 0 } name ? name : label.Text;

    /// <summary>"Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer", and its shorter forms.</summary>
    private static string Start(PathFacet? city, PathFacet? cls, Func<byte, string> grandCompanyName)
    {
        // Who the quest is for: the options named ("Ul'dah Gladiator"), or the one option it is not for ("Lancer").
        var only = new List<string>(2);
        var not = new List<string>(2);
        foreach (var facet in new[] { city, cls })
        {
            if (facet is null)
            {
                continue;
            }

            if (facet.Except is { } except)
            {
                not.Add(Label(except, grandCompanyName));
            }
            else
            {
                only.Add(Options(facet, grandCompanyName));
            }
        }

        var who = only.Count > 0
            ? F("Core.Path.OnlyFor", "Only for {0} starters", string.Join(' ', only))
            : string.Empty;
        if (not.Count > 0)
        {
            var notFor = F("Core.Path.NotFor", "Not for {0} starters", string.Join(' ', not));
            who = who.Length == 0 ? notFor : who + ", " + notFor;
        }

        var cityChosen = city?.Chosen is { } c ? Label(c, grandCompanyName) : null;
        var classChosen = cls?.Chosen is { } k ? WithArticle(Label(k, grandCompanyName)) : null;
        var you = (cityChosen, classChosen) switch
        {
            ({ } inCity, { } asClass) => F("Core.Path.YouStartedInAs", "you started in {0} as {1}", inCity, asClass),
            ({ } inCity, null) => F("Core.Path.YouStartedIn", "you started in {0}", inCity),
            (null, { } asClass) => F("Core.Path.YouStartedAs", "you started as {0}", asClass),
            _ => string.Empty,
        };

        return you.Length == 0 ? who : who + BlockerText.Separator + you;
    }

    /// <summary>"a Lancer", "an Archer": the class names are English labels (localization is frozen).</summary>
    private static string WithArticle(string label) =>
        label.Length > 0 && "AEIOUaeiou".Contains(label[0], StringComparison.Ordinal) ? "an " + label : "a " + label;
}
