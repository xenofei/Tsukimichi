namespace Tsukimichi.Core.Ui;

/// <summary>The quest table's columns, in the order the table sets them up (the player may reorder them on screen).</summary>
public enum QuestColumn
{
    /// <summary>The state moon and the pinned dot.</summary>
    Glyph,

    /// <summary>The quest's name.</summary>
    Name,

    /// <summary>The level pill.</summary>
    Level,

    /// <summary>The job icon and abbreviation.</summary>
    Job,

    /// <summary>The state word and its reason.</summary>
    Status,

    /// <summary>The expansion pill.</summary>
    Expansion,

    /// <summary>The reward icons.</summary>
    Rewards,
}

/// <summary>
/// The measured content widths of the quest table's columns (pixels), for <see cref="TableGeometry.QuestColumnSpecs"/>.
/// </summary>
/// <param name="Glyph">The glyph column.</param>
/// <param name="Level">The level column (its widest pill or its header).</param>
/// <param name="JobIcon">The job column with its icon alone (or its header, when wider).</param>
/// <param name="Job">The job column with icon, gap and the widest label.</param>
/// <param name="StateWord">The widest state word in the current language.</param>
/// <param name="Expansion">The expansion column.</param>
/// <param name="Rewards">The rewards column.</param>
/// <param name="CellOverhead">What the table adds to each column's content (the cell padding either side and the border).</param>
/// <param name="Scale">Pixels per logical pixel, for the logical budgets (name minimum, status pad, hysteresis).</param>
public readonly record struct QuestTableWidths(float Glyph, float Level, float JobIcon, float Job, float StateWord, float Expansion, float Rewards, float CellOverhead, float Scale = 1f);

/// <summary>What <see cref="TableGeometry.PlanQuestTable"/> decided for a frame, beyond the columns.</summary>
/// <param name="TwoLine">The rows are two-line: the name and the level, then the status under the name.</param>
/// <param name="JobIconOnly">The job column shows its icon alone (its label did not fit, the icon did).</param>
public readonly record struct QuestTablePlan(bool TwoLine, bool JobIconOnly);

/// <summary>
/// The quest table's column plan (feature plan v4 L4, UI audit §4.3, design v4 §8.2): Name stretches, Status never
/// loses its state word, the other columns hide Rewards → Expansion → Job (its icon alone first) → Level, and under
/// 360 px the rows go two-line.
/// </summary>
public static partial class TableGeometry
{
    /// <summary>How many columns the quest table has (<see cref="QuestColumn"/>).</summary>
    public const int QuestColumnCount = 7;

    /// <summary>
    /// The quest table's column specs, in <see cref="QuestColumn"/> order: the glyph, the name and the status never
    /// hide (priority 0); the name stretches with three shares and at least <see cref="LayoutBudgets.TableNameMinLogical"/>,
    /// the status with two shares and at least the widest state word plus <see cref="LayoutBudgets.TableStatusPadLogical"/>;
    /// then Level (2), Job with its label (3), Expansion (4) and Rewards (5), which hides first. A column the player hid
    /// from the table's menu takes no room. Every width is a column's content width plus
    /// <see cref="QuestTableWidths.CellOverhead"/>.
    /// </summary>
    /// <param name="widths">The measured content widths.</param>
    /// <param name="playerHidden">Whether the player hid each column, in <see cref="QuestColumn"/> order; empty for none.</param>
    /// <param name="specs">Filled with the specs; at least <see cref="QuestColumnCount"/> long.</param>
    public static void QuestColumnSpecs(in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, Span<ColumnSpec> specs)
    {
        if (specs.Length < QuestColumnCount)
        {
            throw new ArgumentException("The specs span is shorter than the quest table's columns.", nameof(specs));
        }

        var pad = NonNegative(widths.CellOverhead);
        var name = LayoutBudgets.TableNameMinLogical * ScaleOf(widths) + pad;
        var status = StatusColumnMin(widths);
        specs[(int)QuestColumn.Glyph] = FixedSpec(0, widths.Glyph, pad);
        specs[(int)QuestColumn.Name] = new(0, name, name, LayoutBudgets.TableNameWeight);
        specs[(int)QuestColumn.Level] = FixedSpec(2, widths.Level, pad);
        specs[(int)QuestColumn.Job] = FixedSpec(3, MathF.Max(NonNegative(widths.Job), NonNegative(widths.JobIcon)), pad);
        specs[(int)QuestColumn.Status] = new(0, status, status, LayoutBudgets.TableStatusWeight);
        specs[(int)QuestColumn.Expansion] = FixedSpec(4, widths.Expansion, pad);
        specs[(int)QuestColumn.Rewards] = FixedSpec(5, widths.Rewards, pad);
        for (var i = (int)QuestColumn.Level; i < QuestColumnCount && i < playerHidden.Length; i++)
        {
            if (playerHidden[i])
            {
                specs[i] = new(0, 0f, 0f);
            }
        }
    }

    /// <summary>The status column's least width: the widest state word, <see cref="LayoutBudgets.TableStatusPadLogical"/> and the cell's overhead.</summary>
    public static float StatusColumnMin(in QuestTableWidths widths) =>
        NonNegative(widths.StateWord) + LayoutBudgets.TableStatusPadLogical * ScaleOf(widths) + NonNegative(widths.CellOverhead);

    /// <summary>
    /// Whether the quest table's rows go two-line (design v4 §8.2): the name with the level on the first line, the status
    /// on the second. They do below <see cref="LayoutBudgets.TableTwoLineUnderLogical"/>, or below
    /// <paramref name="oneLineNeed"/> (the glyph, name and status columns' minimums) when that is wider, since a one-line
    /// row must never lose its status. Coming back to one line takes <paramref name="hysteresis"/> more, so a width
    /// resting on the threshold does not flip the rows.
    /// </summary>
    /// <param name="available">The table's width in pixels.</param>
    /// <param name="oneLineNeed">The least width one-line rows need with their status.</param>
    /// <param name="scale">Pixels per logical pixel.</param>
    /// <param name="wasTwoLine">Last frame's choice.</param>
    /// <param name="hysteresis">The margin, in pixels.</param>
    public static bool TwoLineRows(float available, float oneLineNeed, float scale, bool wasTwoLine, float hysteresis)
    {
        var width = float.IsFinite(available) ? available : 0f;
        var unit = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        var threshold = MathF.Max(LayoutBudgets.TableTwoLineUnderLogical * unit, NonNegative(oneLineNeed));
        return width < threshold + (wasTwoLine ? NonNegative(hysteresis) : 0f);
    }

    /// <summary>
    /// The quest table's layout for a width. Two-line rows (<see cref="TwoLineRows"/>) show only the glyph and the name
    /// columns, the name taking the rest. One-line rows are planned by <see cref="PlanColumns"/> over
    /// <see cref="QuestColumnSpecs"/>; when the job column with its label would hide, it is planned again with its icon
    /// alone (Rewards and Expansion staying hidden), so the job goes to its icon before it goes. Either way the status
    /// shows at every width the centre pane allows: on the second line, or as a column that never hides (one-line rows
    /// only happen where the glyph, name and status fit). A column the player hid stays hidden, and when the player hid
    /// the status the rows stay one-line (a second line would be empty). Every change back to a wider layout (one line,
    /// a column, the job's label) waits for <see cref="LayoutBudgets.HysteresisLogical"/> to spare. Nothing allocates.
    /// </summary>
    /// <param name="available">The table's width in pixels.</param>
    /// <param name="widths">The measured content widths.</param>
    /// <param name="playerHidden">Whether the player hid each column; empty for none.</param>
    /// <param name="was">Last frame's plan.</param>
    /// <param name="wasVisible">Last frame's visibility; empty on the first frame.</param>
    /// <param name="specs">Filled with the specs (the job's as planned).</param>
    /// <param name="visible">Filled with this frame's visibility.</param>
    /// <param name="columnWidths">Filled with each column's width, overhead included (0 when hidden).</param>
    public static QuestTablePlan PlanQuestTable(float available, in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, QuestTablePlan was, ReadOnlySpan<bool> wasVisible, Span<ColumnSpec> specs, Span<bool> visible, Span<float> columnWidths)
    {
        if (visible.Length < QuestColumnCount || columnWidths.Length < QuestColumnCount)
        {
            throw new ArgumentException("The output spans are shorter than the quest table's columns.");
        }

        QuestColumnSpecs(widths, playerHidden, specs);
        var room = float.IsFinite(available) ? MathF.Max(0f, available) : 0f;
        var scale = ScaleOf(widths);
        var hysteresis = LayoutBudgets.HysteresisLogical * scale;
        var glyph = specs[(int)QuestColumn.Glyph].Min;
        if (!Hidden(playerHidden, QuestColumn.Status))
        {
            var oneLineNeed = glyph + specs[(int)QuestColumn.Name].Min + specs[(int)QuestColumn.Status].Min;
            if (TwoLineRows(room, oneLineNeed, scale, was.TwoLine, hysteresis))
            {
                for (var i = 0; i < QuestColumnCount; i++)
                {
                    visible[i] = i is (int)QuestColumn.Glyph or (int)QuestColumn.Name;
                    columnWidths[i] = 0f;
                }

                columnWidths[(int)QuestColumn.Glyph] = glyph;
                columnWidths[(int)QuestColumn.Name] = MathF.Max(0f, room - glyph);
                return new QuestTablePlan(TwoLine: true, JobIconOnly: false);
            }
        }

        // The job's label, when it fits; last frame's icon-only job counts as hidden here, so the label waits for the
        // hysteresis to come back.
        var columns = specs[..QuestColumnCount];
        Span<bool> history = stackalloc bool[QuestColumnCount];
        var hasHistory = wasVisible.Length >= QuestColumnCount;
        if (hasHistory)
        {
            wasVisible[..QuestColumnCount].CopyTo(history);
            history[(int)QuestColumn.Job] &= !was.JobIconOnly;
        }

        PlanColumns(room, columns, hasHistory ? history : [], visible, columnWidths, hysteresis);
        var iconOnly = false;
        if (!visible[(int)QuestColumn.Job] && !Hidden(playerHidden, QuestColumn.Job))
        {
            // Its icon alone: the columns that hide before the job stay hidden.
            var jobPriority = columns[(int)QuestColumn.Job].Priority;
            Span<bool> before = stackalloc bool[QuestColumnCount];
            for (var i = 0; i < QuestColumnCount; i++)
            {
                before[i] = columns[i].Priority > jobPriority;
                if (before[i])
                {
                    columns[i] = new(0, 0f, 0f);
                }
            }

            columns[(int)QuestColumn.Job] = FixedSpec(jobPriority, widths.JobIcon, NonNegative(widths.CellOverhead));
            PlanColumns(room, columns, wasVisible, visible, columnWidths, hysteresis);
            iconOnly = visible[(int)QuestColumn.Job];
            for (var i = 0; i < QuestColumnCount; i++)
            {
                if (before[i])
                {
                    visible[i] = false;
                    columnWidths[i] = 0f;
                }
            }

            // The specs as planned: every column's own, the job's with its icon alone when that is what shows.
            QuestColumnSpecs(widths, playerHidden, specs);
            if (iconOnly)
            {
                specs[(int)QuestColumn.Job] = FixedSpec(jobPriority, widths.JobIcon, NonNegative(widths.CellOverhead));
            }
        }

        for (var i = (int)QuestColumn.Level; i < QuestColumnCount; i++)
        {
            if (Hidden(playerHidden, (QuestColumn)i))
            {
                visible[i] = false;
                columnWidths[i] = 0f;
            }
        }

        return new QuestTablePlan(TwoLine: false, JobIconOnly: iconOnly);
    }

    private static bool Hidden(ReadOnlySpan<bool> playerHidden, QuestColumn column) =>
        playerHidden.Length > (int)column && playerHidden[(int)column];

    private static ColumnSpec FixedSpec(int priority, float content, float pad) => new(priority, NonNegative(content) + pad, NonNegative(content) + pad);

    private static float NonNegative(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;

    private static float ScaleOf(in QuestTableWidths widths) => float.IsFinite(widths.Scale) && widths.Scale > 0f ? widths.Scale : 1f;
}
