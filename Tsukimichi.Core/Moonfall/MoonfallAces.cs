using System.Text.Json;

namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// Ace scores (plan v9 G7) [R §3 l.93]: "each level has a fixed 'Ace' score target; beating it grants a bonus and an
/// 'Aced' badge, 'like beating an expert score'". The research has no values, so they are our own, kept by level id in
/// <c>Moonfall/Modes/aces.json</c> (<c>{ "format": "moonfall-aces", "version": 1, "aces": { "base-01": 290000 } }</c>),
/// apart from the level files so a level's author can tune its Ace without touching its pegs. A level without one has no
/// Ace: no badge, no bonus. [J] Each value is set from the greedy player (<see cref="Suggest"/>): the score three in
/// four of its won games stay under, rounded up to 10,000, so a careful human beats it and a lucky one sometimes does.
/// </summary>
public static class MoonfallAces
{
    public const string Format = "moonfall-aces";

    private const string Resource = "Tsukimichi.Core.Moonfall.Modes.aces.json";

    private static readonly Lazy<(IReadOnlyDictionary<string, long> Aces, IReadOnlyList<string> Errors)> BuiltIn = new(LoadBuiltIn);

    /// <summary>The shipped Ace scores by level id.</summary>
    public static IReadOnlyDictionary<string, long> All => BuiltIn.Value.Aces;

    /// <summary>Why the shipped file did not read (a shipped build has none: a test checks).</summary>
    public static IReadOnlyList<string> Errors => BuiltIn.Value.Errors;

    /// <summary>The level's Ace score, or null when it has none.</summary>
    public static long? For(string? levelId) => levelId is not null && All.TryGetValue(levelId, out var ace) ? ace : null;

    /// <summary>Whether a won level's score is an Ace: the level has one and the score reaches it.</summary>
    public static bool IsAce(string? levelId, long score) => For(levelId) is { } ace && score >= ace;

    /// <summary>What an Ace adds to a won level's score: <see cref="MoonfallRules.AceBonus"/> when <see cref="IsAce"/>, else 0.</summary>
    public static long Bonus(string? levelId, bool won, long score) => won && IsAce(levelId, score) ? MoonfallRules.AceBonus : 0;

    /// <summary>
    /// The Ace score the greedy player suggests for a level: the 75th percentile of its won games' scores, rounded up
    /// to 10,000; null when it won none.
    /// </summary>
    public static long? Suggest(MoonfallPlayabilityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var won = report.Games.Where(static g => g.Won).Select(static g => g.Score).Order().ToList();
        if (won.Count == 0)
        {
            return null;
        }

        var at = won[Math.Min(won.Count - 1, (int)Math.Ceiling(won.Count * 0.75) - 1)];
        return (at + 9_999) / 10_000 * 10_000;
    }

    /// <summary>Reads an aces file; never throws on bad input. Ids must be level ids, values above 0.</summary>
    public static (IReadOnlyDictionary<string, long> Aces, IReadOnlyList<string> Errors) Parse(string? json)
    {
        var aces = new Dictionary<string, long>(StringComparer.Ordinal);
        var errors = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != Format)
            {
                errors.Add($"format must be \"{Format}\"");
                return (aces, errors);
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 1)
            {
                errors.Add("version must be 1");
                return (aces, errors);
            }

            if (!root.TryGetProperty("aces", out var list) || list.ValueKind != JsonValueKind.Object)
            {
                errors.Add("aces must be an object of level id to score");
                return (aces, errors);
            }

            foreach (var entry in list.EnumerateObject())
            {
                if (!MoonfallLevelLoader.IsLevelId(entry.Name))
                {
                    errors.Add($"\"{entry.Name}\" is not a level id");
                }
                else if (entry.Value.ValueKind != JsonValueKind.Number || !entry.Value.TryGetInt64(out var score) || score <= 0)
                {
                    errors.Add($"{entry.Name}: the Ace must be a whole score above 0");
                }
                else
                {
                    aces[entry.Name] = score;
                }
            }
        }
        catch (JsonException ex)
        {
            errors.Add("not JSON: " + ex.Message);
        }

        return errors.Count == 0 ? (aces, errors) : (new Dictionary<string, long>(StringComparer.Ordinal), errors);
    }

    private static (IReadOnlyDictionary<string, long>, IReadOnlyList<string>) LoadBuiltIn()
    {
        using var stream = typeof(MoonfallAces).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return (new Dictionary<string, long>(StringComparer.Ordinal), ["the aces file is missing"]);
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
