using System.Diagnostics.CodeAnalysis;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// What Tsukimichi's IPC gates answer from (docs/ipc.md): the catalog, the logged-in character's evaluations and the
/// name lookups, with quest names routed through that character's spoiler shield. The plugin captures one on the
/// framework thread whenever the session changes; everything it holds is immutable (the catalog, a published
/// evaluation map that is never written again, the frozen name tables and the mask), so a gate invoked on any thread
/// reads it without a lock and never touches game memory.
/// <para>
/// Every answer is total. An id the catalog does not hold, a view with no catalog yet or no character evaluated
/// reads as <c>false</c>, <c>""</c>, an empty array or 0; nothing here throws for any <see cref="uint"/>.
/// </para>
/// </summary>
public sealed class IpcView
{
    /// <summary>The first Quest sheet row id; smaller ids are runtime quest ids (the row id's low 16 bits).</summary>
    public const uint FirstRowId = 0x10000;

    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> NoStates = new Dictionary<uint, QuestEvaluation>();

    /// <summary>No catalog, no character: every answer is the not-ready one.</summary>
    public static readonly IpcView Empty = new(null, null, BlockerNames.Default);

    private readonly Lazy<uint> msqNext;

    /// <param name="catalog">The built catalog; null while it loads or after a failure.</param>
    /// <param name="states">The logged-in character's evaluations by row id; null or empty while nobody is logged in or the first evaluation runs.</param>
    /// <param name="names">Name lookups; its <see cref="BlockerNames.Catalog"/> is replaced by <paramref name="catalog"/> when they differ.</param>
    public IpcView(QuestCatalog? catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        Catalog = catalog;
        States = catalog is null ? NoStates : states ?? NoStates;
        Names = catalog is not null && !ReferenceEquals(names.Catalog, catalog) ? names with { Catalog = catalog } : names;
        msqNext = new Lazy<uint>(ComputeMsqNext, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public QuestCatalog? Catalog { get; }

    /// <summary>The logged-in character's evaluations; empty without a catalog or a character.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation> States { get; }

    public BlockerNames Names { get; }

    /// <summary>True once the catalog is built and the logged-in character has been evaluated.</summary>
    public bool IsReady => Catalog is { Count: > 0 } && States.Count > 0;

    /// <summary>
    /// The quest an IPC id names: a Quest sheet row id (65536 and up) through <see cref="QuestCatalog.ByRowId"/>, a
    /// runtime quest id (1 to 65535) through <see cref="QuestCatalog.ByQuestId"/>. Null for 0, an id the catalog does
    /// not hold, or no catalog.
    /// </summary>
    public static QuestRecord? FindQuest(QuestCatalog? catalog, uint id)
    {
        if (catalog is null || id == 0)
        {
            return null;
        }

        return id >= FirstRowId ? catalog.GetByRowId(id) : catalog.GetByQuestId((ushort)id);
    }

    /// <summary><see cref="FindQuest(QuestCatalog?, uint)"/> over this view's catalog.</summary>
    public QuestRecord? Find(uint id) => FindQuest(Catalog, id);

    /// <summary>The quest and its evaluation for the logged-in character; false when either is missing.</summary>
    public bool TryEvaluate(uint id, [NotNullWhen(true)] out QuestRecord? quest, [NotNullWhen(true)] out QuestEvaluation? evaluation)
    {
        if (Find(id) is { } found && States.TryGetValue(found.RowId, out var resolved) && resolved is not null)
        {
            quest = found;
            evaluation = resolved;
            return true;
        }

        quest = null;
        evaluation = null;
        return false;
    }

    /// <summary>Ready or Ready on another job; false for any other state and when there is no answer.</summary>
    public bool IsQuestAvailable(uint id) =>
        TryEvaluate(id, out _, out var evaluation) && evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob;

    /// <summary>The <see cref="QuestState"/> member's name ("Ready", "ReadyOnOtherJob", "Accepted", …); empty when there is no answer.</summary>
    public string State(uint id) => TryEvaluate(id, out _, out var evaluation) ? evaluation.State.ToString() : string.Empty;

    /// <summary>The state as every surface shows it (<see cref="StateNames"/>: "In journal", "Done today", "Locked out"); empty when there is no answer.</summary>
    public string StateName(uint id) =>
        TryEvaluate(id, out var quest, out var evaluation) ? StateNames.Name(evaluation.State, quest) : string.Empty;

    /// <summary>
    /// The quest's "why": first the status line (<see cref="BlockerText.StatusText"/>, "Blocked · after MSQ: The
    /// Vault", with the job in parentheses for Ready on another job, as the diagnostic block prints it), then one
    /// line per requirement in the evaluator's order, in the diagnostic block's format
    /// (<see cref="QuestDiagnostic.RequirementLine"/>, "Level: unmet (80 &gt; 50)"). Empty when there is no answer.
    /// </summary>
    public string[] Blockers(uint id)
    {
        if (!TryEvaluate(id, out var quest, out var evaluation))
        {
            return [];
        }

        var requirements = evaluation.Requirements;
        var lines = new string[1 + requirements.Count];
        lines[0] = StatusLine(evaluation, quest);
        for (var i = 0; i < requirements.Count; i++)
        {
            lines[i + 1] = QuestDiagnostic.RequirementLine(requirements[i], Names);
        }

        return lines;
    }

    /// <summary>
    /// The row id of the logged-in character's next main scenario quest (<see cref="MsqProgress"/>); 0 once the
    /// story is complete and when there is no answer (<see cref="IsReady"/> tells the two apart). Computed once per view.
    /// </summary>
    public uint MsqNext() => msqNext.Value;

    /// <summary>
    /// Whether two evaluation sets differ in what a consumer sees: a quest added or gone, or a quest's state, its
    /// ready-on job or its journal step changed. The same instance never differs; requirement detail alone (the
    /// current level printed in a Level line) does not count.
    /// </summary>
    public static bool StatesDiffer(IReadOnlyDictionary<uint, QuestEvaluation>? previous, IReadOnlyDictionary<uint, QuestEvaluation>? current)
    {
        previous ??= NoStates;
        current ??= NoStates;
        if (ReferenceEquals(previous, current))
        {
            return false;
        }

        if (previous.Count != current.Count)
        {
            return true;
        }

        foreach (var (rowId, before) in previous)
        {
            if (!current.TryGetValue(rowId, out var after))
            {
                return true;
            }

            if (ReferenceEquals(before, after))
            {
                continue;
            }

            if (before is null || after is null
                || before.State != after.State
                || before.ReadyOnJob != after.ReadyOnJob
                || before.Sequence != after.Sequence)
            {
                return true;
            }
        }

        return false;
    }

    private string StatusLine(QuestEvaluation evaluation, QuestRecord quest)
    {
        var status = BlockerText.StatusText(evaluation, quest, Names, States);
        if (evaluation.ReadyOnJob is { } job && Names.JobAbbreviation(job) is { Length: > 0 } abbreviation)
        {
            return status + " (" + abbreviation + ")";
        }

        return status;
    }

    private uint ComputeMsqNext()
    {
        if (!IsReady || Catalog is not { } catalog)
        {
            return 0;
        }

        return MsqProgress.Compute(catalog, States)?.Next?.RowId ?? 0;
    }
}
