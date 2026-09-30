using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>One lit run of a state stripe, from <see cref="Start"/> to <see cref="End"/> as fractions of the row height (0 = top).</summary>
public readonly record struct StripeSegment(float Start, float End);

/// <summary>
/// The pattern of the 3 px state stripe on the left edge of a quest table row (feature plan v3 T15, accessibility
/// panel A3): each state lights a different set of runs along the row height, so the stripe still tells the states
/// apart with the colour removed (a greyscale stream, a colour filter, achromatopsia), where Moon and Silver are
/// the same grey. Colour only reinforces it. The eight patterns:
/// <list type="table">
/// <item><term>Completed</term><description>solid, the full height ("full bar")</description></item>
/// <item><term>In journal</term><description>two segments, the top and bottom thirds with the middle open ("two bars")</description></item>
/// <item><term>Ready</term><description>60 % of the height, centred ("short bar")</description></item>
/// <item><term>Ready on another job</term><description>a centred tick of 20 %, a small Ready ("centre tick")</description></item>
/// <item><term>Done today / this week</term><description>the lower half, filled for now until the reset ("lower half")</description></item>
/// <item><term>Blocked</term><description>none ("no bar")</description></item>
/// <item><term>Locked out</term><description>three long dashes ("dashed")</description></item>
/// <item><term>Not checked</term><description>six short dots ("dotted")</description></item>
/// </list>
/// Every pattern is symmetric about the middle except "lower half", and every run lies inside 0..1 in ascending
/// order. Segments are read-only spans over static arrays: nothing allocates per frame.
/// </summary>
public static class StripePattern
{
    private static readonly StripeSegment[] Full = [new(0f, 1f)];
    private static readonly StripeSegment[] TwoBars = [new(0f, 0.3f), new(0.7f, 1f)];
    private static readonly StripeSegment[] ShortBar = [new(0.2f, 0.8f)];
    private static readonly StripeSegment[] CentreTick = [new(0.4f, 0.6f)];
    private static readonly StripeSegment[] LowerHalf = [new(0.5f, 1f)];
    private static readonly StripeSegment[] None = [];
    private static readonly StripeSegment[] Dashed = [new(0.055f, 0.275f), new(0.39f, 0.61f), new(0.725f, 0.945f)];
    private static readonly StripeSegment[] Dotted = BuildDots(6, 0.06f);

    /// <summary>Between the state name and the pattern name in <see cref="Tooltip(QuestState, byte)"/>.</summary>
    public const string Separator = StateNames.TooltipSeparator;

    /// <summary>The tooltips per state, then the done-today and done-this-week variants; composed once per language.</summary>
    private static readonly TextCache<string[]> Tooltips = new(BuildTooltips);

    /// <summary>The lit runs of a state's stripe, top to bottom; empty for Blocked (and for a value outside the enum).</summary>
    public static ReadOnlySpan<StripeSegment> Segments(QuestState state) => state switch
    {
        QuestState.Completed => Full,
        QuestState.Accepted => TwoBars,
        QuestState.Ready => ShortBar,
        QuestState.ReadyOnOtherJob => CentreTick,
        QuestState.DoneThisCycle => LowerHalf,
        QuestState.Blocked => None,
        QuestState.Foreclosed => Dashed,
        QuestState.Unknown => Dotted,
        _ => None,
    };

    /// <summary>The pattern's name as the stripe's tooltip says it ("short bar").</summary>
    public static string Name(QuestState state) => state switch
    {
        QuestState.Completed => CoreText.T("Core.Stripe.Completed", "full bar"),
        QuestState.Accepted => CoreText.T("Core.Stripe.Accepted", "two bars"),
        QuestState.Ready => CoreText.T("Core.Stripe.Ready", "short bar"),
        QuestState.ReadyOnOtherJob => CoreText.T("Core.Stripe.ReadyOnOtherJob", "centre tick"),
        QuestState.DoneThisCycle => CoreText.T("Core.Stripe.DoneThisCycle", "lower half"),
        QuestState.Blocked => CoreText.T("Core.Stripe.Blocked", "no bar"),
        QuestState.Foreclosed => CoreText.T("Core.Stripe.Foreclosed", "dashed"),
        QuestState.Unknown => CoreText.T("Core.Stripe.Unknown", "dotted"),
        _ => string.Empty,
    };

    /// <summary>
    /// The stripe's tooltip: the state's display name and the pattern ("Ready · short bar"); a done repeatable says
    /// today or this week (<see cref="StateNames.Name(QuestState, byte)"/>). Precomposed; allocates nothing.
    /// </summary>
    public static string Tooltip(QuestState state, byte repeatInterval = 0)
    {
        var tooltips = Tooltips.Value;
        var states = tooltips.Length - 2;
        if (state == QuestState.DoneThisCycle)
        {
            return repeatInterval switch
            {
                StateNames.DailyInterval => tooltips[states],
                StateNames.WeeklyInterval => tooltips[states + 1],
                _ => tooltips[(int)QuestState.DoneThisCycle],
            };
        }

        return (uint)state < (uint)states ? tooltips[(int)state] : StateNames.Name(state);
    }

    private static StripeSegment[] BuildDots(int count, float length)
    {
        var dots = new StripeSegment[count];
        var period = 1f / count;
        for (var i = 0; i < count; i++)
        {
            var centre = (i + 0.5f) * period;
            dots[i] = new StripeSegment(centre - length * 0.5f, centre + length * 0.5f);
        }

        return dots;
    }

    private static string[] BuildTooltips()
    {
        var states = Enum.GetValues<QuestState>();
        var max = 0;
        foreach (var state in states)
        {
            max = Math.Max(max, (int)state);
        }

        var table = new string[max + 3];
        foreach (var state in states)
        {
            table[(int)state] = StateNames.Name(state) + Separator + Name(state);
        }

        table[max + 1] = StateNames.DoneToday + Separator + Name(QuestState.DoneThisCycle);
        table[max + 2] = StateNames.DoneThisWeek + Separator + Name(QuestState.DoneThisCycle);
        return table;
    }
}
