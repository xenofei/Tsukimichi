using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
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
        Changed?.Invoke();
    }

    /// <summary>The keys a shipped language translates, read from its satellite assembly (empty when it did not load).</summary>
    public static int CountTranslated(string language) =>
        language == English ? EnglishTable.Count : Read(new CultureInfo(language), tryParents: false).Count;

    private static Dictionary<string, string> EnsureLoaded()
    {
        lock (Resources)
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
                merged[key] = Pseudo(value);
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

    // Spans pseudo-localization must keep as they are: composite format items, printf specifiers, ImGui ids, the
    // combo separator and line breaks.
    private static readonly Regex Protected = new(@"\{[^{}]*\}|%[-+ #0]*\d*(?:\.\d+)?[a-zA-Z%]|##.*$|\0|\n", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// English stretched by 40 % and bracketed: letters swap to accented look-alikes, a run of "·" pads the end, and
    /// placeholders, printf specifiers and ImGui ids stay intact so every format call and window id still works.
    /// An empty string stays empty; an ImGui "###id" suffix stays outside the brackets.
    /// </summary>
    public static string Pseudo(string english)
    {
        if (english.Length == 0)
        {
            return english;
        }

        var visible = english;
        var id = string.Empty;
        var idAt = english.IndexOf("##", StringComparison.Ordinal);
        if (idAt >= 0)
        {
            visible = english[..idAt];
            id = english[idAt..];
        }

        if (visible.Length == 0)
        {
            return english;
        }

        // Combo item lists ("Inherit\0On\0Off\0") stretch each item on its own.
        if (visible.Contains('\0', StringComparison.Ordinal))
        {
            var parts = visible.Split('\0');
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Length == 0 ? parts[i] : Pseudo(parts[i]);
            }

            return string.Join('\0', parts) + id;
        }

        var sb = new StringBuilder(visible.Length * 2);
        sb.Append('[');
        var last = 0;
        foreach (Match match in Protected.Matches(visible))
        {
            Accent(sb, visible, last, match.Index);
            sb.Append(match.Value);
            last = match.Index + match.Length;
        }

        Accent(sb, visible, last, visible.Length);
        var pad = (int)Math.Ceiling(visible.Length * 0.4);
        sb.Append(' ').Append('·', Math.Max(1, pad - 1));
        sb.Append(']');
        sb.Append(id);
        return sb.ToString();
    }

    private static void Accent(StringBuilder sb, string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            var c = text[i];
            sb.Append(c switch
            {
                'a' => 'á',
                'e' => 'é',
                'i' => 'í',
                'o' => 'ö',
                'u' => 'ü',
                'c' => 'ç',
                'n' => 'ñ',
                'y' => 'ý',
                'A' => 'Å',
                'E' => 'É',
                'I' => 'Î',
                'O' => 'Ø',
                'U' => 'Ü',
                'C' => 'Ç',
                'N' => 'Ñ',
                _ => c,
            });
        }
    }

    /// <summary>Serves Core's keys from the merged table; null (English) where the table has none.</summary>
    private sealed class CoreProvider : ITextProvider
    {
        public string? Find(string key) => table.TryGetValue(key, out var value) ? value : null;
    }
}
