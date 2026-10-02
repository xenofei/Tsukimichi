using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>The Moonlit State filter: which rows to keep by their quest's state for the viewed character.</summary>
public enum MoonlitStateFilter
{
    Any,

    /// <summary>Ready, here or on another job.</summary>
    ReadyNow,

    /// <summary>In the journal.</summary>
    InJournal,

    Blocked,

    /// <summary>Completed, or done this cycle.</summary>
    Done,
}

public static class MoonlitStateFilters
{
    /// <summary>Whether a quest in <paramref name="state"/> (null: no evaluation) passes <paramref name="filter"/>.</summary>
    public static bool Passes(MoonlitStateFilter filter, QuestState? state) => filter switch
    {
        MoonlitStateFilter.Any => true,
        MoonlitStateFilter.ReadyNow => state is QuestState.Ready or QuestState.ReadyOnOtherJob,
        MoonlitStateFilter.InJournal => state is QuestState.Accepted,
        MoonlitStateFilter.Blocked => state is QuestState.Blocked,
        MoonlitStateFilter.Done => state is QuestState.Completed or QuestState.DoneThisCycle,
        _ => true,
    };
}
