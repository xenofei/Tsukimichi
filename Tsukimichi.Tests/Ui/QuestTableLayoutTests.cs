using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The quest table's column plan and row mode (feature plan v4 L4, UI audit §4.3, design v4 §8.2): Name stretches,
/// Status never loses its state word, the other columns hide Rewards → Expansion → Job (icon first) → Level, and under
/// 360 px the rows go two-line. The widths are the table's in English at UI scale 1, measured with Dalamud's font.
/// </summary>
public class QuestTableLayoutTests(ITestOutputHelper output)
{
    private const float H = LayoutBudgets.HysteresisLogical;
    private const int N = TableGeometry.QuestColumnCount;

    private static readonly string[] StateKeys =
    [
        "Core.State.Ready", "Core.State.ReadyOnOtherJob", "Core.State.Accepted", "Core.State.Blocked", "Core.State.DoneToday",
        "Core.State.DoneThisWeek", "Core.State.DoneThisCycle", "Core.State.Completed", "Core.State.Foreclosed", "Core.State.Unknown",
    ];

    /// <summary>The table's content widths in English at scale 1, as TablePane measures them (the body font, 16 px).</summary>
    private static readonly Lazy<QuestTableWidths> EnglishWidths = new(static () =>
    {
        var en = ResxFiles.Load(string.Empty);
        static float Body(string text) => FontAdvances.Width(text, LayoutBudgets.BodyFontPx);
        float Header(string key, bool sortable) => Body(en[key]) + (sortable ? LayoutBudgets.SortArrowLogical : 0f);

        // Comfortable rows: 28 px of content, the moon's box beside an 8 px lead and a 2 px gap.
        var glyph = 8f + (TableGeometry.GlyphRadius(28f, 6f) * TableGeometry.GlyphBoxPerRadius) + 2f;
        var level = MathF.Max(MathF.Max(LayoutBudgets.LevelColumnLogical, MathF.Max(28f, Body("100") + 12f)), Header("ColumnLevel", sortable: true));
        var jobHeader = Header("ColumnJob", sortable: false);
        var job = MathF.Max(MathF.Max(LayoutBudgets.JobColumnLogical, 16f + 5f + Body(en["JobDohDol"])), jobHeader);
        var jobIcon = MathF.Max(16f, jobHeader);
        var expansion = MathF.Max(LayoutBudgets.ExpansionColumnLogical, Header("ColumnExpansion", sortable: true));
        var rewards = MathF.Max((14f * 4f) + (2f * 3f) + 8f, Header("ColumnRewards", sortable: false));
        var stateWord = StateKeys.Max(k => Body(en[k]));
        return new QuestTableWidths(glyph, level, jobIcon, job, stateWord, expansion, rewards, (2f * LayoutBudgets.CellPaddingLogical) + 1f);
    });

    private static QuestTableWidths English => EnglishWidths.Value;

    private static int Col(QuestColumn column) => (int)column;

    private static (QuestTablePlan Plan, bool[] Visible, float[] Widths) Plan(float width, QuestTableWidths? measures = null, bool[]? hidden = null, QuestTablePlan was = default, bool[]? wasVisible = null)
    {
        var specs = new ColumnSpec[N];
        var visible = new bool[N];
        var widths = new float[N];
        var plan = TableGeometry.PlanQuestTable(width, measures ?? English, hidden ?? [], was, wasVisible ?? [], specs, visible, widths);
        return (plan, visible, widths);
    }

    private static ColumnSpec[] Specs(QuestTableWidths measures, bool[]? hidden = null)
    {
        var specs = new ColumnSpec[N];
        TableGeometry.QuestColumnSpecs(measures, hidden ?? [], specs);
        return specs;
    }

    private static float Need(params QuestColumn[] columns)
    {
        var specs = Specs(English);
        return columns.Sum(c => specs[Col(c)].Min);
    }

    private static float JobIconSpec => English.JobIcon + English.CellOverhead;

    /// <summary>The narrowest width (to 0.25 px), narrowing from wide without history, at which <paramref name="shows"/> still holds.</summary>
    private static float LowestWhere(Func<(QuestTablePlan Plan, bool[] Visible), bool> shows)
    {
        var width = 1200f;
        while (width > 0f)
        {
            var next = Plan(width - 0.25f);
            if (!shows((next.Plan, next.Visible)))
            {
                break;
            }

            width -= 0.25f;
        }

        return width;
    }

    [Fact]
    public void The_specs_follow_the_audit_table()
    {
        var specs = Specs(English);
        Assert.Equal(0, specs[Col(QuestColumn.Glyph)].Priority);
        Assert.False(specs[Col(QuestColumn.Glyph)].Stretch);

        var name = specs[Col(QuestColumn.Name)];
        Assert.Equal(0, name.Priority);
        Assert.Equal(LayoutBudgets.TableNameWeight, name.Weight);
        Assert.Equal(LayoutBudgets.TableNameMinLogical + English.CellOverhead, name.Min);

        // Status never hides: priority 0, its minimum the widest state word and a small pad.
        var status = specs[Col(QuestColumn.Status)];
        Assert.Equal(0, status.Priority);
        Assert.Equal(LayoutBudgets.TableStatusWeight, status.Weight);
        Assert.Equal(English.StateWord + LayoutBudgets.TableStatusPadLogical + English.CellOverhead, status.Min);

        Assert.Equal(2, specs[Col(QuestColumn.Level)].Priority);
        Assert.Equal(3, specs[Col(QuestColumn.Job)].Priority);
        Assert.Equal(4, specs[Col(QuestColumn.Expansion)].Priority);
        Assert.Equal(5, specs[Col(QuestColumn.Rewards)].Priority);
        Assert.Equal(English.Job + English.CellOverhead, specs[Col(QuestColumn.Job)].Min);
    }

    [Fact]
    public void The_logical_budgets_scale_with_the_ui()
    {
        var specs = Specs(English with { Scale = 2f });
        Assert.Equal((2f * LayoutBudgets.TableNameMinLogical) + English.CellOverhead, specs[Col(QuestColumn.Name)].Min);
        Assert.Equal(English.StateWord + (2f * LayoutBudgets.TableStatusPadLogical) + English.CellOverhead, specs[Col(QuestColumn.Status)].Min);
        Assert.True(TableGeometry.TwoLineRows(719f, 0f, 2f, wasTwoLine: false, 0f));
        Assert.False(TableGeometry.TwoLineRows(720f, 0f, 2f, wasTwoLine: false, 0f));
    }

    [Fact]
    public void A_wide_table_shows_every_column_and_name_and_status_share_three_to_two()
    {
        var (plan, visible, widths) = Plan(1000f);
        Assert.False(plan.TwoLine);
        Assert.False(plan.JobIconOnly);
        Assert.All(visible, static v => Assert.True(v));
        Assert.Equal(1000f, widths.Sum(), 2);

        var specs = Specs(English);
        var extraName = widths[Col(QuestColumn.Name)] - specs[Col(QuestColumn.Name)].Min;
        var extraStatus = widths[Col(QuestColumn.Status)] - specs[Col(QuestColumn.Status)].Min;
        Assert.Equal(1.5f, extraName / extraStatus, 3);
    }

    [Fact]
    public void Columns_step_aside_rewards_expansion_job_icon_job_level_and_never_status()
    {
        var all = Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Level, QuestColumn.Job, QuestColumn.Status, QuestColumn.Expansion, QuestColumn.Rewards);
        var noRewards = all - Need(QuestColumn.Rewards);
        var noExpansion = noRewards - Need(QuestColumn.Expansion);
        var jobIcon = noExpansion - Need(QuestColumn.Job) + JobIconSpec;
        var noJob = noExpansion - Need(QuestColumn.Job);

        void Shows(float width, bool rewards, bool expansion, bool job, bool icon, bool level)
        {
            var (plan, visible, _) = Plan(width);
            Assert.False(plan.TwoLine);
            Assert.Equal(rewards, visible[Col(QuestColumn.Rewards)]);
            Assert.Equal(expansion, visible[Col(QuestColumn.Expansion)]);
            Assert.Equal(job, visible[Col(QuestColumn.Job)]);
            Assert.Equal(icon, plan.JobIconOnly);
            Assert.Equal(level, visible[Col(QuestColumn.Level)]);
            Assert.True(visible[Col(QuestColumn.Status)]);
            Assert.True(visible[Col(QuestColumn.Name)]);
            Assert.True(visible[Col(QuestColumn.Glyph)]);
        }

        Shows(all, true, true, true, false, true);
        Shows(all - 1f, false, true, true, false, true);
        Shows(noRewards - 1f, false, false, true, false, true);
        Shows(noExpansion - 1f, false, false, true, true, true);
        Shows(jobIcon - 1f, false, false, false, false, true);
        Assert.Equal(noJob, jobIcon - JobIconSpec);
        Shows(noJob - 1f, false, false, false, false, false);
    }

    [Fact]
    public void An_icon_only_job_does_not_let_expansion_back()
    {
        // Just too narrow for the job's label with Expansion: the job goes to its icon, and Expansion (less important)
        // stays hidden although it would fit beside the icon.
        var noExpansion = Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Level, QuestColumn.Job, QuestColumn.Status);
        var (plan, visible, _) = Plan(noExpansion - 1f);
        Assert.True(plan.JobIconOnly);
        Assert.False(visible[Col(QuestColumn.Expansion)]);
        Assert.False(visible[Col(QuestColumn.Rewards)]);
    }

    [Fact]
    public void The_job_label_comes_back_with_the_hysteresis()
    {
        var label = Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Level, QuestColumn.Job, QuestColumn.Status);
        var narrow = Plan(label - 1f);
        Assert.True(narrow.Plan.JobIconOnly);

        Assert.True(Plan(label, was: narrow.Plan, wasVisible: narrow.Visible).Plan.JobIconOnly);
        Assert.True(Plan(label + H - 1f, was: narrow.Plan, wasVisible: narrow.Visible).Plan.JobIconOnly);
        Assert.False(Plan(label + H, was: narrow.Plan, wasVisible: narrow.Visible).Plan.JobIconOnly);

        // Narrowing, the label goes as soon as it does not fit.
        var wide = Plan(label + H);
        Assert.True(Plan(label - 1f, was: wide.Plan, wasVisible: wide.Visible).Plan.JobIconOnly);
    }

    [Fact]
    public void The_tier_breakpoints_line_up_with_the_english_plan()
    {
        // Each tier in LayoutBudgets is where the English table (UI scale 1) takes that step, to within half the
        // hysteresis: the design's table (§8.2) and the code say the same thing.
        var rewards = LowestWhere(static p => p.Visible[(int)QuestColumn.Rewards]);
        var expansion = LowestWhere(static p => p.Visible[(int)QuestColumn.Expansion]);
        var jobLabel = LowestWhere(static p => p.Visible[(int)QuestColumn.Job] && !p.Plan.JobIconOnly);
        var level = LowestWhere(static p => p.Visible[(int)QuestColumn.Level]);
        var job = LowestWhere(static p => p.Visible[(int)QuestColumn.Job]);
        var oneLine = LowestWhere(static p => !p.Plan.TwoLine);
        output.WriteLine($"rewards {rewards}, expansion {expansion}, job label {jobLabel}, job icon {job}, level {level}, one line {oneLine}");

        Assert.True(jobLabel > job && job > level, "the job goes to its icon, then hides, before the level");
        Assert.True(LayoutBudgets.TableNoLevelLogical >= LayoutBudgets.TableTwoLineUnderLogical);
        Assert.True(LayoutBudgets.TableTwoLineUnderLogical > LayoutBudgets.TableTwoLineLogical);

        Assert.InRange(rewards, LayoutBudgets.TableFullLogical - (H / 2f), LayoutBudgets.TableFullLogical + (H / 2f));
        Assert.InRange(expansion, LayoutBudgets.TableNoRewardsLogical - (H / 2f), LayoutBudgets.TableNoRewardsLogical + (H / 2f));
        Assert.InRange(jobLabel, LayoutBudgets.TableNoExpansionLogical - (H / 2f), LayoutBudgets.TableNoExpansionLogical + (H / 2f));
        Assert.InRange(level, LayoutBudgets.TableNoJobLogical - (H / 2f), LayoutBudgets.TableNoJobLogical + (H / 2f));
        Assert.InRange(oneLine, LayoutBudgets.TableNoLevelLogical - (H / 2f), LayoutBudgets.TableNoLevelLogical + (H / 2f));
    }

    [Fact]
    public void Rows_go_two_line_where_glyph_name_and_status_stop_fitting_and_come_back_with_the_hysteresis()
    {
        var need = Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Status);
        var threshold = MathF.Max(LayoutBudgets.TableTwoLineUnderLogical, need);
        Assert.True(Plan(threshold - 1f).Plan.TwoLine);
        Assert.False(Plan(threshold).Plan.TwoLine);
        Assert.True(Plan(LayoutBudgets.TableTwoLineLogical).Plan.TwoLine);

        var two = new QuestTablePlan(TwoLine: true, JobIconOnly: false);
        Assert.True(Plan(threshold, was: two).Plan.TwoLine);
        Assert.True(Plan(threshold + H - 1f, was: two).Plan.TwoLine);
        Assert.False(Plan(threshold + H, was: two).Plan.TwoLine);
    }

    [Fact]
    public void The_two_line_threshold_is_360_when_the_status_is_short()
    {
        Assert.True(TableGeometry.TwoLineRows(359f, 300f, 1f, wasTwoLine: false, H));
        Assert.False(TableGeometry.TwoLineRows(360f, 300f, 1f, wasTwoLine: false, H));
        Assert.True(TableGeometry.TwoLineRows(375f, 300f, 1f, wasTwoLine: true, H));
        Assert.False(TableGeometry.TwoLineRows(376f, 300f, 1f, wasTwoLine: true, H));

        var shortStates = English with { StateWord = 40f };
        Assert.True(Plan(359f, shortStates).Plan.TwoLine);
        Assert.False(Plan(360f, shortStates).Plan.TwoLine);
    }

    [Fact]
    public void Two_line_rows_show_the_glyph_and_the_name_across_the_rest()
    {
        var (plan, visible, widths) = Plan(340f);
        Assert.True(plan.TwoLine);
        Assert.Equal(new[] { true, true, false, false, false, false, false }, visible);
        Assert.Equal(English.Glyph + English.CellOverhead, widths[Col(QuestColumn.Glyph)], 3);
        Assert.Equal(340f, widths.Sum(), 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(60f)]
    [InlineData(236f)]
    public void The_status_shows_at_every_width_down_to_the_table_floor(float stateWordOrEnglish)
    {
        // Narrowing and widening a pixel at a time, with the history the table keeps: at every width from the
        // centre pane's floor up, the state word has room, as a column or on the second line.
        var measures = stateWordOrEnglish > 0f ? English with { StateWord = stateWordOrEnglish } : English;
        var stateWord = measures.StateWord;
        var floor = LayoutBudgets.TableTwoLineLogical;
        Assert.Equal(PaneLayout.CentreFloorLogical, floor);

        QuestTablePlan was = default;
        bool[] wasVisible = [];
        void Check(float width)
        {
            var specs = new ColumnSpec[N];
            var visible = new bool[N];
            var widths = new float[N];
            was = TableGeometry.PlanQuestTable(width, measures, [], was, wasVisible, specs, visible, widths);
            wasVisible = visible;
            if (was.TwoLine)
            {
                var line = widths[Col(QuestColumn.Name)] - measures.CellOverhead;
                Assert.True(line >= stateWord, $"the second line has {line} px for a {stateWord} px state word at {width}");
            }
            else
            {
                Assert.True(visible[Col(QuestColumn.Status)], $"one-line rows without their status at {width}");
                Assert.True(widths[Col(QuestColumn.Status)] - measures.CellOverhead >= stateWord, $"the status column is cut at {width}");
                Assert.True(widths.Sum() <= width + 0.01f, $"the columns overflow {width}");
            }
        }

        for (var width = 1200f; width >= floor; width -= 1f)
        {
            Check(width);
        }

        for (var width = floor; width <= 1200f; width += 1f)
        {
            Check(width);
        }
    }

    [Fact]
    public void A_width_wobbling_on_the_two_line_threshold_does_not_flip_the_rows()
    {
        var need = MathF.Max(LayoutBudgets.TableTwoLineUnderLogical, Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Status));
        var plan = Plan(need - 1f).Plan;
        Assert.True(plan.TwoLine);
        foreach (var width in new[] { need + 2f, need - 2f, need + 8f, need + H - 1f, need })
        {
            plan = Plan(width, was: plan).Plan;
            Assert.True(plan.TwoLine, $"the rows went one-line at {width}");
        }
    }

    [Fact]
    public void A_column_the_player_hid_stays_hidden_and_gives_its_room_back()
    {
        var hidden = new bool[N];
        hidden[Col(QuestColumn.Level)] = true;

        // Just too narrow for Rewards with the level column; the level column's room brings it back.
        var all = Specs(English).Sum(static s => s.Min);
        Assert.False(Plan(all - 1f).Visible[Col(QuestColumn.Rewards)]);
        var (_, visible, widths) = Plan(all - 1f, hidden: hidden);
        Assert.False(visible[Col(QuestColumn.Level)]);
        Assert.Equal(0f, widths[Col(QuestColumn.Level)]);
        Assert.True(visible[Col(QuestColumn.Rewards)]);

        // Hidden at every width, however wide.
        Assert.False(Plan(2000f, hidden: hidden).Visible[Col(QuestColumn.Level)]);
    }

    [Fact]
    public void A_job_column_the_player_hid_does_not_come_back_as_an_icon()
    {
        var hidden = new bool[N];
        hidden[Col(QuestColumn.Job)] = true;
        var noExpansion = Need(QuestColumn.Glyph, QuestColumn.Name, QuestColumn.Level, QuestColumn.Job, QuestColumn.Status);
        var (plan, visible, _) = Plan(noExpansion - 1f, hidden: hidden);
        Assert.False(plan.JobIconOnly);
        Assert.False(visible[Col(QuestColumn.Job)]);
    }

    [Fact]
    public void Hiding_the_status_keeps_rows_on_one_line()
    {
        var hidden = new bool[N];
        hidden[Col(QuestColumn.Status)] = true;
        var (plan, visible, _) = Plan(330f, hidden: hidden);
        Assert.False(plan.TwoLine);
        Assert.False(visible[Col(QuestColumn.Status)]);
        Assert.True(visible[Col(QuestColumn.Name)]);
    }

    [Fact]
    public void The_glyph_and_the_name_ignore_a_hide_request()
    {
        var hidden = new bool[N];
        hidden[Col(QuestColumn.Glyph)] = true;
        hidden[Col(QuestColumn.Name)] = true;
        var (_, visible, _) = Plan(800f, hidden: hidden);
        Assert.True(visible[Col(QuestColumn.Glyph)]);
        Assert.True(visible[Col(QuestColumn.Name)]);
    }

    [Fact]
    public void The_plan_is_the_same_for_the_same_width_and_history()
    {
        var a = Plan(431f);
        var b = Plan(431f);
        Assert.Equal(a.Plan, b.Plan);
        Assert.Equal(a.Visible, b.Visible);
        Assert.Equal(a.Widths, b.Widths);
    }

    [Fact]
    public void Unreadable_inputs_are_safe_and_short_spans_are_refused()
    {
        var nan = new QuestTableWidths(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
        var (plan, visible, widths) = Plan(float.NaN, nan);
        Assert.True(plan.TwoLine);
        Assert.True(visible[Col(QuestColumn.Name)]);
        Assert.All(widths, static w => Assert.True(float.IsFinite(w)));
        Assert.Throws<ArgumentException>(() => TableGeometry.QuestColumnSpecs(English, [], new ColumnSpec[3]));
        Assert.Throws<ArgumentException>(() => TableGeometry.PlanQuestTable(500f, English, [], default, [], new ColumnSpec[N], new bool[2], new float[N]));
    }
}
