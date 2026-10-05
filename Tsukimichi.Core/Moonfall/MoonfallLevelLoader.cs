using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Moonfall;

/// <summary>A level file read: the level, or null with every reason it was refused.</summary>
public sealed record MoonfallLevelLoad(MoonfallLevel? Level, IReadOnlyList<string> Errors)
{
    public bool Ok => Level is not null;
}

/// <summary>
/// Reads and checks a Moonfall level file (plan v9 G1, G6). The format, version 1:
/// <code>
/// {
///   "format": "moonfall-level",
///   "version": 1,
///   "id": "base-01",                     // lower-case letters, digits and hyphens
///   "name": "First Light",               // what the player sees, up to 40 characters
///   "playfield": { "width": 800, "height": 600 },
///   "pegs": [
///     { "x": 400, "y": 300 },            // a round peg; "r" (default 10, 6–20), "canBeOrange" (default true)
///     { "x": 460, "y": 300, "move": { "kind": "orbit", "x": 400, "y": 300, "period": 6, "clockwise": true } },
///     { "x": 200, "y": 400, "move": { "kind": "slide", "x": 300, "y": 400, "period": 4 } }
///   ],
///   "bricks": [
///     { "kind": "line", "x1": 200, "y1": 250, "x2": 230, "y2": 250 },          // "thickness" default 20 (8–30)
///     { "kind": "arc", "x": 400, "y": 300, "r": 120, "start": 200, "sweep": 30 } // degrees: 0 along +x, turning towards +y
///   ]
/// }
/// </code>
/// Everything in the original's 800×600 units (the only playfield version 1 has). Comments and trailing commas are
/// allowed; unknown properties are ignored, so a newer editor's extras do not break an older build, but a newer
/// <c>version</c> is refused. The checks: every number finite; every peg and brick, along its whole motion, inside the
/// walls, below the launcher's swing and above the bucket; round pegs and bricks not overlapping each other (bricks may
/// touch bricks); enough pegs that may be orange for the 25 the level needs, and some to spare for the blue, green and
/// purple ones.
/// </summary>
public static partial class MoonfallLevelLoader
{
    public const string Format = "moonfall-level";

    /// <summary>Most pegs and bricks a level may hold.</summary>
    public const int MaxPegs = 400;

    /// <summary>Fewest pegs and bricks: the 25 oranges, 2 greens and a purple, and one blue at least.</summary>
    public const int MinPegs = MoonfallRules.OrangeCount + MoonfallRules.GreenCount + 2;

    /// <summary>How high a peg may stand: clear of the ball's spawn point at every aim (the barrel plus a ball).</summary>
    public const double LauncherClearance = MoonfallRules.BarrelLength + (2 * MoonfallRules.BallRadius);

    /// <summary>How low a peg may reach: a ball's diameter and a pixel above the bucket's rim, so a ball can always pass over the bucket.</summary>
    public const double LowestEdge = MoonfallRules.BucketTop - (2 * MoonfallRules.BallRadius) - 1;

    /// <summary>How far two pieces may overlap before it is an error (rounding in hand-placed coordinates).</summary>
    private const double OverlapTolerance = 0.5;

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>Reads <paramref name="json"/>; never throws on bad input.</summary>
    public static MoonfallLevelLoad Parse(string? json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("the file is empty");
            return new MoonfallLevelLoad(null, errors);
        }

        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var level = Read(document.RootElement, errors);
            return new MoonfallLevelLoad(errors.Count == 0 ? level : null, errors);
        }
        catch (JsonException ex)
        {
            errors.Add("not JSON: " + ex.Message);
            return new MoonfallLevelLoad(null, errors);
        }
    }

    private static MoonfallLevel? Read(JsonElement root, List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("the file is not a JSON object");
            return null;
        }

        if (Text(root, "format") != Format)
        {
            errors.Add($"format must be \"{Format}\"");
            return null;
        }

        if (!root.TryGetProperty("version", out var versionNode) || versionNode.ValueKind != JsonValueKind.Number || !versionNode.TryGetInt32(out var version))
        {
            errors.Add("version is missing or not a whole number");
            return null;
        }

        if (version > MoonfallRules.LevelFormatVersion)
        {
            errors.Add($"version {version} was written by a newer Moonfall (this one reads {MoonfallRules.LevelFormatVersion})");
            return null;
        }

        if (version < 1)
        {
            errors.Add($"version {version} is not a level version");
            return null;
        }

        var id = Text(root, "id");
        if (id is null || !IdPattern().IsMatch(id))
        {
            errors.Add("id must be lower-case letters, digits and hyphens");
        }

        var name = Text(root, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40)
        {
            errors.Add("name must be 1 to 40 characters");
        }

        if (!root.TryGetProperty("playfield", out var playfield) || playfield.ValueKind != JsonValueKind.Object
            || Number(playfield, "width") != MoonfallRules.Width || Number(playfield, "height") != MoonfallRules.Height)
        {
            errors.Add($"playfield must be {{ \"width\": {MoonfallRules.Width}, \"height\": {MoonfallRules.Height} }}");
        }

        var pegs = new List<MoonfallPeg>();
        var labels = new List<string>();
        if (root.TryGetProperty("pegs", out var pegList))
        {
            if (pegList.ValueKind != JsonValueKind.Array)
            {
                errors.Add("pegs must be a list");
            }
            else
            {
                var i = 0;
                foreach (var node in pegList.EnumerateArray())
                {
                    var label = $"pegs[{i++}]";
                    if (ReadPeg(node, label, errors) is { } peg)
                    {
                        pegs.Add(peg);
                        labels.Add(label);
                    }
                }
            }
        }

        if (root.TryGetProperty("bricks", out var brickList))
        {
            if (brickList.ValueKind != JsonValueKind.Array)
            {
                errors.Add("bricks must be a list");
            }
            else
            {
                var i = 0;
                foreach (var node in brickList.EnumerateArray())
                {
                    var label = $"bricks[{i++}]";
                    if (ReadBrick(node, label, errors) is { } brick)
                    {
                        pegs.Add(brick);
                        labels.Add(label);
                    }
                }
            }
        }

        if (pegs.Count < MinPegs || pegs.Count > MaxPegs)
        {
            errors.Add($"a level holds {MinPegs} to {MaxPegs} pegs and bricks; this one has {pegs.Count}");
        }

        var level = new MoonfallLevel(id ?? string.Empty, name ?? string.Empty, pegs);
        if (level.OrangeCandidates < MoonfallRules.OrangeCount)
        {
            errors.Add($"{level.OrangeCandidates} pegs may be orange; a level needs {MoonfallRules.OrangeCount}");
        }

        CheckOverlaps(pegs, labels, errors);
        return level;
    }

    private static MoonfallPeg? ReadPeg(JsonElement node, string label, List<string> errors)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label}: not an object");
            return null;
        }

        var before = errors.Count;
        var x = Required(node, "x", label, errors);
        var y = Required(node, "y", label, errors);
        var r = Optional(node, "r", MoonfallRules.PegRadius, label, errors);
        var canBeOrange = Flag(node, "canBeOrange", true, label, errors);
        if (r is < 6 or > 20)
        {
            errors.Add($"{label}: r must be 6 to 20");
        }

        var mover = PegMover.None;
        if (node.TryGetProperty("move", out var move))
        {
            mover = ReadMover(move, label, errors);
        }

        if (errors.Count != before)
        {
            return null;
        }

        var peg = MoonfallPeg.Round(x, y, r, canBeOrange, mover);
        CheckRoundPath(peg, label, errors);
        return errors.Count == before ? peg : null;
    }

    private static PegMover ReadMover(JsonElement move, string label, List<string> errors)
    {
        if (move.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label}.move: not an object");
            return PegMover.None;
        }

        var kind = Text(move, "kind") switch
        {
            "orbit" => MoverKind.Orbit,
            "slide" => MoverKind.Slide,
            _ => MoverKind.None,
        };
        if (kind == MoverKind.None)
        {
            errors.Add($"{label}.move: kind must be \"orbit\" or \"slide\"");
            return PegMover.None;
        }

        var x = Required(move, "x", label + ".move", errors);
        var y = Required(move, "y", label + ".move", errors);
        var period = Required(move, "period", label + ".move", errors);
        var clockwise = Flag(move, "clockwise", true, label + ".move", errors);
        if (period is < 1 or > 60)
        {
            errors.Add($"{label}.move: period must be 1 to 60 seconds");
        }

        return new PegMover(kind, x, y, period, clockwise);
    }

    private static MoonfallPeg? ReadBrick(JsonElement node, string label, List<string> errors)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label}: not an object");
            return null;
        }

        var before = errors.Count;
        var thickness = Optional(node, "thickness", MoonfallRules.BrickThickness, label, errors);
        var canBeOrange = Flag(node, "canBeOrange", true, label, errors);
        if (thickness is < 8 or > 30)
        {
            errors.Add($"{label}: thickness must be 8 to 30");
        }

        MoonfallPeg? brick = null;
        switch (Text(node, "kind"))
        {
            case "line":
            {
                var x1 = Required(node, "x1", label, errors);
                var y1 = Required(node, "y1", label, errors);
                var x2 = Required(node, "x2", label, errors);
                var y2 = Required(node, "y2", label, errors);
                if (errors.Count == before && MoonfallGeometry.Hypot(x2 - x1, y2 - y1) < 4)
                {
                    errors.Add($"{label}: a line brick must be at least 4 long");
                }

                brick = MoonfallPeg.Line(x1, y1, x2, y2, thickness, canBeOrange);
                break;
            }

            case "arc":
            {
                var x = Required(node, "x", label, errors);
                var y = Required(node, "y", label, errors);
                var r = Required(node, "r", label, errors);
                var start = Required(node, "start", label, errors);
                var sweep = Required(node, "sweep", label, errors);
                if (r is < 20 or > 400)
                {
                    errors.Add($"{label}: r must be 20 to 400");
                }

                if (sweep is <= 0 or > 360)
                {
                    errors.Add($"{label}: sweep must be above 0 and at most 360 degrees");
                }

                brick = MoonfallPeg.Arc(x, y, r, start, sweep, thickness, canBeOrange);
                break;
            }

            default:
                errors.Add($"{label}: kind must be \"line\" or \"arc\"");
                return null;
        }

        if (errors.Count != before)
        {
            return null;
        }

        CheckBrickBounds(brick, label, errors);
        return errors.Count == before ? brick : null;
    }

    // ---- Bounds ----

    /// <summary>Whether a circle of <paramref name="radius"/> at (x, y) stays on the board: inside the walls, clear of the launcher, above the bucket.</summary>
    public static bool InBounds(double x, double y, double radius) =>
        x - radius >= MoonfallRules.LeftWall
        && x + radius <= MoonfallRules.RightWall
        && y + radius <= LowestEdge
        && y - radius >= MoonfallRules.Ceiling
        && MoonfallGeometry.Hypot(x - MoonfallRules.LauncherX, y - MoonfallRules.LauncherY) - radius >= LauncherClearance;

    private static void CheckRoundPath(MoonfallPeg peg, string label, List<string> errors)
    {
        // Sampled along the whole motion: 64 points a cycle find a mover's widest swing within a fraction of a pixel.
        var samples = peg.Mover.Kind == MoverKind.None ? 1 : 64;
        var orbitRadius = MoonfallGeometry.Hypot(peg.X - peg.Mover.X, peg.Y - peg.Mover.Y);
        var orbitAngle = Math.Atan2(peg.Y - peg.Mover.Y, peg.X - peg.Mover.X);
        for (var k = 0; k < samples; k++)
        {
            var (x, y) = (peg.X, peg.Y);
            var phase = 2 * Math.PI * k / samples;
            if (peg.Mover.Kind == MoverKind.Orbit)
            {
                (x, y) = (peg.Mover.X + (orbitRadius * Math.Cos(orbitAngle + phase)), peg.Mover.Y + (orbitRadius * Math.Sin(orbitAngle + phase)));
            }
            else if (peg.Mover.Kind == MoverKind.Slide)
            {
                var s = (1 - Math.Cos(phase)) * 0.5;
                (x, y) = (peg.X + ((peg.Mover.X - peg.X) * s), peg.Y + ((peg.Mover.Y - peg.Y) * s));
            }

            if (!InBounds(x, y, peg.Radius))
            {
                errors.Add($"{label}: {(samples == 1 ? "is" : "moves")} off the board (inside the walls, below the launcher, above the bucket) at ({x.ToString("0.#", CultureInfo.InvariantCulture)}, {y.ToString("0.#", CultureInfo.InvariantCulture)})");
                return;
            }
        }
    }

    private static void CheckBrickBounds(MoonfallPeg brick, string label, List<string> errors)
    {
        var half = brick.Thickness * 0.5;
        const int Samples = 32;
        for (var k = 0; k <= Samples; k++)
        {
            double x, y;
            if (brick.Shape == PegShape.Line)
            {
                x = brick.X + ((brick.X2 - brick.X) * k / Samples);
                y = brick.Y + ((brick.Y2 - brick.Y) * k / Samples);
            }
            else
            {
                var angle = MoonfallGeometry.Radians(brick.StartDegrees + (brick.SweepDegrees * k / Samples));
                x = brick.X + (brick.Radius * Math.Cos(angle));
                y = brick.Y + (brick.Radius * Math.Sin(angle));
            }

            if (!InBounds(x, y, half))
            {
                errors.Add($"{label}: is off the board (inside the walls, below the launcher, above the bucket) at ({x.ToString("0.#", CultureInfo.InvariantCulture)}, {y.ToString("0.#", CultureInfo.InvariantCulture)})");
                return;
            }
        }
    }

    // ---- Overlaps ----

    private static void CheckOverlaps(List<MoonfallPeg> pegs, List<string> labels, List<string> errors)
    {
        for (var i = 0; i < pegs.Count; i++)
        {
            var a = pegs[i];
            if (a.Shape != PegShape.Round || a.Mover.Kind != MoverKind.None)
            {
                continue;
            }

            for (var j = 0; j < pegs.Count; j++)
            {
                var b = pegs[j];
                if (j == i || (b.Shape == PegShape.Round && j < i) || b.Mover.Kind != MoverKind.None)
                {
                    continue;
                }

                if (MoonfallGeometry.SurfaceDistance(b, b.X, b.Y, a.X, a.Y) - a.Radius < -OverlapTolerance)
                {
                    errors.Add($"{labels[i]} overlaps {labels[j]}");
                }
            }
        }
    }

    // ---- Reading values ----

    private static string? Text(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static double Required(JsonElement node, string name, string label, List<string> errors)
    {
        if (Number(node, name) is { } value)
        {
            return value;
        }

        errors.Add($"{label}: {name} is missing or not a number");
        return 0;
    }

    private static double Optional(JsonElement node, string name, double fallback, string label, List<string> errors)
    {
        if (!node.TryGetProperty(name, out _))
        {
            return fallback;
        }

        if (Number(node, name) is { } value)
        {
            return value;
        }

        errors.Add($"{label}: {name} is not a number");
        return fallback;
    }

    private static bool Flag(JsonElement node, string name, bool fallback, string label, List<string> errors)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        errors.Add($"{label}: {name} must be true or false");
        return fallback;
    }
}
