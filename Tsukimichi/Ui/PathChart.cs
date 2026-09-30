using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Path card body: the star chart of docs/design/path-section-proposal.md §4. One thread down a
/// gutter with the chain's moons on it, expansion sky bands behind (alternating faint tint, a horizon line, a
/// pre-seeded star field, a four-point star sigil and the band's name with a fading rule), the thread gold where the
/// step above is done and dashed silver where it is not, completed stretches as capsule beads that open into compact
/// rows, Any-join alternatives as hollow ghost nodes whose curve merges into the join (a click re-roots the chart on
/// that branch), the target as the one large moon under a three-layer halo and a thin ring on a spotlight row, and
/// the Unlocks next comb hanging off the dashed spine below it. The chart scrolls inside a clipped child with the
/// target placed at 60 % on selection, a "target" pill while the target is out of view and a thin minimap beside it
/// while it scrolls.
///
/// Everything with text or a seed is built in <see cref="Load"/> (when the pane's model refreshes); the pixel layout
/// and the star positions are rebuilt only when a bead opens or closes or a scale or width changes. A frame draws
/// only the rows in view, formats nothing and allocates nothing. Nothing moves on its own: the "Show path" pulse on
/// the target's ring is a <see cref="Motion"/> pulse, off under Reduce motion.
/// </summary>
public sealed class PathChart
{
    /// <summary>"or via {0}" split around the quest name, per language.</summary>
    private static readonly Localization.LocText AlternativeBefore = new(static () => Strings.SplitAtLink(Strings.PathAlternativeFormat, Strings.LinkSlot).Before);
    private static readonly Localization.LocText AlternativeAfter = new(static () => Strings.SplitAtLink(Strings.PathAlternativeFormat, Strings.LinkSlot).After);

    private const float PulseSeconds = 1.5f;
    private const ulong PulseKeyBase = 0x5041_5448_0000_0000UL; // "PATH"

    private enum VKind : byte
    {
        Band,
        Step,
        Target,
        Bead,
        RunStep,
        Alternative,
        MoreAlternatives,
        Caption,
        UnlocksHeader,
        Unlock,
        UnlocksMore,
        Dots,
    }

    /// <summary>One drawn row in pixels (content coordinates of the chart child).</summary>
    private struct VRow
    {
        public VKind Kind;
        public int Core;        // index into rows (-1 for the tail)
        public int Item;        // path index (Step, Target, RunStep), unlock index (Unlock), -1 otherwise
        public float Y;
        public float H;
        public float NodeY;     // centre of the row's node
        public float NodeR;     // main-thread node radius; 0 when the row has none on the thread
        public int Prev;        // previous main-thread node row, -1 for none
        public bool OutDone;    // the thread leaving this node downwards is walked (gold)
        public int Join;        // Alternative: the join's row
    }

    private readonly struct StarPx(float x, float y, StarMagnitude magnitude)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly StarMagnitude Magnitude = magnitude;
    }

    private readonly Action<uint> select;

    // ---- per path (Load) ----
    private IReadOnlyList<PathStep> path = [];
    private IReadOnlyList<PathRow> rows = [];
    private IReadOnlyList<StarBand> bands = [];
    private string[] stepNames = [];
    private string[] stepDetails = [];
    private string[] rowLabels = [];        // band name, bead label, alternative name, "and N more"
    private string[] rowSuffixes = [];      // alternative " · N steps"
    private string[] rowTooltips = [];      // alternative tooltip, "and N more" list
    private readonly List<Unlock> unlocks = [];
    private string unlocksHeader = Strings.PathUnlocksHeaderNone;
    private string? unlocksMore;
    private string? unlocksMoreTooltip;
    private string? caption;
    private bool captionVeiled;
    private int targetCore = -1;
    private uint targetRowId = uint.MaxValue;
    private float walkedFraction;
    // Opened beads, keyed by the run's first quest: a refresh that re-numbers the runs (a step completed above) keeps
    // the same stretch open.
    private readonly HashSet<uint> expandedRuns = [];

    // ---- layout (pixels) ----
    private readonly List<VRow> layout = new(64);
    private readonly List<StarPx> stars = new(128);
    private readonly List<(float Y0, float Y1)> bandSpans = new(8);
    private int[] coreToRow = [];
    private bool layoutDirty = true;
    private float layoutScale;
    private float layoutIconScale;
    private float layoutFont;
    private float layoutWidth;
    private float contentHeight;
    private int targetRow = -1;
    private float threadX;
    private float labelX;
    private float ghostX;
    private float ghostLabelX;

    // ---- scroll and motion ----
    private int scrollFrames;

    private readonly record struct Unlock(uint RowId, string Name, QuestState State, string Detail);

    /// <param name="select">Selects a quest (a step, an alternative or an unlock), as the rest of the pane does.</param>
    public PathChart(Action<uint> select)
    {
        this.select = select ?? throw new ArgumentNullException(nameof(select));
    }

    /// <summary>"N steps · M done" (or "1 step") for the card header.</summary>
    public string HeaderCaption { get; private set; } = string.Empty;

    /// <summary>The path's steps, first to target (for tests of intent and the pane's own use).</summary>
    public int StepCount => path.Count;

    /// <summary>
    /// Builds the chart for <paramref name="quest"/>: the path, its Any-join alternatives, rows, star field, every label
    /// through the spoiler shield, and what the quest unlocks next (at most <paramref name="maxUnlocks"/>). A new target
    /// forgets opened beads and asks for the scroll that puts it at 60 %.
    /// </summary>
    public void Load(SessionState session, CatalogBundle bundle, QuestRecord quest, int maxUnlocks)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(quest);

        var catalog = bundle.Catalog;
        var states = session.States;
        var spoilers = session.Spoilers;
        if (quest.RowId != targetRowId)
        {
            targetRowId = quest.RowId;
            expandedRuns.Clear();
            scrollFrames = 2;
        }

        path = PathFinder.PathTo(quest.RowId, catalog, states);
        var alternatives = PathFinder.Alternatives(path, catalog, states);
        rows = PathRows.Build(path, id => catalog.GetByRowId(id)?.Expansion ?? quest.Expansion, alternatives);
        bands = StarField.ForPath(rows);
        targetCore = PathRows.TargetIndex(rows);

        stepNames = new string[path.Count];
        stepDetails = new string[path.Count];
        var done = 0;
        var firstUndone = -1;
        for (var i = 0; i < path.Count; i++)
        {
            var step = catalog.GetByRowId(path[i].RowId);
            stepNames[i] = step is null ? path[i].RowId.ToString(CultureInfo.InvariantCulture) : spoilers.DisplayName(step);
            stepDetails[i] = step is null ? string.Empty : Detail(bundle, step);
            if (path[i].Done)
            {
                done++;
            }
            else if (firstUndone < 0)
            {
                firstUndone = i;
            }
        }

        walkedFraction = path.Count == 0 ? 0f : (firstUndone < 0 ? path.Count : firstUndone) / (float)path.Count;
        HeaderCaption = path.Count <= 1
            ? Strings.PathCaptionOne
            : string.Format(CultureInfo.CurrentCulture, Strings.PathCaptionFormat, path.Count, done);

        rowLabels = new string[rows.Count];
        rowSuffixes = new string[rows.Count];
        rowTooltips = new string[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            rowLabels[i] = string.Empty;
            rowSuffixes[i] = string.Empty;
            rowTooltips[i] = string.Empty;
            switch (row.Kind)
            {
                case PathRowKind.Band:
                    var name = bundle.Names.Expansion(row.Expansion);
                    rowLabels[i] = (name.Length > 0 ? name : Strings.ExpansionShort(row.Expansion)).ToUpper(CultureInfo.CurrentCulture);
                    break;
                case PathRowKind.FoldedRun:
                    rowLabels[i] = string.Format(CultureInfo.CurrentCulture, Strings.PathMoonsWalkedFormat, row.Count);
                    break;
                case PathRowKind.Alternative:
                    rowLabels[i] = spoilers.DisplayName(catalog, row.RowId, row.RowId.ToString(CultureInfo.InvariantCulture));
                    rowSuffixes[i] = row.RemainingCount switch
                    {
                        0 => Strings.PathAlternativeDone,
                        1 => Strings.PathAlternativeOneStep,
                        _ => string.Format(CultureInfo.CurrentCulture, Strings.PathAlternativeStepsFormat, row.RemainingCount),
                    };
                    rowTooltips[i] = row.RemainingCount == 0
                        ? Strings.PathAlternativeTooltipDone
                        : string.Format(CultureInfo.CurrentCulture, Strings.PathAlternativeTooltipFormat, row.RemainingCount);
                    break;
                case PathRowKind.MoreAlternatives:
                    rowLabels[i] = string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, row.Count);
                    rowTooltips[i] = OverflowNames(row, catalog, spoilers);
                    break;
            }
        }

        LoadUnlocks(session, bundle, quest, maxUnlocks);

        var statesKnown = session.ViewedSnapshot is not null && states.Count > 0;
        captionVeiled = !statesKnown;
        caption = !statesKnown ? Strings.PathNotCheckedCaption
            : path.Count > 1 ? null
            : unlocks.Count == 0 ? Strings.PathAloneCaption
            : Strings.PathSingleCaption;
        layoutDirty = true;
    }

    /// <summary>The names an "and N more" alternatives line stands for, one per line, through the spoiler shield.</summary>
    private static string OverflowNames(PathRow row, QuestCatalog catalog, Core.Query.SpoilerMask spoilers)
    {
        if (row.OverflowRowIds.Count == 0)
        {
            return string.Empty;
        }

        var names = new StringBuilder();
        foreach (var id in row.OverflowRowIds)
        {
            if (names.Length > 0)
            {
                names.Append('\n');
            }

            names.Append(spoilers.DisplayName(catalog, id, id.ToString(CultureInfo.InvariantCulture)));
        }

        return names.ToString();
    }

    /// <summary>Scrolls the chart so the target sits at 60 %; with <paramref name="pulse"/> its ring pulses (the "Show path" reveal).</summary>
    public void RequestScrollToTarget(bool pulse)
    {
        scrollFrames = 2;
        if (pulse)
        {
            Motion.Trigger(PulseKeyBase ^ targetRowId);
        }
    }

    private static string Detail(CatalogBundle bundle, QuestRecord quest)
    {
        var expansion = bundle.Names.Expansion(quest.Expansion);
        return string.Format(CultureInfo.CurrentCulture, Strings.PathStepDetailFormat, quest.DisplayLevel, expansion.Length > 0 ? expansion : Strings.ExpansionShort(quest.Expansion));
    }

    /// <summary>Quests that list the target among their previous quests, in catalog order, the first few named.</summary>
    private void LoadUnlocks(SessionState session, CatalogBundle bundle, QuestRecord quest, int maxUnlocks)
    {
        unlocks.Clear();
        unlocksMore = null;
        unlocksMoreTooltip = null;
        var more = 0;
        StringBuilder? moreNames = null;
        if (session.Index is { } index)
        {
            foreach (var dependentId in index.Dependents(quest.RowId))
            {
                // The index also lists quests that merely lock on this one; only a true prerequisite is an unlock.
                if (bundle.Catalog.GetByRowId(dependentId) is not { } dependent || Array.IndexOf(dependent.PreviousQuests.QuestIds, quest.RowId) < 0)
                {
                    continue;
                }

                var name = session.Spoilers.DisplayName(dependent);
                if (unlocks.Count >= maxUnlocks)
                {
                    if (more < maxUnlocks)
                    {
                        (moreNames ??= new StringBuilder()).AppendLine(name);
                    }

                    more++;
                    continue;
                }

                var state = session.States.TryGetValue(dependentId, out var evaluation) ? evaluation.State : QuestState.Unknown;
                unlocks.Add(new Unlock(dependentId, name, state, Detail(bundle, dependent)));
            }
        }

        unlocksHeader = unlocks.Count == 0
            ? Strings.PathUnlocksHeaderNone
            : string.Format(CultureInfo.CurrentCulture, Strings.PathUnlocksHeaderFormat, unlocks.Count + more);
        if (more > 0)
        {
            unlocksMore = string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, more);
            unlocksMoreTooltip = moreNames?.ToString().TrimEnd();
        }
    }

    // ------------------------------------------------------------------ layout

    private float Px(float logical) => UiMetrics.Px(logical);

    private static float CaptionScale => 0.85f;

    private void EnsureLayout(float width)
    {
        var scale = UiMetrics.Scale;
        var iconScale = UiMetrics.IconScale;
        var font = ImGui.GetFontSize();
        var dense = UiMetrics.Density == RowDensity.Dense;
        if (!layoutDirty && scale == layoutScale && iconScale == layoutIconScale && font == layoutFont && dense == layoutDense && MathF.Abs(width - layoutWidth) < 0.5f)
        {
            return;
        }

        layoutDirty = false;
        layoutScale = scale;
        layoutIconScale = iconScale;
        layoutFont = font;
        layoutDense = dense;
        layoutWidth = width;
        BuildLayout(width);
        BuildStars(width);
    }

    // Dense rows (§4.12): step 20 px, moons r 6 and a target of r 8, no sparkle crosses.
    private bool layoutDense;

    private float RadiusFor(VKind kind) => kind switch
    {
        VKind.Target => UiMetrics.Icon(layoutDense ? 8f : 9f),
        VKind.Step => UiMetrics.Icon(layoutDense ? 6f : 7f),
        VKind.Bead => UiMetrics.Icon(7f),
        VKind.RunStep => UiMetrics.Icon(6f),
        VKind.Unlock => UiMetrics.Icon(6f),
        VKind.Alternative => UiMetrics.Icon(5f),
        VKind.Band or VKind.UnlocksHeader => UiMetrics.Icon(4f),
        _ => 0f,
    };

    private float HeightFor(VKind kind, float line, float captionLine)
    {
        var r = RadiusFor(kind);
        return kind switch
        {
            VKind.Band or VKind.UnlocksHeader => MathF.Max(Px(20f), captionLine + Px(6f)),
            VKind.Step => MathF.Max(Px(layoutDense ? 20f : PathRows.StepHeight), MathF.Max(line + Px(layoutDense ? 3f : 6f), 2.4f * r)),
            VKind.Target => MathF.Max(Px(PathRows.TargetHeight), MathF.Max(line + Px(10f), 2.6f * r)),
            VKind.Bead => MathF.Max(Px(PathRows.BeadHeight), MathF.Max(captionLine + Px(6f), UiMetrics.Icon(22f) + Px(2f))),
            VKind.RunStep => MathF.Max(Px(PathRows.ExpandedStepHeight), MathF.Max(line + Px(4f), 2.4f * r)),
            VKind.Alternative => MathF.Max(Px(PathRows.AlternativeHeight), MathF.Max(captionLine + Px(6f), 2.4f * r)),
            VKind.Unlock => MathF.Max(Px(PathRows.UnlockHeight), MathF.Max(line + Px(4f), 2.4f * r)),
            VKind.MoreAlternatives or VKind.UnlocksMore => MathF.Max(Px(PathRows.MoreHeight), captionLine + Px(4f)),
            VKind.Caption => MathF.Max(Px(16f), captionLine + Px(2f)),
            _ => Px(PathRows.TailDotsHeight),
        };
    }

    private void BuildLayout(float width)
    {
        layout.Clear();
        var line = ImGui.GetTextLineHeight();
        var captionLine = line * CaptionScale;
        threadX = MathF.Round(MathF.Max(Px(18f), UiMetrics.Icon(9f) * 1.5f + Px(2f)));
        labelX = MathF.Max(Px(36f), threadX + UiMetrics.Icon(9f) + Px(8f));
        ghostX = threadX + MathF.Max(Px(22f), UiMetrics.Icon(11f));
        ghostLabelX = ghostX + MathF.Max(Px(12f), UiMetrics.Icon(6f) + Px(6f));

        if (coreToRow.Length != rows.Count)
        {
            coreToRow = new int[rows.Count];
        }

        var y = Px(2f);
        var prevNode = -1;
        var pendingAlternatives = layout.Count;
        targetRow = -1;
        for (var core = 0; core < rows.Count; core++)
        {
            var row = rows[core];
            coreToRow[core] = layout.Count;
            switch (row.Kind)
            {
                case PathRowKind.Band:
                    AddNode(VKind.Band, core, -1, ref y, ref prevNode, line, captionLine, outDone: prevNode >= 0 && layout[prevNode].OutDone);
                    break;
                case PathRowKind.Step when row.IsTarget:
                    targetRow = layout.Count;
                    ResolveJoins(pendingAlternatives, layout.Count);
                    AddNode(VKind.Target, core, row.PathIndex, ref y, ref prevNode, line, captionLine, outDone: false);
                    pendingAlternatives = layout.Count;
                    break;
                case PathRowKind.Step:
                    ResolveJoins(pendingAlternatives, layout.Count);
                    AddNode(VKind.Step, core, row.PathIndex, ref y, ref prevNode, line, captionLine, outDone: row.State == QuestState.Completed);
                    pendingAlternatives = layout.Count;
                    break;
                case PathRowKind.FoldedRun:
                    var open = expandedRuns.Contains(RunKey(row));
                    AddNode(VKind.Bead, core, -1, ref y, ref prevNode, line, captionLine, outDone: true);
                    if (!open)
                    {
                        // Collapsed, the thread stops at the capsule's ends; open, at the ring.
                        var bead = layout[^1];
                        bead.NodeR = UiMetrics.Icon(11f);
                        layout[^1] = bead;
                    }

                    if (open)
                    {
                        for (var i = row.PathIndex; i < row.PathIndex + row.Count; i++)
                        {
                            AddNode(VKind.RunStep, core, i, ref y, ref prevNode, line, captionLine, outDone: true);
                        }
                    }

                    pendingAlternatives = layout.Count;
                    break;
                case PathRowKind.Alternative:
                    AddPlain(VKind.Alternative, core, -1, ref y, line, captionLine);
                    break;
                default:
                    AddPlain(VKind.MoreAlternatives, core, -1, ref y, line, captionLine);
                    break;
            }
        }

        if (caption is not null)
        {
            AddPlain(VKind.Caption, Math.Max(0, rows.Count - 1), -1, ref y, line, captionLine);
        }

        pathEnd = y;

        // The tail: the spine continues from the target (or the last node) to the hollow junction star.
        AddNode(VKind.UnlocksHeader, -1, -1, ref y, ref prevNode, line, captionLine, outDone: false);
        for (var i = 0; i < unlocks.Count; i++)
        {
            AddPlain(VKind.Unlock, -1, i, ref y, line, captionLine);
        }

        if (unlocksMore is not null)
        {
            AddPlain(VKind.UnlocksMore, -1, -1, ref y, line, captionLine);
        }

        AddPlain(VKind.Dots, -1, -1, ref y, line, captionLine);
        contentHeight = y + Px(2f);
    }

    private float pathEnd;

    private void AddNode(VKind kind, int core, int item, ref float y, ref int prevNode, float line, float captionLine, bool outDone)
    {
        var h = HeightFor(kind, line, captionLine);
        var r = RadiusFor(kind);
        layout.Add(new VRow
        {
            Kind = kind,
            Core = core,
            Item = item,
            Y = y,
            H = h,
            NodeY = MathF.Round(y + (h * 0.5f)),
            NodeR = r,
            Prev = prevNode,
            OutDone = outDone,
            Join = -1,
        });
        prevNode = layout.Count - 1;
        y += h;
    }

    private void AddPlain(VKind kind, int core, int item, ref float y, float line, float captionLine)
    {
        var h = HeightFor(kind, line, captionLine);
        layout.Add(new VRow
        {
            Kind = kind,
            Core = core,
            Item = item,
            Y = y,
            H = h,
            NodeY = MathF.Round(y + (h * 0.5f)),
            NodeR = 0f,
            Prev = -1,
            Join = -1,
        });
        y += h;
    }

    /// <summary>Points the alternatives listed since <paramref name="from"/> at the join row about to be added.</summary>
    private void ResolveJoins(int from, int join)
    {
        for (var i = from; i < layout.Count; i++)
        {
            if (layout[i].Kind == VKind.Alternative)
            {
                var row = layout[i];
                row.Join = join;
                layout[i] = row;
            }
        }
    }

    /// <summary>Maps each band's unit stars into its pixel span, leaving out any that would sit on a node, the thread or a name.</summary>
    private void BuildStars(float width)
    {
        stars.Clear();
        bandSpans.Clear();
        if (rows.Count == 0)
        {
            return;
        }

        var inset = Px(2f);
        var fontSize = ImGui.GetFontSize();
        foreach (var band in bands)
        {
            var first = coreToRow[band.FirstRow];
            var end = band.FirstRow + band.RowCount;
            var y0 = layout[first].Y;
            var y1 = end < rows.Count ? layout[coreToRow[end]].Y : pathEnd;
            var lastRow = end < rows.Count ? coreToRow[end] : FirstTailRow();
            bandSpans.Add((y0, y1));
            foreach (var star in band.Stars)
            {
                var x = inset + (star.U * (width - (2f * inset)));
                var y = y0 + (star.V * (y1 - y0));
                if (MathF.Abs(x - threadX) < Px(6f) || Excluded(x, y, first, lastRow, fontSize))
                {
                    continue;
                }

                stars.Add(new StarPx(MathF.Round(x), MathF.Round(y), star.Magnitude));
            }
        }
    }

    private int FirstTailRow()
    {
        for (var i = 0; i < layout.Count; i++)
        {
            if (layout[i].Core < 0)
            {
                return i;
            }
        }

        return layout.Count;
    }

    private bool Excluded(float x, float y, int first, int end, float fontSize)
    {
        var near = Px(12f);
        var labelBand = Px(6f) + (fontSize * 0.5f);
        for (var i = first; i < end; i++)
        {
            var row = layout[i];
            var nodeX = row.Kind == VKind.Alternative ? ghostX : threadX;
            if (row.Kind is not (VKind.Caption or VKind.MoreAlternatives) && Vector2.DistanceSquared(new Vector2(x, y), new Vector2(nodeX, row.NodeY)) < near * near)
            {
                return true;
            }

            if (MathF.Abs(y - row.NodeY) > labelBand)
            {
                continue;
            }

            // Names (full size) keep a clear box; captions may have stars behind them (§4.3).
            var name = row.Kind is VKind.Step or VKind.Target or VKind.RunStep ? stepNames[row.Item] : null;
            if (name is not null && x >= labelX - Px(2f) && x <= labelX + ImGui.CalcTextSize(name).X + Px(4f))
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>
    /// Draws the chart at the cursor, <paramref name="width"/> wide and at most <paramref name="maxHeight"/> tall (at
    /// least 160 px, or the whole chart when shorter), with the minimap in the <paramref name="gutter"/> to its right
    /// while it scrolls.
    /// </summary>
    public void Draw(float width, float maxHeight, float gutter)
    {
        if (rows.Count == 0 || width <= 0f)
        {
            return;
        }

        // Heights do not depend on the width; the star field is laid out clear of the scrollbar's lane.
        EnsureLayout(MathF.Max(1f, width - ScrollbarSize));
        var view = MathF.Min(contentHeight, MathF.Max(Px(160f), maxHeight));
        var scrolls = contentHeight > view + 0.5f;
        var childMin = ImGui.GetCursorScreenPos();
        var scrollY = 0f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, ScrollbarSize);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Theme.WithAlphaVector(Theme.Veil, 0.5f));
        using (var child = ImRaii.Child("##pathChart", new Vector2(width, view), false))
        {
            if (child)
            {
                scrollY = DrawChild(view);
            }
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
        if (scrolls && gutter >= Px(6f))
        {
            DrawMinimap(childMin + new Vector2(width + MathF.Min(Px(3f), gutter * 0.3f), 0f), view, scrollY);
        }
    }

    private float ScrollbarSize => MathF.Max(4f, Px(6f));

    // The rows' width this frame, for the room a name has (L5: names end in an ellipsis instead of being clipped).
    private float drawWidth;

    /// <summary>The room for a step's or the target's name, from its label column to the chart's right edge.</summary>
    private float StepNameRoom => MathF.Max(1f, drawWidth - labelX - Px(4f));

    /// <summary>
    /// The target wears the unmet mark after its name: a quest the character cannot take on the current job, as the
    /// detail pane's "Not yet" callout has it (Blocked, Ready on another job, Locked out).
    /// </summary>
    private static bool TargetWearsUnmet(QuestState state) => state is QuestState.Blocked or QuestState.ReadyOnOtherJob or QuestState.Foreclosed;

    /// <summary>The room the target's bold name is drawn in: the step room less its unmet mark and the bold pass.</summary>
    private float TargetNameRoom(QuestState state) =>
        MathF.Max(1f, StepNameRoom - (TargetWearsUnmet(state) ? UiMetrics.RequirementMarkSize + Px(4f) : 0f) - MathF.Max(1f, Px(0.6f)));

    /// <summary>The room a step's or the target's name has as drawn.</summary>
    private float NameRoom(VRow row) => row.Kind == VKind.Target ? TargetNameRoom(path[row.Item].State) : StepNameRoom;

    /// <summary>The room for an unlock's name, from its label column to the chart's right edge.</summary>
    private float UnlockNameRoom => MathF.Max(1f, drawWidth - ghostLabelX - Px(4f));

    /// <summary>Whether <paramref name="name"/> is cut short in <paramref name="room"/>, so its tooltip names it in full.</summary>
    private static bool IsCut(string name, float room) => ImGui.CalcTextSize(name).X > room + 0.5f;

    /// <summary>A row's tooltip with the full name first, for a name the row cut short: the name, the state, the detail.</summary>
    private static void NameTooltip(string name, string state, string detail)
    {
        using var style = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        ImGui.PushFont(Dalamud.Interface.UiBuilder.DefaultFont);
        UiMetrics.ApplyFontScale();
        ImGui.TextUnformatted(name);
        ImGui.TextDisabled(state);
        if (detail.Length > 0)
        {
            ImGui.TextDisabled(detail);
        }

        ImGui.PopFont();
    }

    private float DrawChild(float view)
    {
        if (scrollFrames > 0 && targetRow >= 0)
        {
            scrollFrames--;
            ImGui.SetScrollY(PathRows.ScrollFor(layout[targetRow].NodeY, contentHeight, view));
        }
        else if (pendingScroll >= 0f)
        {
            ImGui.SetScrollY(pendingScroll);
            pendingScroll = -1f;
        }

        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var scrollY = ImGui.GetScrollY();
        var width = ImGui.GetContentRegionAvail().X;
        drawWidth = width;
        var top = scrollY - Px(40f);
        var bottom = scrollY + view + Px(40f);

        var first = FirstRowAt(top);
        var last = first;
        while (last < layout.Count && layout[last].Y <= bottom)
        {
            last++;
        }

        // Rows below the view whose thread still reaches into it (a join under a run of alternatives).
        var threadEnd = last;
        while (threadEnd < layout.Count && (layout[threadEnd].Prev < 0 || layout[layout[threadEnd].Prev].NodeY <= bottom) && threadEnd - last < 8)
        {
            threadEnd++;
        }

        dl.ChannelsSplit(4);

        // The jump pill first, so its item wins the hover over the rows under it.
        DrawJumpPill(dl, origin, width, view, scrollY);

        dl.ChannelsSetCurrent(0);
        DrawSky(dl, origin, width, top, bottom);
        DrawCulledOwners(dl, origin, first);
        var hovered = -1;
        for (var i = first; i < threadEnd; i++)
        {
            var row = layout[i];
            if (row.Prev >= 0)
            {
                DrawThread(dl, origin, layout[row.Prev], row);
            }

            if (i >= last)
            {
                continue;
            }

            if (RowItem(origin, width, i, row))
            {
                hovered = i;
            }

            DrawRow(dl, origin, width, i, row, hovered == i);
        }

        dl.ChannelsMerge();

        // The chart's full height, so the child scrolls over all of it.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(1f, contentHeight));
        return scrollY;
    }

    /// <summary>
    /// Decorations whose owner row is culled above the view but that reach into it: the bracket of an opened bead
    /// whose steps are in view, and the ghost curves of alternatives whose join is in view (they sit just above it).
    /// </summary>
    private void DrawCulledOwners(ImDrawListPtr dl, Vector2 origin, int first)
    {
        if (first <= 0 || first >= layout.Count)
        {
            return;
        }

        var row = layout[first];
        if (row.Kind == VKind.RunStep && row.Core >= 0 && coreToRow[row.Core] < first)
        {
            var bead = layout[coreToRow[row.Core]];
            dl.ChannelsSetCurrent(2);
            DrawRunBracket(dl, origin, bead, origin + new Vector2(threadX, bead.NodeY));
        }

        for (var i = first - 1; i >= 0 && layout[i].Kind is VKind.Alternative or VKind.MoreAlternatives; i--)
        {
            var alternative = layout[i];
            if (alternative.Kind == VKind.Alternative && alternative.Join >= first)
            {
                DrawJoinCurve(dl, origin, alternative);
            }
        }

        dl.ChannelsSetCurrent(0);
    }

    /// <summary>An alternative's curve from its ghost node into its join's upper rim, on the threads' channel (1).</summary>
    private void DrawJoinCurve(ImDrawListPtr dl, Vector2 origin, VRow row)
    {
        if (row.Join < 0)
        {
            return;
        }

        var ghost = origin + new Vector2(ghostX, row.NodeY);
        var r = UiMetrics.Icon(5f);
        var join = layout[row.Join];
        var end = origin + new Vector2(threadX, join.NodeY - join.NodeR);
        var start = ghost + new Vector2(0f, r);
        var bend = Px(12f);
        dl.ChannelsSetCurrent(1);
        dl.AddBezierCubic(start, start + new Vector2(0f, bend), end - new Vector2(0f, bend), end, Theme.WithAlpha(Theme.Surface.TextTertiary, 0.6f), UiMetrics.Hairline, 12);
    }

    /// <summary>The faint bracket beside an opened bead's steps, from the bead down to its last step.</summary>
    private void DrawRunBracket(ImDrawListPtr dl, Vector2 origin, VRow bead, Vector2 node)
    {
        var lastChild = FindLastChild(bead.Core, rows[bead.Core].Count);
        if (lastChild < 0)
        {
            return;
        }

        var x = origin.X + Px(4f);
        dl.AddLine(new Vector2(x, node.Y - Px(8f)), new Vector2(x, origin.Y + layout[lastChild].NodeY + Px(8f)), Theme.WithAlpha(Theme.Moon, 0.25f), UiMetrics.Hairline);
    }

    private int FirstRowAt(float y)
    {
        var lo = 0;
        var hi = layout.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (layout[mid].Y + layout[mid].H < y)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return Math.Max(0, lo);
    }

    private void DrawSky(ImDrawListPtr dl, Vector2 origin, float width, float top, float bottom)
    {
        var s = Theme.Surface;
        var inset = Px(2f);
        for (var i = 0; i < bandSpans.Count; i++)
        {
            var (y0, y1) = bandSpans[i];
            if (y1 < top || y0 > bottom)
            {
                continue;
            }

            var min = origin + new Vector2(inset, y0);
            var max = origin + new Vector2(width - inset, y1);
            if (i % 2 == 1)
            {
                dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.03f), Px(4f));
            }

            dl.AddRectFilled(min, new Vector2(max.X, min.Y + UiMetrics.Hairline), Theme.WithAlpha(s.Text, 0.05f));
        }

        var faint = Theme.WithAlpha(s.Text, 0.14f);
        var small = Theme.WithAlpha(s.Text, 0.22f);
        var bright = Theme.WithAlpha(s.Text, 0.32f);
        var sparkle = Theme.WithAlpha(s.Text, 0.12f);
        var arm = Px(2.5f);
        foreach (var star in stars)
        {
            if (star.Y < top || star.Y > bottom)
            {
                continue;
            }

            var p = origin + new Vector2(star.X, star.Y);
            switch (star.Magnitude)
            {
                case StarMagnitude.Faint:
                    dl.AddRectFilled(p, p + Vector2.One, faint);
                    break;
                case StarMagnitude.Small:
                    dl.AddCircleFilled(p, MathF.Max(1f, Px(1f)), small, 6);
                    break;
                default:
                    dl.AddCircleFilled(p, MathF.Max(1.5f, Px(1.5f)), bright, 8);
                    if (!layoutDense)
                    {
                        dl.AddLine(p - new Vector2(arm, 0f), p + new Vector2(arm, 0f), sparkle, 1f);
                        dl.AddLine(p - new Vector2(0f, arm), p + new Vector2(0f, arm), sparkle, 1f);
                    }

                    break;
            }
        }
    }

    /// <summary>The thread segment from an upper node's lower rim to a lower node's upper rim: gold where walked, else dashed.</summary>
    private void DrawThread(ImDrawListPtr dl, Vector2 origin, VRow upper, VRow lower)
    {
        var from = origin + new Vector2(threadX, upper.NodeY + upper.NodeR);
        var to = origin + new Vector2(threadX, lower.NodeY - lower.NodeR);
        if (to.Y <= from.Y)
        {
            return;
        }

        dl.ChannelsSetCurrent(1);
        if (lower.Kind == VKind.UnlocksHeader || upper.Kind == VKind.Target)
        {
            Dashed(dl, from, to, Theme.WithAlpha(Theme.Surface.Text, 0.35f));
        }
        else if (upper.OutDone)
        {
            dl.AddLine(from, to, Theme.WithAlpha(Theme.Moon, 0.10f), MathF.Max(3f, Px(5f)));
            dl.AddLine(from, to, Theme.WithAlpha(Theme.Moon, 0.9f), MathF.Max(1.5f, Px(2f)));
        }
        else
        {
            Dashed(dl, from, to, Theme.WithAlpha(Theme.Surface.Text, 0.45f));
        }

        dl.ChannelsSetCurrent(0);
    }

    private void Dashed(ImDrawListPtr dl, Vector2 from, Vector2 to, uint color)
    {
        var dash = MathF.Max(2f, Px(3f));
        var thickness = UiMetrics.Hairline;
        for (var y = from.Y; y < to.Y; y += dash * 2f)
        {
            dl.AddLine(new Vector2(from.X, y), new Vector2(from.X, MathF.Min(y + dash, to.Y)), color, thickness);
        }
    }

    /// <summary>The row's item (for hover, click, tooltip and keyboard focus); true when hovered.</summary>
    private bool RowItem(Vector2 origin, float width, int index, VRow row)
    {
        float x0;
        switch (row.Kind)
        {
            case VKind.Step:
            case VKind.RunStep:
            case VKind.Bead:
                x0 = MathF.Max(0f, threadX - row.NodeR - Px(2f));
                break;
            case VKind.Target when IsCut(stepNames[row.Item], TargetNameRoom(path[row.Item].State)):
                // Only for its tooltip: the target's name, cut short, is named in full on hover.
                x0 = MathF.Max(0f, threadX - row.NodeR - Px(2f));
                break;
            case VKind.Alternative:
            case VKind.Unlock:
            case VKind.UnlocksMore:
            case VKind.MoreAlternatives:
                x0 = ghostX - UiMetrics.Icon(6f) - Px(2f);
                break;
            default:
                return false;
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(x0, row.Y));
        ImGui.PushID(index);
        var clicked = ImGui.InvisibleButton("##row", new Vector2(MathF.Max(1f, width - x0), row.H));
        var hovered = ImGui.IsItemHovered();

        // The ring on the rows' channel, over the hover fill and the threads (a ring on channel 0 hid under them).
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSetCurrent(2);
        Chrome.FocusRing(Px(4f));
        dl.ChannelsSetCurrent(0);
        ImGui.PopID();
        if (hovered)
        {
            ImGui.SetMouseCursor(row.Kind is VKind.UnlocksMore or VKind.MoreAlternatives or VKind.Target ? ImGuiMouseCursor.Arrow : ImGuiMouseCursor.Hand);
        }

        var focused = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        if (hovered || focused)
        {
            RowTooltip(row);
        }

        if (clicked)
        {
            RowClicked(row);
        }

        return hovered;
    }

    private void RowTooltip(VRow row)
    {
        switch (row.Kind)
        {
            case VKind.Step:
            case VKind.RunStep:
            case VKind.Target:
                if (IsCut(stepNames[row.Item], NameRoom(row)))
                {
                    NameTooltip(stepNames[row.Item], Strings.StateTooltip(path[row.Item].State), stepDetails[row.Item]);
                }
                else
                {
                    UiMetrics.Tooltip(Strings.StateTooltip(path[row.Item].State), stepDetails[row.Item]);
                }

                break;
            case VKind.Bead:
                var expanded = expandedRuns.Contains(RunKey(rows[row.Core]));
                UiMetrics.Tooltip(expanded ? Strings.FoldedRunCollapseTooltip : Strings.FoldedRunExpandTooltip);
                break;
            case VKind.Alternative:
                UiMetrics.Tooltip(rowTooltips[row.Core]);
                break;
            case VKind.Unlock:
                var unlock = unlocks[row.Item];
                if (IsCut(unlock.Name, UnlockNameRoom))
                {
                    NameTooltip(unlock.Name, Strings.StateTooltip(unlock.State), unlock.Detail);
                }
                else
                {
                    UiMetrics.Tooltip(Strings.StateTooltip(unlock.State), unlock.Detail);
                }

                break;
            case VKind.UnlocksMore when unlocksMoreTooltip is { Length: > 0 } names:
                UiMetrics.Tooltip(names);
                break;
            case VKind.MoreAlternatives when rowTooltips[row.Core] is { Length: > 0 } others:
                UiMetrics.Tooltip(others);
                break;
        }
    }

    private void RowClicked(VRow row)
    {
        switch (row.Kind)
        {
            case VKind.Step:
            case VKind.RunStep:
                select(path[row.Item].RowId);
                break;
            case VKind.Bead:
                var run = RunKey(rows[row.Core]);
                if (!expandedRuns.Remove(run))
                {
                    expandedRuns.Add(run);
                }

                layoutDirty = true;
                break;
            case VKind.Alternative:
                // Selecting the road not taken re-roots the chart on it: its own path is how that branch is read.
                select(rows[row.Core].RowId);
                break;
            case VKind.Unlock:
                select(unlocks[row.Item].RowId);
                break;
        }
    }

    private void DrawRow(ImDrawListPtr dl, Vector2 origin, float width, int index, VRow row, bool hovered)
    {
        var s = Theme.Surface;
        var node = origin + new Vector2(threadX, row.NodeY);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        var captionSize = fontSize * CaptionScale;
        var textY = origin.Y + row.NodeY - (fontSize * 0.5f);
        var captionY = origin.Y + row.NodeY - (captionSize * 0.5f);

        if (hovered && row.Kind is VKind.Step or VKind.RunStep or VKind.Unlock)
        {
            dl.AddRectFilled(origin + new Vector2(labelX - Px(2f), row.Y + Px(2f)), origin + new Vector2(width - Px(2f), row.Y + row.H - Px(2f)), Theme.U32(s.Hover), Px(4f));
        }

        dl.ChannelsSetCurrent(2);
        switch (row.Kind)
        {
            case VKind.Band:
            {
                FourPointStar(dl, node, UiMetrics.Icon(4f), UiMetrics.Icon(1.6f), Theme.U32(s.TextTertiary), filled: true);
                var label = rowLabels[row.Core];
                dl.AddText(font, captionSize, new Vector2(origin.X + labelX, captionY), Theme.U32(s.TextSecondary), label);
                FadingRule(dl, origin.X + labelX + (ImGui.CalcTextSize(label).X * CaptionScale) + Px(8f), origin.X + width - Px(4f), node.Y);
                break;
            }

            case VKind.Step:
            case VKind.RunStep:
            {
                var step = path[row.Item];
                var r = RadiusFor(row.Kind);
                if (hovered)
                {
                    HoverHalo(dl, node, r);
                }

                MoonGlyph.Draw(dl, node, r, step.State);
                var color = hovered ? s.Text : row.Kind == VKind.RunStep ? s.TextSecondary : NameColor(step.State);
                Chrome.EllipsisTextAt(dl, new Vector2(origin.X + labelX, textY), StepNameRoom, stepNames[row.Item], Theme.U32(color));
                break;
            }

            case VKind.Target:
                DrawTarget(dl, origin, width, row, node, font, fontSize, textY);
                break;

            case VKind.Bead:
                DrawBead(dl, origin, row, node, hovered, font, captionSize, captionY);
                break;

            case VKind.Alternative:
            {
                var ghost = origin + new Vector2(ghostX, row.NodeY);
                var r = UiMetrics.Icon(5f);
                GhostRing(dl, ghost, r, Theme.U32(hovered ? s.Text : s.TextTertiary));
                DrawJoinCurve(dl, origin, row);
                dl.ChannelsSetCurrent(2);

                var x = origin.X + ghostLabelX;
                var dusk = Theme.U32(s.TextTertiary);
                var before = AlternativeBefore.Value;
                dl.AddText(font, captionSize, new Vector2(x, captionY), dusk, before);
                x += ImGui.CalcTextSize(before).X * CaptionScale;
                var name = rowLabels[row.Core];
                dl.AddText(font, captionSize, new Vector2(x, captionY), Theme.U32(hovered ? s.TextSecondary : Theme.VeilText), name);
                x += ImGui.CalcTextSize(name).X * CaptionScale;
                var after = AlternativeAfter.Value;
                if (after.Length > 0)
                {
                    dl.AddText(font, captionSize, new Vector2(x, captionY), dusk, after);
                    x += ImGui.CalcTextSize(after).X * CaptionScale;
                }

                dl.AddText(font, captionSize, new Vector2(x, captionY), dusk, rowSuffixes[row.Core]);
                break;
            }

            case VKind.MoreAlternatives:
                dl.AddText(font, captionSize, new Vector2(origin.X + ghostLabelX, captionY), Theme.U32(hovered ? s.TextSecondary : s.TextTertiary), rowLabels[row.Core]);
                break;

            case VKind.Caption:
                dl.AddText(font, captionSize, new Vector2(origin.X + labelX, captionY), captionVeiled ? Theme.VeilTextU32 : Theme.U32(s.TextTertiary), caption ?? string.Empty);
                break;

            case VKind.UnlocksHeader:
                FourPointStar(dl, node, UiMetrics.Icon(4f), UiMetrics.Icon(1.6f), Theme.U32(s.TextTertiary), filled: false);
                dl.AddText(font, captionSize, new Vector2(origin.X + labelX, captionY), Theme.U32(unlocks.Count == 0 ? s.TextTertiary : s.TextSecondary), unlocksHeader);
                FadingRule(dl, origin.X + labelX + (ImGui.CalcTextSize(unlocksHeader).X * CaptionScale) + Px(8f), origin.X + width - Px(4f), node.Y);
                break;

            case VKind.Unlock:
                DrawUnlock(dl, origin, index, row, hovered, font, fontSize, textY);
                break;

            case VKind.UnlocksMore:
                dl.AddText(font, captionSize, new Vector2(origin.X + ghostLabelX, captionY), Theme.U32(hovered ? s.TextSecondary : s.TextTertiary), unlocksMore ?? string.Empty);
                break;

            case VKind.Dots:
            {
                // The constellation trails off: three fading dots under the spine's end.
                var y = origin.Y + row.Y + Px(1f);
                for (var d = 0; d < 3; d++)
                {
                    var alpha = 0.3f - (0.1f * d);
                    dl.AddRectFilled(new Vector2(origin.X + threadX, y), new Vector2(origin.X + threadX + 1f, y + 1f), Theme.WithAlpha(s.Text, alpha));
                    y += Px(3f);
                }

                break;
            }
        }

        dl.ChannelsSetCurrent(0);
    }

    private void DrawTarget(ImDrawListPtr dl, Vector2 origin, float width, VRow row, Vector2 node, ImFontPtr font, float fontSize, float textY)
    {
        var s = Theme.Surface;
        var r = RadiusFor(VKind.Target);

        // Spotlight row: gold fading left to right, under everything.
        dl.ChannelsSetCurrent(0);
        var inset = Px(2f);
        var left = Theme.WithAlpha(Theme.Moon, 0.14f);
        var right = Theme.WithAlpha(Theme.Moon, 0.02f);
        dl.AddRectFilledMultiColor(origin + new Vector2(inset, row.Y + inset), origin + new Vector2(width - inset, row.Y + row.H - inset), left, right, right, left);
        dl.ChannelsSetCurrent(2);

        // Halo: three soft discs, the orrery ring, the moon; the "Show path" pulse widens the ring twice.
        dl.AddCircleFilled(node, r * 2.2f, Theme.WithAlpha(Theme.Moon, 0.04f), 24);
        dl.AddCircleFilled(node, r * 1.7f, Theme.WithAlpha(Theme.Moon, 0.07f), 24);
        dl.AddCircleFilled(node, r * 1.35f, Theme.WithAlpha(Theme.Moon, 0.10f), 24);
        dl.AddCircle(node, r * 1.5f, Theme.WithAlpha(Theme.Moon, 0.45f), 24, UiMetrics.Hairline);
        var pulse = Motion.Pulse(PulseKeyBase ^ targetRowId, PulseSeconds);
        if (pulse >= 0f)
        {
            var phase = (pulse * 2f) % 1f;
            dl.AddCircle(node, r * (1.5f + (0.9f * phase)), Theme.WithAlpha(Theme.Moon, 0.7f * (1f - phase)), 24, MathF.Max(1f, Px(1.5f)));
        }

        MoonGlyph.Draw(dl, node, r, path[row.Item].State);

        // The name in Silver, two-pass bold, ending in an ellipsis when the chart is too narrow for it (L5); a target
        // the character cannot take yet wears the unmet mark after its name, as in the detail pane and table (L8).
        var pos = new Vector2(origin.X + labelX, textY);
        var ink = Theme.U32(s.Text);
        var name = stepNames[row.Item];
        var state = path[row.Item].State;
        var mark = UiMetrics.RequirementMarkSize;
        var unmet = TargetWearsUnmet(state);
        var bold = MathF.Max(1f, Px(0.6f));
        var room = TargetNameRoom(state);
        var nameWidth = ImGui.CalcTextSize(name).X;
        Chrome.EllipsisTextAt(dl, pos, room, name, ink, nameWidth);
        Chrome.EllipsisTextAt(dl, pos + new Vector2(bold, 0f), room, name, ink, nameWidth);
        if (unmet && room + bold > mark)
        {
            var x = pos.X + MathF.Min(nameWidth, room) + bold + Px(4f) + (mark * 0.5f);
            Marks.Draw(dl, new Vector2(x, textY + (fontSize * 0.5f)), mark, Mark.Unmet);
        }
    }

    private void DrawBead(ImDrawListPtr dl, Vector2 origin, VRow row, Vector2 node, bool hovered, ImFontPtr font, float captionSize, float captionY)
    {
        var s = Theme.Surface;
        var expanded = expandedRuns.Contains(RunKey(rows[row.Core]));
        var label = rowLabels[row.Core];
        if (!expanded)
        {
            // Capsule on the thread with three stacked gold dots: moons, folded.
            var half = new Vector2(UiMetrics.Icon(7f), UiMetrics.Icon(11f));
            dl.AddRectFilled(node - half, node + half, Theme.U32(s.Sunken), half.X);
            dl.AddRect(node - half, node + half, Theme.WithAlpha(Theme.Moon, hovered ? 1f : 0.6f), half.X, ImDrawFlags.None, UiMetrics.Hairline);
            var dot = MathF.Max(1f, UiMetrics.Icon(1.1f));
            var step = UiMetrics.Icon(5f);
            for (var d = -1; d <= 1; d++)
            {
                dl.AddCircleFilled(node + new Vector2(0f, d * step), dot, Theme.MoonU32, 6);
            }
        }
        else
        {
            // Open ring with a down chevron, and a faint bracket spanning the opened steps.
            var r = UiMetrics.Icon(7f);
            dl.AddCircle(node, r, Theme.MoonU32, 16, UiMetrics.Hairline);
            var c = UiMetrics.Icon(2.5f);
            dl.AddLine(node + new Vector2(-c, -c * 0.4f), node + new Vector2(0f, c * 0.6f), Theme.MoonU32, UiMetrics.Hairline);
            dl.AddLine(node + new Vector2(0f, c * 0.6f), node + new Vector2(c, -c * 0.4f), Theme.MoonU32, UiMetrics.Hairline);
            DrawRunBracket(dl, origin, row, node);
        }

        var ink = expanded || hovered ? s.TextSecondary : s.TextTertiary;
        dl.AddText(font, captionSize, new Vector2(origin.X + labelX, captionY), Theme.U32(ink), label);
        if (!expanded)
        {
            // A small chevron after the label says it opens.
            var x = origin.X + labelX + (ImGui.CalcTextSize(label).X * CaptionScale) + Px(6f);
            var c = Px(2.5f);
            var y = node.Y;
            dl.AddLine(new Vector2(x, y - c), new Vector2(x + c, y), Theme.U32(ink), UiMetrics.Hairline);
            dl.AddLine(new Vector2(x + c, y), new Vector2(x, y + c), Theme.U32(ink), UiMetrics.Hairline);
        }
    }

    /// <summary>A folded run's key in <see cref="expandedRuns"/>: its first quest's row id.</summary>
    private uint RunKey(PathRow row) => row.PathIndex >= 0 && row.PathIndex < path.Count ? path[row.PathIndex].RowId : uint.MaxValue;

    private int FindLastChild(int core, int runCount)
    {
        var start = coreToRow[core];
        var end = start + runCount;
        return end < layout.Count && layout[end].Kind == VKind.RunStep && layout[end].Core == core ? end : -1;
    }

    private void DrawUnlock(ImDrawListPtr dl, Vector2 origin, int index, VRow row, bool hovered, ImFontPtr font, float fontSize, float textY)
    {
        var s = Theme.Surface;
        var unlock = unlocks[row.Item];
        var r = UiMetrics.Icon(6f);
        var y = origin.Y + row.NodeY;
        var spineX = origin.X + threadX;

        // The comb: the spine passes down the gutter and a quarter arc branches into each unlock's moon.
        dl.ChannelsSetCurrent(1);
        var spine = Theme.WithAlpha(s.Text, 0.35f);
        var branchTop = Px(11f);
        var isLast = index + 1 >= layout.Count || layout[index + 1].Kind != VKind.Unlock;
        var previous = layout[index - 1];
        var spineFrom = previous.Kind == VKind.UnlocksHeader ? origin.Y + previous.NodeY + previous.NodeR : origin.Y + row.Y;
        var spineTo = isLast ? y + Px(2f) : origin.Y + row.Y + row.H;
        Dashed(dl, new Vector2(spineX, spineFrom), new Vector2(spineX, spineTo), spine);
        dl.PathClear();
        dl.PathLineTo(new Vector2(spineX, y - branchTop));
        dl.PathBezierQuadraticCurveTo(new Vector2(spineX, y), new Vector2(spineX + Px(12f), y), 8);
        dl.PathLineTo(new Vector2(origin.X + ghostX - r, y));
        dl.PathStroke(spine, ImDrawFlags.None, UiMetrics.Hairline);
        dl.ChannelsSetCurrent(2);

        var node = new Vector2(origin.X + ghostX, y);
        if (hovered)
        {
            HoverHalo(dl, node, r);
        }

        MoonGlyph.Draw(dl, node, r, unlock.State);
        Chrome.EllipsisTextAt(dl, new Vector2(origin.X + ghostLabelX, textY), UnlockNameRoom, unlock.Name, Theme.U32(hovered ? s.Text : NameColor(unlock.State)));
    }

    private void DrawJumpPill(ImDrawListPtr dl, Vector2 origin, float width, float view, float scrollY)
    {
        if (targetRow < 0)
        {
            return;
        }

        var target = layout[targetRow];
        var above = target.NodeY + target.NodeR < scrollY;
        var below = target.NodeY - target.NodeR > scrollY + view;
        if (!above && !below)
        {
            return;
        }

        var captionSize = ImGui.GetFontSize() * CaptionScale;
        var textWidth = ImGui.CalcTextSize(Strings.PathJumpToTarget).X * CaptionScale;
        var height = MathF.Max(Px(18f), captionSize + Px(4f));
        var arrow = Px(4f);
        var size = new Vector2(Px(8f) + arrow * 2f + Px(4f) + textWidth + Px(8f), height);
        var y = above ? scrollY + Px(4f) : scrollY + view - height - Px(4f);
        var min = origin + new Vector2((width - size.X) * 0.5f, y);
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##jumpTarget", size))
        {
            RequestScrollToTarget(pulse: true);
        }

        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiMetrics.Tooltip(Strings.PathJumpToTargetTooltip);
        }

        dl.ChannelsSetCurrent(3);
        var max = min + size;
        dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Surface.Window, 0.9f), height * 0.5f);
        dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Moon, hovered ? 0.24f : 0.16f), height * 0.5f);
        var cx = min.X + Px(8f) + arrow;
        var cy = min.Y + (height * 0.5f);
        if (above)
        {
            dl.AddTriangleFilled(new Vector2(cx, cy - arrow * 0.6f), new Vector2(cx + arrow, cy + arrow * 0.6f), new Vector2(cx - arrow, cy + arrow * 0.6f), Theme.MoonU32);
        }
        else
        {
            dl.AddTriangleFilled(new Vector2(cx - arrow, cy - arrow * 0.6f), new Vector2(cx + arrow, cy - arrow * 0.6f), new Vector2(cx, cy + arrow * 0.6f), Theme.MoonU32);
        }

        dl.AddText(ImGui.GetFont(), captionSize, new Vector2(cx + arrow + Px(4f), cy - (captionSize * 0.5f)), Theme.MoonU32, Strings.PathJumpToTarget);

        // The pill's ring on its own channel, after its fill (the button is still the last item).
        Chrome.FocusRing(height * 0.5f);
        dl.ChannelsSetCurrent(0);
    }

    /// <summary>
    /// The thread minimap in the card's right padding while the chart scrolls: the track, the walked part of the chain
    /// by step index, the visible window and a dot for the target; a click scrolls there.
    /// </summary>
    private void DrawMinimap(Vector2 min, float view, float scrollY)
    {
        var dl = ImGui.GetWindowDrawList();
        var width = MathF.Max(3f, Px(4f));
        var max = min + new Vector2(width, view);
        var rounding = width * 0.5f;
        dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Veil, 0.35f), rounding);
        if (walkedFraction > 0f)
        {
            dl.AddRectFilled(min, new Vector2(max.X, min.Y + (view * walkedFraction)), Theme.WithAlpha(Theme.Moon, 0.8f), rounding);
        }

        var windowTop = min.Y + (view * scrollY / contentHeight);
        var windowBottom = min.Y + (view * MathF.Min(1f, (scrollY + view) / contentHeight));
        dl.AddRectFilled(new Vector2(min.X - 1f, windowTop), new Vector2(max.X + 1f, windowBottom), Theme.WithAlpha(Theme.Surface.Text, 0.12f), rounding);
        if (targetRow >= 0)
        {
            var dotY = min.Y + (view * layout[targetRow].NodeY / contentHeight);
            dl.AddCircleFilled(new Vector2(min.X + (width * 0.5f), dotY), MathF.Max(1.5f, Px(2f)), Theme.MoonU32, 8);
        }

        // A click on the track scrolls proportionally (a mouse convenience; the pill serves the keyboard).
        var cursor = ImGui.GetCursorScreenPos();
        var hit = new Vector2(MathF.Max(width, Px(8f)), view);
        ImGui.SetCursorScreenPos(new Vector2(min.X + (width * 0.5f) - (hit.X * 0.5f), min.Y));
        if (ImGui.InvisibleButton("##pathMinimap", hit))
        {
            var fraction = Math.Clamp((ImGui.GetMousePos().Y - min.Y) / view, 0f, 1f);
            pendingScroll = PathRows.ScrollFor(fraction * contentHeight, contentHeight, view, 0.5f);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiMetrics.Tooltip(Strings.PathMinimapTooltip);
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    private float pendingScroll = -1f;

    private static void HoverHalo(ImDrawListPtr dl, Vector2 center, float r)
    {
        dl.AddCircleFilled(center, r * 1.6f, Theme.WithAlpha(Theme.Moon, 0.016f), 16);
        dl.AddCircleFilled(center, r * 1.35f, Theme.WithAlpha(Theme.Moon, 0.028f), 16);
        dl.AddCircleFilled(center, r * 1.15f, Theme.WithAlpha(Theme.Moon, 0.04f), 16);
    }

    /// <summary>A hollow ring of eight 22° dashes: a road not taken, a cousin of the Unknown moon.</summary>
    private static void GhostRing(ImDrawListPtr dl, Vector2 center, float r, uint color)
    {
        var dash = 22f * MathF.PI / 180f;
        var step = MathF.PI / 4f;
        for (var i = 0; i < 8; i++)
        {
            var a = (-MathF.PI * 0.5f) + (i * step) - (dash * 0.5f);
            dl.PathClear();
            dl.PathArcTo(center, r, a, a + dash, 4);
            dl.PathStroke(color, ImDrawFlags.None, 1f);
        }
    }

    /// <summary>
    /// A four-point star: filled as two thin crossed rhombi (convex each; the star itself is not), or stroked through
    /// its eight points.
    /// </summary>
    private static void FourPointStar(ImDrawListPtr dl, Vector2 c, float outer, float inner, uint color, bool filled)
    {
        if (filled)
        {
            // Each rhombus's half-width is chosen so the two cross at the inner radius on the diagonals.
            var a = inner / MathF.Sqrt(2f);
            var w = a / (1f - (a / outer));
            dl.AddQuadFilled(c + new Vector2(0f, -outer), c + new Vector2(w, 0f), c + new Vector2(0f, outer), c + new Vector2(-w, 0f), color);
            dl.AddQuadFilled(c + new Vector2(-outer, 0f), c + new Vector2(0f, -w), c + new Vector2(outer, 0f), c + new Vector2(0f, w), color);
            return;
        }

        dl.PathClear();
        for (var i = 0; i < 8; i++)
        {
            var angle = (-MathF.PI * 0.5f) + (i * MathF.PI / 4f);
            var radius = i % 2 == 0 ? outer : inner;
            dl.PathLineTo(c + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
        }

        dl.PathStroke(color, ImDrawFlags.Closed, 1f);
    }

    /// <summary>A 1 px rule fading from VeilLine at 0.6 to nothing, after a caption.</summary>
    private void FadingRule(ImDrawListPtr dl, float x0, float x1, float y)
    {
        if (x1 <= x0)
        {
            return;
        }

        var line = Theme.Surface.StrongLine;
        var from = Theme.WithAlpha(line, 0.6f);
        var to = Theme.WithAlpha(line, 0f);
        dl.AddRectFilledMultiColor(new Vector2(x0, y), new Vector2(x1, y + UiMetrics.Hairline), from, to, to, from);
    }

    /// <summary>A step's name colour by state (§4.5): done recedes to Mist, the rest Silver, Locked out and Not checked their text tones.</summary>
    private static Vector4 NameColor(QuestState state) => state switch
    {
        QuestState.Completed => Theme.Surface.TextSecondary,
        QuestState.Foreclosed => Theme.EclipseText,
        QuestState.Unknown => Theme.VeilText,
        _ => Theme.Surface.Text,
    };
}
