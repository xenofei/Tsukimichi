using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>Bit set over <see cref="QuestState"/>; bit index equals the enum ordinal.</summary>
[Flags]
public enum QuestStateMask : uint
{
    None = 0,
    Ready = 1u << QuestState.Ready,
    ReadyOnOtherJob = 1u << QuestState.ReadyOnOtherJob,
    Accepted = 1u << QuestState.Accepted,
    Blocked = 1u << QuestState.Blocked,
    DoneThisCycle = 1u << QuestState.DoneThisCycle,
    Completed = 1u << QuestState.Completed,
    Foreclosed = 1u << QuestState.Foreclosed,
    Unknown = 1u << QuestState.Unknown,
    All = Ready | ReadyOnOtherJob | Accepted | Blocked | DoneThisCycle | Completed | Foreclosed | Unknown,
}

public static class QuestStateMaskExtensions
{
    public static QuestStateMask ToMask(this QuestState state) => (QuestStateMask)(1u << (int)state);

    public static bool Contains(this QuestStateMask mask, QuestState state) => (mask & state.ToMask()) != 0;
}
