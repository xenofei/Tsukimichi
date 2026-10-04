namespace Tsukimichi.Core.Discovery;

/// <summary>What the server info bar entry counts (spec-1.22 M1, Settings › In game › Server info bar › "The entry counts").</summary>
public enum DtrCounts : byte
{
    /// <summary>"◐ 12 Ready": the quests Ready on the current job (the default). Click opens Tsukimichi; right-click Tonight.</summary>
    Ready = 0,

    /// <summary>"◐ 3 here": the quests that can start in this zone, as 1.x's Nearby entry. Click opens Nearby.</summary>
    Zone = 1,
}

/// <summary>How the entry draws its moon: the font's own "◐", or one of the game's icons where the font lacks it.</summary>
public enum DtrMoonKind : byte
{
    /// <summary>The text character <see cref="ServerInfoBar.Moon"/>, coloured with the theme's moon.</summary>
    Glyph,

    /// <summary>The game's own bitmap icon <see cref="ServerInfoBar.FallbackIcon"/> (owner decision 8).</summary>
    Icon,
}

/// <summary>
/// The one server info bar entry (plan v8 M1; spec-1.22 M1, decisions 13, 14 and 20), pure: whether it shows, what it
/// says, its tooltip's lines and how its moon is drawn. The plugin's <c>Game.DtrEntry</c> builds the SeString from these.
/// <para>
/// <b>The moon.</b> "◐" (U+25D0, lit on the left like every Tsukimichi moon). The game's UI font (AXIS) has neither it
/// nor 1.x's "☾" (checked against <c>common/font/AXIS_12.fdt</c>, game 2026.09), so in the game's own bar the entry
/// draws the game's own icon instead (owner decision 8). The game has no moon among its bitmap font icons
/// (<c>common/font/gfdata.gfd</c>, 188 entries): the nearest is <see cref="FallbackIcon"/>, 126, "Meteor", a gold ring
/// with a soft halo; it is a bitmap, so it keeps its own gold rather than the theme's colour.
/// </para>
/// </summary>
public static class ServerInfoBar
{
    /// <summary>The moon character, lit on the left (spec-1.22 decision 20).</summary>
    public const string Moon = "◐";

    /// <summary>Its code point, for the font check.</summary>
    public const int MoonCodePoint = 0x25D0;

    /// <summary>
    /// The game's bitmap font icon drawn when the font lacks <see cref="Moon"/>: <c>BitmapFontIcon.Meteor</c> (126 in
    /// <c>gfdata.gfd</c>), a gold ring with a halo, the nearest the game has to a moon.
    /// </summary>
    public const uint FallbackIcon = 126;

    /// <summary>Quest names listed in the Quests in this zone tooltip before "and N more".</summary>
    public const int MaxZoneNames = 5;

    /// <summary>
    /// Whether the entry shows at all (spec-1.22 M1 "On by default"): as the player set it once they chose; otherwise on
    /// while Umbra is installed and Tsukimichi for Umbra is not (the add-on shows the same in Umbra's own widgets), and off.
    /// </summary>
    /// <param name="chosen">The player set the switch (or had the 1.x entry, which keeps their choice).</param>
    /// <param name="show">The switch's value when chosen.</param>
    /// <param name="umbraInstalled">Umbra is installed.</param>
    /// <param name="addonInstalled">Tsukimichi for Umbra is installed.</param>
    public static bool Shown(bool chosen, bool show, bool umbraInstalled, bool addonInstalled) =>
        chosen ? show : umbraInstalled && !addonInstalled;

    /// <summary>
    /// The 1.22 settings from a file written by 1.21 or earlier (spec-1.22 M1): a player who had the Nearby entry on
    /// keeps it, counting Quests in this zone, and their on or off stays their choice. A fresh install chooses nothing:
    /// the entry counts Ready and follows <see cref="Shown"/>'s default.
    /// </summary>
    /// <param name="settings">The loaded settings; changed in place.</param>
    /// <param name="fileExisted">The settings file existed before this load (an update, not a fresh install).</param>
    /// <returns>Whether anything changed (the caller saves).</returns>
    public static bool Migrate(DiscoverySettings settings, bool fileExisted)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ServerInfoBarSchema >= DiscoverySettings.CurrentServerInfoBarSchema)
        {
            return false;
        }

        settings.ServerInfoBarSchema = DiscoverySettings.CurrentServerInfoBarSchema;
        if (fileExisted)
        {
            settings.DtrEntryChosen = true;
            settings.DtrCounts = settings.ShowDtrEntry ? DtrCounts.Zone : DtrCounts.Ready;
        }
        else
        {
            settings.DtrEntryChosen = false;
            settings.DtrCounts = DtrCounts.Ready;
        }

        return true;
    }

    /// <summary>How the moon is drawn: the glyph when the game's font has it, else the game's icon.</summary>
    public static DtrMoonKind MoonKind(bool fontHasMoon) => fontHasMoon ? DtrMoonKind.Glyph : DtrMoonKind.Icon;

    /// <summary>
    /// The words after the moon: "12 Ready", "3 here", "Nothing Ready" or "Nothing here" at zero with Show at zero on;
    /// null when the entry hides (zero without Show at zero).
    /// </summary>
    /// <param name="counts">What the entry counts.</param>
    /// <param name="ready">The quests Ready on the current job.</param>
    /// <param name="here">The quests that can start in this zone.</param>
    /// <param name="showAtZero">"Show at zero" (1.x's setting, kept).</param>
    /// <param name="words">The words: {0} the count ("{0} Ready", "{0} here") and the two zero forms.</param>
    public static string? Text(DtrCounts counts, int ready, int here, bool showAtZero, DtrWords words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var count = counts == DtrCounts.Zone ? here : ready;
        if (count <= 0)
        {
            return showAtZero ? (counts == DtrCounts.Zone ? words.NothingHere : words.NothingReady) : null;
        }

        return string.Format(System.Globalization.CultureInfo.CurrentCulture, counts == DtrCounts.Zone ? words.HereFormat : words.ReadyFormat, count);
    }

    /// <summary>
    /// The tooltip's lines (spec-1.22 M1 "The hover is text lines"): the title, "Up next: …" and its step, both counts,
    /// the journal and ending-soon lines, then what a click does. Empty lines are left out.
    /// </summary>
    public static IReadOnlyList<string> TooltipLines(string title, string? upNext, string? upNextStep, string counts, string? journal, IEnumerable<string> endingSoon, string clicks)
    {
        ArgumentNullException.ThrowIfNull(endingSoon);
        var lines = new List<string>(8) { title };
        if (!string.IsNullOrWhiteSpace(upNext))
        {
            lines.Add(upNext);
            if (!string.IsNullOrWhiteSpace(upNextStep))
            {
                lines.Add(upNextStep);
            }
        }

        if (!string.IsNullOrWhiteSpace(counts))
        {
            lines.Add(counts);
        }

        if (!string.IsNullOrWhiteSpace(journal))
        {
            lines.Add(journal);
        }

        foreach (var ending in endingSoon)
        {
            if (!string.IsNullOrWhiteSpace(ending))
            {
                lines.Add(ending);
            }
        }

        lines.Add(clicks);
        return lines;
    }

    /// <summary>
    /// Whether a game font file (<c>.fdt</c>) has a glyph for <paramref name="codePoint"/>. The file is the game's font
    /// table: an "fcsv" header whose third word is the offset of the "fthd" table, which holds its entry count at +4 and
    /// its 16-byte entries from +0x20, each led by the character's UTF-8 bytes packed big-endian into a little-endian
    /// word, sorted. False for a file this cannot read.
    /// </summary>
    public static bool FontHas(ReadOnlySpan<byte> fdt, int codePoint)
    {
        const int EntrySize = 16;
        if (fdt.Length < 0x20 || !fdt[..4].SequenceEqual("fcsv"u8))
        {
            return false;
        }

        var table = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fdt[8..]);
        if (table < 0 || table + 0x20 > fdt.Length || !fdt.Slice(table, 4).SequenceEqual("fthd"u8))
        {
            return false;
        }

        var count = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fdt[(table + 4)..]);
        var wanted = Utf8Key(codePoint);
        long lo = 0;
        long hi = Math.Min(count, (uint)((fdt.Length - table - 0x20) / EntrySize)) - 1L;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var key = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fdt[(table + 0x20 + (int)(mid * EntrySize))..]);
            if (key == wanted)
            {
                return true;
            }

            if (key < wanted)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return false;
    }

    /// <summary>A code point's UTF-8 bytes packed big-endian into a word, as the game's font tables key their glyphs.</summary>
    public static uint Utf8Key(int codePoint)
    {
        Span<byte> bytes = stackalloc byte[4];
        var length = new System.Text.Rune(codePoint).EncodeToUtf8(bytes);
        uint key = 0;
        for (var i = 0; i < length; i++)
        {
            key = (key << 8) | bytes[i];
        }

        return key;
    }

    /// <summary>
    /// The UIColor row nearest <paramref name="rgb"/> (0xRRGGBB) among <paramref name="rows"/> (row id, its colour as
    /// 0xRRGGBBAA, the game's order), by distance in RGB; null when there are none. The entry's moon and its tooltip's
    /// title take the game's own colours this way (spec-1.22 M1: "the UIColor row nearest the theme's phase colour").
    /// </summary>
    public static ushort? NearestUiColor(uint rgb, IEnumerable<(ushort Row, uint Rgba)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int r = (int)((rgb >> 16) & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)(rgb & 0xFF);
        ushort? best = null;
        var bestDistance = long.MaxValue;
        foreach (var (row, rgba) in rows)
        {
            if ((rgba & 0xFF) == 0)
            {
                // A transparent row draws nothing.
                continue;
            }

            int dr = (int)((rgba >> 24) & 0xFF) - r, dg = (int)((rgba >> 16) & 0xFF) - g, db = (int)((rgba >> 8) & 0xFF) - b;
            long distance = (2L * dr * dr) + (4L * dg * dg) + (3L * db * db);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (row, distance);
            }
        }

        return best;
    }
}

/// <summary>The entry's words, from the plugin's strings (English only; localization is frozen).</summary>
/// <param name="ReadyFormat">"{0} Ready".</param>
/// <param name="HereFormat">"{0} here".</param>
/// <param name="NothingReady">"Nothing Ready".</param>
/// <param name="NothingHere">"Nothing here".</param>
public sealed record DtrWords(string ReadyFormat, string HereFormat, string NothingReady, string NothingHere);
