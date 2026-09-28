using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The one display name per <see cref="QuestState"/> that every surface shows (table, chips, tooltips, detail pane,
/// Moonlit, Compare, the todo overlay, Nearby, chat, help and the tutorial), and the moon-phase subtitle each glyph
/// carries in the Help legend and the glyph window. The enum keeps its internal spellings; nothing user-facing
/// spells a state any other way (docs/glossary.md, feature plan v3 T23).
/// </summary>
public static class StateNames
{
    public const string Ready = "Ready";
    public const string ReadyOnOtherJob = "Ready on another job";
    public const string Accepted = "In journal";
    public const string Blocked = "Blocked";
    public const string DoneToday = "Done today";
    public const string DoneThisWeek = "Done this week";
    /// <summary>Fallback for a repeatable whose reset is neither daily nor weekly, and for surfaces with no quest at hand.</summary>
    public const string DoneThisCycle = "Done this cycle";
    public const string Completed = "Completed";
    public const string Foreclosed = "Locked out";
    public const string Unknown = "Not checked";

    /// <summary><see cref="QuestRecord.RepeatInterval"/> of a quest that resets daily (the allied society dailies).</summary>
    public const byte DailyInterval = 1;

    /// <summary><see cref="QuestRecord.RepeatInterval"/> of a quest that resets weekly (custom deliveries).</summary>
    public const byte WeeklyInterval = 2;

    /// <summary>Display name of a state with no quest at hand; <see cref="QuestState.DoneThisCycle"/> gets the cycle fallback.</summary>
    public static string Name(QuestState state) => state switch
    {
        QuestState.Ready => Ready,
        QuestState.ReadyOnOtherJob => ReadyOnOtherJob,
        QuestState.Accepted => Accepted,
        QuestState.Blocked => Blocked,
        QuestState.DoneThisCycle => DoneThisCycle,
        QuestState.Completed => Completed,
        QuestState.Foreclosed => Foreclosed,
        QuestState.Unknown => Unknown,
        _ => state.ToString(),
    };

    /// <summary>Display name of a state for a quest with the given repeat interval: done repeatables say today or this week.</summary>
    public static string Name(QuestState state, byte repeatInterval) =>
        state == QuestState.DoneThisCycle ? DoneName(repeatInterval) : Name(state);

    /// <summary>Display name of a state for a quest; null quest falls back to <see cref="Name(QuestState)"/>.</summary>
    public static string Name(QuestState state, QuestRecord? quest) =>
        quest is null ? Name(state) : Name(state, quest.RepeatInterval);

    /// <summary>"Done today" for a daily, "Done this week" for a weekly, "Done this cycle" otherwise.</summary>
    public static string DoneName(byte repeatInterval) => repeatInterval switch
    {
        DailyInterval => DoneToday,
        WeeklyInterval => DoneThisWeek,
        _ => DoneThisCycle,
    };

    /// <summary>Between the display name and the shape hint in a moon's tooltip.</summary>
    public const string TooltipSeparator = " · ";

    /// <summary>One tooltip per state, composed once; indexed by the enum value.</summary>
    private static readonly string[] Tooltips = BuildTooltips();

    private static readonly string DoneTodayTooltip = ComposeTooltip(DoneToday, GlyphSubtitle(QuestState.DoneThisCycle));
    private static readonly string DoneThisWeekTooltip = ComposeTooltip(DoneThisWeek, GlyphSubtitle(QuestState.DoneThisCycle));

    /// <summary>
    /// What a state moon says on hover, wherever one is drawn: the display name, then the glyph's shape so the moon
    /// can be told apart next time ("Blocked · new moon, silver ring"). Precomposed per state; allocates nothing.
    /// </summary>
    public static string Tooltip(QuestState state) =>
        (uint)state < (uint)Tooltips.Length ? Tooltips[(int)state] : ComposeTooltip(Name(state), GlyphSubtitle(state));

    /// <summary>Tooltip of a state for a quest with the given repeat interval: a done repeatable says today or this week.</summary>
    public static string Tooltip(QuestState state, byte repeatInterval) => state == QuestState.DoneThisCycle
        ? repeatInterval switch
        {
            DailyInterval => DoneTodayTooltip,
            WeeklyInterval => DoneThisWeekTooltip,
            _ => Tooltips[(int)QuestState.DoneThisCycle],
        }
        : Tooltip(state);

    /// <summary>Tooltip of a state for a quest; null quest falls back to <see cref="Tooltip(QuestState)"/>.</summary>
    public static string Tooltip(QuestState state, QuestRecord? quest) =>
        quest is null ? Tooltip(state) : Tooltip(state, quest.RepeatInterval);

    /// <summary>"Name · subtitle", or the name alone when there is no subtitle.</summary>
    public static string ComposeTooltip(string name, string subtitle) =>
        string.IsNullOrEmpty(subtitle) ? name : name + TooltipSeparator + subtitle;

    private static string[] BuildTooltips()
    {
        var states = Enum.GetValues<QuestState>();
        var max = 0;
        foreach (var state in states)
        {
            max = Math.Max(max, (int)state);
        }

        var table = new string[max + 1];
        foreach (var state in states)
        {
            table[(int)state] = ComposeTooltip(Name(state), GlyphSubtitle(state));
        }

        return table;
    }

    /// <summary>
    /// The moon-phase name of a state's glyph: a subtitle under the display name in the Help legend and the glyph
    /// window, and the shape hint in every moon's tooltip (<see cref="Tooltip(QuestState)"/>).
    /// </summary>
    public static string GlyphSubtitle(QuestState state) => state switch
    {
        QuestState.Ready => "first quarter, glow",
        QuestState.ReadyOnOtherJob => "first quarter, silver, gold ring",
        QuestState.Accepted => "waxing gibbous, gold ring",
        QuestState.Blocked => "new moon, silver ring",
        QuestState.DoneThisCycle => "waning gibbous, silver",
        QuestState.Completed => "full moon",
        QuestState.Foreclosed => "eclipsed",
        QuestState.Unknown => "veiled",
        _ => string.Empty,
    };
}
