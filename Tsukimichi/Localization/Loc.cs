using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Resources;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Localization;

/// <summary>
/// The plugin's text for the current UI language (V2-19). English is the source: <c>Localization/Strings.resx</c>
/// holds every key, and <c>Strings.ja.resx</c>, <c>Strings.de.resx</c> and <c>Strings.fr.resx</c> (draft translations,
/// shipped as satellite assemblies) override what they translate; a key a language lacks reads in English. The tables
/// are merged into one dictionary per switch, so <see cref="Get"/> is a single lookup on the draw path.
/// <para>
/// <see cref="PseudoLanguage"/> ("qps") is no file: it is English stretched by 40 % and wrapped in brackets, for
/// checking that the layout survives long strings (German runs about a third longer than English) and that no text
/// bypasses the tables.
/// </para>
/// <para>
/// Thread-safe for reads: a switch builds the new table aside and swaps the reference. Switches happen on the
/// framework thread. Core's phrases are served through <see cref="Provider"/>, installed with <see cref="CoreText.Use"/>.
/// </para>
/// </summary>
public static class Loc
{
    public const string English = "en";
    public const string Japanese = "ja";
    public const string German = "de";
    public const string French = "fr";
    public const string PseudoLanguage = "qps";

    /// <summary>The languages that ship a resource file, English first.</summary>
    public static readonly string[] Shipped = [English, Japanese, German, French];

    private const string ResourceBaseName = "Tsukimichi.Localization.Strings";

    private static readonly ResourceManager Resources = new(ResourceBaseName, typeof(Loc).Assembly);

    // Taken by every writer of the table (SetLanguage and the lazy English load), so one never overwrites the other.
    private static readonly object Gate = new();

    private static Dictionary<string, string>? englishTable;
    private static volatile Dictionary<string, string> table = new(StringComparer.Ordinal);
    private static volatile ConcurrentDictionary<string, string[]> arrays = new(StringComparer.Ordinal);
    private static int version;

    /// <summary>The current language code: <see cref="English"/>, a shipped translation or <see cref="PseudoLanguage"/>.</summary>
    public static string Language { get; private set; } = English;

    /// <summary>Bumps on every switch; caches of composed labels compare against it.</summary>
    public static int Version => version;

    /// <summary>Keys the current language translates (English: all of them). Settings shows coverage from it.</summary>
    public static int TranslatedCount { get; private set; }

    /// <summary>The last error a resource file failed to load with (a corrupt or locked satellite assembly); null when none did.</summary>
    public static Exception? LastReadError { get; private set; }

    /// <summary>Keys in English.</summary>
    public static int KeyCount => EnglishTable.Count;

    /// <summary>Raised on the framework thread after a switch, once the new table is in place.</summary>
    public static event Action? Changed;

    /// <summary>Core's view of the tables (<see cref="CoreText"/>).</summary>
    public static ITextProvider Provider { get; } = new CoreProvider();

    private static Dictionary<string, string> EnglishTable => englishTable ??= Read(CultureInfo.InvariantCulture, tryParents: true);

    /// <summary>The text for <paramref name="key"/> in the current language; English where it lacks one; the key itself if English lacks it too.</summary>
    public static string Get(string key)
    {
        var current = table;
        if (current.Count == 0)
        {
            current = EnsureLoaded();
        }

        return current.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// The array stored as <c>key.0</c>, <c>key.1</c>, … (help cards, tips), in the current language; the length is
    /// English's, so a translation that lags keeps the English element. Cached until the next switch.
    /// </summary>
    public static string[] Array(string key)
    {
        var cache = arrays;
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var items = new List<string>();
        var english = EnglishTable;
        for (var i = 0; english.ContainsKey(key + "." + i.ToString(CultureInfo.InvariantCulture)); i++)
        {
            items.Add(Get(key + "." + i.ToString(CultureInfo.InvariantCulture)));
        }

        var result = items.ToArray();
        cache[key] = result;
        return result;
    }

    /// <summary>
    /// The plural form for <paramref name="count"/>: <paramref name="one"/> or <paramref name="other"/>, both format
    /// strings taking the count as {0}. English and German use the one form for 1 only, French for 0 and 1, Japanese
    /// never (one form for every count).
    /// </summary>
    public static string Plural(long count, string one, string other) =>
        string.Format(CultureInfo.CurrentCulture, IsOne(Language, count) ? one : other, count);

    /// <summary>The plural rule of <paramref name="language"/>: whether <paramref name="count"/> takes the one form.</summary>
    public static bool IsOne(string language, long count) => language switch
    {
        Japanese => false,
        French => count is 0 or 1,
        _ => count == 1,
    };

    /// <summary>A language's name in that language, as the Settings choice shows it; never translated.</summary>
    public static string NativeName(string language) => language switch
    {
        English => "English",
        Japanese => "日本語",
        German => "Deutsch",
        French => "Français",
        PseudoLanguage => "Pseudo",
        _ => language,
    };

    /// <summary>"draft" while a translation awaits players' review (the file's <c>Meta.TranslationStatus</c>).</summary>
    public static bool IsDraft => Language is Japanese or German or French && Get("Meta.TranslationStatus") == "draft";

    /// <summary>
    /// Maps a Dalamud UI language code ("en", "ja", "de", "fr", "it", …) to a shipped language; anything without a
    /// resource file reads English.
    /// </summary>
    public static string Resolve(string? dalamudLanguage)
    {
        var code = (dalamudLanguage ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length > 2 && code[2] is '-' or '_')
        {
            code = code[..2];
        }

        return code switch
        {
            Japanese or German or French or PseudoLanguage => code,
            _ => English,
        };
    }

    /// <summary>
    /// Switches the language (framework thread): builds the merged table, installs it, bumps <see cref="Version"/>,
    /// tells Core, then raises <see cref="Changed"/>. A switch to the current language does nothing.
    /// </summary>
    public static void SetLanguage(string language)
    {
        language = Resolve(language);

        // The same lock as EnsureLoaded: a worker's first Get must not install English over the table set here.
        lock (Gate)
        {
            if (language == Language && table.Count > 0)
            {
                return;
            }

            var merged = Build(language, out var translated);
            Language = language;
            TranslatedCount = translated;
            table = merged;
            arrays = new ConcurrentDictionary<string, string[]>(StringComparer.Ordinal);
            System.Threading.Interlocked.Increment(ref version);
            CoreText.Use(language == English ? null : Provider);
        }

        Changed?.Invoke();
    }

    /// <summary>The keys a shipped language translates, read from its satellite assembly (empty when it did not load).</summary>
    public static int CountTranslated(string language) =>
        language == English ? EnglishTable.Count : Read(new CultureInfo(language), tryParents: false).Count;

    private static Dictionary<string, string> EnsureLoaded()
    {
        lock (Gate)
        {
            if (table.Count == 0)
            {
                table = Build(English, out var translated);
                TranslatedCount = translated;
            }

            return table;
        }
    }

    private static Dictionary<string, string> Build(string language, out int translated)
    {
        var english = EnglishTable;
        var merged = new Dictionary<string, string>(english, StringComparer.Ordinal);
        translated = english.Count;
        if (language == PseudoLanguage)
        {
            foreach (var (key, value) in english)
            {
                if (!PseudoText.IsMachineKey(key))
                {
                    merged[key] = PseudoText.Stretch(value);
                }
            }
        }
        else if (language != English)
        {
            var own = Read(new CultureInfo(language), tryParents: false);
            translated = 0;
            foreach (var (key, value) in own)
            {
                // A key English no longer has is stale; it is never shown.
                if (english.ContainsKey(key))
                {
                    merged[key] = value;
                    translated++;
                }
            }
        }

        return merged;
    }

    private static Dictionary<string, string> Read(CultureInfo culture, bool tryParents)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        LastReadError = null;
        ResourceSet? set;
        try
        {
            set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: tryParents);
        }
        catch (MissingManifestResourceException)
        {
            set = null;
        }
        catch (MissingSatelliteAssemblyException)
        {
            set = null;
        }
        catch (Exception ex) when (ex is FileLoadException or BadImageFormatException or IOException)
        {
            // A satellite assembly that is corrupt, locked or unreadable: the language reads English, and LocService
            // logs why (LastReadError) instead of the plugin failing to load.
            LastReadError = ex;
            set = null;
        }

        if (set is null)
        {
            return result;
        }

        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>English stretched for the layout check (<see cref="PseudoText.Stretch"/>).</summary>
    public static string Pseudo(string english) => PseudoText.Stretch(english);

    /// <summary>Serves Core's keys from the merged table; null (English) where the table has none.</summary>
    private sealed class CoreProvider : ITextProvider
    {
        public string? Find(string key) => table.TryGetValue(key, out var value) ? value : null;
    }
}
