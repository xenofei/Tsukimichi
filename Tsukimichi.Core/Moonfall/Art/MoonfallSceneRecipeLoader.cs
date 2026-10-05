using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>A recipe read: the recipe, or null with every reason it was refused.</summary>
public sealed record MoonfallSceneRecipeLoad(MoonfallSceneRecipe? Recipe, IReadOnlyList<string> Errors)
{
    public bool Ok => Recipe is not null;
}

/// <summary>
/// Reads and checks scene recipes (format <c>moonfall-scene</c>; docs/design/v9/scene-recipe.md). Every number is
/// range-checked, every colour is <c>#RRGGBB</c>, every count is capped (the motion budget: at most
/// <see cref="MaxParticles"/> particles a board), a game source may only name a loading-screen painting or a map, and a
/// picture only a scene's name, so no recipe can reach outside those folders. A newer version is refused; unknown
/// properties are ignored. Never throws on bad input: a refused recipe leaves its level on the shipped picture or the
/// night sky, as before.
/// </summary>
public static partial class MoonfallSceneRecipeLoader
{
    public const string Format = "moonfall-scene";

    /// <summary>The newest recipe version this build reads.</summary>
    public const int Version = 1;

    /// <summary>Where the shipped recipes are embedded (Tsukimichi.Core/Moonfall/Levels/scenes/&lt;name&gt;.json).</summary>
    public const string ResourcePrefix = "Tsukimichi.Core.Moonfall.Levels.scenes.";

    /// <summary>The motion budget (spec-rich2.md §4, rule 6): dust, fireflies and stars together.</summary>
    public const int MaxParticles = 120;

    public const int MaxLights = 48;
    public const int MaxGroups = 8;
    public const int MaxShapes = 48;
    public const int MaxLeaves = 240;
    public const int MaxRecipeBytes = 64 * 1024;

    /// <summary>The most a shaft may add (F4).</summary>
    public const float MaxShaftK = 0.08f;

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly string[] Styles = ["laurel", "oak", "fir", "fern", "willow"];

    // A game source names a loading-screen painting or a map texture only: lower-case path segments, ending .tex.
    [GeneratedRegex("^ui/(loadingimage|map)/[a-z0-9_./-]+\\.tex\\z", RegexOptions.CultureInvariant)]
    private static partial Regex GamePath();

    /// <summary>Every recipe embedded in Core, by name; refused ones are reported in <paramref name="errors"/>.</summary>
    public static IReadOnlyDictionary<string, MoonfallSceneRecipe> LoadBuiltIn(IList<string>? errors = null)
    {
        var assembly = typeof(MoonfallSceneRecipeLoader).Assembly;
        var recipes = new Dictionary<string, MoonfallSceneRecipe>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Where(static n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var name = resource[ResourcePrefix.Length..^".json".Length];
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var load = Parse(reader.ReadToEnd(), name);
            if (load.Recipe is { } recipe)
            {
                recipes[recipe.Name] = recipe;
            }
            else
            {
                errors?.Add($"{name}.json: {string.Join("; ", load.Errors)}");
            }
        }

        return recipes;
    }

    /// <summary>
    /// Reads a recipe; <paramref name="expectedName"/> (the file's name, without .json) must be its <c>name</c> when given.
    /// </summary>
    public static MoonfallSceneRecipeLoad Parse(string? json, string? expectedName = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(null, ["the recipe is empty"]);
        }

        if (json.Length > MaxRecipeBytes)
        {
            return new(null, [$"the recipe is longer than {MaxRecipeBytes} characters"]);
        }

        try
        {
            using var document = JsonDocument.Parse(json, Options);
            var recipe = new Reader(errors).Recipe(document.RootElement, expectedName);
            return new(errors.Count == 0 ? recipe : null, errors);
        }
        catch (JsonException ex)
        {
            return new(null, ["not JSON: " + ex.Message]);
        }
    }

    /// <summary>
    /// The scene a level is drawn on (spec item 8, "default recipes if none"): the recipe its file names; a level whose
    /// file names a picture with no recipe of that name keeps its picture (null); a level whose file names no scene takes
    /// the recipe that lists its id in <c>levels</c>, else the <c>default</c> recipe, else none (the night sky).
    /// </summary>
    public static MoonfallSceneRecipe? Pick(IReadOnlyDictionary<string, MoonfallSceneRecipe> recipes, MoonfallLevel? level)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        if (level is null)
        {
            return null;
        }

        if (level.Scene is { } scene)
        {
            return recipes.TryGetValue(scene, out var named) ? named : null;
        }

        MoonfallSceneRecipe? fallback = null;
        foreach (var recipe in recipes.Values.OrderBy(static r => r.Name, StringComparer.Ordinal))
        {
            if (recipe.Levels.Contains(level.Id, StringComparer.Ordinal))
            {
                return recipe;
            }

            fallback ??= recipe.Default ? recipe : null;
        }

        return fallback;
    }

    /// <summary>Problems across the shipped recipes: a level listed by two, or more than one default.</summary>
    public static IReadOnlyList<string> CheckSet(IReadOnlyDictionary<string, MoonfallSceneRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var problems = new List<string>();
        var defaults = recipes.Values.Where(static r => r.Default).Select(static r => r.Name).Order(StringComparer.Ordinal).ToList();
        if (defaults.Count > 1)
        {
            problems.Add("more than one default recipe: " + string.Join(", ", defaults));
        }

        foreach (var twice in recipes.Values.SelectMany(static r => r.Levels.Select(l => (Level: l, r.Name))).GroupBy(static x => x.Level).Where(static g => g.Count() > 1))
        {
            problems.Add($"{twice.Key} is listed by {string.Join(" and ", twice.Select(static x => x.Name).Order(StringComparer.Ordinal))}");
        }

        return problems;
    }

    private sealed class Reader(List<string> errors)
    {
        private const int MaxLevels = 64;

        public IReadOnlyList<string> LevelIds(JsonElement root)
        {
            if (!root.TryGetProperty("levels", out var node) || node.ValueKind == JsonValueKind.Null)
            {
                return [];
            }

            if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() > MaxLevels)
            {
                errors.Add($"levels must be a list of up to {MaxLevels} level ids");
                return [];
            }

            var ids = new List<string>();
            foreach (var item in node.EnumerateArray())
            {
                var id = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (!MoonfallLevelLoader.IsLevelId(id))
                {
                    errors.Add("levels: each must be a level id");
                    return [];
                }

                if (!ids.Contains(id, StringComparer.Ordinal))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }


        public MoonfallSceneRecipe? Recipe(JsonElement root, string? expectedName)
        {
            if (root.ValueKind != JsonValueKind.Object || Text(root, "format") != Format)
            {
                errors.Add($"format must be \"{Format}\"");
                return null;
            }

            if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version < 1)
            {
                errors.Add("version is missing or not a version");
                return null;
            }

            if (version > Version)
            {
                errors.Add($"version {version} was written for a newer Moonfall (this one reads {Version})");
                return null;
            }

            var name = Text(root, "name");
            if (!MoonfallLevelLoader.IsSceneName(name))
            {
                errors.Add("name must be lower-case letters, digits and hyphens");
                return null;
            }

            if (expectedName is not null && !string.Equals(expectedName, name, StringComparison.Ordinal))
            {
                errors.Add($"name \"{name}\" does not match the file \"{expectedName}.json\"");
            }

            var source = Source(root);
            string? fallback = null;
            if (root.TryGetProperty("fallback", out var fb) && fb.ValueKind != JsonValueKind.Null)
            {
                fallback = fb.ValueKind == JsonValueKind.String ? fb.GetString() : null;
                if (!MoonfallLevelLoader.IsSceneName(fallback))
                {
                    errors.Add("fallback must name a shipped picture (a scene name)");
                    fallback = null;
                }
            }

            var recipe = new MoonfallSceneRecipe
            {
                Name = name,
                Source = source ?? new MoonfallSceneSource(MoonfallSourceKind.Picture, name, false, 0, 0, false, null),
                Fallback = fallback,
                Grade = Grade(root),
                Vignette = Num(root, "vignette", 0, 0, 0.6f),
                Grain = Num(root, "grain", 0, 0, 0.05f),
                Palette = Palette(root),
                Paint = Light(root, "paint"),
                Light = Light(root, "light"),
                Framing = Framing(root),
                Lights = SmallLights(root),
                Fireflies = Fireflies(root),
                Veil = Num(root, "veil", 0.30f, 0, 0.6f),
                Motion = Motion(root),
                Chrome = Chrome(root),
                Levels = LevelIds(root),
                Default = Bool(root, "default"),
            };
            recipe = recipe with { Moon = Moon(root, [.. recipe.Paint, .. recipe.Light]) };
            if (recipe.Paint.Concat(recipe.Light).OfType<MoonfallMoon>().Count() > 1)
            {
                errors.Add("paint and light hold more than one moon");
            }

            if (recipe.Paint.OfType<MoonfallShafts>().Any(static s => s.Moving))
            {
                errors.Add("paint: a moving shaft belongs in light (the palette would recolour what moves)");
            }

            var particles = recipe.Motion.Dust + recipe.Motion.Stars + (recipe.Fireflies?.Count ?? 0);
            if (particles > MaxParticles)
            {
                errors.Add($"dust, stars and fireflies come to {particles}; the motion budget is {MaxParticles}");
            }

            if (recipe.Motion.Dust > 0 && !recipe.Light.OfType<MoonfallShafts>().Any(static s => s.Moving))
            {
                errors.Add("motion.dust drifts in the moving shafts, and no shaft is moving");
            }

            return recipe;
        }

        private MoonfallSceneSource? Source(JsonElement root)
        {
            if (!root.TryGetProperty("source", out var s) || s.ValueKind != JsonValueKind.Object)
            {
                errors.Add("source must be an object with \"game\" or \"picture\"");
                return null;
            }

            var game = Text(s, "game");
            var picture = Text(s, "picture");
            if ((game is null) == (picture is null))
            {
                errors.Add("source names exactly one of \"game\" (a texture in the install) and \"picture\" (a shipped scene)");
                return null;
            }

            if (game is not null && (!GamePath().IsMatch(game) || game.Contains("..", StringComparison.Ordinal) || game.Contains("//", StringComparison.Ordinal)))
            {
                errors.Add("source.game must be a ui/loadingimage/ or ui/map/ texture path ending .tex (no parent steps)");
                return null;
            }

            if (picture is not null && !MoonfallLevelLoader.IsSceneName(picture))
            {
                errors.Add("source.picture must be a scene name");
                return null;
            }

            var pad = s.TryGetProperty("pad", out var p) ? Floats(p, "source.pad", 2, 0, 1024) : [0, 0];
            var mode = Text(s, "padMode") ?? "edge";
            if (mode is not ("edge" or "reflect"))
            {
                errors.Add("source.padMode must be \"edge\" or \"reflect\"");
            }

            (float, float, float, float)? crop = null;
            if (s.TryGetProperty("crop", out var c))
            {
                var box = Floats(c, "source.crop", 4, -4096, 4096);
                if (box.Length == 4)
                {
                    if (!(box[2] >= 16 && box[3] >= 12) || MathF.Abs((box[2] / box[3]) - (4f / 3f)) > 0.02f)
                    {
                        errors.Add("source.crop's w and h must be at least 16 x 12 and 4:3, the board's shape");
                    }

                    crop = (box[0], box[1], box[2], box[3]);
                }
            }

            return new MoonfallSceneSource(game is not null ? MoonfallSourceKind.Game : MoonfallSourceKind.Picture, game ?? picture!,
                Bool(s, "mirror"), pad.Length == 2 ? (int)pad[0] : 0, pad.Length == 2 ? (int)pad[1] : 0, mode == "reflect", crop);
        }

        private MoonfallNightGrade? Grade(JsonElement root)
        {
            if (!root.TryGetProperty("grade", out var g) || g.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (g.ValueKind != JsonValueKind.Object)
            {
                errors.Add("grade must be an object");
                return null;
            }

            var kind = Text(g, "kind") ?? "night";
            if (kind is not ("night" or "violet" or "none"))
            {
                errors.Add("grade.kind must be \"night\", \"violet\" or \"none\"");
                return null;
            }

            if (kind == "none")
            {
                return null;
            }

            var d = kind == "violet" ? MoonfallNightGrade.Violet : new MoonfallNightGrade();
            return d with
            {
                Exposure = Num(g, "exposure", d.Exposure, 0.1f, 2f),
                Gamma = Num(g, "gamma", d.Gamma, 0.5f, 3f),
                Ceiling = Num(g, "ceiling", d.Ceiling, 0.2f, 0.6f),
                Knee = Num(g, "knee", d.Knee, 0.05f, 0.5f),
                Detail = Num(g, "detail", d.Detail, 0f, 3f),
                ChromaMid = Num(g, "chromaMid", d.ChromaMid, 0f, 2f),
                ChromaHigh = Num(g, "chromaHigh", d.ChromaHigh, 0f, 2f),
                Tint = Colour(g, "tint", d.Tint),
                TintK = Num(g, "tintK", d.TintK, 0f, 1f),
                BaseHue = Colour(g, "baseHue", d.BaseHue),
                SkyDrop = Num(g, "skyDrop", d.SkyDrop, 0f, 0.9f),
                SkyTop = Num(g, "skyTop", d.SkyTop, 0f, 1f),
                SkyBottom = Num(g, "skyBottom", d.SkyBottom, 0f, 1f),
                WarmKeep = Num(g, "warmKeep", d.WarmKeep, 0f, 1f),
                Form = Num(g, "form", d.Form, 0f, 0.5f),
                FormRadius = Num(g, "formRadius", d.FormRadius, 2f, 80f),
                BandK = Num(g, "bandK", d.BandK, 0f, 2f),
            };
        }

        private MoonfallPaletteRecipe? Palette(JsonElement root)
        {
            if (!root.TryGetProperty("palette", out var p) || p.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (p.ValueKind != JsonValueKind.Object)
            {
                errors.Add("palette must be an object");
                return null;
            }

            var bands = Stops(p, "bands", "palette.bands", -200, 800);
            if (bands.Count == 0)
            {
                errors.Add("palette.bands needs at least one [y, \"#RRGGBB\"]");
            }

            var regions = new List<MoonfallRegionRecipe>();
            if (p.TryGetProperty("regions", out var rs))
            {
                if (rs.ValueKind != JsonValueKind.Array || rs.GetArrayLength() > 6)
                {
                    errors.Add("palette.regions must be a list of at most 6");
                }
                else
                {
                    var i = 0;
                    foreach (var r in rs.EnumerateArray())
                    {
                        var at = $"palette.regions[{i++}]";
                        if (r.ValueKind != JsonValueKind.Object)
                        {
                            errors.Add(at + " must be an object");
                            continue;
                        }

                        regions.Add(new MoonfallRegionRecipe(ColourV(r, "hue", at), Num(r, "chroma", 0.08f, 0f, 0.2f), Num(r, "weight", 1f, 0f, 1f), Mask(r, "where", at)));
                    }
                }
            }

            return new MoonfallPaletteRecipe
            {
                Bands = bands,
                ValueHues = Stops(p, "valueHues", "palette.valueHues", 0, 1),
                Mix = Num(p, "mix", 0.5f, 0f, 1f),
                Chroma = Num(p, "chroma", 1f, 0f, 2f),
                Floor = Num(p, "floor", 0.024f, 0f, 0.1f),
                Keep = Num(p, "keep", 0.30f, 0f, 1f),
                KeepHigh = Num(p, "keepHigh", 0.75f, 0f, 1f),
                Regions = regions,
                Where = Mask(p, "where", "palette"),
            };
        }

        private List<MoonfallMaskTerm> Mask(JsonElement owner, string key, string at)
        {
            var terms = new List<MoonfallMaskTerm>();
            if (!owner.TryGetProperty(key, out var list))
            {
                return terms;
            }

            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 6)
            {
                errors.Add($"{at}.{key} must be a list of at most 6 terms");
                return terms;
            }

            var i = 0;
            foreach (var t in list.EnumerateArray())
            {
                var here = $"{at}.{key}[{i++}]";
                if (t.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(here + " must be an object");
                    continue;
                }

                (MoonfallMaskKind Kind, string Name, int Count)? found = null;
                foreach (var (kind, name, count) in new[] { (MoonfallMaskKind.Lum, "lum", 2), (MoonfallMaskKind.Y, "y", 2), (MoonfallMaskKind.X, "x", 2), (MoonfallMaskKind.Disc, "disc", 4), (MoonfallMaskKind.Near, "near", 2) })
                {
                    if (t.TryGetProperty(name, out _))
                    {
                        if (found is not null)
                        {
                            errors.Add(here + " names more than one of lum, y, x, disc, near");
                        }

                        found = (kind, name, count);
                    }
                }

                if (found is not { } f)
                {
                    errors.Add(here + " must name one of lum, y, x, disc, near");
                    continue;
                }

                var args = Floats(t.GetProperty(f.Name), here + "." + f.Name, f.Count, -1000, 2000);
                if (args.Length == f.Count && f.Kind == MoonfallMaskKind.Disc && !(args[2] > 0 && args[3] >= 0))
                {
                    errors.Add(here + ".disc's r must be above 0 and its feather at least 0");
                }

                terms.Add(new MoonfallMaskTerm(f.Kind, args, Num(t, "blur", 0f, 0f, 40f), Bool(t, "invert"), Num(t, "scale", 1f, 0f, 1f)));
            }

            return terms;
        }

        private List<MoonfallLightRecipe> Light(JsonElement root, string key)
        {
            var list = new List<MoonfallLightRecipe>();
            if (!root.TryGetProperty(key, out var items))
            {
                return list;
            }

            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 16)
            {
                errors.Add($"{key} must be a list of at most 16 layers");
                return list;
            }

            var i = 0;
            foreach (var l in items.EnumerateArray())
            {
                var at = $"{key}[{i++}]";
                if (l.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(at + " must be an object");
                    continue;
                }

                switch (Text(l, "kind"))
                {
                    case "shafts":
                        var angles = Floats(Req(l, "angles", at), at + ".angles", -1, -180, 180);
                        var widths = Floats(Req(l, "widths", at), at + ".widths", angles.Length, 1, 200);
                        if (angles.Length is < 1 or > 8)
                        {
                            errors.Add(at + ".angles must hold 1 to 8 angles");
                        }

                        list.Add(new MoonfallShafts(Vec2(l, "origin", at, new(-140, -220)), angles, widths, Num(l, "k", 0.07f, 0f, MaxShaftK), ColourV(l, "colour", at, "#BFD2FF"),
                            Int(l, "seed", 3, 0, 100_000), Num(l, "reach", 900f, 100f, 2000f), Num(l, "near", 150f, 0f, 1000f), Bool(l, "moving")));
                        break;
                    case "glow":
                        list.Add(new MoonfallGlow(Num(l, "x", 0, -800, 1600), Num(l, "y", 0, -600, 1200), Num(l, "r", 100, 1, 2000), ColourV(l, "colour", at), Num(l, "k", 0.05f, 0, 0.5f)));
                        break;
                    case "moonGlow":
                        list.Add(new MoonfallMoonGlow(Num(l, "x", -50, -800, 1600), Num(l, "y", -60, -600, 1200), Num(l, "rCore", 330, 1, 3000), Num(l, "rWide", 900, 1, 4000),
                            Num(l, "kCore", 0.12f, 0, 0.5f), Num(l, "kWide", 0.05f, 0, 0.5f), ColourV(l, "colour", at, "#B9C8F0")));
                        break;
                    case "moon":
                        list.Add(new MoonfallMoon(Num(l, "x", 150, 0, 800), Num(l, "y", 100, 0, 600), Num(l, "r", 28, 4, 120), Int(l, "seed", 3, 0, 100_000)));
                        break;
                    case "aurora":
                        list.Add(new MoonfallAurora(Num(l, "y", 300, 41, 594), Num(l, "k", 0.16f, 0, 0.4f)));
                        break;
                    case "nebula":
                        list.Add(new MoonfallNebula(Num(l, "k", 0.42f, 0, 0.8f), Int(l, "seed", 31, 0, 100_000)));
                        break;
                    case "compassRose":
                        list.Add(new MoonfallCompassRose(Num(l, "x", 138, 75, 725), Num(l, "y", 112, 41, 594), Num(l, "r", 58, 10, 160)));
                        break;
                    case "neatline":
                        list.Add(new MoonfallNeatline(Num(l, "inset", 2, 0.5f, 12)));
                        break;
                    case "route":
                        var points = Points(Req(l, "points", at), at + ".points", 2, 256);
                        list.Add(new MoonfallRoute(points, Int(l, "smooth", 8, 1, 32), ColourV(l, "colour", at, "#D9BE82"), Num(l, "width", 2.2f, 0.5f, 8f),
                            Num(l, "dash", 5f, 1f, 40f), Num(l, "gap", 4.5f, 1f, 40f), Num(l, "alpha", 0.6f, 0f, 1f)));
                        break;
                    default:
                        errors.Add(at + ".kind must be shafts, glow, moonGlow, moon, aurora, nebula, compassRose, neatline or route");
                        break;
                }
            }

            return list;
        }

        private List<MoonfallFramingGroup> Framing(JsonElement root)
        {
            var groups = new List<MoonfallFramingGroup>();
            if (!root.TryGetProperty("framing", out var items))
            {
                return groups;
            }

            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > MaxGroups)
            {
                errors.Add($"framing must be a list of at most {MaxGroups} groups");
                return groups;
            }

            var gi = 0;
            foreach (var g in items.EnumerateArray())
            {
                var at = $"framing[{gi++}]";
                if (g.ValueKind != JsonValueKind.Object || !g.TryGetProperty("shapes", out var shapes) || shapes.ValueKind != JsonValueKind.Array)
                {
                    errors.Add(at + " must be an object with a list of shapes");
                    continue;
                }

                if (shapes.GetArrayLength() > MaxShapes)
                {
                    errors.Add($"{at}.shapes holds more than {MaxShapes}");
                    continue;
                }

                var list = new List<MoonfallShapeRecipe>();
                var si = 0;
                foreach (var s in shapes.EnumerateArray())
                {
                    var here = $"{at}.shapes[{si++}]";
                    if (s.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add(here + " must be an object");
                        continue;
                    }

                    if (Shape(s, here) is { } shape)
                    {
                        list.Add(shape);
                    }
                }

                Vector3? snow = g.TryGetProperty("snow", out var sn) && sn.ValueKind != JsonValueKind.Null ? ColourV(g, "snow", at) : null;
                groups.Add(new MoonfallFramingGroup(ColourV(g, "body", at, "#05060E"), ColourV(g, "inner", at, "#0C1230"), Num(g, "innerK", 0.5f, 0, 1),
                    ColourV(g, "rim", at, "#9EB4FF"), Num(g, "rimK", 0.5f, 0, 1), Num(g, "rimWidth", 1.6f, 0.5f, 6), snow, Int(g, "seed", 1, 0, 100_000), list));
            }

            return groups;
        }

        private MoonfallShapeRecipe? Shape(JsonElement s, string at)
        {
            switch (Text(s, "kind"))
            {
                case "frond":
                    var style = Text(s, "style") ?? "laurel";
                    if (Array.IndexOf(Styles, style) < 0)
                    {
                        errors.Add(at + ".style must be laurel, oak, fir, fern or willow");
                    }

                    return new MoonfallFrond(style, Num(s, "x", 0, -100, 900), Num(s, "y", 0, -100, 700), Num(s, "length", 120, 4, 600), Num(s, "angle", 0, -360, 360),
                        Num(s, "droop", 0.4f, -2, 2), Num(s, "leaf", 16, 1, 60), Int(s, "leaves", 12, 0, MaxLeaves), Int(s, "seed", 1, 0, 100_000), Num(s, "width", 1.8f, 0.3f, 8), Int(s, "twigs", 0, 0, 12));
                case "trunk":
                    return new MoonfallTrunk(Num(s, "x", 0, -100, 900), Num(s, "y0", 0, -100, 700), Num(s, "y1", 600, -100, 700), Num(s, "w0", 12, 1, 80), Num(s, "w1", 18, 1, 80),
                        Num(s, "lean", 0, -60, 60), Int(s, "seed", 2, 0, 100_000));
                case "pines":
                    var trees = new List<Vector4>();
                    if (Req(s, "trees", at) is { ValueKind: JsonValueKind.Array } list && list.GetArrayLength() <= 16)
                    {
                        var k = 0;
                        foreach (var t in list.EnumerateArray())
                        {
                            var v = Floats(t, $"{at}.trees[{k++}]", 4, -100, 900);
                            if (v.Length == 4)
                            {
                                trees.Add(new Vector4(v[0], v[1], v[2], v[3]));
                            }
                        }
                    }
                    else
                    {
                        errors.Add(at + ".trees must be a list of at most 16 [x, baseY, height, width]");
                    }

                    return new MoonfallPines(trees.ToArray(), Int(s, "seed", 4, 0, 100_000));
                case "outcrop":
                    return new MoonfallOutcrop(Points(Req(s, "ridge", at), at + ".ridge", 2, 32), Int(s, "seed", 7, 0, 100_000), Num(s, "rough", 10, 0, 40));
                case "rock":
                    return new MoonfallRock(Num(s, "x", 0, -100, 900), Num(s, "y", 0, -100, 700), Num(s, "r", 15, 2, 60), Int(s, "seed", 23, 0, 100_000));
                case "crystal":
                    return new MoonfallCrystal(Num(s, "x", 0, -100, 900), Num(s, "y", 0, -100, 700), Num(s, "h", 40, 4, 200), Num(s, "w", 6, 1, 40), Num(s, "tilt", 0, -1.5f, 1.5f),
                        ColourV(s, "body", at, "#2A1E6A"), ColourV(s, "lit", at, "#D6C8FF"));
                case "rope":
                    return new MoonfallRope(Vec2(s, "from", at, default), Vec2(s, "to", at, default), Num(s, "sag", 20, -200, 200), Num(s, "width", 1.6f, 0.3f, 2f));
                default:
                    errors.Add(at + ".kind must be frond, trunk, pines, outcrop, rock, crystal or rope");
                    return null;
            }
        }

        private List<MoonfallSmallLight> SmallLights(JsonElement root)
        {
            var lights = new List<MoonfallSmallLight>();
            if (!root.TryGetProperty("lights", out var items))
            {
                return lights;
            }

            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > MaxLights)
            {
                errors.Add($"lights must be a list of at most {MaxLights}");
                return lights;
            }

            var i = 0;
            foreach (var l in items.EnumerateArray())
            {
                var at = $"lights[{i++}]";
                if (l.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(at + " must be an object");
                    continue;
                }

                lights.Add(new MoonfallSmallLight(Num(l, "x", 0, 0, 800), Num(l, "y", 0, 0, 600), Num(l, "size", 1, 0.2f, 4), ColourV(l, "colour", at, "#FFC86E"),
                    Num(l, "core", 1.4f, 0.3f, 8), Num(l, "k", 0.8f, 0, 1), Num(l, "halo", 5, 0, 30), Num(l, "haloK", 0.25f, 0, 1), Bool(l, "flicker")));
            }

            return lights;
        }

        private MoonfallFireflies? Fireflies(JsonElement root)
        {
            if (!root.TryGetProperty("fireflies", out var f) || f.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (f.ValueKind != JsonValueKind.Object)
            {
                errors.Add("fireflies must be an object");
                return null;
            }

            var region = Floats(Req(f, "region", "fireflies"), "fireflies.region", 4, 0, 800);
            return new MoonfallFireflies(Int(f, "count", 12, 0, 40), Int(f, "seed", 5, 0, 100_000),
                region.Length == 4 ? new Vector4(region[0], region[1], region[2], region[3]) : default, ColourV(f, "colour", "fireflies", "#FFD27A"));
        }

        private MoonfallMotionRecipe Motion(JsonElement root)
        {
            if (!root.TryGetProperty("motion", out var m) || m.ValueKind == JsonValueKind.Null)
            {
                return new();
            }

            if (m.ValueKind != JsonValueKind.Object)
            {
                errors.Add("motion must be an object");
                return new();
            }

            var mist = new List<MoonfallMist>();
            if (m.TryGetProperty("mist", out var ms))
            {
                if (ms.ValueKind != JsonValueKind.Array || ms.GetArrayLength() > 3)
                {
                    errors.Add("motion.mist must be a list of at most 3 layers");
                }
                else
                {
                    var i = 0;
                    foreach (var l in ms.EnumerateArray())
                    {
                        var at = $"motion.mist[{i++}]";
                        var band = Floats(Req(l, "y", at), at + ".y", 2, 0, 600);
                        if (band.Length == 2 && !(band[1] > band[0]))
                        {
                            errors.Add(at + ".y must run down the board");
                        }

                        mist.Add(new MoonfallMist(band.Length == 2 ? band[0] : 0, band.Length == 2 ? band[1] : 0, Num(l, "speed", 4, 1, 10),
                            Num(l, "alpha", 0.07f, 0, 0.2f), Num(l, "cell", 120, 16, 400), Int(l, "seed", 11, 0, 100_000), ColourV(l, "colour", at, "#AFC0F0")));
                    }
                }
            }

            var region = m.TryGetProperty("starRegion", out var sr) ? Floats(sr, "motion.starRegion", 4, 0, 800) : [75, 41, 725, 330];
            return new MoonfallMotionRecipe
            {
                Dust = Int(m, "dust", 0, 0, MaxParticles),
                Stars = Int(m, "stars", 0, 0, MaxParticles),
                StarRegion = region.Length == 4 ? new Vector4(region[0], region[1], region[2], region[3]) : new(75, 41, 725, 330),
                Mist = mist,
            };
        }

        private MoonfallChromePalette Chrome(JsonElement root)
        {
            if (!root.TryGetProperty("chrome", out var c) || c.ValueKind == JsonValueKind.Null)
            {
                return MoonfallChromePalette.Medallion;
            }

            var d = MoonfallChromePalette.Medallion;
            return new MoonfallChromePalette(ColourV(c, "sky", "chrome", Hex(d.Sky)), ColourV(c, "deep", "chrome", Hex(d.Deep)),
                ColourV(c, "jewel1", "chrome", Hex(d.Jewel1)), ColourV(c, "jewel2", "chrome", Hex(d.Jewel2)));
        }

        private MoonfallMoon? Moon(JsonElement root, IReadOnlyList<MoonfallLightRecipe> light)
        {
            if (root.TryGetProperty("feverMoon", out var f) && f.ValueKind != JsonValueKind.Null)
            {
                var v = Floats(f, "feverMoon", 3, 0, 800);
                return v.Length == 3 ? new MoonfallMoon(v[0], v[1], v[2], 0) : null;
            }

            return light.OfType<MoonfallMoon>().FirstOrDefault();
        }

        // ---- Values ----

        private JsonElement Req(JsonElement node, string key, string at)
        {
            if (node.TryGetProperty(key, out var v))
            {
                return v;
            }

            errors.Add($"{at}.{key} is missing");
            return default;
        }

        private float Num(JsonElement node, string key, float fallback, float min, float max)
        {
            if (!node.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var d) || !double.IsFinite(d) || d < min - 1e-6 || d > max + 1e-6)
            {
                errors.Add($"{key} must be a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}");
                return fallback;
            }

            return (float)d;
        }

        private int Int(JsonElement node, string key, int fallback, int min, int max)
        {
            if (!node.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var i) || i < min || i > max)
            {
                errors.Add($"{key} must be a whole number from {min} to {max}");
                return fallback;
            }

            return i;
        }

        private bool Bool(JsonElement node, string key)
        {
            if (!node.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{key} must be true or false");
                return false;
            }

            return v.GetBoolean();
        }

        private static string? Text(JsonElement node, string key) =>
            node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private string Colour(JsonElement node, string key, string fallback)
        {
            if (!node.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            var text = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            if (!MoonfallColor.TryHex(text, out _))
            {
                errors.Add($"{key} must be a #RRGGBB colour");
                return fallback;
            }

            return text!;
        }

        private Vector3 ColourV(JsonElement node, string key, string at, string? fallback = null)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                if (fallback is null)
                {
                    errors.Add($"{at}.{key} is missing");
                    return Vector3.One;
                }

                return MoonfallColor.Hex(fallback);
            }

            if (!MoonfallColor.TryHex(v.ValueKind == JsonValueKind.String ? v.GetString() : null, out var c))
            {
                errors.Add($"{at}.{key} must be a #RRGGBB colour");
                return Vector3.One;
            }

            return c;
        }

        private float[] Floats(JsonElement node, string at, int count, float min, float max)
        {
            if (node.ValueKind != JsonValueKind.Array || (count >= 0 && node.GetArrayLength() != count) || node.GetArrayLength() > 64)
            {
                errors.Add(count >= 0 ? $"{at} must be a list of {count} numbers" : $"{at} must be a list of numbers");
                return [];
            }

            var values = new float[node.GetArrayLength()];
            var i = 0;
            foreach (var item in node.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var d) || !double.IsFinite(d) || d < min - 1e-6 || d > max + 1e-6)
                {
                    errors.Add($"{at}[{i}] must be a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}");
                    return [];
                }

                values[i++] = (float)d;
            }

            return values;
        }

        private Vector2 Vec2(JsonElement node, string key, string at, Vector2 fallback)
        {
            if (!node.TryGetProperty(key, out var v))
            {
                return fallback;
            }

            var f = Floats(v, $"{at}.{key}", 2, -2000, 2000);
            return f.Length == 2 ? new Vector2(f[0], f[1]) : fallback;
        }

        private Vector2[] Points(JsonElement node, string at, int min, int max)
        {
            if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() < min || node.GetArrayLength() > max)
            {
                errors.Add($"{at} must be a list of {min} to {max} [x, y] points");
                return [];
            }

            var points = new List<Vector2>();
            var i = 0;
            foreach (var p in node.EnumerateArray())
            {
                var f = Floats(p, $"{at}[{i++}]", 2, -200, 1000);
                if (f.Length == 2)
                {
                    points.Add(new Vector2(f[0], f[1]));
                }
            }

            return points.ToArray();
        }

        private List<(float, Vector3)> Stops(JsonElement node, string key, string at, float min, float max)
        {
            var stops = new List<(float, Vector3)>();
            if (!node.TryGetProperty(key, out var list))
            {
                return stops;
            }

            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 8)
            {
                errors.Add($"{at} must be a list of at most 8 [position, \"#RRGGBB\"]");
                return stops;
            }

            var i = 0;
            var last = float.NegativeInfinity;
            foreach (var s in list.EnumerateArray())
            {
                var here = $"{at}[{i++}]";
                if (s.ValueKind != JsonValueKind.Array || s.GetArrayLength() != 2 || s[0].ValueKind != JsonValueKind.Number || !s[0].TryGetDouble(out var p)
                    || !double.IsFinite(p) || p < min - 1e-6 || p > max + 1e-6 || !MoonfallColor.TryHex(s[1].ValueKind == JsonValueKind.String ? s[1].GetString() : null, out var c))
                {
                    errors.Add($"{here} must be [position {min}..{max}, \"#RRGGBB\"]");
                    continue;
                }

                if (p < last)
                {
                    errors.Add($"{here}: positions must not go back");
                }

                last = (float)p;
                stops.Add(((float)p, c));
            }

            return stops;
        }

        private static string Hex(Vector3 c) => "#" + MoonfallImage.ToByte(c.X).ToString("X2", CultureInfo.InvariantCulture)
            + MoonfallImage.ToByte(c.Y).ToString("X2", CultureInfo.InvariantCulture) + MoonfallImage.ToByte(c.Z).ToString("X2", CultureInfo.InvariantCulture);
    }
}
