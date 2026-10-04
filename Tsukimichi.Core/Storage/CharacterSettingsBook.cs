namespace Tsukimichi.Core.Storage;

/// <summary>
/// The per-character settings this game client holds (<c>user/characters.json</c>, 1.8.0), with its edits not saved
/// yet. Edits apply here at once and are saved on the background writer through
/// <see cref="CharacterSettingsFile.SaveChanges"/>, one save in flight at a time; another client's save is merged in by
/// <see cref="ReloadFromDisk"/> with this client's edits kept on top. Framework thread only (the writer's completions
/// run there too), like the pins the query runner keeps.
/// </summary>
public sealed class CharacterSettingsBook
{
    private static readonly IReadOnlyList<string> NoIds = [];

    private readonly string path;
    private readonly SerialWriter? writer;
    private readonly Action<string, Exception?>? warn;
    private readonly List<CharacterSettingChange> pending = [];
    private Dictionary<ulong, CharacterSettings> map = [];
    private List<CharacterSettingChange>? inFlight;

    // Bumped by Reset ("Delete all data"): a save or a read queued before it is dropped when it lands.
    private int generation;

    /// <param name="path">The file, normally <see cref="PluginPaths.CharacterSettingsFile"/>.</param>
    /// <param name="writer">The background queue; null saves and reads at once on the calling thread (tests).</param>
    /// <param name="warn">Receives a problem to log (and the exception, when there is one).</param>
    public CharacterSettingsBook(string path, SerialWriter? writer = null, Action<string, Exception?>? warn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
        this.writer = writer;
        this.warn = warn;
    }

    /// <summary>Bumps whenever what this book answers changed (an edit here, or another client's merged in).</summary>
    public int Version { get; private set; }

    /// <summary>Raised after <see cref="Version"/> moved, with whether any spoiler override changed (the masks rebuild then).</summary>
    public event Action<bool>? Changed;

    /// <summary>Edits not written yet (queued or in flight).</summary>
    public int PendingCount => pending.Count + (inFlight?.Count ?? 0);

    /// <summary>Every character with settings, as held here (read only).</summary>
    public IReadOnlyDictionary<ulong, CharacterSettings> All => map;

    /// <summary>Reads the file as it is; problems go to the warning callback. Framework thread, at load.</summary>
    public void Load()
    {
        var warnings = new List<string>();
        Adopt(CharacterSettingsFile.Load(path, warnings));
        Warn(warnings);
    }

    public CharacterSettings? Get(ulong contentId) => map.TryGetValue(contentId, out var entry) ? entry : null;

    /// <summary>The character's spoiler override; null follows Settings › Spoilers.</summary>
    public bool? SpoilerShield(ulong contentId) => Get(contentId)?.SpoilerShield;

    public bool IsHidden(ulong contentId) => Get(contentId)?.Hidden == true;

    /// <summary>False after "Don't track this character": nothing of it is written while it is logged in.</summary>
    public bool IsTracked(ulong contentId) => Get(contentId)?.DontTrack != true;

    public ulong? CompareWith(ulong contentId) => Get(contentId)?.CompareWith;

    /// <summary>The gate ids announced in chat for the character.</summary>
    public IReadOnlyList<string> Noticed(ulong contentId) => Get(contentId)?.PayoffGatesNoticed ?? NoIds;

    /// <summary>Every character's "I've done this" gate marks: content id to quest row ids, characters without one left out.</summary>
    public Dictionary<ulong, IReadOnlyList<uint>> GatesDoneByCharacter() =>
        map.Where(kv => kv.Value.GatesDone.Count > 0).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<uint>)[.. kv.Value.GatesDone]);

    /// <summary>Whether the player marked the game gate of <paramref name="questRowId"/> passed on the character ("I've done this").</summary>
    public bool IsGateDone(ulong contentId, uint questRowId) => Get(contentId)?.GatesDone.Contains(questRowId) == true;

    /// <summary>Every character's "Go with the game" choices: content id to quest row ids, characters without one left out.</summary>
    public Dictionary<ulong, IReadOnlyList<uint>> GoWithGameByCharacter() =>
        map.Where(kv => kv.Value.GoWithGame.Count > 0).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<uint>)[.. kv.Value.GoWithGame]);

    /// <summary>Whether the player chose "Go with the game" for <paramref name="questRowId"/> on the character.</summary>
    public bool IsGoWithGame(ulong contentId, uint questRowId) => Get(contentId)?.GoWithGame.Contains(questRowId) == true;

    /// <summary>Whether the player hid the card <paramref name="cardId"/> for the character (<see cref="CharacterSettings.CardsDismissed"/>).</summary>
    public bool IsCardDismissed(ulong contentId, string cardId) =>
        Get(contentId)?.CardsDismissed.Contains(cardId, StringComparer.Ordinal) == true;

    public bool IsWhyOpen(ulong contentId, string gateId) =>
        Get(contentId)?.PayoffWhyOpen.Contains(gateId, StringComparer.Ordinal) == true;

    /// <summary>
    /// The available quests the player has seen (the Journal badge's "Newly ready"); null before the character's first
    /// look, and for a set kept under another <see cref="Query.NewlyReady.RulesVersion"/>, which is seeded again.
    /// </summary>
    public IReadOnlyList<uint>? SeenReady(ulong contentId) =>
        Get(contentId) is { SeenReady: { } seen } entry && (entry.SeenReadyRules ?? 1) == Query.NewlyReady.RulesVersion ? seen : null;

    /// <summary>Applies one edit here and queues its save.</summary>
    public void Edit(CharacterSettingChange change) => Edit([change]);

    /// <summary>Applies the edits here (in order) and queues their save. Nothing happens when they change nothing.</summary>
    public void Edit(IReadOnlyList<CharacterSettingChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            return;
        }

        var next = CharacterSettingsFile.Copy(map);
        CharacterSettingsFile.Apply(next, changes);
        if (Same(map, next))
        {
            return;
        }

        pending.AddRange(changes);
        Adopt(next);
        Save();
    }

    /// <summary>
    /// Queues the edits made here on the background writer: applied to the file as it is on disk, field by field, under
    /// the cross-client lock. One save is in flight at a time (edits made meanwhile follow it), except at unload
    /// (<paramref name="final"/>), when the writer runs both in order.
    /// </summary>
    public void Save(bool final = false)
    {
        if (pending.Count == 0 || (inFlight is not null && !final))
        {
            return;
        }

        var changes = new List<CharacterSettingChange>(pending);
        pending.Clear();
        inFlight = changes;
        var fallback = CharacterSettingsFile.Copy(map);
        var ticket = generation;
        var warnings = new List<string>();
        SerialWriter.Submit(writer, () => CharacterSettingsFile.SaveChanges(path, changes, fallback, warnings), (merged, error) =>
        {
            if (ticket != generation)
            {
                return;
            }

            if (ReferenceEquals(inFlight, changes))
            {
                inFlight = null;
            }

            Warn(warnings);
            if (error is not null || merged is null)
            {
                // Kept: the next edit (or unload) tries again.
                pending.InsertRange(0, changes);
                warn?.Invoke("Character settings could not be saved", error);
                return;
            }

            // The saved file, with the edits made here since it was queued.
            CharacterSettingsFile.Apply(merged, pending);
            Adopt(merged);
            if (pending.Count > 0)
            {
                Save();
            }
        });
    }

    /// <summary>
    /// Multibox (D11): <c>user/characters.json</c> changed on disk (another client saved). The file is read on the
    /// background writer, after any save queued before, and this client's edits not saved yet are applied on top. A file
    /// that cannot be read or parsed right now, or that is missing, leaves what is held here as it is.
    /// </summary>
    public void ReloadFromDisk()
    {
        var ticket = generation;
        SerialWriter.Submit(writer, () => CharacterSettingsFile.LoadShared(path), (read, error) =>
        {
            if (ticket != generation || error is not null || !read.IsLoaded)
            {
                return;
            }

            var merged = read.Value!;
            if (inFlight is { } flying)
            {
                CharacterSettingsFile.Apply(merged, flying);
            }

            CharacterSettingsFile.Apply(merged, pending);
            Adopt(merged);
        });
    }

    /// <summary>
    /// The one-time move of the settings 1.7 kept in the Dalamud configuration. They show here at once; the file takes
    /// them on the writer (<see cref="CharacterSettingsFile.MergeLegacy"/>), and <paramref name="done"/> runs with true
    /// once they are on disk (the caller then empties the old copy), false when the save failed (tried again next load).
    /// </summary>
    public void MigrateLegacy(LegacyCharacterSettings legacy, Action<bool>? done = null)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.IsEmpty)
        {
            done?.Invoke(true);
            return;
        }

        var shown = CharacterSettingsFile.Copy(map);
        CharacterSettingsFile.Apply(shown, CharacterSettingsFile.LegacyChanges(legacy, shown));
        Adopt(shown);

        var ticket = generation;
        var warnings = new List<string>();
        SerialWriter.Submit(writer, () => CharacterSettingsFile.MergeLegacy(path, legacy, warnings), (merged, error) =>
        {
            Warn(warnings);
            if (error is not null || merged is null)
            {
                warn?.Invoke("Per-character settings could not be moved to user/characters.json; they stay in the settings for now", error);
                done?.Invoke(false);
                return;
            }

            if (ticket == generation)
            {
                if (inFlight is { } flying)
                {
                    CharacterSettingsFile.Apply(merged, flying);
                }

                CharacterSettingsFile.Apply(merged, pending);
                Adopt(merged);
            }

            done?.Invoke(true);
        });
    }

    /// <summary>
    /// "Delete all data": every character's settings go except those <paramref name="keep"/> selects (characters live
    /// in another game client), and every character keeps its hidden and not-tracked choices
    /// (<see cref="CharacterSettingsFile.KeepOnly(IReadOnlyDictionary{ulong, CharacterSettings}, Func{ulong, bool})"/>):
    /// an untracked character logged in here is not written at once. Edits not saved yet are dropped; the file is
    /// rewritten under the lock on the writer.
    /// </summary>
    public void Reset(Func<ulong, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        generation++;
        pending.Clear();
        inFlight = null;
        Adopt(CharacterSettingsFile.KeepOnly(map, keep));
        var ticket = generation;
        var warnings = new List<string>();
        SerialWriter.Submit(writer, () => CharacterSettingsFile.KeepOnly(path, keep, warnings), (merged, error) =>
        {
            Warn(warnings);
            if (error is not null)
            {
                warn?.Invoke("Delete all data: character settings could not be reset", error);
                return;
            }

            if (ticket == generation && merged is not null)
            {
                CharacterSettingsFile.Apply(merged, pending);
                Adopt(merged);
            }
        });
    }

    /// <summary>Takes a map as the one held here; bumps and raises <see cref="Changed"/> when it reads differently.</summary>
    private void Adopt(Dictionary<ulong, CharacterSettings> next)
    {
        if (Same(map, next))
        {
            map = next;
            return;
        }

        var spoilers = !SameSpoilers(map, next);
        map = next;
        Version++;
        Changed?.Invoke(spoilers);
    }

    private void Warn(List<string> warnings)
    {
        foreach (var warning in warnings)
        {
            warn?.Invoke("Character settings: " + warning, null);
        }
    }

    private static bool SameSpoilers(IReadOnlyDictionary<ulong, CharacterSettings> a, IReadOnlyDictionary<ulong, CharacterSettings> b)
    {
        foreach (var (id, entry) in a)
        {
            if ((b.TryGetValue(id, out var other) ? other.SpoilerShield : null) != entry.SpoilerShield)
            {
                return false;
            }
        }

        foreach (var (id, entry) in b)
        {
            if (entry.SpoilerShield is not null && !a.ContainsKey(id))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameIds(List<uint>? a, List<uint>? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.SequenceEqual(b));

    /// <summary>The same characters with the same settings (lists compared in order; extra fields not compared).</summary>
    private static bool Same(IReadOnlyDictionary<ulong, CharacterSettings> a, IReadOnlyDictionary<ulong, CharacterSettings> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (id, x) in a)
        {
            if (!b.TryGetValue(id, out var y)
                || x.SpoilerShield != y.SpoilerShield
                || x.Hidden != y.Hidden
                || x.DontTrack != y.DontTrack
                || x.CompareWith != y.CompareWith
                || !x.PayoffGatesNoticed.SequenceEqual(y.PayoffGatesNoticed, StringComparer.Ordinal)
                || !x.PayoffWhyOpen.SequenceEqual(y.PayoffWhyOpen, StringComparer.Ordinal)
                || !x.GatesDone.SequenceEqual(y.GatesDone)
                || !x.GoWithGame.SequenceEqual(y.GoWithGame)
                || !x.CardsDismissed.SequenceEqual(y.CardsDismissed, StringComparer.Ordinal)
                || !SameIds(x.SeenReady, y.SeenReady)
                || x.SeenReadyRules != y.SeenReadyRules)
            {
                return false;
            }
        }

        return true;
    }
}
