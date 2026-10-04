using System.Text.Json;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// One character's own settings in <c>user/characters.json</c> (1.8.0, R7 D): what used to live in the Dalamud settings
/// keyed by content id (the spoiler override, the "Before you continue" notices and open "why?" disclosures), plus the
/// alt-list choices (hidden, not tracked, the Compare target). Every game client saves it through a locked merge, field
/// by field, so two clients never overwrite each other's change. Fields a newer build adds are kept as they are
/// (<see cref="Extra"/>), so an older client saving the file does not drop them.
/// </summary>
public sealed class CharacterSettings
{
    /// <summary>Spoiler shield override: true always shields, false shows everything, null follows Settings › Spoilers.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SpoilerShield { get; set; }

    /// <summary>Left out of the character switcher, the Characters list, the Compare picker, the account view and the collection grid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; set; }

    /// <summary>
    /// "Don't track this character": while logged in, nothing of it is written (its existing file stays until Forget).
    /// Forget and "Delete all data" keep it, as they keep <see cref="Hidden"/> (<see cref="Lasting"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DontTrack { get; set; }

    /// <summary>The character the Characters dashboard compares this one with; null takes the first in the list.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? CompareWith { get; set; }

    /// <summary>"Before you continue" gate ids already announced in chat for this character (once ever).</summary>
    [OmitWhenEmpty]
    public List<string> PayoffGatesNoticed { get; set; } = [];

    /// <summary>"Before you continue" gate ids whose "why? (spoiler)" the player opened for this character.</summary>
    [OmitWhenEmpty]
    public List<string> PayoffWhyOpen { get; set; } = [];

    /// <summary>
    /// The available quests the player has seen (plan v7, the Journal badge's "Newly ready", <see cref="Query.NewlyReady"/>),
    /// ascending row ids. Null until the character is first looked at, when it is seeded with everything available; an
    /// empty list is a character with nothing seen. Ids accepted, done or locked out are pruned, so it stays about the size
    /// of the Ready list.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<uint>? SeenReady { get; set; }

    /// <summary>
    /// The <see cref="Query.NewlyReady.RulesVersion"/> <see cref="SeenReady"/> was kept under (null reads as 1, the
    /// first). A set kept under another version is ignored and seeded again (<see cref="CharacterSettingsBook.SeenReady"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SeenReadyRules { get; set; }

    /// <summary>
    /// Quest row ids whose game gate the player marked passed ("I've done this", feature plan v7 C3): a gate
    /// Tsukimichi cannot check reads met for this character (<c>EvalContext.GateMarkedDone</c>). Ascending.
    /// </summary>
    [OmitWhenEmpty]
    public List<uint> GatesDone { get; set; } = [];

    /// <summary>Properties this build does not know (a newer build's), written back unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Nothing set: such an entry is dropped from the file.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        SpoilerShield is null && !Hidden && !DontTrack && CompareWith is null
        && PayoffGatesNoticed.Count == 0 && PayoffWhyOpen.Count == 0 && GatesDone.Count == 0 && SeenReady is null && SeenReadyRules is null && (Extra is null || Extra.Count == 0);

    /// <summary>
    /// What outlives Forget character and "Delete all data": the player's choices about the character itself, hidden
    /// and not tracked, and nothing else (no spoiler override, notices, Compare target or a newer build's fields).
    /// Forgetting an untracked character is how its old file is deleted, and must not start saving it again at its
    /// next login; a hidden one stays out of the lists if it comes back. Null when neither is set.
    /// </summary>
    public CharacterSettings? Lasting() =>
        Hidden || DontTrack ? new CharacterSettings { Hidden = Hidden, DontTrack = DontTrack } : null;

    /// <summary>A deep copy, safe to hand to the background writer while this one keeps changing.</summary>
    public CharacterSettings Copy() => new()
    {
        SpoilerShield = SpoilerShield,
        Hidden = Hidden,
        DontTrack = DontTrack,
        CompareWith = CompareWith,
        PayoffGatesNoticed = [.. PayoffGatesNoticed],
        PayoffWhyOpen = [.. PayoffWhyOpen],
        GatesDone = [.. GatesDone],
        SeenReady = SeenReady is null ? null : [.. SeenReady],
        SeenReadyRules = SeenReadyRules,
        Extra = Extra is null ? null : new Dictionary<string, JsonElement>(Extra, StringComparer.Ordinal),
    };

    /// <summary>Lists a hand-edited file left null read as empty.</summary>
    internal void Normalize()
    {
        PayoffGatesNoticed ??= [];
        PayoffWhyOpen ??= [];
        GatesDone ??= [];
    }
}

/// <summary>Which field one <see cref="CharacterSettingChange"/> sets.</summary>
public enum CharacterSettingField
{
    SpoilerShield,
    Hidden,
    DontTrack,
    CompareWith,

    /// <summary>Adds a gate id to <see cref="CharacterSettings.PayoffGatesNoticed"/> (never removed but with the character).</summary>
    GateNoticed,

    /// <summary>Opens (<see cref="CharacterSettingChange.Flag"/> true) or closes a gate's "why?".</summary>
    WhyOpen,

    /// <summary>
    /// Replaces <see cref="CharacterSettings.SeenReady"/> with <see cref="CharacterSettingChange.RowIds"/> (seeded, pruned
    /// or grown in this client, which is the only one logged in as the character).
    /// </summary>
    SeenReady,

    /// <summary>
    /// Marks the game gate of quest <see cref="CharacterSettingChange.RowIds"/>[0] passed (<see cref="CharacterSettingChange.Flag"/>
    /// true) or takes the mark back (<see cref="CharacterSettings.GatesDone"/>).
    /// </summary>
    GateDone,

    /// <summary>
    /// Forget character: drops the character's entry except <see cref="CharacterSettings.Hidden"/> and
    /// <see cref="CharacterSettings.DontTrack"/> (<see cref="CharacterSettings.Lasting"/>).
    /// </summary>
    Forget,
}

/// <summary>
/// One edit made in this game client. Saves apply edits to the file as it is on disk, field by field (sets one id at a
/// time), so two clients changing different fields of the same character, or adding different gates, keep both edits.
/// </summary>
/// <param name="Flag">The new value of a flag field; for <see cref="CharacterSettingField.WhyOpen"/>, open or closed.</param>
/// <param name="Other">The Compare target for <see cref="CharacterSettingField.CompareWith"/>.</param>
/// <param name="Id">The gate id for <see cref="CharacterSettingField.GateNoticed"/> and <see cref="CharacterSettingField.WhyOpen"/>.</param>
/// <param name="RowIds">The seen set for <see cref="CharacterSettingField.SeenReady"/>, ascending.</param>
public readonly record struct CharacterSettingChange(ulong ContentId, CharacterSettingField Field, bool? Flag = null, ulong? Other = null, string? Id = null, IReadOnlyList<uint>? RowIds = null)
{
    public static CharacterSettingChange Seen(ulong contentId, IReadOnlyList<uint> rowIds) => new(contentId, CharacterSettingField.SeenReady, RowIds: rowIds);

    public static CharacterSettingChange Spoiler(ulong contentId, bool? shield) => new(contentId, CharacterSettingField.SpoilerShield, shield);

    public static CharacterSettingChange Hide(ulong contentId, bool hidden) => new(contentId, CharacterSettingField.Hidden, hidden);

    public static CharacterSettingChange Track(ulong contentId, bool tracked) => new(contentId, CharacterSettingField.DontTrack, !tracked);

    public static CharacterSettingChange Compare(ulong contentId, ulong? other) => new(contentId, CharacterSettingField.CompareWith, Other: other);

    public static CharacterSettingChange Noticed(ulong contentId, string gateId) => new(contentId, CharacterSettingField.GateNoticed, Id: gateId);

    public static CharacterSettingChange Why(ulong contentId, string gateId, bool open) => new(contentId, CharacterSettingField.WhyOpen, open, Id: gateId);

    public static CharacterSettingChange GateDone(ulong contentId, uint questRowId, bool done) => new(contentId, CharacterSettingField.GateDone, done, RowIds: [questRowId]);

    public static CharacterSettingChange Forget(ulong contentId) => new(contentId, CharacterSettingField.Forget);
}

/// <summary>
/// What 1.7 and earlier kept per character in the Dalamud settings, read once and moved into <c>user/characters.json</c>
/// (<see cref="CharacterSettingsFile.MergeLegacy"/>).
/// </summary>
public sealed record LegacyCharacterSettings(
    IReadOnlyDictionary<ulong, bool> SpoilerShield,
    IReadOnlyDictionary<ulong, HashSet<string>> PayoffGatesNoticed,
    IReadOnlyDictionary<ulong, HashSet<string>> PayoffWhyOpen)
{
    public bool IsEmpty => SpoilerShield.Count == 0 && PayoffGatesNoticed.Count == 0 && PayoffWhyOpen.Count == 0;
}

/// <summary>
/// <c>user/characters.json</c>: content id to that character's <see cref="CharacterSettings"/>.
/// Shape: <c>{ "&lt;contentId&gt;": { "spoilerShield": true, "hidden": true, "compareWith": 123, "payoffGatesNoticed": [ "eden" ] } }</c>;
/// every field may be absent. Saved like <c>user/pins.json</c> (D11): under the cross-client lock, read again, this
/// client's edits applied field by field, written atomically.
/// </summary>
public static class CharacterSettingsFile
{
    /// <summary>Loads the file; a missing one yields an empty map, an unreadable one is quarantined and reported in <paramref name="warnings"/>.</summary>
    public static Dictionary<ulong, CharacterSettings> Load(string path, IList<string>? warnings = null) =>
        Clean(UserFile.Load<Dictionary<ulong, CharacterSettings>>(path, warnings));

    /// <summary>Reads the file for a merge into memory; never quarantines. Only <see cref="SharedLoad.Loaded"/> is worth adopting.</summary>
    public static SharedRead<Dictionary<ulong, CharacterSettings>> LoadShared(string path)
    {
        var read = UserFile.LoadShared<Dictionary<ulong, CharacterSettings>>(path);
        return read.Value is { } map ? read with { Value = Clean(map) } : read;
    }

    public static void Save(string path, IReadOnlyDictionary<ulong, CharacterSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AtomicFile.Write(path, JsonSerializer.Serialize(settings, StorageJson.Options));
    }

    /// <summary>A deep copy, safe to hand to the background writer while the original keeps changing.</summary>
    public static Dictionary<ulong, CharacterSettings> Copy(IReadOnlyDictionary<ulong, CharacterSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var copy = new Dictionary<ulong, CharacterSettings>(settings.Count);
        foreach (var (id, entry) in settings)
        {
            copy[id] = entry.Copy();
        }

        return copy;
    }

    /// <summary>
    /// Applies <paramref name="changes"/> in order to <paramref name="settings"/>, field by field; a character left with
    /// nothing set is removed. Applying the same edits twice changes nothing more.
    /// </summary>
    public static void Apply(Dictionary<ulong, CharacterSettings> settings, IEnumerable<CharacterSettingChange> changes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            if (change.Field == CharacterSettingField.Forget)
            {
                // Hidden and not tracked stay: forgetting an untracked character must not track it again.
                if (settings.TryGetValue(change.ContentId, out var forgotten) && forgotten.Lasting() is { } lasting)
                {
                    settings[change.ContentId] = lasting;
                }
                else
                {
                    settings.Remove(change.ContentId);
                }

                continue;
            }

            if (!settings.TryGetValue(change.ContentId, out var entry))
            {
                entry = new CharacterSettings();
                settings[change.ContentId] = entry;
            }

            switch (change.Field)
            {
                case CharacterSettingField.SpoilerShield:
                    entry.SpoilerShield = change.Flag;
                    break;
                case CharacterSettingField.Hidden:
                    entry.Hidden = change.Flag == true;
                    break;
                case CharacterSettingField.DontTrack:
                    entry.DontTrack = change.Flag == true;
                    break;
                case CharacterSettingField.CompareWith:
                    entry.CompareWith = change.Other;
                    break;
                case CharacterSettingField.GateNoticed:
                    AddId(entry.PayoffGatesNoticed, change.Id);
                    break;
                case CharacterSettingField.WhyOpen:
                    if (change.Flag == true)
                    {
                        AddId(entry.PayoffWhyOpen, change.Id);
                    }
                    else if (change.Id is not null)
                    {
                        entry.PayoffWhyOpen.Remove(change.Id);
                    }

                    break;
                case CharacterSettingField.GateDone:
                    if (change.RowIds is [var gateRow])
                    {
                        if (change.Flag == true && !entry.GatesDone.Contains(gateRow))
                        {
                            entry.GatesDone.Add(gateRow);
                            entry.GatesDone.Sort();
                        }
                        else if (change.Flag != true)
                        {
                            entry.GatesDone.Remove(gateRow);
                        }
                    }

                    break;
                case CharacterSettingField.SeenReady:
                    entry.SeenReady = change.RowIds is null ? null : [.. change.RowIds];
                    entry.SeenReadyRules = change.RowIds is null ? null : Query.NewlyReady.RulesVersion;
                    break;
            }

            if (entry.IsEmpty)
            {
                settings.Remove(change.ContentId);
            }
        }
    }

    /// <summary>
    /// Saves edits made in this client without losing any other client's (D11): under <see cref="SharedFile.Lock"/> the
    /// file is read again, <paramref name="changes"/> are applied to it (<see cref="Apply"/>), and the result is written
    /// and returned for the caller to adopt. A file that exists but cannot be read throws rather than being replaced;
    /// one that cannot be parsed is quarantined and <paramref name="fallback"/> (this client's own map, edits included)
    /// stands in for it.
    /// </summary>
    public static Dictionary<ulong, CharacterSettings> SaveChanges(
        string path,
        IReadOnlyList<CharacterSettingChange> changes,
        IReadOnlyDictionary<ulong, CharacterSettings> fallback,
        IList<string>? warnings = null,
        TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(fallback);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<ulong, CharacterSettings>>(path, warnings, out var invalid);
            var merged = invalid ? Copy(fallback) : Clean(disk);
            Apply(merged, changes);
            Save(path, merged);
            return merged;
        }
    }

    /// <summary>
    /// The one-time move from the Dalamud settings (1.8.0): under the lock, the file on disk takes what
    /// <paramref name="legacy"/> holds, without overriding anything already there. A spoiler override fills only a
    /// character with none (another client may have moved it, or changed it since); notices and open disclosures are
    /// added to the character's sets. Safe to run in two clients at once, and again. Returns the merged map.
    /// </summary>
    public static Dictionary<ulong, CharacterSettings> MergeLegacy(string path, LegacyCharacterSettings legacy, IList<string>? warnings = null, TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = UserFile.LoadForMerge<Dictionary<ulong, CharacterSettings>>(path, warnings, out _);
            var merged = Clean(disk);
            Apply(merged, LegacyChanges(legacy, merged));
            Save(path, merged);
            return merged;
        }
    }

    /// <summary>
    /// The edits that move <paramref name="legacy"/> onto <paramref name="current"/>: a spoiler override only where
    /// <paramref name="current"/> has none, every notice and open disclosure (adding is idempotent).
    /// </summary>
    public static List<CharacterSettingChange> LegacyChanges(LegacyCharacterSettings legacy, IReadOnlyDictionary<ulong, CharacterSettings> current)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(current);
        var changes = new List<CharacterSettingChange>();
        foreach (var (id, shield) in legacy.SpoilerShield)
        {
            if (!(current.TryGetValue(id, out var entry) && entry.SpoilerShield is not null))
            {
                changes.Add(CharacterSettingChange.Spoiler(id, shield));
            }
        }

        foreach (var (id, gates) in legacy.PayoffGatesNoticed)
        {
            foreach (var gate in Sorted(gates))
            {
                changes.Add(CharacterSettingChange.Noticed(id, gate));
            }
        }

        foreach (var (id, gates) in legacy.PayoffWhyOpen)
        {
            foreach (var gate in Sorted(gates))
            {
                changes.Add(CharacterSettingChange.Why(id, gate, open: true));
            }
        }

        return changes;
    }

    /// <summary>
    /// "Delete all data" (D11): under the lock, rewrites the file with what
    /// <see cref="KeepOnly(IReadOnlyDictionary{ulong, CharacterSettings}, Func{ulong, bool})"/> leaves of it. Returns
    /// what was kept.
    /// </summary>
    public static Dictionary<ulong, CharacterSettings> KeepOnly(string path, Func<ulong, bool> keep, IList<string>? warnings = null, TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(keep);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var kept = KeepOnly(Clean(UserFile.LoadForMerge<Dictionary<ulong, CharacterSettings>>(path, warnings, out _)), keep);
            Save(path, kept);
            return kept;
        }
    }

    /// <summary>
    /// What "Delete all data" leaves of <paramref name="settings"/>: every setting of the characters <paramref name="keep"/>
    /// selects (those live in another game client, whose settings that client still uses), and of every other one only
    /// its hidden and not-tracked choices (<see cref="CharacterSettings.Lasting"/>): deleting everything must not start
    /// saving a character the player chose not to track, the one logged in here included. A new map; the entries kept
    /// in full are shared with <paramref name="settings"/>.
    /// </summary>
    public static Dictionary<ulong, CharacterSettings> KeepOnly(IReadOnlyDictionary<ulong, CharacterSettings> settings, Func<ulong, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keep);
        var kept = new Dictionary<ulong, CharacterSettings>();
        foreach (var (id, entry) in settings)
        {
            if (keep(id))
            {
                kept[id] = entry;
            }
            else if (entry.Lasting() is { } lasting)
            {
                kept[id] = lasting;
            }
        }

        return kept;
    }

    private static IEnumerable<string> Sorted(HashSet<string>? ids) =>
        ids is null ? [] : ids.Where(static g => !string.IsNullOrEmpty(g)).Order(StringComparer.Ordinal);

    private static void AddId(List<string> list, string? id)
    {
        if (!string.IsNullOrEmpty(id) && !list.Contains(id, StringComparer.Ordinal))
        {
            list.Add(id);
        }
    }

    /// <summary>A loaded map with null entries dropped and null lists made empty; null reads as empty.</summary>
    private static Dictionary<ulong, CharacterSettings> Clean(Dictionary<ulong, CharacterSettings>? map)
    {
        if (map is null)
        {
            return [];
        }

        foreach (var key in map.Where(static p => p.Value is null).Select(static p => p.Key).ToList())
        {
            map.Remove(key);
        }

        foreach (var entry in map.Values)
        {
            entry.Normalize();
        }

        return map;
    }
}
