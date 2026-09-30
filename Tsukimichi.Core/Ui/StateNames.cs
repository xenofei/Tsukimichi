using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The one display name per <see cref="QuestState"/> that every surface shows (table, chips, tooltips, detail pane,
/// Moonlit, Compare, the todo overlay, Nearby, chat, help and the tutorial), and the moon-phase subtitle each glyph
/// carries in the Help legend and the glyph window. The enum keeps its internal spellings; nothing user-facing
/// spells a state any other way (docs/glossary.md, feature plan v3 T23).
/// <para>
/// Each name is English here and follows the UI language through <see cref="CoreText"/> (keys <c>Core.State.*</c>,
/// <c>Core.Glyph.*</c>, <c>Core.HighContrast.*</c>); the composed tooltips are cached per language.
/// </para>
/// </summary>
public static class StateNames
{
    public static string Ready => CoreText.T("Core.State.Ready", "Ready");
    public static string ReadyOnOtherJob => CoreText.T("Core.State.ReadyOnOtherJob", "Ready on another job");
    public static string Accepted => CoreText.T("Core.State.Accepted", "In journal");
    public static string Blocked => CoreText.T("Core.State.Blocked", "Blocked");
    public static string DoneToday => CoreText.T("Core.State.DoneToday", "Done today");
    public static string DoneThisWeek => CoreText.T("Core.State.DoneThisWeek", "Done this week");
    /// <summary>Fallback for a repeatable whose reset is neither daily nor weekly, and for surfaces with no quest at hand.</summary>
    public static string DoneThisCycle => CoreText.T("Core.State.DoneThisCycle", "Done this cycle");
    public static string Completed => CoreText.T("Core.State.Completed", "Completed");
    public static string Foreclosed => CoreText.T("Core.State.Foreclosed", "Locked out");
    public static string Unknown => CoreText.T("Core.State.Unknown", "Not checked");

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

    /// <summary>
    /// The tooltips of one palette, composed once per language: one per state indexed by the enum value, then the
    /// done-today and done-this-week variants.
    /// </summary>
    private sealed record TooltipSet(string[] States, string DoneToday, string DoneThisWeek);

    private static readonly TextCache<TooltipSet> StandardTooltips = new(static () => BuildSet(GlyphSubtitle));
    private static readonly TextCache<TooltipSet> HighContrastSet = new(static () => BuildSet(HighContrastSubtitle));

    private static TooltipSet BuildSet(Func<QuestState, string> subtitle) => new(
        BuildTooltips(subtitle),
        ComposeTooltip(DoneToday, subtitle(QuestState.DoneThisCycle)),
        ComposeTooltip(DoneThisWeek, subtitle(QuestState.DoneThisCycle)));

    /// <summary>
    /// What a state moon says on hover, wherever one is drawn: the display name, then the glyph's shape so the moon
    /// can be told apart next time ("Blocked · new moon, silver ring"). Precomposed per state; allocates nothing.
    /// </summary>
    public static string Tooltip(QuestState state)
    {
        var tooltips = StandardTooltips.Value.States;
        return (uint)state < (uint)tooltips.Length ? tooltips[(int)state] : ComposeTooltip(Name(state), GlyphSubtitle(state));
    }

    /// <summary>Tooltip of a state for a quest with the given repeat interval: a done repeatable says today or this week.</summary>
    public static string Tooltip(QuestState state, byte repeatInterval)
    {
        if (state != QuestState.DoneThisCycle)
        {
            return Tooltip(state);
        }

        var set = StandardTooltips.Value;
        return repeatInterval switch
        {
            DailyInterval => set.DoneToday,
            WeeklyInterval => set.DoneThisWeek,
            _ => set.States[(int)QuestState.DoneThisCycle],
        };
    }

    /// <summary>Tooltip of a state for a quest; null quest falls back to <see cref="Tooltip(QuestState)"/>.</summary>
    public static string Tooltip(QuestState state, QuestRecord? quest) =>
        quest is null ? Tooltip(state) : Tooltip(state, quest.RepeatInterval);

    /// <summary>"Name · subtitle", or the name alone when there is no subtitle.</summary>
    public static string ComposeTooltip(string name, string subtitle) =>
        string.IsNullOrEmpty(subtitle) ? name : name + TooltipSeparator + subtitle;

    private static string[] BuildTooltips(Func<QuestState, string> subtitle)
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
            table[(int)state] = ComposeTooltip(Name(state), subtitle(state));
        }

        return table;
    }

    /// <summary>
    /// A state moon's tooltip in the glyph palette in use: <see cref="Tooltip(QuestState, byte)"/> for Standard, the
    /// same name with <see cref="HighContrastSubtitle"/> as the shape hint for the high-contrast palette.
    /// </summary>
    public static string Tooltip(QuestState state, byte repeatInterval, bool highContrast)
    {
        if (!highContrast)
        {
            return Tooltip(state, repeatInterval);
        }

        var set = HighContrastSet.Value;
        if (state == QuestState.DoneThisCycle)
        {
            return repeatInterval switch
            {
                DailyInterval => set.DoneToday,
                WeeklyInterval => set.DoneThisWeek,
                _ => set.States[(int)QuestState.DoneThisCycle],
            };
        }

        return (uint)state < (uint)set.States.Length
            ? set.States[(int)state]
            : ComposeTooltip(Name(state), HighContrastSubtitle(state));
    }

    /// <summary>The high-contrast tooltip for a quest; null quest falls back to the state's own.</summary>
    public static string Tooltip(QuestState state, QuestRecord? quest, bool highContrast) =>
        Tooltip(state, quest?.RepeatInterval ?? 0, highContrast);

    /// <summary>
    /// The shape of a state's glyph in the high-contrast palette (Settings › Display › Glyph palette; accessibility
    /// panel §2.2): its silhouette and in-disc mark, as the Help legend and the moon tooltips name them.
    /// </summary>
    public static string HighContrastSubtitle(QuestState state) => state switch
    {
        QuestState.Ready => CoreText.T("Core.HighContrast.Ready", "bright half, bold bar"),
        QuestState.ReadyOnOtherJob => CoreText.T("Core.HighContrast.ReadyOnOtherJob", "dim half, hollow bar"),
        QuestState.Accepted => CoreText.T("Core.HighContrast.Accepted", "bright gibbous, large seal"),
        QuestState.Blocked => CoreText.T("Core.HighContrast.Blocked", "empty disc, thick rim"),
        QuestState.DoneThisCycle => CoreText.T("Core.HighContrast.DoneThisCycle", "dim gibbous, check"),
        QuestState.Completed => CoreText.T("Core.HighContrast.Completed", "solid bright disc"),
        QuestState.Foreclosed => CoreText.T("Core.HighContrast.Foreclosed", "thick diagonal bar"),
        QuestState.Unknown => CoreText.T("Core.HighContrast.Unknown", "dashed rim"),
        _ => string.Empty,
    };

    /// <summary>
    /// The moon-phase name of a state's glyph: a subtitle under the display name in the Help legend and the glyph
    /// window, and the shape hint in every moon's tooltip (<see cref="Tooltip(QuestState)"/>).
    /// </summary>
    public static string GlyphSubtitle(QuestState state) => state switch
    {
        QuestState.Ready => CoreText.T("Core.Glyph.Ready", "first quarter, glow"),
        QuestState.ReadyOnOtherJob => CoreText.T("Core.Glyph.ReadyOnOtherJob", "first quarter, silver, gold ring"),
        QuestState.Accepted => CoreText.T("Core.Glyph.Accepted", "waxing gibbous, sealed, silver ring"),
        QuestState.Blocked => CoreText.T("Core.Glyph.Blocked", "new moon, silver ring"),
        QuestState.DoneThisCycle => CoreText.T("Core.Glyph.DoneThisCycle", "waning gibbous, silver"),
        QuestState.Completed => CoreText.T("Core.Glyph.Completed", "full moon"),
        QuestState.Foreclosed => CoreText.T("Core.Glyph.Foreclosed", "eclipsed"),
        QuestState.Unknown => CoreText.T("Core.Glyph.Unknown", "veiled"),
        _ => string.Empty,
    };
}
