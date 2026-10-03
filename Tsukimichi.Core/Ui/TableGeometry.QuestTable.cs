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

    /// <summary>The quest's base EXP (1.9.0, R6 G): off unless Settings › Display › Planning turns it on.</summary>
    Exp,

    /// <summary>What the quest unlocks, up to three kind icons (feature plan v6 K4): on by default (Settings › Display › Planning).</summary>
    Opens,

    /// <summary>
    /// The giver's 20 px portrait avatar and name (1.15 design spec A5, F5): off by default, shown from the table's
    /// header menu; it hides first when the table is narrow.
    /// </summary>
    Giver,
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
/// <param name="Exp">The EXP column; 0 when it is not measured, which gives it no room at all.</param>
/// <param name="Opens">The Opens column; 0 when it is not measured, which gives it no room at all.</param>
/// <param name="Icon">A row icon's side (the job, reward and unlock icons): the least a column of icons can be sized to; 0 when not measured.</param>
/// <param name="Giver">The Giver column (the avatar, its gap and the name's room); 0 when it is not measured, which gives it no room at all.</param>
public readonly record struct QuestTableWidths(float Glyph, float Level, float JobIcon, float Job, float StateWord, float Expansion, float Rewards, float CellOverhead, float Scale = 1f, float Exp = 0f, float Opens = 0f, float Icon = 0f, float Giver = 0f);

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
    public const int QuestColumnCount = 10;

    /// <summary>
    /// The quest table's column specs, in <see cref="QuestColumn"/> order: the glyph, the name and the status never
    /// hide (priority 0); the name stretches with three shares and at least <see cref="LayoutBudgets.TableNameMinLogical"/>,
    /// the status with two shares and at least the widest state word plus <see cref="LayoutBudgets.TableStatusPadLogical"/>;
    /// then Level (2), Job with its label (3), Unlocks (4, the owner wants it in view), Expansion (5), Rewards (6), EXP (7)
    /// and Giver (8, 1.15), which hides first. A column the
    /// player hid from the table's menu (or the EXP column while Settings leaves it off) takes no room, as does an EXP
    /// column without a measured width. Every width is a column's content width plus
    /// <see cref="QuestTableWidths.CellOverhead"/>.
    /// </summary>
    /// <param name="widths">The measured content widths.</param>
    /// <param name="playerHidden">Whether the player hid each column, in <see cref="QuestColumn"/> order; empty for none.</param>
    /// <param name="specs">Filled with the specs; at least <see cref="QuestColumnCount"/> long.</param>
    public static void QuestColumnSpecs(in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, Span<ColumnSpec> specs) =>
        QuestColumnSpecs(widths, playerHidden, [], specs);

    /// <summary>
    /// <see cref="QuestColumnSpecs(in QuestTableWidths, ReadOnlySpan{bool}, Span{ColumnSpec})"/> with the widths the
    /// player dragged columns to (feature plan v6 U9): a column the player sized is fixed at that width (never under
    /// <see cref="PlayerColumnFloor"/>), keeps its priority and gives up its stretch share (the status), so it hides in
    /// the same order as before, at the width the player chose. The glyph and the name are never sized by the player.
    /// </summary>
    /// <param name="widths">The measured content widths.</param>
    /// <param name="playerHidden">Whether the player hid each column, in <see cref="QuestColumn"/> order; empty for none.</param>
    /// <param name="playerWidths">The player's content width of each column in pixels, in <see cref="QuestColumn"/> order; 0 (or empty) for automatic.</param>
    /// <param name="specs">Filled with the specs; at least <see cref="QuestColumnCount"/> long.</param>
    public static void QuestColumnSpecs(in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, ReadOnlySpan<float> playerWidths, Span<ColumnSpec> specs)
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
        specs[(int)QuestColumn.Expansion] = FixedSpec(5, widths.Expansion, pad);
        specs[(int)QuestColumn.Rewards] = FixedSpec(6, widths.Rewards, pad);
        specs[(int)QuestColumn.Exp] = NonNegative(widths.Exp) > 0f ? FixedSpec(7, widths.Exp, pad) : new(0, 0f, 0f);
        specs[(int)QuestColumn.Opens] = NonNegative(widths.Opens) > 0f ? FixedSpec(4, widths.Opens, pad) : new(0, 0f, 0f);
        specs[(int)QuestColumn.Giver] = NonNegative(widths.Giver) > 0f ? FixedSpec(8, widths.Giver, pad) : new(0, 0f, 0f);
        for (var i = (int)QuestColumn.Level; i < QuestColumnCount; i++)
        {
            // A column with no room at all (EXP or Unlocks while Settings leaves it off) stays without room.
            if (PlayerSized(playerWidths, (QuestColumn)i) && specs[i].Min > 0f)
            {
                specs[i] = FixedSpec(specs[i].Priority, MathF.Max(playerWidths[i], PlayerColumnFloor((QuestColumn)i, widths)), pad);
            }
        }

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
    public static QuestTablePlan PlanQuestTable(float available, in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, QuestTablePlan was, ReadOnlySpan<bool> wasVisible, Span<ColumnSpec> specs, Span<bool> visible, Span<float> columnWidths) =>
        PlanQuestTable(available, widths, playerHidden, [], was, wasVisible, specs, visible, columnWidths);

    /// <summary>
    /// <see cref="PlanQuestTable(float, in QuestTableWidths, ReadOnlySpan{bool}, QuestTablePlan, ReadOnlySpan{bool}, Span{ColumnSpec}, Span{bool}, Span{float})"/>
    /// with the widths the player dragged columns to (feature plan v6 U9,
    /// <see cref="QuestColumnSpecs(in QuestTableWidths, ReadOnlySpan{bool}, ReadOnlySpan{float}, Span{ColumnSpec})"/>):
    /// the columns the player sized are planned at their width and hide in the usual order, and a job column the player
    /// sized hides whole rather than going to its icon (its cell ellipsises the label instead). Without player widths
    /// (Reset column widths) the plan is the automatic one.
    /// </summary>
    public static QuestTablePlan PlanQuestTable(float available, in QuestTableWidths widths, ReadOnlySpan<bool> playerHidden, ReadOnlySpan<float> playerWidths, QuestTablePlan was, ReadOnlySpan<bool> wasVisible, Span<ColumnSpec> specs, Span<bool> visible, Span<float> columnWidths)
    {
        if (visible.Length < QuestColumnCount || columnWidths.Length < QuestColumnCount)
        {
            throw new ArgumentException("The output spans are shorter than the quest table's columns.");
        }

        QuestColumnSpecs(widths, playerHidden, playerWidths, specs);
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
        if (!visible[(int)QuestColumn.Job] && !Hidden(playerHidden, QuestColumn.Job) && !PlayerSized(playerWidths, QuestColumn.Job))
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
            QuestColumnSpecs(widths, playerHidden, playerWidths, specs);
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

    /// <summary>Whether the player sized <paramref name="column"/> (a finite width over 0); never the glyph or the name.</summary>
    public static bool PlayerSized(ReadOnlySpan<float> playerWidths, QuestColumn column) =>
        column is not (QuestColumn.Glyph or QuestColumn.Name) && playerWidths.Length > (int)column && float.IsFinite(playerWidths[(int)column]) && playerWidths[(int)column] > 0f;

    /// <summary>The widest a saved column width may be, in logical pixels: wider is a hand-edited file, not a drag.</summary>
    public const float MaxPlayerWidthLogical = 2000f;

    /// <summary>A saved column width (logical pixels) as the table uses it: 0 (automatic) when unreadable or not over 0, else at most <see cref="MaxPlayerWidthLogical"/>.</summary>
    public static float SanitizePlayerWidth(float logical) =>
        float.IsFinite(logical) && logical > 0f ? MathF.Min(logical, MaxPlayerWidthLogical) : 0f;

    /// <summary>
    /// The least content width the player can drag <paramref name="column"/> to (feature plan v6 U9), in pixels: the
    /// widest state word for the status (it is never cut, P1), else <see cref="LayoutBudgets.TableColumnMinLogical"/>,
    /// and at least one icon for the job, reward and unlock columns. Never over the column's own automatic width, so a
    /// floor never widens a column; 0 for the glyph and the name, which the player does not size.
    /// </summary>
    public static float PlayerColumnFloor(QuestColumn column, in QuestTableWidths widths)
    {
        var floor = LayoutBudgets.TableColumnMinLogical * ScaleOf(widths);
        var icon = MathF.Max(floor, NonNegative(widths.Icon));
        var auto = column switch
        {
            QuestColumn.Level => NonNegative(widths.Level),
            QuestColumn.Job => NonNegative(widths.JobIcon),
            QuestColumn.Expansion => NonNegative(widths.Expansion),
            QuestColumn.Rewards => NonNegative(widths.Rewards),
            QuestColumn.Exp => NonNegative(widths.Exp),
            QuestColumn.Opens => NonNegative(widths.Opens),
            QuestColumn.Giver => NonNegative(widths.Giver),
            _ => 0f,
        };

        return column switch
        {
            QuestColumn.Glyph or QuestColumn.Name => 0f,
            QuestColumn.Status => NonNegative(widths.StateWord),
            QuestColumn.Job or QuestColumn.Rewards or QuestColumn.Opens or QuestColumn.Giver => auto > 0f ? MathF.Min(icon, auto) : icon,
            _ => auto > 0f ? MathF.Min(floor, auto) : floor,
        };
    }

    /// <summary>
    /// The widths a drag on a column edge leaves (feature plan v6 U9). ImGui moves the edge under the pointer by trading
    /// width between the two columns beside it, or, beside the stretching name, by changing one column while the name
    /// gives or takes the difference. This keeps that, within the rules: every changed column stays at or over its
    /// floor; a traded pair keeps its sum, so its outer edges (and every other column) stay where they are; and the
    /// columns together grow at most by <paramref name="slack"/>, the room the name has over its minimum, so a drag
    /// never pushes another column out of the plan. A change under half a pixel is no change. Nothing allocates.
    /// </summary>
    /// <param name="before">Each column's content width as it was laid out before the drag.</param>
    /// <param name="after">Each column's content width as the drag left it; rewritten to the widths kept.</param>
    /// <param name="floors">Each column's floor (<see cref="PlayerColumnFloor"/>).</param>
    /// <param name="slack">How much the columns may grow together; under 0 counts as 0.</param>
    /// <param name="changed">Filled with whether each column's width changed.</param>
    /// <returns>Whether any column changed.</returns>
    public static bool ApplyColumnDrag(ReadOnlySpan<float> before, Span<float> after, ReadOnlySpan<float> floors, float slack, Span<bool> changed)
    {
        var n = before.Length;
        if (after.Length < n || floors.Length < n || changed.Length < n)
        {
            throw new ArgumentException("The spans are shorter than the columns.");
        }

        var count = 0;
        var first = -1;
        var second = -1;
        var sumBefore = 0f;
        var sumAfter = 0f;
        for (var i = 0; i < n; i++)
        {
            var was = NonNegative(before[i]);
            changed[i] = float.IsFinite(after[i]) && MathF.Abs(after[i] - was) > 0.5f;
            if (!changed[i])
            {
                after[i] = was;
                continue;
            }

            count++;
            sumBefore += was;
            sumAfter += MathF.Max(0f, after[i]);
            if (first < 0)
            {
                first = i;
            }
            else if (second < 0)
            {
                second = i;
            }
        }

        if (count == 0)
        {
            return false;
        }

        if (count == 2 && MathF.Abs(sumAfter - sumBefore) <= 1f)
        {
            // A trade between the two columns beside the edge: the one that shrank stops at its floor and the other
            // takes what the pair had, so the pair's outer edges stay put.
            var shrank = after[first] < NonNegative(before[first]) ? first : second;
            var grew = shrank == first ? second : first;
            var pair = NonNegative(before[first]) + NonNegative(before[second]);
            after[shrank] = MathF.Max(MathF.Max(0f, after[shrank]), NonNegative(floors[shrank]));
            after[grew] = pair - after[shrank];
            if (after[grew] < NonNegative(floors[grew]))
            {
                after[grew] = NonNegative(floors[grew]);
                after[shrank] = MathF.Max(NonNegative(floors[shrank]), pair - after[grew]);
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                if (changed[i])
                {
                    after[i] = MathF.Max(MathF.Max(0f, after[i]), NonNegative(floors[i]));
                }
            }
        }

        // The name gives up at most the slack; the columns that grew give back the rest, never under their floor.
        var growth = 0f;
        for (var i = 0; i < n; i++)
        {
            growth += after[i] - NonNegative(before[i]);
        }

        var excess = growth - NonNegative(slack);
        for (var i = 0; i < n && excess > 0f; i++)
        {
            var grown = after[i] - NonNegative(before[i]);
            if (changed[i] && grown > 0f)
            {
                var give = MathF.Min(excess, MathF.Min(grown, MathF.Max(0f, after[i] - NonNegative(floors[i]))));
                after[i] -= give;
                excess -= give;
            }
        }

        return true;
    }

    /// <summary>
    /// How many of <paramref name="count"/> icons a cell <paramref name="room"/> wide shows whole (feature plan v6 U9):
    /// all of them when they fit, else as many as leave room for the "+N" after them (<paramref name="more"/> wide),
    /// which may be none. An icon is never cut part-way.
    /// </summary>
    /// <param name="room">The cell's width.</param>
    /// <param name="icon">An icon's side.</param>
    /// <param name="gap">The gap between icons, and before the "+N".</param>
    /// <param name="count">How many icons there are.</param>
    /// <param name="more">The "+N" label's width.</param>
    public static int IconsThatFit(float room, float icon, float gap, int count, float more)
    {
        if (count <= 0)
        {
            return 0;
        }

        var width = NonNegative(room) + 0.5f;
        var side = NonNegative(icon);
        var space = NonNegative(gap);
        if ((count * side) + ((count - 1) * space) <= width)
        {
            return count;
        }

        var shown = 0;
        while (shown < count - 1 && ((shown + 1) * (side + space)) + NonNegative(more) <= width)
        {
            shown++;
        }

        return shown;
    }

    private static bool Hidden(ReadOnlySpan<bool> playerHidden, QuestColumn column) =>
        playerHidden.Length > (int)column && playerHidden[(int)column];

    private static ColumnSpec FixedSpec(int priority, float content, float pad) => new(priority, NonNegative(content) + pad, NonNegative(content) + pad);

    private static float NonNegative(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;

    private static float ScaleOf(in QuestTableWidths widths) => float.IsFinite(widths.Scale) && widths.Scale > 0f ? widths.Scale : 1f;
}
