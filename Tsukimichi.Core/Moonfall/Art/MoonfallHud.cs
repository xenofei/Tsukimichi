namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The in-play chrome's layout and type rules (spec-rich2.md §1, chrome2.py), in board units so it scales with the board:
/// the side rails' instruments, the top rail's plates, and the text floors that hold at the 640 × 480 minimum. A label's
/// cap height is at least <see cref="LabelFloor"/> display pixels and a number's at least <see cref="NumberFloor"/>; a
/// label that cannot meet its floor at its design size is left out, not shrunk (SCORE, BALLS, ORANGES and the power's
/// name on the rail, at 640), and a number is drawn at its floor when its design size falls short.
/// </summary>
public static class MoonfallHud
{
    /// <summary>The least cap height of a label, display pixels.</summary>
    public const float LabelFloor = 7f;

    /// <summary>The least cap height of a number, display pixels.</summary>
    public const float NumberFloor = 8f;

    /// <summary>The left rail's centre line (its interior between the outer frame's band and the wall's), units.</summary>
    public const float LeftRail = 33f;

    /// <summary>The right rail's centre line.</summary>
    public const float RightRail = 767f;

    /// <summary>The rails' inner width.</summary>
    public const float RailWidth = 42f;

    /// <summary>The ball tube: x either side of the left rail's centre, and its top and foot, units.</summary>
    public const float TubeHalf = 13f, TubeTop = 66f, TubeFoot = 330f;

    /// <summary>The balls' count plate, y top and foot.</summary>
    public const float CountTop = 370f, CountFoot = 396f;

    /// <summary>The rails' shelf rule, y.</summary>
    public const float Shelf = 424f;

    /// <summary>The multiplier dial's centre y and face radius.</summary>
    public const float DialY = 96f, DialR = 14f;

    /// <summary>The oranges-left moon and count, y.</summary>
    public const float OrangesY = 148f;

    /// <summary>The companion's medallion centre y and portrait radius.</summary>
    public const float MedallionY = 232f, MedallionR = 14.5f;

    /// <summary>The power's name plate top, y.</summary>
    public const float PowerNameY = 268f;

    /// <summary>The level's name plate (a pill) on the top rail, units: x0, y0, x1, y1.</summary>
    public static readonly (float X0, float Y0, float X1, float Y1) NamePlate = (86f, 8f, 300f, 34f);

    /// <summary>The score plate.</summary>
    public static readonly (float X0, float Y0, float X1, float Y1) ScorePlate = (588f, 8f, 716f, 34f);

    /// <summary>
    /// A label's font size in pixels at <paramref name="pxPerUnit"/>, drawn at <paramref name="designSize"/> units in a
    /// face whose caps are <paramref name="capRatio"/> of its size; 0 when it would fall below the label floor (left out).
    /// </summary>
    public static float LabelSize(float designSize, float pxPerUnit, float capRatio) =>
        designSize * pxPerUnit * capRatio + 1e-4f >= LabelFloor ? designSize * pxPerUnit : 0f;

    /// <summary>A number's font size in pixels: its design size, or the size whose caps meet the number floor if that is larger.</summary>
    public static float NumberSize(float designSize, float pxPerUnit, float capRatio) =>
        MathF.Max(designSize * pxPerUnit, NumberFloor / MathF.Max(capRatio, 0.1f));

    /// <summary>A label that must show (a name, a button) at its design size, raised to the label floor if it falls short.</summary>
    public static float RequiredLabelSize(float designSize, float pxPerUnit, float capRatio) =>
        MathF.Max(designSize * pxPerUnit, LabelFloor / MathF.Max(capRatio, 0.1f));

    /// <summary>Whether the window is wide enough beside the board for the power's card to slide into its margin (spec: 1280 and wider), pixels.</summary>
    public static bool CardInMargin(float marginPx) => marginPx >= 96f;

    /// <summary>The card's width in a margin of <paramref name="marginPx"/> pixels: the margin less 16, at most 180.</summary>
    public static float MarginCardWidth(float marginPx) => MathF.Min(180f, marginPx - 16f);
}
