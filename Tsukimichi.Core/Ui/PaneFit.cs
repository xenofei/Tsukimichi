namespace Tsukimichi.Core.Ui;

/// <summary>The row layout of My blues at a pane width (design v4 §7.8, §8.2).</summary>
public enum PlanRowTier
{
    /// <summary>One line: moon, name, kind pills, status, Flag and Reveal.</summary>
    OneLine,

    /// <summary>Two lines: moon, name and kind pills, then the status under the name; Flag and Reveal on the right.</summary>
    TwoLine,

    /// <summary>Two lines, with Flag and Reveal folded into one "…" menu.</summary>
    TwoLineMenu,
}

/// <summary>
/// The narrow-width rules of the panes other than the tree, the table and the detail pane (feature plan v4 L6, UI
/// audit §4.3, design v4 §8.2): My blues' row tiers and its one-line split, when Flight's and Abandoned's row buttons
/// fold into "…", the column plans of Flight, Moonlit and the Moonlit verdicts in Settings, and the clamps that keep a
/// fixed control or an empty state's column inside the room there is. Pure arithmetic in pixels (or logical pixels
/// where the name says so), so the plugin draws with it every frame without allocating and the tests pin it.
/// </summary>
public static class PaneFit
{
    /// <summary>My blues: at or above this a row shows every kind pill that fits; under it only its primary kind.</summary>
    public const float PlanAllKindsLogical = 520f;

    /// <summary>My blues' one-line row: the share of the middle (between the moon and the buttons) the status takes.</summary>
    public const float PlanStatusShare = 0.38f;

    /// <summary>My blues' one-line row: the share of what the status leaves that goes to the name; the pills get the rest.</summary>
    public const float PlanNameShare = 0.66f;

    /// <summary>Flight and Abandoned: under this pane width the row buttons fold into one "…" menu (as My blues').</summary>
    public const float FoldActionsLogical = LayoutBudgets.PlanMenuLogical;

    /// <summary>Flight: the quest's state moon column, the first to hide (the status line names the state too).</summary>
    public const int FlightStatePriority = 1;

    /// <summary>Moonlit: the Kind column hides second.</summary>
    public const int MoonlitKindPriority = 1;

    /// <summary>Moonlit: the Confidence column hides first.</summary>
    public const int MoonlitConfidencePriority = 2;

    /// <summary>Settings › Moonlit verdicts: the date hides second.</summary>
    public const int VerdictDatePriority = 1;

    /// <summary>Settings › Moonlit verdicts: the note hides first.</summary>
    public const int VerdictNotePriority = 2;

    /// <summary>The My blues row tier at a row <paramref name="widthLogical"/> wide; an unreadable width takes the narrowest.</summary>
    public static PlanRowTier PlanTier(float widthLogical)
    {
        if (!(widthLogical >= LayoutBudgets.PlanMenuLogical))
        {
            return PlanRowTier.TwoLineMenu;
        }

        return widthLogical < LayoutBudgets.PlanOneLineLogical ? PlanRowTier.TwoLine : PlanRowTier.OneLine;
    }

    /// <summary>Whether a My blues row <paramref name="widthLogical"/> wide shows every kind pill (else its primary kind only).</summary>
    public static bool PlanAllKinds(float widthLogical) => widthLogical >= PlanAllKindsLogical;

    /// <summary>Whether a Flight or Abandoned row <paramref name="widthLogical"/> wide folds its buttons into "…".</summary>
    public static bool FoldActions(float widthLogical) => !(widthLogical >= FoldActionsLogical);

    /// <summary>
    /// My blues' one-line row: how the <paramref name="middle"/> (the room between the moon and the buttons, gaps
    /// taken out) splits into the name, the kind pills and the status. The status takes
    /// <see cref="PlanStatusShare"/> but never less than <paramref name="statusMin"/> (its state word and a little
    /// reason), so the state word is never cut; the name and the pills share the rest.
    /// </summary>
    public static (float Name, float Pills, float Status) PlanOneLine(float middle, float statusMin)
    {
        var room = float.IsFinite(middle) ? MathF.Max(0f, middle) : 0f;
        var floor = float.IsFinite(statusMin) ? MathF.Max(0f, statusMin) : 0f;
        var status = MathF.Min(room, MathF.Max(floor, room * PlanStatusShare));
        var rest = room - status;
        var name = rest * PlanNameShare;
        return (name, rest - name, status);
    }

    /// <summary>A control's width: <paramref name="ideal"/>, or the <paramref name="available"/> room when that is less; never under 1.</summary>
    public static float ControlWidth(float ideal, float available)
    {
        var want = float.IsFinite(ideal) ? ideal : 1f;
        var room = float.IsFinite(available) ? available : want;
        return MathF.Max(1f, MathF.Min(want, room));
    }

    /// <summary>
    /// The width of a centred column (an empty state's text): <paramref name="max"/> at most and
    /// <paramref name="available"/> less a <paramref name="margin"/> otherwise, not under <paramref name="floor"/>
    /// while there is room for it, and never wider than <paramref name="available"/>.
    /// </summary>
    public static float Column(float available, float floor, float max, float margin)
    {
        var room = float.IsFinite(available) ? MathF.Max(1f, available) : MathF.Max(1f, max);
        var want = MathF.Min(room - MathF.Max(0f, margin), max);
        return MathF.Max(1f, MathF.Min(room, MathF.Max(floor, want)));
    }

    /// <summary>
    /// Flight's quest table (display order Attuned, Quest, State, Status, Actions): the moons and the actions are
    /// fixed and never hide, the quest's name and the status stretch 3 : 3 with the status never under its state
    /// word, and the state moon hides first.
    /// </summary>
    /// <param name="glyph">The width of a moon column.</param>
    /// <param name="nameMin">The least room the quest name keeps (<see cref="LayoutBudgets.RowNameMinLogical"/> in pixels).</param>
    /// <param name="statusMin">The status' least width: its widest state word and a little reason.</param>
    /// <param name="actions">The actions column: the buttons, or the "…" button when they fold.</param>
    /// <param name="columns">Filled with the five columns; at least five long.</param>
    public static void FlightColumns(float glyph, float nameMin, float statusMin, float actions, Span<ColumnSpec> columns)
    {
        columns[0] = new ColumnSpec(0, glyph, glyph);
        columns[1] = new ColumnSpec(0, nameMin, nameMin, 3f);
        columns[2] = new ColumnSpec(FlightStatePriority, glyph, glyph);
        columns[3] = new ColumnSpec(0, statusMin, statusMin, 3f);
        columns[4] = new ColumnSpec(0, actions, actions);
    }

    /// <summary>
    /// The Moonlit table (display order Obtained, Reward, Kind, Quest, State, Confidence): Confidence hides first,
    /// then Kind; State, the two names and Obtained never hide.
    /// </summary>
    /// <param name="glyph">The width of a moon column.</param>
    /// <param name="rewardMin">The reward cell's least width: its icon, a short name and the "…" button.</param>
    /// <param name="kind">The Kind column's width.</param>
    /// <param name="questMin">The quest name's least width.</param>
    /// <param name="confidence">The Confidence column's width.</param>
    /// <param name="columns">Filled with the six columns; at least six long.</param>
    public static void MoonlitColumns(float glyph, float rewardMin, float kind, float questMin, float confidence, Span<ColumnSpec> columns)
    {
        columns[0] = new ColumnSpec(0, glyph, glyph);
        columns[1] = new ColumnSpec(0, rewardMin, rewardMin, 3f);
        columns[2] = new ColumnSpec(MoonlitKindPriority, kind, kind);
        columns[3] = new ColumnSpec(0, questMin, questMin, 3f);
        columns[4] = new ColumnSpec(0, glyph, glyph);
        columns[5] = new ColumnSpec(MoonlitConfidencePriority, confidence, confidence);
    }

    /// <summary>
    /// The Moonlit verdicts in Settings (display order Quest, Verdict, Note, Date, Restore): the note hides first,
    /// then the date; the quest, the verdict and Restore stay.
    /// </summary>
    /// <param name="nameMin">The least room the quest name and the note keep.</param>
    /// <param name="verdict">The widest verdict.</param>
    /// <param name="date">The widest date.</param>
    /// <param name="restore">The Restore button.</param>
    /// <param name="columns">Filled with the five columns; at least five long.</param>
    public static void VerdictColumns(float nameMin, float verdict, float date, float restore, Span<ColumnSpec> columns)
    {
        columns[0] = new ColumnSpec(0, nameMin, nameMin, 3f);
        columns[1] = new ColumnSpec(0, verdict, verdict);
        columns[2] = new ColumnSpec(VerdictNotePriority, nameMin, nameMin, 3f);
        columns[3] = new ColumnSpec(VerdictDatePriority, date, date);
        columns[4] = new ColumnSpec(0, restore, restore);
    }
}
