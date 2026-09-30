namespace Tsukimichi.Core.Localization;

/// <summary>
/// Where Core's player-visible phrases come from (V2-19). Core stays free of Dalamud and of resource files: every
/// phrase is written in English at its call site, <c>CoreText.T("Core.State.Ready", "Ready")</c>, and the plugin
/// installs a provider that returns the phrase in the player's language for that key. Without a provider (the tests,
/// DataGen, a build before the plugin starts) every phrase is the English one written beside it.
/// </summary>
public interface ITextProvider
{
    /// <summary>The phrase for <paramref name="key"/> in the current language, or null to use the English default.</summary>
    string? Find(string key);
}

/// <summary>
/// The ambient text provider Core's builders read. Swapping it bumps <see cref="Version"/>, which every Core cache of
/// composed text (<see cref="TextCache{T}"/>) compares against. <see cref="English"/> opens a scope, on the calling
/// thread only, in which every phrase is English whatever the provider says: the diagnostic block and the export
/// headers are for bug reports and tools, so they never follow the UI language.
/// </summary>
public static class CoreText
{
    private static volatile ITextProvider? provider;
    private static int version;

    [ThreadStatic]
    private static int englishDepth;

    /// <summary>Changes whenever the provider (or the language behind it) changes.</summary>
    public static int Version => Volatile.Read(ref version);

    /// <summary>True inside an <see cref="English"/> scope on this thread, or while no provider is installed.</summary>
    public static bool IsEnglish => englishDepth > 0 || provider is null;

    /// <summary>
    /// Installs the provider (null restores English) and bumps <see cref="Version"/>. Also the call the plugin makes
    /// when the language behind the same provider changes.
    /// </summary>
    public static void Use(ITextProvider? textProvider)
    {
        provider = textProvider;
        Interlocked.Increment(ref version);
    }

    /// <summary>The phrase for <paramref name="key"/>: the provider's, or <paramref name="english"/>.</summary>
    public static string T(string key, string english)
    {
        if (englishDepth > 0)
        {
            return english;
        }

        var current = provider;
        return current?.Find(key) ?? english;
    }

    /// <summary>
    /// A scope in which <see cref="T"/> returns English on this thread; dispose it to leave. Scopes nest.
    /// <code>using (CoreText.English()) { /* build the diagnostic block */ }</code>
    /// </summary>
    public static EnglishScope English()
    {
        englishDepth++;
        return new EnglishScope();
    }

    /// <summary>Leaves the English scope <see cref="English"/> opened.</summary>
    public readonly struct EnglishScope : IDisposable
    {
        public void Dispose()
        {
            if (englishDepth > 0)
            {
                englishDepth--;
            }
        }
    }
}

/// <summary>
/// A value composed from <see cref="CoreText"/> phrases once and kept until the language changes: one slot for the
/// current language, one for English (read inside <see cref="CoreText.English"/>). Reads are safe from any thread;
/// two threads racing after a change may both compose, and either result is correct.
/// </summary>
public sealed class TextCache<T>
    where T : class
{
    private readonly Func<T> build;
    private Entry? current;
    private T? english;

    public TextCache(Func<T> build) => this.build = build;

    public T Value
    {
        get
        {
            if (CoreText.IsEnglish)
            {
                return english ??= build();
            }

            var version = CoreText.Version;
            var entry = Volatile.Read(ref current);
            if (entry is null || entry.Version != version)
            {
                entry = new Entry(version, build());
                Volatile.Write(ref current, entry);
            }

            return entry.Value;
        }
    }

    private sealed record Entry(int Version, T Value);
}
