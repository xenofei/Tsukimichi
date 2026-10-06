using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Moonfall;

/// <summary>What a challenge asks (plan v9 G7) [R §6 l.129–130].</summary>
public enum MoonfallChallengeKind : byte
{
    /// <summary>Score at least the target over the run ("200,000 with 7 balls"). A lost level ends the run; its score counts.</summary>
    Score,

    /// <summary>Win every level of the run, under the challenge's balls and oranges ("45 Orange Pegs", "half-ball").</summary>
    Win,

    /// <summary>Clear every peg of every level: the last orange hit with every other peg lit or gone ("In the Clear").</summary>
    ClearAll,

    /// <summary>Win a duel on every level against the challenge's opponent at its difficulty ("three-level Duels against Masters").</summary>
    Duel,
}

/// <summary>One challenge (<see cref="MoonfallChallengeLoader"/> reads them).</summary>
/// <param name="Id">Its id ("ch-01"): lower-case letters, digits and hyphens. Progress keeps results by it.</param>
/// <param name="Name">What the player sees, up to 40 characters.</param>
/// <param name="Text">One line saying what to do, up to 120 characters.</param>
/// <param name="Kind">What it asks.</param>
/// <param name="LevelIds">The levels it runs through, in order (1 to <see cref="MoonfallRules.MaxChallengeLevels"/>).</param>
/// <param name="Target">The score to reach (<see cref="MoonfallChallengeKind.Score"/>; 0 otherwise).</param>
/// <param name="Balls">Balls each level starts with (each side's in a duel).</param>
/// <param name="Oranges">Oranges each level picks.</param>
/// <param name="Companion">The companion it fixes; <see cref="MoonfallCompanion.None"/> lets the player pick an available one.</param>
/// <param name="Opponent">A duel's opponent.</param>
/// <param name="Difficulty">A duel opponent's difficulty.</param>
public sealed record MoonfallChallenge(
    string Id,
    string Name,
    string Text,
    MoonfallChallengeKind Kind,
    IReadOnlyList<string> LevelIds,
    long Target,
    int Balls,
    int Oranges,
    MoonfallCompanion Companion,
    MoonfallCompanion Opponent,
    MoonfallAiDifficulty Difficulty);

/// <summary>A challenges file read: the challenges, or none with every reason it was refused.</summary>
public sealed record MoonfallChallengeLoad(IReadOnlyList<MoonfallChallenge> Challenges, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Reads and checks a challenges file (plan v9 G7). The format, version 1:
/// <code>
/// {
///   "format": "moonfall-challenges",
///   "version": 1,
///   "challenges": [
///     {
///       "id": "ch-01",                       // lower-case letters, digits and hyphens; unique
///       "name": "Seven Lanterns",            // up to 40 characters
///       "text": "Score 150,000 with 7 balls", // up to 120 characters
///       "kind": "score",                     // "score", "win", "clearAll" or "duel"
///       "levels": [ "base-03" ],             // 1 to 6 level ids, played in order
///       "target": 150000,                    // score only: above 0
///       "balls": 7,                          // optional: 1 to 20 (default 10; a duel's default is 5 a side)
///       "oranges": 25,                       // optional: 25 to 45 (default 25)
///       "companion": "yshtola",              // optional: a companion's key; absent, the player picks
///       "opponent": "louisoix",              // duel only: the opponent's key
///       "difficulty": "adept"                // duel only: "novice", "adept" or "master" (default adept)
///     }
///   ]
/// }
/// </code>
/// Comments and trailing commas are allowed; unknown properties are ignored; a newer version is refused. Level ids are
/// checked for form here; whether a level is shipped is the menu's business (<see cref="MoonfallChallenges.Playable"/>),
/// since levels are authored apart. Never throws on bad input; a file with any error loads no challenge.
/// </summary>
public static partial class MoonfallChallengeLoader
{
    public const string Format = "moonfall-challenges";

    public const int Version = 1;

    /// <summary>Most challenges a file may hold.</summary>
    public const int MaxChallenges = 200;

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>Reads <paramref name="json"/>; never throws on bad input.</summary>
    public static MoonfallChallengeLoad Parse(string? json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("the file is empty");
            return new MoonfallChallengeLoad([], errors);
        }

        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var list = Read(document.RootElement, errors);
            return new MoonfallChallengeLoad(errors.Count == 0 ? list : [], errors);
        }
        catch (JsonException ex)
        {
            errors.Add("not JSON: " + ex.Message);
            return new MoonfallChallengeLoad([], errors);
        }
    }

    private static List<MoonfallChallenge> Read(JsonElement root, List<string> errors)
    {
        var list = new List<MoonfallChallenge>();
        if (root.ValueKind != JsonValueKind.Object || Text(root, "format") != Format)
        {
            errors.Add($"format must be \"{Format}\"");
            return list;
        }

        if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version < 1)
        {
            errors.Add("version is missing or not a whole number from 1");
            return list;
        }

        if (version > Version)
        {
            errors.Add($"version {version} was written by a newer Moonfall (this one reads {Version})");
            return list;
        }

        if (!root.TryGetProperty("challenges", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            errors.Add("challenges must be a list");
            return list;
        }

        if (items.GetArrayLength() > MaxChallenges)
        {
            errors.Add($"a file holds at most {MaxChallenges} challenges");
            return list;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var i = 0;
        foreach (var node in items.EnumerateArray())
        {
            var label = $"challenges[{i++}]";
            if (ReadOne(node, label, errors) is { } challenge)
            {
                if (!ids.Add(challenge.Id))
                {
                    errors.Add($"{label}: id \"{challenge.Id}\" is used twice");
                    continue;
                }

                list.Add(challenge);
            }
        }

        return list;
    }

    private static MoonfallChallenge? ReadOne(JsonElement node, string label, List<string> errors)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label}: not an object");
            return null;
        }

        var before = errors.Count;
        var id = Text(node, "id");
        if (id is null || !IdPattern().IsMatch(id))
        {
            errors.Add($"{label}: id must be lower-case letters, digits and hyphens");
        }

        var name = Text(node, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40)
        {
            errors.Add($"{label}: name must be 1 to 40 characters");
        }

        var text = Text(node, "text")?.Trim() ?? string.Empty;
        if (text.Length > 120)
        {
            errors.Add($"{label}: text must be at most 120 characters");
        }

        MoonfallChallengeKind? kind = Text(node, "kind") switch
        {
            "score" => MoonfallChallengeKind.Score,
            "win" => MoonfallChallengeKind.Win,
            "clearAll" => MoonfallChallengeKind.ClearAll,
            "duel" => MoonfallChallengeKind.Duel,
            _ => null,
        };
        if (kind is null)
        {
            errors.Add($"{label}: kind must be \"score\", \"win\", \"clearAll\" or \"duel\"");
        }

        var levels = new List<string>();
        if (!node.TryGetProperty("levels", out var levelList) || levelList.ValueKind != JsonValueKind.Array
            || levelList.GetArrayLength() is < 1 or > MoonfallRules.MaxChallengeLevels)
        {
            errors.Add($"{label}: levels must list 1 to {MoonfallRules.MaxChallengeLevels} level ids");
        }
        else
        {
            foreach (var level in levelList.EnumerateArray())
            {
                var levelId = level.ValueKind == JsonValueKind.String ? level.GetString() : null;
                if (levelId is null || !IdPattern().IsMatch(levelId))
                {
                    errors.Add($"{label}: levels must be level ids (lower-case letters, digits and hyphens)");
                    break;
                }

                levels.Add(levelId);
            }
        }

        var duel = kind == MoonfallChallengeKind.Duel;
        var target = Whole(node, "target", 0, label, errors);
        if (kind == MoonfallChallengeKind.Score && target <= 0)
        {
            errors.Add($"{label}: a score challenge needs a target above 0");
        }

        var balls = Whole(node, "balls", duel ? MoonfallRules.DuelBallsPerSide : MoonfallRules.BallsPerLevel, label, errors);
        if (balls is < 1 or > MoonfallRules.MaxChallengeBalls)
        {
            errors.Add($"{label}: balls must be 1 to {MoonfallRules.MaxChallengeBalls}");
        }

        var oranges = Whole(node, "oranges", MoonfallRules.OrangeCount, label, errors);
        if (oranges is < MoonfallRules.OrangeCount or > MoonfallRules.MaxChallengeOranges)
        {
            errors.Add($"{label}: oranges must be {MoonfallRules.OrangeCount} to {MoonfallRules.MaxChallengeOranges}");
        }

        var companion = Companion(node, "companion", label, errors);
        var opponent = Companion(node, "opponent", label, errors);
        if (duel && opponent == MoonfallCompanion.None)
        {
            errors.Add($"{label}: a duel needs an opponent (a companion's key)");
        }

        var difficulty = Text(node, "difficulty") switch
        {
            null => MoonfallAiDifficulty.Adept,
            "novice" => MoonfallAiDifficulty.Novice,
            "adept" => MoonfallAiDifficulty.Adept,
            "master" => MoonfallAiDifficulty.Master,
            _ => (MoonfallAiDifficulty?)null,
        };
        if (difficulty is null)
        {
            errors.Add($"{label}: difficulty must be \"novice\", \"adept\" or \"master\"");
        }

        if (errors.Count != before)
        {
            return null;
        }

        return new MoonfallChallenge(id!, name!, text, kind!.Value, levels, target, (int)balls, (int)oranges, companion, opponent, difficulty!.Value);
    }

    private static MoonfallCompanion Companion(JsonElement node, string name, string label, List<string> errors)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return MoonfallCompanion.None;
        }

        var companion = MoonfallCompanions.ByKey(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
        if (companion == MoonfallCompanion.None)
        {
            errors.Add($"{label}: {name} must be a companion's key (\"minfilia\" … \"moogle\")");
        }

        return companion;
    }

    private static string? Text(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Whole(JsonElement node, string name, long fallback, string label, List<string> errors)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        errors.Add($"{label}: {name} must be a whole number");
        return fallback;
    }
}

/// <summary>Where a challenge stands for the menu.</summary>
public enum MoonfallChallengeState : byte
{
    /// <summary>Challenges are not open yet: they open once The Moon Road is won [R §6 l.126].</summary>
    Sealed,

    /// <summary>Open, but a level it runs through is not shipped yet.</summary>
    Unavailable,

    /// <summary>Open and playable.</summary>
    Open,

    /// <summary>Completed at least once.</summary>
    Done,

    /// <summary>Open, but a level it runs through is set past the player's story (<see cref="MoonfallShield"/>): closed until the story reaches it.</summary>
    Veiled,
}

/// <summary>The challenges that ship (<c>Moonfall/Modes/challenges.json</c>) and who may play them.</summary>
public static class MoonfallChallenges
{
    private const string Resource = "Tsukimichi.Core.Moonfall.Modes.challenges.json";

    /// <summary>Reads the shipped challenges (a shipped build has no errors: a test checks).</summary>
    public static MoonfallChallengeLoad LoadBuiltIn()
    {
        var assembly = typeof(MoonfallChallenges).Assembly;
        using var stream = assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return new MoonfallChallengeLoad([], ["the challenges file is missing"]);
        }

        using var reader = new StreamReader(stream);
        return MoonfallChallengeLoader.Parse(reader.ReadToEnd());
    }

    /// <summary>Whether challenges are open: "Challenge mode unlocks after Adventure" [R §6 l.126], The Moon Road won.</summary>
    public static bool Open(MoonfallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return progress.BaseCleared >= MoonfallStages.BaseLevels;
    }

    /// <summary>Whether every level the challenge runs through is shipped, with pegs enough to be orange for the oranges it asks.</summary>
    public static bool Playable(MoonfallChallenge challenge, MoonfallCampaigns campaigns)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(campaigns);
        return challenge.LevelIds.All(id => campaigns.Find(id) is { } level && level.OrangeCandidates >= challenge.Oranges);
    }

    /// <summary>Where the challenge stands for the menu.</summary>
    public static MoonfallChallengeState State(MoonfallChallenge challenge, MoonfallCampaigns campaigns, MoonfallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.IsChallengeDone(challenge.Id))
        {
            return MoonfallChallengeState.Done;
        }

        if (!Open(progress))
        {
            return MoonfallChallengeState.Sealed;
        }

        return Playable(challenge, campaigns) ? MoonfallChallengeState.Open : MoonfallChallengeState.Unavailable;
    }

    /// <summary>The level number a challenge plays a level at: its place in Adventure, and at least 3, so greens (and the companion's power) are on the board.</summary>
    public static int LevelNumber(string levelId) =>
        Math.Max(MoonfallRules.FirstGreenLevel, MoonfallStages.TryPlace(levelId, out var place) ? place.Number : MoonfallRules.FirstGreenLevel);
}
