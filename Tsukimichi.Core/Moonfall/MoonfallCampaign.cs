using System.Reflection;

namespace Tsukimichi.Core.Moonfall;

/// <summary>Which campaign a level belongs to (plan v9 "Your answers", G6): the base game, then its expansion.</summary>
public enum MoonfallCampaignKind
{
    Base,
    Expansion,
}

/// <summary>One campaign's levels, in play order.</summary>
public sealed record MoonfallCampaign(MoonfallCampaignKind Kind, IReadOnlyList<MoonfallLevel> Levels);

/// <summary>
/// The levels that ship inside Tsukimichi.Core (embedded <c>Moonfall/Levels/*.json</c>): <c>base-NN.json</c> for the base
/// campaign and <c>expansion-NN.json</c> for the expansion, each in file-name order. One game: the expansion opens once
/// every base level is won (<see cref="ExpansionOpen"/>), as the original's second game followed its first.
/// </summary>
public sealed class MoonfallCampaigns
{
    private const string Prefix = "Tsukimichi.Core.Moonfall.Levels.";

    public MoonfallCampaigns(MoonfallCampaign baseCampaign, MoonfallCampaign expansion, IReadOnlyList<string> errors)
    {
        Base = baseCampaign ?? throw new ArgumentNullException(nameof(baseCampaign));
        Expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        foreach (var level in Base.Levels.Concat(Expansion.Levels))
        {
            byId.TryAdd(level.Id, level);
        }
    }

    private readonly Dictionary<string, MoonfallLevel> byId = new(StringComparer.Ordinal);

    /// <summary>
    /// The level of id <paramref name="id"/> (<see cref="MoonfallStages"/> names Adventure's, a challenge names its own),
    /// or null when no level of that id is shipped (not authored yet). The first of two levels with one id wins.
    /// </summary>
    public MoonfallLevel? Find(string? id) => id is not null && byId.TryGetValue(id, out var level) ? level : null;

    public MoonfallCampaign Base { get; }

    public MoonfallCampaign Expansion { get; }

    /// <summary>Level files that did not load, each with its reasons (a shipped build has none: a test checks).</summary>
    public IReadOnlyList<string> Errors { get; }

    public MoonfallCampaign this[MoonfallCampaignKind kind] => kind == MoonfallCampaignKind.Expansion ? Expansion : Base;

    /// <summary>Whether the expansion can be played: every base level won and the expansion has levels.</summary>
    public bool ExpansionOpen(MoonfallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return Base.Levels.Count > 0 && progress.BaseCleared >= Base.Levels.Count && Expansion.Levels.Count > 0;
    }

    /// <summary>
    /// The levels the player may pick in <paramref name="kind"/>: every won level and the next one (0 when the
    /// campaign is closed or empty).
    /// </summary>
    public int Playable(MoonfallCampaignKind kind, MoonfallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var campaign = this[kind];
        if (campaign.Levels.Count == 0 || (kind == MoonfallCampaignKind.Expansion && !ExpansionOpen(progress)))
        {
            return 0;
        }

        return Math.Clamp(progress.Cleared(kind) + 1, 1, campaign.Levels.Count);
    }

    /// <summary>Reads the levels embedded in this assembly.</summary>
    public static MoonfallCampaigns LoadBuiltIn()
    {
        var assembly = typeof(MoonfallCampaigns).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();
        var errors = new List<string>();
        var baseLevels = new List<MoonfallLevel>();
        var expansionLevels = new List<MoonfallLevel>();
        foreach (var name in names)
        {
            var file = name[Prefix.Length..];
            var target = file.StartsWith("expansion-", StringComparison.Ordinal) ? expansionLevels
                : file.StartsWith("base-", StringComparison.Ordinal) ? baseLevels
                : null;
            if (target is null)
            {
                continue;
            }

            var load = MoonfallLevelLoader.Parse(Read(assembly, name));
            if (load.Level is { } level)
            {
                target.Add(level);
            }
            else
            {
                errors.Add(file + ": " + string.Join("; ", load.Errors));
            }
        }

        return new MoonfallCampaigns(
            new MoonfallCampaign(MoonfallCampaignKind.Base, baseLevels),
            new MoonfallCampaign(MoonfallCampaignKind.Expansion, expansionLevels),
            errors);
    }

    private static string? Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
