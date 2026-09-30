using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// A fixed, non-resizable table laid out by <see cref="TableGeometry.PlanColumns"/> (feature plan v4 L6): columns hide
/// by priority rather than run off the right edge, the fixed ones keep their width, and the stretch ones share the
/// rest in the planned proportions, so a stretch column's minimum (a status' state word) holds as the table narrows.
/// Used by Flight's quest table, the Characters dashboard's Abandoned table and the Moonlit verdicts in Settings.
/// <para>
/// Per frame: <see cref="Plan"/>, then <see cref="Begin"/> (the visible set is pushed as an id scope, so each set is
/// its own ImGui table with no stale hidden column), <see cref="Setup"/> for every column in display order, and
/// <see cref="Next"/> for every cell, which skips a hidden column. Last frame's visibility feeds the plan's hysteresis.
/// Nothing allocates after construction.
/// </para>
/// </summary>
public sealed class ColumnFit
{
    private readonly bool[] shown;
    private readonly bool[] was;
    private readonly bool[] stretch;
    private readonly float[] widths;
    private bool planned;
    private float padding;

    public ColumnFit(int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        shown = new bool[columns];
        was = new bool[columns];
        stretch = new bool[columns];
        widths = new float[columns];
    }

    /// <summary>Widens a fixed column's spec to its header label, which ImGui would widen the column to anyway.</summary>
    public static void FitHeader(Span<ColumnSpec> specs, int column, string header)
    {
        var spec = specs[column];
        var width = MathF.Max(spec.Min, ImGui.CalcTextSize(header).X);
        specs[column] = spec with { Min = width, Ideal = MathF.Max(width, spec.Ideal) };
    }

    /// <summary>How many columns show this frame.</summary>
    public int Count { get; private set; }

    /// <summary>Whether <paramref name="column"/> shows this frame.</summary>
    public bool Shown(int column) => shown[column];

    /// <summary>
    /// Plans a table <paramref name="available"/> pixels wide. The specs' widths are content widths; the cell padding
    /// either side is added here.
    /// </summary>
    public void Plan(float available, ReadOnlySpan<ColumnSpec> specs)
    {
        if (specs.Length != shown.Length)
        {
            throw new ArgumentException("One spec per column.", nameof(specs));
        }

        padding = ImGui.GetStyle().CellPadding.X * 2f;
        Span<ColumnSpec> padded = stackalloc ColumnSpec[specs.Length];
        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            padded[i] = spec with { Min = spec.Min + padding, Ideal = spec.Ideal + padding };
            stretch[i] = spec.Stretch;
        }

        TableGeometry.PlanColumns(available, padded, planned ? was : [], shown, widths, UiMetrics.Px(LayoutBudgets.HysteresisLogical));
        planned = true;
        Array.Copy(shown, was, shown.Length);
        Count = 0;
        for (var i = 0; i < shown.Length; i++)
        {
            Count += shown[i] ? 1 : 0;
        }
    }

    /// <summary>Begins the table with the planned columns in an id scope of the visible set; dispose it at the end of the table.</summary>
    public Table Begin(string id, ImGuiTableFlags flags)
    {
        var mask = 0;
        for (var i = 0; i < shown.Length; i++)
        {
            mask |= shown[i] ? 1 << i : 0;
        }

        ImGui.PushID(mask);
        return new Table(ImRaii.Table(id, Math.Max(1, Count), flags));
    }

    /// <summary>The table begun by <see cref="Begin"/>: ends it and pops the id scope on dispose.</summary>
    public ref struct Table
    {
        private ImRaii.TableDisposable table;

        internal Table(ImRaii.TableDisposable table) => this.table = table;

        /// <summary>Whether the table is being drawn (ImGui's <c>BeginTable</c> result).</summary>
        public readonly bool Success => table.Success;

        public void Dispose()
        {
            table.Dispose();
            ImGui.PopID();
        }
    }

    /// <summary>Sets up <paramref name="column"/> when it shows: fixed at its planned width, or stretching in the planned proportion.</summary>
    public void Setup(int column, string label, ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.None)
    {
        if (!shown[column])
        {
            return;
        }

        var width = MathF.Max(1f, widths[column] - padding);
        if (stretch[column])
        {
            ImGui.TableSetupColumn(label, flags | ImGuiTableColumnFlags.WidthStretch, width);
        }
        else
        {
            ImGui.TableSetupColumn(label, flags | ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, width);
        }
    }

    /// <summary>Moves to <paramref name="column"/>'s cell of the current row; false when the column is hidden (or clipped).</summary>
    public bool Next(int column) => shown[column] && ImGui.TableNextColumn();
}
