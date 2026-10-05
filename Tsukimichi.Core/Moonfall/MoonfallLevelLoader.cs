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
/// Reads and checks a Moonfall level file (plan v9 G1, G6). The format, version 2 (version 1 is the same without
/// <c>scene</c>, and still reads; a <c>scene</c> in a version 1 file is ignored like any unknown property):
/// <code>
/// {
///   "format": "moonfall-level",
///   "version": 2,
///   "id": "base-01",                     // lower-case letters, digits and hyphens
///   "name": "First Light",               // what the player sees, up to 40 characters
///   "scene": "moon-road-night",          // optional: the background picture's name (MoonfallLevel.Scene)
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
/// <c>version</c> is refused. The checks: the peg count first (an oversized file is refused unread); every number
/// finite; every peg and brick, along its whole length and motion, inside the walls, below the launcher's swing and above
/// the bucket; round pegs and bricks not overlapping each other (bricks may touch bricks), and no mover passing through a
/// still piece or moving faster than <see cref="MaxMoverSpeed"/>; enough pegs that may be orange for the 25 the level
/// needs, and some to spare for the blue, green and purple ones. At most <see cref="MaxErrors"/> errors are listed.
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

    /// <summary>Longest background scene name (format version 2).</summary>
    public const int MaxSceneLength = 40;

    /// <summary>Most errors listed for one file; the rest are counted ("and 12 more").</summary>
    public const int MaxErrors = 40;

    /// <summary>
    /// [J] A mover's top speed (px/s): a little over the ball's launch speed. The ball's sub-steps follow the ball's own
    /// speed, so a faster peg could step past it between two checks; at this speed a peg moves 4.2 px a tick against a
    /// contact distance of 16.
    /// </summary>
    public const double MaxMoverSpeed = 420;

    /// <summary>How far apart (px) the bounds and overlap checks sample a brick's length or a mover's path.</summary>
    private const double SampleStep = 2;

    /// <summary>How far two pieces may overlap before it is an error (rounding in hand-placed coordinates).</summary>
    private const double OverlapTolerance = 0.5;

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // \z, not $: $ also matches before a final newline, which would let "night\n" through as a name.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>
    /// Whether <paramref name="scene"/> is a scene's name as a level gives it: up to <see cref="MaxSceneLength"/> lower-case
    /// letters, digits and hyphens, so it names a picture in <c>scenes/</c> and never a path.
    /// </summary>
    public static bool IsSceneName([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? scene) => scene is { Length: > 0 and <= MaxSceneLength } && IdPattern().IsMatch(scene);

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

        // The count first, so an oversized file is refused before any peg is read or compared.
        var count = (root.TryGetProperty("pegs", out var pegCount) && pegCount.ValueKind == JsonValueKind.Array ? pegCount.GetArrayLength() : 0)
            + (root.TryGetProperty("bricks", out var brickCount) && brickCount.ValueKind == JsonValueKind.Array ? brickCount.GetArrayLength() : 0);
        if (count < MinPegs || count > MaxPegs)
        {
            errors.Add($"a level holds {MinPegs} to {MaxPegs} pegs and bricks; this one has {count}");
            return null;
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
                    if (errors.Count >= MaxErrors)
                    {
                        break;
                    }

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
                    if (errors.Count >= MaxErrors)
                    {
                        break;
                    }

                    var label = $"bricks[{i++}]";
                    if (ReadBrick(node, label, errors) is { } brick)
                    {
                        pegs.Add(brick);
                        labels.Add(label);
                    }
                }
            }
        }

        var level = new MoonfallLevel(id ?? string.Empty, name ?? string.Empty, pegs) { Scene = version >= 2 ? ReadScene(root, errors) : null };
        if (level.OrangeCandidates < MoonfallRules.OrangeCount)
        {
            errors.Add($"{level.OrangeCandidates} pegs may be orange; a level needs {MoonfallRules.OrangeCount}");
        }

        if (errors.Count < MaxErrors)
        {
            CheckOverlaps(pegs, labels, errors);
        }

        if (errors.Count > MaxErrors)
        {
            var more = errors.Count - MaxErrors;
            errors.RemoveRange(MaxErrors, more);
            errors.Add($"and {more} more");
        }

        return level;
    }

    /// <summary>
    /// Version 2's optional background <c>scene</c>: a picture's name, so a file name and never a path. Whether the
    /// picture exists is not checked here; a missing one falls back to the night sky when the level is drawn.
    /// </summary>
    private static string? ReadScene(JsonElement root, List<string> errors)
    {
        if (!root.TryGetProperty("scene", out var node) || node.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var scene = node.ValueKind == JsonValueKind.String ? node.GetString() : null;
        if (!IsSceneName(scene))
        {
            errors.Add($"scene must be up to {MaxSceneLength} lower-case letters, digits and hyphens (a picture's name, not a path)");
            return null;
        }

        return scene;
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
        if (mover.Kind != MoverKind.None && TopSpeed(peg) > MaxMoverSpeed)
        {
            errors.Add($"{label}.move: moves faster than {MaxMoverSpeed.ToString("0", CultureInfo.InvariantCulture)} px/s; give it a longer period");
            return null;
        }

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
            return PegMover.None;
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

    /// <summary>A mover's fastest speed (px/s): an orbit's steady 2πr/T, a slide's π·d/T at its middle.</summary>
    private static double TopSpeed(MoonfallPeg peg)
    {
        var d = MoonfallGeometry.Hypot(peg.X - peg.Mover.X, peg.Y - peg.Mover.Y);
        return peg.Mover.Kind == MoverKind.Orbit ? 2 * Math.PI * d / peg.Mover.PeriodSeconds : Math.PI * d / peg.Mover.PeriodSeconds;
    }

    /// <summary>How many points sample a mover's path: one every <see cref="SampleStep"/> px of it, and at least 64.</summary>
    private static int PathSamples(MoonfallPeg peg)
    {
        if (peg.Mover.Kind == MoverKind.None)
        {
            return 1;
        }

        var d = MoonfallGeometry.Hypot(peg.X - peg.Mover.X, peg.Y - peg.Mover.Y);
        var length = peg.Mover.Kind == MoverKind.Orbit ? 2 * Math.PI * d : 2 * d;
        return Math.Clamp((int)Math.Ceiling(length / SampleStep), 64, 4096);
    }

    /// <summary>Where a round peg is at <paramref name="k"/> of <paramref name="samples"/> points along its motion's cycle.</summary>
    private static (double X, double Y) PathPoint(MoonfallPeg peg, int k, int samples)
    {
        var phase = 2 * Math.PI * k / samples;
        switch (peg.Mover.Kind)
        {
            case MoverKind.Orbit:
            {
                var radius = MoonfallGeometry.Hypot(peg.X - peg.Mover.X, peg.Y - peg.Mover.Y);
                var start = Math.Atan2(peg.Y - peg.Mover.Y, peg.X - peg.Mover.X);
                return (peg.Mover.X + (radius * Math.Cos(start + phase)), peg.Mover.Y + (radius * Math.Sin(start + phase)));
            }

            case MoverKind.Slide:
            {
                var s = (1 - Math.Cos(phase)) * 0.5;
                return (peg.X + ((peg.Mover.X - peg.X) * s), peg.Y + ((peg.Mover.Y - peg.Y) * s));
            }

            default:
                return (peg.X, peg.Y);
        }
    }

    private static void CheckRoundPath(MoonfallPeg peg, string label, List<string> errors)
    {
        var samples = PathSamples(peg);
        for (var k = 0; k < samples; k++)
        {
            var (x, y) = PathPoint(peg, k, samples);
            if (!InBounds(x, y, peg.Radius))
            {
                errors.Add($"{label}: {(samples == 1 ? "is" : "moves")} off the board (inside the walls, below the launcher, above the bucket) at ({x.ToString("0.#", CultureInfo.InvariantCulture)}, {y.ToString("0.#", CultureInfo.InvariantCulture)})");
                return;
            }
        }
    }

    /// <summary>A brick's middle line, sampled every <see cref="SampleStep"/> px of its length (a long arc cannot bulge past a check between samples).</summary>
    private static int BrickSamples(MoonfallPeg brick)
    {
        var length = brick.Shape == PegShape.Line
            ? MoonfallGeometry.Hypot(brick.X2 - brick.X, brick.Y2 - brick.Y)
            : brick.Radius * MoonfallGeometry.Radians(brick.SweepDegrees);
        return Math.Clamp((int)Math.Ceiling(length / SampleStep), 1, 4096);
    }

    private static void CheckBrickBounds(MoonfallPeg brick, string label, List<string> errors)
    {
        var half = brick.Thickness * 0.5;
        var samples = BrickSamples(brick);
        for (var k = 0; k <= samples; k++)
        {
            double x, y;
            if (brick.Shape == PegShape.Line)
            {
                x = brick.X + ((brick.X2 - brick.X) * k / samples);
                y = brick.Y + ((brick.Y2 - brick.Y) * k / samples);
            }
            else
            {
                var angle = MoonfallGeometry.Radians(brick.StartDegrees + (brick.SweepDegrees * k / samples));
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
        for (var i = 0; i < pegs.Count && errors.Count < MaxErrors; i++)
        {
            var a = pegs[i];
            if (a.Shape != PegShape.Round)
            {
                continue;
            }

            var moving = a.Mover.Kind != MoverKind.None;
            var samples = PathSamples(a);
            for (var j = 0; j < pegs.Count; j++)
            {
                var b = pegs[j];
                // Still round pegs are checked once a pair; movers against every still piece; movers never against
                // each other (a ring of them turns together).
                if (j == i || b.Mover.Kind != MoverKind.None || (!moving && b.Shape == PegShape.Round && j < i))
                {
                    continue;
                }

                for (var k = 0; k < samples; k++)
                {
                    var (x, y) = PathPoint(a, k, samples);
                    if (MoonfallGeometry.SurfaceDistance(b, b.X, b.Y, x, y) - a.Radius < -OverlapTolerance)
                    {
                        errors.Add(moving ? $"{labels[i]} moves into {labels[j]}" : $"{labels[i]} overlaps {labels[j]}");
                        break;
                    }
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
