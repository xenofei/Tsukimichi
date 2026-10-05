namespace Tsukimichi.Core.Moonfall;

/// <summary>A peg's shape: a round peg, or a brick ("a peg of a different shape" [R §2]) straight or curved.</summary>
public enum PegShape : byte
{
    /// <summary>A round peg: a circle of <see cref="MoonfallPeg.Radius"/> at (<see cref="MoonfallPeg.X"/>, <see cref="MoonfallPeg.Y"/>).</summary>
    Round,

    /// <summary>A straight brick: a capsule from (X, Y) to (X2, Y2), <see cref="MoonfallPeg.Thickness"/> across.</summary>
    Line,

    /// <summary>
    /// A curved brick: the arc of radius <see cref="MoonfallPeg.Radius"/> about (X, Y) from <see cref="MoonfallPeg.StartDegrees"/>
    /// through <see cref="MoonfallPeg.SweepDegrees"/> (0° along +x, positive turning towards +y, which is clockwise on
    /// screen), <see cref="MoonfallPeg.Thickness"/> across with rounded ends.
    /// </summary>
    Arc,
}

/// <summary>How a round peg moves (plan v9 G1, level design [R §7]): not at all, round a centre, or to and fro.</summary>
public enum MoverKind : byte
{
    None,

    /// <summary>Round (<see cref="PegMover.X"/>, <see cref="PegMover.Y"/>) at the peg's own distance, once a period.</summary>
    Orbit,

    /// <summary>From the peg's place to (<see cref="PegMover.X"/>, <see cref="PegMover.Y"/>) and back once a period, eased like the bucket.</summary>
    Slide,
}

/// <summary>A round peg's motion; <see cref="None"/> for a still one.</summary>
/// <param name="Kind">How it moves.</param>
/// <param name="X">The orbit's centre, or the slide's far end.</param>
/// <param name="Y">The orbit's centre, or the slide's far end.</param>
/// <param name="PeriodSeconds">One full cycle, in game seconds.</param>
/// <param name="Clockwise">An orbit's direction on screen.</param>
public readonly record struct PegMover(MoverKind Kind, double X, double Y, double PeriodSeconds, bool Clockwise)
{
    public static readonly PegMover None = default;
}

/// <summary>One peg or brick as a level places it (<see cref="MoonfallLevelLoader"/>). Its colour is picked when the level starts.</summary>
public sealed record MoonfallPeg
{
    public PegShape Shape { get; init; }

    /// <summary>A round peg's centre, a straight brick's first end, or a curved brick's centre.</summary>
    public double X { get; init; }

    /// <inheritdoc cref="X"/>
    public double Y { get; init; }

    /// <summary>A straight brick's second end.</summary>
    public double X2 { get; init; }

    /// <inheritdoc cref="X2"/>
    public double Y2 { get; init; }

    /// <summary>A round peg's radius, or a curved brick's radius to the middle of its thickness.</summary>
    public double Radius { get; init; }

    /// <summary>A curved brick's first angle, in degrees.</summary>
    public double StartDegrees { get; init; }

    /// <summary>A curved brick's sweep, in degrees, above 0 and at most 360.</summary>
    public double SweepDegrees { get; init; }

    /// <summary>A brick's thickness.</summary>
    public double Thickness { get; init; }

    /// <summary>Whether the level start may make it orange ([R §2] "Can Be Orange", on by default).</summary>
    public bool CanBeOrange { get; init; } = true;

    /// <summary>
    /// Whether the level start may make it green (format version 2, on by default): off keeps a power's peg off a
    /// figure's eye or a constellation's star (plan v9 decision 16). Greens come from the pegs left blue after the
    /// oranges are picked, so a peg may be both.
    /// </summary>
    public bool CanBeGreen { get; init; } = true;

    /// <summary>How it moves; round pegs only.</summary>
    public PegMover Mover { get; init; }

    public static MoonfallPeg Round(double x, double y, double radius = MoonfallRules.PegRadius, bool canBeOrange = true, PegMover mover = default, bool canBeGreen = true) =>
        new() { Shape = PegShape.Round, X = x, Y = y, Radius = radius, CanBeOrange = canBeOrange, Mover = mover, CanBeGreen = canBeGreen };

    public static MoonfallPeg Line(double x1, double y1, double x2, double y2, double thickness = MoonfallRules.BrickThickness, bool canBeOrange = true, bool canBeGreen = true) =>
        new() { Shape = PegShape.Line, X = x1, Y = y1, X2 = x2, Y2 = y2, Thickness = thickness, CanBeOrange = canBeOrange, CanBeGreen = canBeGreen };

    public static MoonfallPeg Arc(double cx, double cy, double radius, double startDegrees, double sweepDegrees, double thickness = MoonfallRules.BrickThickness, bool canBeOrange = true, bool canBeGreen = true) =>
        new() { Shape = PegShape.Arc, X = cx, Y = cy, Radius = radius, StartDegrees = startDegrees, SweepDegrees = sweepDegrees, Thickness = thickness, CanBeOrange = canBeOrange, CanBeGreen = canBeGreen };
}

/// <summary>A level: its pegs and bricks in the order the file lists them (pegs first, then bricks).</summary>
/// <param name="Id">The level's id, lower-case letters, digits and hyphens ("base-01").</param>
/// <param name="Name">The level's name as the player sees it.</param>
/// <param name="Pegs">The pegs and bricks.</param>
public sealed record MoonfallLevel(string Id, string Name, IReadOnlyList<MoonfallPeg> Pegs)
{
    /// <summary>
    /// The level's background picture (format version 2): a name of lower-case letters, digits and hyphens, drawn from
    /// <c>assets/moonfall/scenes/&lt;scene&gt;.png</c> (800 × 600) and <c>&lt;scene&gt;@2x.png</c> (1600 × 1200). Null for
    /// none, and a picture that is missing or fails to load is none too: the board shows the shared night sky.
    /// </summary>
    public string? Scene { get; init; }

    /// <summary>
    /// The fewest greens the level start can always find, whichever oranges it picks: the pegs that may be green less
    /// those the oranges could take (every peg that may be both, up to <see cref="MoonfallRules.OrangeCount"/>).
    /// </summary>
    public int GreenCandidatesAtWorst
    {
        get
        {
            int green = 0, both = 0;
            foreach (var peg in Pegs)
            {
                if (peg.CanBeGreen)
                {
                    green++;
                    both += peg.CanBeOrange ? 1 : 0;
                }
            }

            return green - Math.Min(both, Math.Min(MoonfallRules.OrangeCount, OrangeCandidates));
        }
    }

    /// <summary>How many pegs may be orange.</summary>
    public int OrangeCandidates
    {
        get
        {
            var count = 0;
            foreach (var peg in Pegs)
            {
                if (peg.CanBeOrange)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
