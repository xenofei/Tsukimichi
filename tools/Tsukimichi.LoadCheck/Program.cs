using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Tsukimichi.LoadCheck;
using Tsukimichi.Localization;

// The load check (see the project file): every UI type's static constructor, then, in every language, the value of
// every static LocText, LocArray and LocCache<T> field in Tsukimichi.Ui. Any throw is a plugin that would not load, or
// would fail on first draw, in that language. Then every Lumina type and member GameData and the plugin reference,
// found in Dalamud's Lumina (LuminaCheck). Exit code 0 when all pass, 1 otherwise.
const string UiNamespace = "Tsukimichi.Ui";
string[] languages = [Loc.English, Loc.German, Loc.French, Loc.Japanese, Loc.PseudoLanguage];

var dalamud = DalamudLibPath();
if (dalamud is null || !File.Exists(Path.Combine(dalamud, "Dalamud.dll")))
{
    Console.Error.WriteLine($"Load check: Dalamud not found at \"{dalamud}\" (set DALAMUD_HOME)");
    return 2;
}

// Dalamud's assemblies are not copied beside the tool; they load from the folder the plugin was built against.
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (!string.IsNullOrEmpty(name.CultureName))
    {
        return null;
    }

    var path = Path.Combine(dalamud, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};

return Run();

int Run()
{
    var failures = new List<string>();
    var plugin = typeof(Loc).Assembly;
    Console.WriteLine($"Load check: {plugin.Location}");
    Console.WriteLine($"  Dalamud from {dalamud}");

    Type[] types;
    try
    {
        types = plugin.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        foreach (var loader in ex.LoaderExceptions.Where(static e => e is not null).Distinct())
        {
            failures.Add($"type load: {Describe(loader!)}");
        }

        types = ex.Types.Where(static t => t is not null).ToArray()!;
    }

    var ui = types.Where(static t => t.Namespace == UiNamespace && !t.ContainsGenericParameters).ToArray();

    // The static constructors run once per process, so once, in English (the language the plugin starts in when
    // Dalamud's is not a shipped one); a language-dependent field initializer is covered by the per-language pass.
    Loc.SetLanguage(Loc.English);
    foreach (var type in ui)
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
        catch (Exception ex)
        {
            failures.Add($"static constructor {type.FullName}: {Describe(ex)}");
        }
    }

    var fields = ui
        .SelectMany(static t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .Where(static f => IsLocalizedCache(f.FieldType))
        .ToArray();

    foreach (var language in languages)
    {
        Loc.SetLanguage(language);
        if (Loc.Language != language)
        {
            failures.Add($"{language}: Loc.SetLanguage left the language at {Loc.Language}");
            continue;
        }

        if (language is Loc.German or Loc.French or Loc.Japanese && Loc.TranslatedCount == 0)
        {
            failures.Add($"{language}: the translation's resource assembly did not load ({language}/Tsukimichi.resources.dll)");
        }

        var read = 0;
        foreach (var field in fields)
        {
            try
            {
                var holder = field.GetValue(null) ?? throw new InvalidOperationException("the field is null");
                var value = field.FieldType.GetProperty("Value")!.GetValue(holder) ?? throw new InvalidOperationException("Value is null");
                if (value is string[] items && Array.IndexOf(items, null) >= 0)
                {
                    throw new InvalidOperationException("the array holds a null");
                }

                read++;
            }
            catch (Exception ex)
            {
                failures.Add($"{language}: {field.DeclaringType!.FullName}.{field.Name}: {Describe(ex)}");
            }
        }

        Console.WriteLine($"  {language}: {read} of {fields.Length} localized fields read, {Loc.TranslatedCount} of {Loc.KeyCount} keys translated");
    }

    // A scan that finds (almost) nothing means the namespace or the cache types moved, not that everything passed.
    if (ui.Length < 20 || fields.Length < 20)
    {
        failures.Add($"the scan found only {ui.Length} types and {fields.Length} localized fields in {UiNamespace}; update the load check");
    }

    // Patch-day safety: GameData compiles against the Lumina NuGet packages but runs against Dalamud's copies; every
    // Lumina type and member it (and the plugin) uses must exist in those, or the catalog build throws in the game.
    var pluginDir = Path.GetDirectoryName(plugin.Location)!;
    var luminaMembers = LuminaCheck.Run(dalamud!, [Path.Combine(pluginDir, "Tsukimichi.GameData.dll"), plugin.Location], failures);
    if (luminaMembers < 20)
    {
        failures.Add($"lumina: the scan found only {luminaMembers} member references into Lumina; update the load check");
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"Load check FAILED ({failures.Count}):");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine("  " + failure);
        }

        return 1;
    }

    Console.WriteLine($"Load check passed: {ui.Length} types' static constructors, {fields.Length} localized fields in {languages.Length} languages, {luminaMembers} Lumina member references.");
    return 0;
}

static bool IsLocalizedCache(Type type) =>
    type == typeof(LocText) || type == typeof(LocArray) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LocCache<>));

// The innermost cause of a reflection or type-initializer wrapper, with the frame that threw.
static string Describe(Exception ex)
{
    while (ex is TargetInvocationException or TypeInitializationException && ex.InnerException is not null)
    {
        ex = ex.InnerException;
    }

    var frame = ex.StackTrace?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    return $"{ex.GetType().Name}: {ex.Message.Split('\n')[0].Trim()}{(frame is null ? string.Empty : " (" + frame + ")")}";
}

// DALAMUD_HOME at run time, else the folder the tool was built against.
static string? DalamudLibPath()
{
    var home = Environment.GetEnvironmentVariable("DALAMUD_HOME");
    if (!string.IsNullOrWhiteSpace(home))
    {
        return home;
    }

    return Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(static a => a.Key == "DalamudLibPath")?.Value;
}
