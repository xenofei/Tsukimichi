using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>How a pasted text read as a share code (<see cref="ShareCode.Decode"/>).</summary>
public enum ShareCodeStatus
{
    /// <summary>A code: <see cref="ShareCodeRead.Look"/> holds it, with any id this build does not know left out and named.</summary>
    Ok,

    /// <summary>Nothing to read: the text is empty, or only spaces, dashes and "TM".</summary>
    Empty,

    /// <summary>A character outside the code's alphabet, or format version 0.</summary>
    Unreadable,

    /// <summary>The checksum, the length or the padding does not hold: a character was mistyped, dropped or added.</summary>
    Checksum,

    /// <summary>A format version above this build's (<see cref="ShareCodeRead.Version"/>): nothing in it can be read.</summary>
    Newer,
}

/// <summary>Which part of a look a left-out id belongs to.</summary>
public enum ShareCodeField
{
    Theme,
    Palette,
    Frames,

    /// <summary>One state's pick in the mix (<see cref="ShareCodeOmission.State"/>).</summary>
    State,
}

/// <summary>
/// One id a code names that this build cannot apply, so it is left out and the rest applies (spec-1.17 §C1). An id this
/// build does not know at all (a newer build's) is not <paramref name="Registered"/>; a registered one is a choice this
/// build lists but does not offer yet (a theme still in its design round), or a set that cannot be mixed (Classic).
/// </summary>
/// <param name="State">The state, for <see cref="ShareCodeField.State"/>; otherwise <see cref="QuestState.Ready"/>.</param>
/// <param name="Id">The id as the code carries it (a palette's share-code number, <see cref="ShareCode.PaletteWire"/>).</param>
public readonly record struct ShareCodeOmission(ShareCodeField Field, QuestState State, int Id, bool Registered);

/// <summary>
/// A look as a share code carries it: ids only (spec-1.17 §C1). <paramref name="Theme"/> is a <see cref="ThemeId"/>,
/// <paramref name="Frames"/> a <see cref="FrameKitId"/> and each pick a <see cref="GlyphSetId"/>, by number;
/// <paramref name="Palette"/> is the code's own palette number (<see cref="ShareCode.PaletteWire"/>). 0 means "from the
/// theme" for the palette, the frames and each pick. <paramref name="Picks"/> packs the eight picks four bits each, in
/// <see cref="QuestState"/> order with Ready in the top nibble, as the code writes them.
/// </summary>
public readonly record struct ShareLook(int Theme, int Palette, int Frames, bool HighContrast, uint Picks)
{
    /// <summary>Whether any state has a pick of its own (the code's "has mix" bit).</summary>
    public bool HasMix => Picks != 0;

    /// <summary>The pick for the state at <paramref name="index"/> (0–7, <see cref="QuestState"/> order); 0 for from the theme.</summary>
    public int Pick(int index) => (uint)index < AppearanceStates.Count ? (int)((Picks >> Shift(index)) & 0xF) : 0;

    /// <summary>The pick for <paramref name="state"/>; 0 for from the theme.</summary>
    public int Pick(QuestState state) => Pick(AppearanceStates.Index(state));

    /// <summary>This look with <paramref name="state"/>'s pick set to <paramref name="id"/> (0–15; 0 for from the theme).</summary>
    public ShareLook WithPick(QuestState state, int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, 15);
        var shift = Shift(AppearanceStates.Index(state));
        return this with { Picks = (Picks & ~(0xFu << shift)) | ((uint)id << shift) };
    }

    private static int Shift(int index) => 4 * (AppearanceStates.Count - 1 - index);
}

/// <summary>What <see cref="ShareCode.Decode"/> read.</summary>
public sealed class ShareCodeRead
{
    internal ShareCodeRead(ShareCodeStatus status, int version, bool prefixed, int length, ShareLook look, IReadOnlyList<ShareCodeOmission> unknown)
    {
        Status = status;
        Version = version;
        Prefixed = prefixed;
        Length = length;
        Look = look;
        Unknown = unknown;
    }

    public ShareCodeStatus Status { get; }

    /// <summary>Whether the text is a code this build reads (<see cref="ShareCodeStatus.Ok"/>).</summary>
    public bool Ok => Status == ShareCodeStatus.Ok;

    /// <summary>The format version the code's first character names; 0 when there was nothing to read.</summary>
    public int Version { get; }

    /// <summary>Whether the text began with "TM" (after spaces and dashes): a code, rather than any word.</summary>
    public bool Prefixed { get; }

    /// <summary>How many code characters the text holds after "TM" (spaces and dashes not counted).</summary>
    public int Length { get; }

    /// <summary>
    /// The look, when <see cref="Ok"/>: the ids this build knows, with each it does not replaced as the reference does
    /// (an unknown theme reads as Menphina's Medallion; an unknown palette, frames or pick as from the theme).
    /// </summary>
    public ShareLook Look { get; }

    /// <summary>The ids this build does not know, in the code's order (theme, palette, frames, then the picks); empty when none.</summary>
    public IReadOnlyList<ShareCodeOmission> Unknown { get; }
}

/// <summary>
/// The share code (feature plan v7 T12; docs/design/v7/ui/spec-1.17.md §C; the reference is
/// docs/design/v7/ui/1.17/sharecode.py, and <c>ShareCodeTests</c> checks this against its vectors). "TM", then the
/// payload in Crockford base32, shown as the version character, a dash, and groups of four:
/// <list type="table">
/// <item><term>version, 5 bits</term><description>1, so every code this build writes begins "TM1-"</description></item>
/// <item><term>theme, 4</term><description>a <see cref="ThemeId"/> (the same numbers as <see cref="GlyphSetId"/>)</description></item>
/// <item><term>palette, 4</term><description>0 from the theme, else <see cref="PaletteWire"/></description></item>
/// <item><term>frames, 4</term><description>0 from the theme, else a <see cref="FrameKitId"/></description></item>
/// <item><term>high contrast, 1; has mix, 1</term><description></description></item>
/// <item><term>8 picks × 4</term><description>only with the mix: 0 from the theme, else a <see cref="GlyphSetId"/>, in <see cref="QuestState"/> order</description></item>
/// <item><term>CRC-8, 8</term><description>polynomial 0x07, initial 0, over every bit before it; the padding bits must be 0</description></item>
/// </list>
/// A look without a mix is 6 characters ("TM1-8003-0"), one with a mix 12 ("TM1-202C-000C-02C"). Reading is tolerant:
/// letter case, spaces, dashes and a missing "TM" do not matter, O reads 0 and I or L read 1 (U is never written). Every
/// single-character typo fails the checksum. Codes carry ids only, so reading checks ranges and nothing else. Pure, and
/// it allocates only what it returns.
/// </summary>
public static class ShareCode
{
    /// <summary>The format version this build writes and reads.</summary>
    public const int Version = 1;

    /// <summary>A code's characters after "TM" with a mix (the longest a code this build writes is).</summary>
    public const int MixLength = 12;

    /// <summary>A code's characters after "TM" without a mix.</summary>
    public const int PresetLength = 6;

    /// <summary>The longest text the paste field and the chat command take: a code with room for spaces and dashes.</summary>
    public const int MaxTextLength = 64;

    /// <summary>Crockford's base32 alphabet: no I, L, O or U.</summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int FixedBits = 19;
    private const int MixBits = 32;
    private const int CrcBits = 8;
    private const int MixFlagBit = 18;

    /// <summary>
    /// The share code's number for <paramref name="palette"/> (spec-1.17 §C1: 1 Night, 2 Ishgard Snow, 3 Dawn, 4 Kugane
    /// Lacquer, 5 Follow Dalamud). The code numbers palettes in the order the spec's table lists them, which is not
    /// <see cref="PaletteId"/>'s order (Dawn is 2 there, Ishgard Snow 3), so the code maps them and both stay fixed.
    /// </summary>
    public static int PaletteWire(PaletteId palette) => palette switch
    {
        PaletteId.Night => 1,
        PaletteId.IshgardSnow => 2,
        PaletteId.Dawn => 3,
        PaletteId.KuganeLacquer => 4,
        PaletteId.FollowDalamud => 5,
        _ => 0,
    };

    /// <summary>The palette a share code's number names; false for 0 (from the theme) or a number this build does not know.</summary>
    public static bool TryPaletteFromWire(int wire, out PaletteId palette)
    {
        palette = wire switch
        {
            1 => PaletteId.Night,
            2 => PaletteId.IshgardSnow,
            3 => PaletteId.Dawn,
            4 => PaletteId.KuganeLacquer,
            5 => PaletteId.FollowDalamud,
            _ => 0,
        };
        return palette != 0;
    }

    /// <summary>The code for <paramref name="look"/>: "TM1-XXXX-X" without a mix, "TM1-XXXX-XXXX-XXX" with one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An id outside 0–15.</exception>
    public static string Encode(ShareLook look)
    {
        CheckNibble(look.Theme, nameof(look.Theme));
        CheckNibble(look.Palette, nameof(look.Palette));
        CheckNibble(look.Frames, nameof(look.Frames));

        Span<byte> bits = stackalloc byte[FixedBits + MixBits + CrcBits + 4];
        var count = 0;
        Put(bits, ref count, Version, 5);
        Put(bits, ref count, look.Theme, 4);
        Put(bits, ref count, look.Palette, 4);
        Put(bits, ref count, look.Frames, 4);
        Put(bits, ref count, look.HighContrast ? 1 : 0, 1);
        Put(bits, ref count, look.HasMix ? 1 : 0, 1);
        if (look.HasMix)
        {
            for (var i = 0; i < AppearanceStates.Count; i++)
            {
                Put(bits, ref count, look.Pick(i), 4);
            }
        }

        Put(bits, ref count, Crc8(bits[..count]), CrcBits);
        while (count % 5 != 0)
        {
            bits[count++] = 0;
        }

        var chars = count / 5;
        var text = new StringBuilder(2 + chars + (chars / 4) + 1);
        text.Append("TM");
        for (var c = 0; c < chars; c++)
        {
            // The version character, then groups of four: TM1-XXXX-XXXX-XXX.
            if (c >= 1 && (c - 1) % 4 == 0)
            {
                text.Append('-');
            }

            var value = 0;
            for (var b = 0; b < 5; b++)
            {
                value = (value << 1) | bits[(c * 5) + b];
            }

            text.Append(Alphabet[value]);
        }

        return text.ToString();
    }

    /// <summary>
    /// The code for the saved appearance <paramref name="config"/>, as it is saved (not as it resolves): its theme (the
    /// default theme for a key this build does not know), its palette and frames overrides, high contrast, and its mix.
    /// A key this build does not know (a newer build's) has no number, so it is written as from the theme.
    /// </summary>
    public static string Encode(AppearanceConfig config) => Encode(LookOf(config));

    /// <summary>The look <paramref name="config"/> saves, in the code's ids (see <see cref="Encode(AppearanceConfig)"/>).</summary>
    public static ShareLook LookOf(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var theme = ThemePresets.TryGet(config.Theme, out var preset) ? preset.Id : ThemePresets.Default.Id;
        var palette = PaletteChoices.TryGet(config.Palette, out var p) ? PaletteWire(p.Id) : 0;
        var frames = FrameKits.TryGet(config.Frames, out var kit) ? (int)kit.Id : 0;
        var look = new ShareLook((int)theme, palette, frames, config.HighContrast, 0);
        if (config.Glyphs is { Count: > 0 } glyphs)
        {
            foreach (var (stateKey, setKey) in glyphs)
            {
                if (AppearanceStates.TryParse(stateKey, out var state) && GlyphSets.TryGet(setKey, out var set))
                {
                    look = look.WithPick(state, (int)set.Id);
                }
            }
        }

        return look;
    }

    /// <summary>
    /// Reads <paramref name="text"/> as a share code (see the class remarks for what is tolerated). Never throws, and a
    /// text that does not read changes nothing: only an <see cref="ShareCodeStatus.Ok"/> read carries a look.
    /// </summary>
    public static ShareCodeRead Decode(string? text)
    {
        // Normalise: upper case; spaces (any white space) and dashes dropped; O reads 0, I and L read 1.
        var source = text ?? string.Empty;
        var normal = new StringBuilder(source.Length);
        foreach (var raw in source)
        {
            if (raw == '-' || char.IsWhiteSpace(raw))
            {
                continue;
            }

            var ch = char.ToUpperInvariant(raw);
            normal.Append(ch switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => ch,
            });
        }

        var prefixed = normal.Length >= 2 && normal[0] == 'T' && normal[1] == 'M';
        var body = prefixed ? normal.ToString(2, normal.Length - 2) : normal.ToString();
        if (body.Length == 0)
        {
            return Fail(ShareCodeStatus.Empty, 0, prefixed, 0);
        }

        var bits = new byte[body.Length * 5];
        for (var c = 0; c < body.Length; c++)
        {
            var value = Alphabet.IndexOf(body[c], StringComparison.Ordinal);
            if (value < 0)
            {
                return Fail(ShareCodeStatus.Unreadable, 0, prefixed, body.Length);
            }

            for (var b = 0; b < 5; b++)
            {
                bits[(c * 5) + b] = (byte)((value >> (4 - b)) & 1);
            }
        }

        var version = Get(bits, 0, 5);
        if (version != Version)
        {
            return Fail(version > Version ? ShareCodeStatus.Newer : ShareCodeStatus.Unreadable, version, prefixed, body.Length);
        }

        // Too short to hold the fixed fields is too short to hold its checksum (the reference indexes past the end here).
        if (bits.Length <= MixFlagBit)
        {
            return Fail(ShareCodeStatus.Checksum, version, prefixed, body.Length);
        }

        var payload = FixedBits + (bits[MixFlagBit] == 1 ? MixBits : 0);
        if (bits.Length < payload + CrcBits || Crc8(bits.AsSpan(0, payload)) != Get(bits, payload, CrcBits) || bits.AsSpan(payload + CrcBits).ContainsAnyExcept((byte)0))
        {
            return Fail(ShareCodeStatus.Checksum, version, prefixed, body.Length);
        }

        List<ShareCodeOmission>? unknown = null;
        var theme = Get(bits, 5, 4);
        if (!Known(ThemePresets.All, theme, static t => (int)t.Id))
        {
            (unknown ??= []).Add(new ShareCodeOmission(ShareCodeField.Theme, QuestState.Ready, theme, Registered: false));
            theme = (int)ThemeId.Medallion;
        }

        var palette = Get(bits, 9, 4);
        if (palette != 0 && !TryPaletteFromWire(palette, out _))
        {
            (unknown ??= []).Add(new ShareCodeOmission(ShareCodeField.Palette, QuestState.Ready, palette, Registered: false));
            palette = 0;
        }

        var frames = Get(bits, 13, 4);
        if (frames != 0 && !Known(FrameKits.All, frames, static k => (int)k.Id))
        {
            (unknown ??= []).Add(new ShareCodeOmission(ShareCodeField.Frames, QuestState.Ready, frames, Registered: false));
            frames = 0;
        }

        var look = new ShareLook(theme, palette, frames, bits[17] == 1, 0);
        if (bits[MixFlagBit] == 1)
        {
            for (var i = 0; i < AppearanceStates.Count; i++)
            {
                var state = AppearanceStates.All[i];
                var pick = Get(bits, FixedBits + (4 * i), 4);
                if (pick != 0 && !Known(GlyphSets.All, pick, static s => (int)s.Id))
                {
                    (unknown ??= []).Add(new ShareCodeOmission(ShareCodeField.State, state, pick, Registered: false));
                    pick = 0;
                }

                look = look.WithPick(state, pick);
            }
        }

        return new ShareCodeRead(ShareCodeStatus.Ok, version, prefixed, body.Length, look, unknown is null ? [] : unknown);
    }

    /// <summary>
    /// What applying <paramref name="look"/> over <paramref name="saved"/> saves: a copy of <paramref name="saved"/> (its
    /// version and the fields a newer build saved are kept) with the code's theme, palette, frames, high contrast and mix.
    /// A palette or frames equal to the theme's own is saved as from the theme, as the Themes page saves it. What this
    /// build lists but does not offer (a theme, palette or kit still in its design round, or a pick of a set that is not
    /// offered or cannot be mixed) is left out and added to <paramref name="leftOut"/>: a theme then reads as the default
    /// theme, the rest as from the theme.
    /// </summary>
    public static AppearanceConfig Apply(AppearanceConfig saved, ShareLook look, ICollection<ShareCodeOmission> leftOut)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(leftOut);

        var theme = ThemePresets.Get((ThemeId)look.Theme);
        if ((int)theme.Id != look.Theme || !theme.Offered)
        {
            leftOut.Add(new ShareCodeOmission(ShareCodeField.Theme, QuestState.Ready, look.Theme, (int)theme.Id == look.Theme));
            theme = ThemePresets.Default;
        }

        string? palette = null;
        if (look.Palette != 0)
        {
            if (TryPaletteFromWire(look.Palette, out var id) && PaletteChoices.Get(id) is { Offered: true } info)
            {
                palette = info.Id == theme.Palette ? null : info.Key;
            }
            else
            {
                leftOut.Add(new ShareCodeOmission(ShareCodeField.Palette, QuestState.Ready, look.Palette, TryPaletteFromWire(look.Palette, out _)));
            }
        }

        string? frames = null;
        if (look.Frames != 0)
        {
            var kit = FrameKits.Get((FrameKitId)look.Frames);
            if ((int)kit.Id == look.Frames && kit.Offered)
            {
                frames = kit.Id == theme.Frames ? null : kit.Key;
            }
            else
            {
                leftOut.Add(new ShareCodeOmission(ShareCodeField.Frames, QuestState.Ready, look.Frames, (int)kit.Id == look.Frames));
            }
        }

        Dictionary<string, string>? glyphs = null;
        foreach (var state in AppearanceStates.All)
        {
            var pick = look.Pick(state);
            if (pick == 0)
            {
                continue;
            }

            var set = GlyphSets.Get((GlyphSetId)pick);
            if ((int)set.Id == pick && set.Offered && set.Mixable)
            {
                (glyphs ??= new Dictionary<string, string>(StringComparer.Ordinal))[AppearanceStates.Key(state)] = set.Key;
            }
            else
            {
                leftOut.Add(new ShareCodeOmission(ShareCodeField.State, state, pick, (int)set.Id == pick));
            }
        }

        var result = saved.Clone();
        result.Theme = theme.Key;
        result.Palette = palette;
        result.Frames = frames;
        result.HighContrast = look.HighContrast;
        result.Glyphs = glyphs;
        return result;
    }

    /// <summary>
    /// Everything that would look different going from <paramref name="before"/> to <paramref name="after"/>, in the
    /// preview's order: the theme, the palette and the frames as they resolve (a theme brings its own), high contrast, then
    /// each state whose own pick changes. Empty when the two look the same.
    /// </summary>
    public static IReadOnlyList<ShareChange> Changes(AppearanceConfig before, AppearanceConfig after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var from = AppearanceResolver.Resolve(before);
        var to = AppearanceResolver.Resolve(after);
        var changes = new List<ShareChange>();
        if (from.Theme.Id != to.Theme.Id)
        {
            changes.Add(new ShareChange(ShareChangeKind.Theme, QuestState.Ready, (int)from.Theme.Id, (int)to.Theme.Id));
        }

        if (from.Palette != to.Palette)
        {
            changes.Add(new ShareChange(ShareChangeKind.Palette, QuestState.Ready, (int)from.Palette, (int)to.Palette));
        }

        if (from.Frames != to.Frames)
        {
            changes.Add(new ShareChange(ShareChangeKind.Frames, QuestState.Ready, (int)from.Frames, (int)to.Frames));
        }

        if (before.HighContrast != after.HighContrast)
        {
            changes.Add(new ShareChange(ShareChangeKind.HighContrast, QuestState.Ready, before.HighContrast ? 1 : 0, after.HighContrast ? 1 : 0));
        }

        foreach (var state in AppearanceStates.All)
        {
            var was = OwnPick(before, state);
            var now = OwnPick(after, state);
            if (was != now)
            {
                changes.Add(new ShareChange(ShareChangeKind.State, state, (int)was, (int)now));
            }
        }

        return changes;
    }

    /// <summary>
    /// The set <paramref name="state"/> is picked from in <paramref name="config"/>'s mix when the pick counts (an offered,
    /// mixable set; <see cref="AppearanceResolver"/>'s rule), else 0 (from the theme).
    /// </summary>
    public static GlyphSetId OwnPick(AppearanceConfig config, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Glyphs is not { Count: > 0 } glyphs)
        {
            return 0;
        }

        GlyphSetId found = 0;
        foreach (var (stateKey, setKey) in glyphs)
        {
            if (AppearanceStates.TryParse(stateKey, out var s) && s == state && GlyphSets.TryGet(setKey, out var set) && set.Offered && set.Mixable)
            {
                found = set.Id;
            }
        }

        return found;
    }

    private static ShareCodeRead Fail(ShareCodeStatus status, int version, bool prefixed, int length) =>
        new(status, version, prefixed, length, default, []);

    private static bool Known<T>(IReadOnlyList<T> all, int id, Func<T, int> idOf)
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (idOf(all[i]) == id)
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckNibble(int value, string name)
    {
        if ((uint)value > 15)
        {
            throw new ArgumentOutOfRangeException(name, value, "A share code holds ids 0–15.");
        }
    }

    private static void Put(Span<byte> bits, ref int count, int value, int width)
    {
        for (var i = width - 1; i >= 0; i--)
        {
            bits[count++] = (byte)((value >> i) & 1);
        }
    }

    private static int Get(ReadOnlySpan<byte> bits, int start, int width)
    {
        var value = 0;
        for (var i = 0; i < width; i++)
        {
            value = (value << 1) | bits[start + i];
        }

        return value;
    }

    /// <summary>CRC-8, polynomial 0x07, initial 0, one bit at a time, most significant first (the reference's crc8).</summary>
    private static int Crc8(ReadOnlySpan<byte> bits)
    {
        var crc = 0;
        foreach (var bit in bits)
        {
            var top = (crc >> 7) & 1;
            crc = ((crc << 1) & 0xFF) ^ ((top ^ bit) != 0 ? 0x07 : 0);
        }

        return crc;
    }
}

/// <summary>What a share-code change touches.</summary>
public enum ShareChangeKind
{
    Theme,
    Palette,
    Frames,
    HighContrast,

    /// <summary>One state's own pick (<see cref="ShareChange.State"/>).</summary>
    State,
}

/// <summary>
/// One line of the share preview ("Theme Menphina's Medallion → Astrologian's Orrery", "Ready from Aether Crystal"):
/// <paramref name="From"/> and <paramref name="To"/> are a <see cref="ThemeId"/>, <see cref="PaletteId"/> or
/// <see cref="FrameKitId"/> by number, 0 or 1 for high contrast, and a <see cref="GlyphSetId"/> or 0 (from the theme)
/// for a state.
/// </summary>
/// <param name="State">The state, for <see cref="ShareChangeKind.State"/>; otherwise <see cref="QuestState.Ready"/>.</param>
public readonly record struct ShareChange(ShareChangeKind Kind, QuestState State, int From, int To);

/// <summary>What the paste field says about its text (<see cref="SharePreview.Verdict"/>).</summary>
public enum ShareVerdict
{
    /// <summary>Nothing yet: the field is empty, or a code is still being typed.</summary>
    None,

    /// <summary>A code that would change the look: the preview card, with Apply.</summary>
    Preview,

    /// <summary>A code for the look in use already.</summary>
    NothingToChange,

    /// <summary>"That code doesn't read: a character may be mistyped. Nothing was changed."</summary>
    Mistyped,

    /// <summary>"This code is from a newer Tsukimichi."</summary>
    Newer,
}

/// <summary>
/// The Themes page's share preview (spec-1.17 §C2): a pasted text read against the saved appearance, what applying it
/// would save, what that changes and what is left out. Pure; the page rebuilds it only when the text or the saved
/// appearance changes, and applying it saves <see cref="Result"/> with an Undo.
/// </summary>
public sealed class SharePreview
{
    private SharePreview(ShareCodeRead read, AppearanceConfig? result, IReadOnlyList<ShareChange> changes, IReadOnlyList<ShareCodeOmission> leftOut)
    {
        Read = read;
        Result = result;
        Changes = changes;
        LeftOut = leftOut;
    }

    public ShareCodeRead Read { get; }

    /// <summary>What Apply saves; null unless the code read.</summary>
    public AppearanceConfig? Result { get; }

    /// <summary>What would look different (<see cref="ShareCode.Changes"/>); empty unless the code read and changes something.</summary>
    public IReadOnlyList<ShareChange> Changes { get; }

    /// <summary>Every id the code names that this build cannot apply: unknown ones first, then those it does not offer.</summary>
    public IReadOnlyList<ShareCodeOmission> LeftOut { get; }

    /// <summary>Reads <paramref name="text"/> against <paramref name="saved"/>.</summary>
    public static SharePreview Of(string? text, AppearanceConfig saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var read = ShareCode.Decode(text);
        if (!read.Ok)
        {
            return new SharePreview(read, null, [], []);
        }

        var leftOut = new List<ShareCodeOmission>(read.Unknown);
        var result = ShareCode.Apply(saved, read.Look, leftOut);
        return new SharePreview(read, result, ShareCode.Changes(saved, result), leftOut);
    }

    /// <summary>
    /// What the field shows. A code being typed (<paramref name="editing"/>, and shorter than a mix code) says nothing
    /// until it reads or is finished, so typing never flashes an error; a character outside the alphabet or format version
    /// 0 is wrong whatever follows, so it says so at once. "From a newer Tsukimichi" needs the text to start with "TM": a
    /// word that happens to read as a later version is a mistyped code, not a newer one.
    /// </summary>
    public ShareVerdict Verdict(bool editing)
    {
        switch (Read.Status)
        {
            case ShareCodeStatus.Empty:
                return ShareVerdict.None;
            case ShareCodeStatus.Ok:
                return Changes.Count > 0 ? ShareVerdict.Preview : ShareVerdict.NothingToChange;
            case ShareCodeStatus.Unreadable:
                return ShareVerdict.Mistyped;
            case ShareCodeStatus.Newer when Read.Prefixed:
                return ShareVerdict.Newer;
            default:
                return editing && Read.Length < ShareCode.MixLength ? ShareVerdict.None : ShareVerdict.Mistyped;
        }
    }
}
