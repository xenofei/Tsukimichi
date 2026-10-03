using System.Collections.Frozen;
using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// Everything each quest opens, in one index built once per catalog (feature plan v6 K1): areas and world-map regions,
/// aetherytes, duties, features and systems, jobs, flying, actions and emotes, collectables and next quests. The
/// detail pane's Unlocks section, the table's tooltip and Opens column, the game panels, the Todo overlay, Path, Moonlit
/// and the chat lines all read it.
/// <para>
/// Sources, from most to least trusted (one row per target; the most trusted wins, <see cref="UnlockTargets.Trust"/>):
/// </para>
/// <list type="number">
/// <item>curated: <c>duty_unlocks.json</c> and <c>system_unlocks.json</c> (through the unique-reward data), and
/// <c>aetheryte_unlocks.json</c>, which replaces whatever the first-visit rule inferred for its aetherytes;</item>
/// <item>the sheets (<see cref="UnlockLinks"/>): quest-gated warps, world-map regions, aethernet gates;</item>
/// <item>the unique-reward data (<see cref="UniqueRewardCatalog"/>, without the user's verdicts, as
/// <see cref="UnlockTags"/> reads it) and the quest's own rewards: duties, jobs, flying, features, actions, emotes,
/// collectables;</item>
/// <item>the first-visit rule (<see cref="UnlockAreas"/>): zones and aetherytes the main scenario first reaches;</item>
/// <item>next quests: the quests this one is a true prerequisite of (a lock-only dependent is none).</item>
/// </list>
/// Dedupe: an instance and the duty unlock of one duty are one row, and so are a deep dungeon's floor sets (one row
/// named for the dungeon); a curated feature named like a duty of the quest is that duty; a zone a warp opens and the
/// rule also finds is one Sheet row; two rows of one kind with one name ("Collect", a world map and its place name)
/// are one row; a feature named like an action of the quest is that action ("Aether Compass").
/// <para>
/// The split (<see cref="RewardSplit"/>): the quest's own unlock-class rewards (an instance, a job, an action, a
/// general action, an aether current, a named feature) are Unlocks rows, and no Rewards surface draws them; a
/// reward-class row (an emote, a mount, a title) is marked <see cref="UnlockEntry.InRewards"/> and left out of
/// <see cref="For"/>, as is any row one of the quest's reward-class rewards names (<see cref="UnlockRewards"/>), so no
/// surface draws a thing twice. The reward-class rows the quest's own list does not carry (a title the reward data
/// names) are <see cref="ExtraRewards"/>, which the Rewards tiles draw. Order: by group (<see cref="UnlockGroup"/>), by
/// target, by expansion, by the target's own order (a duty's level, a zone's sheet order), by name; next quests last,
/// in catalog order.
/// </para>
/// Immutable; reads allocate nothing.
/// </summary>
public sealed class QuestUnlocks
{
    public static readonly QuestUnlocks Empty = new(
        null,
        FrozenDictionary<uint, UnlockEntry[]>.Empty,
        FrozenDictionary<uint, UnlockEntry[]>.Empty,
        FrozenDictionary<uint, UnlockEntry[]>.Empty,
        FrozenDictionary<(UnlockTarget, uint), uint[]>.Empty);

    private static readonly UnlockEntry[] NoEntries = [];
    private static readonly uint[] NoQuests = [];

    private readonly FrozenDictionary<uint, UnlockEntry[]> byQuest;
    private readonly FrozenDictionary<uint, UnlockEntry[]> withRewards;
    private readonly FrozenDictionary<uint, UnlockEntry[]> extraRewards;
    private readonly FrozenDictionary<(UnlockTarget, uint), uint[]> byTarget;

    private QuestUnlocks(
        QuestCatalog? catalog,
        FrozenDictionary<uint, UnlockEntry[]> byQuest,
        FrozenDictionary<uint, UnlockEntry[]> withRewards,
        FrozenDictionary<uint, UnlockEntry[]> extraRewards,
        FrozenDictionary<(UnlockTarget, uint), uint[]> byTarget)
    {
        Catalog = catalog;
        this.byQuest = byQuest;
        this.withRewards = withRewards;
        this.extraRewards = extraRewards;
        this.byTarget = byTarget;
    }

    /// <summary>The catalog the index was built over; null for <see cref="Empty"/>.</summary>
    public QuestCatalog? Catalog { get; }

    /// <summary>How many quests open anything, counting what their Rewards already show.</summary>
    public int Count => withRewards.Count;

    /// <summary>Every quest that opens anything, by row id, counting what their Rewards already show.</summary>
    public IEnumerable<uint> Quests => withRewards.Keys;

    /// <summary>
    /// What the quest opens, grouped and in display order, without the rows its Rewards already show
    /// (<see cref="UnlockEntry.InRewards"/>): what every surface draws. Empty when nothing.
    /// </summary>
    public IReadOnlyList<UnlockEntry> For(uint rowId) => byQuest.GetValueOrDefault(rowId) ?? NoEntries;

    /// <summary>
    /// What the quest opens with the rows its Rewards already show (marked <see cref="UnlockEntry.InRewards"/>), in
    /// display order: for the reverse lookup and the tests, never drawn as it is. Empty when nothing.
    /// </summary>
    public IReadOnlyList<UnlockEntry> IncludingRewards(uint rowId) => withRewards.GetValueOrDefault(rowId) ?? NoEntries;

    /// <summary>
    /// The reward-class rows (<see cref="RewardSplit"/>) the quest's own reward list does not carry: a title, or a
    /// collectable the reward data names that no reward slot holds. Never an Unlocks row; the detail pane's Rewards
    /// tiles draw them after the quest's own. Empty when none.
    /// </summary>
    public IReadOnlyList<UnlockEntry> ExtraRewards(uint rowId) => extraRewards.GetValueOrDefault(rowId) ?? NoEntries;

    /// <summary>
    /// The quests that open a target ("which quest opens Kugane?"), in catalog order, a quest whose Rewards show it
    /// included; empty when none.
    /// </summary>
    public IReadOnlyList<uint> UnlockedBy(UnlockTarget target, uint targetId) => byTarget.GetValueOrDefault((target, targetId)) ?? NoQuests;

    /// <summary>The groups the quest has rows in, one bit each (<see cref="UnlockTargets.Bit"/>).</summary>
    public ushort GroupMask(uint rowId)
    {
        ushort mask = 0;
        foreach (var entry in For(rowId))
        {
            mask |= UnlockTargets.Bit(entry.Group);
        }

        return mask;
    }

    /// <summary>
    /// The one row a tooltip, an overlay line or a verdict names: the first row in display order the game or a curated
    /// file states, else the first inferred one; never a next quest. Null when the quest opens nothing else.
    /// </summary>
    public UnlockEntry? Headline(uint rowId)
    {
        UnlockEntry? likely = null;
        foreach (var entry in For(rowId))
        {
            if (entry.Target == UnlockTarget.NextQuest)
            {
                break;
            }

            if (!entry.IsLikely)
            {
                return entry;
            }

            likely ??= entry;
        }

        return likely;
    }

    /// <summary>The rows' names, lowercased, one per line, next quests left out: what a search could match.</summary>
    public string SearchText(uint rowId)
    {
        var names = new List<string>();
        foreach (var entry in For(rowId))
        {
            if (entry.Target != UnlockTarget.NextQuest)
            {
                names.Add(entry.Name.ToLowerInvariant());
            }
        }

        return string.Join('\n', names);
    }

    /// <summary>
    /// Builds the index.
    /// </summary>
    /// <param name="catalog">The quest catalog.</param>
    /// <param name="rewards">
    /// The unique-reward catalog with the curated unlocks merged in, without the user's verdicts (as
    /// <see cref="UnlockTags.Build"/> takes it); entries from the user are skipped either way.
    /// </param>
    /// <param name="duties">Duty kinds and names; <see cref="PlanDuties.Empty"/> files every duty under Other duty with the reward data's name.</param>
    /// <param name="links">What the sheets know; <see cref="UnlockLinks.Empty"/> leaves areas and aetherytes out.</param>
    /// <param name="curated">The curated overlay, for the aetheryte overrides and the system unlocks' notes; null for none.</param>
    public static QuestUnlocks Build(QuestCatalog catalog, UniqueRewardCatalog rewards, PlanDuties duties, UnlockLinks links, CuratedData? curated = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(duties);
        ArgumentNullException.ThrowIfNull(links);
        if (catalog.Count == 0)
        {
            return Empty;
        }

        var builder = new Builder(catalog, duties, links, curated);
        builder.AddAreas(UnlockAreas.Derive(catalog, links));
        builder.AddRewards(rewards);
        builder.AddNextQuests();
        return builder.Finish();
    }

    /// <summary>Collects every quest's rows, deduping as it goes.</summary>
    private sealed class Builder
    {
        private readonly QuestCatalog catalog;
        private readonly PlanDuties duties;
        private readonly UnlockLinks links;
        private readonly CuratedData? curated;
        private readonly Dictionary<uint, UnlockZone> zones = [];
        private readonly Dictionary<uint, UnlockAetheryte> aetherytes = [];
        private readonly Dictionary<uint, UnlockDuty> dutyInfo = [];
        private readonly Dictionary<uint, Rows> byQuest = [];
        private readonly Dictionary<uint, List<UnlockEntry>> nextQuests = [];

        public Builder(QuestCatalog catalog, PlanDuties duties, UnlockLinks links, CuratedData? curated)
        {
            this.catalog = catalog;
            this.duties = duties;
            this.links = links;
            this.curated = curated;
            foreach (var zone in links.Zones)
            {
                zones.TryAdd(zone.TerritoryId, zone);
            }

            foreach (var aetheryte in links.Aetherytes)
            {
                aetherytes.TryAdd(aetheryte.AetheryteId, aetheryte);
            }

            foreach (var duty in links.Duties)
            {
                dutyInfo.TryAdd(duty.ContentFinderConditionId, duty);
            }
        }

        private Rows RowsOf(uint rowId)
        {
            if (!byQuest.TryGetValue(rowId, out var rows))
            {
                rows = new Rows();
                byQuest[rowId] = rows;
            }

            return rows;
        }

        // ------------------------------------------------------------------ areas and aetherytes

        public void AddAreas(UnlockAreas.Result derived)
        {
            foreach (var (rowId, territories) in derived.Warps)
            {
                if (!catalog.ByRowId.ContainsKey(rowId))
                {
                    continue;
                }

                foreach (var territory in territories)
                {
                    if (Zone(territory, UnlockSource.Sheet) is { } entry)
                    {
                        RowsOf(rowId).Add(entry);
                    }
                }
            }

            foreach (var region in links.MapRegions)
            {
                if (region.Name.Length == 0 || !catalog.ByRowId.TryGetValue(region.QuestRowId, out var revealer))
                {
                    continue;
                }

                // A region map belongs to no zone's expansion; it is no older than the quest that reveals it.
                var expansion = Math.Max(region.Expansion, revealer.Expansion);
                RowsOf(region.QuestRowId).Add(new UnlockEntry(UnlockTarget.WorldMap, region.PlaceNameId, region.Name, links.AreaIcon, UnlockSource.Sheet, (byte)expansion, SortKey: region.PlaceNameId));
            }

            foreach (var (rowId, territories) in derived.Zones)
            {
                foreach (var territory in territories)
                {
                    if (Zone(territory, UnlockSource.Derived) is { } entry)
                    {
                        RowsOf(rowId).Add(entry);
                    }
                }
            }

            foreach (var gate in links.GatedAethernet)
            {
                if (catalog.ByRowId.ContainsKey(gate.QuestRowId) && Aetheryte(gate.AetheryteId, UnlockSource.Sheet, null) is { } entry)
                {
                    RowsOf(gate.QuestRowId).Add(entry);
                }
            }

            // Curated aetherytes replace whatever the rule inferred for them.
            var overridden = curated?.AetheryteUnlocks ?? new Dictionary<uint, AetheryteUnlock>();
            foreach (var (rowId, ids) in derived.Aetherytes)
            {
                foreach (var id in ids)
                {
                    if (!overridden.ContainsKey(id) && Aetheryte(id, UnlockSource.Derived, null) is { } entry)
                    {
                        RowsOf(rowId).Add(entry);
                    }
                }
            }

            foreach (var (id, unlock) in overridden)
            {
                foreach (var rowId in unlock.Quests)
                {
                    if (catalog.ByRowId.ContainsKey(rowId) && Aetheryte(id, UnlockSource.Curated, unlock.Note) is { } entry)
                    {
                        RowsOf(rowId).Add(entry);
                    }
                }
            }
        }

        private UnlockEntry? Zone(uint territoryId, UnlockSource source)
        {
            if (!zones.TryGetValue(territoryId, out var zone) || zone.Name.Length == 0)
            {
                return null;
            }

            return new UnlockEntry(UnlockTarget.Zone, territoryId, zone.Name, links.AreaIcon, source, zone.Expansion, zone.SortKey, territoryId, zone.Region);
        }

        private UnlockEntry? Aetheryte(uint aetheryteId, UnlockSource source, string? note)
        {
            if (!aetherytes.TryGetValue(aetheryteId, out var aetheryte) || aetheryte.Name.Length == 0)
            {
                return null;
            }

            var zone = zones.GetValueOrDefault(aetheryte.TerritoryId);
            var target = aetheryte.IsAetheryte ? UnlockTarget.Aetheryte : UnlockTarget.AethernetShard;
            return new UnlockEntry(
                target,
                aetheryteId,
                aetheryte.Name,
                aetheryte.IsAetheryte ? links.AetheryteIcon : links.AethernetIcon,
                source,
                zone?.Expansion ?? 0,
                zone?.SortKey ?? 0,
                aetheryte.TerritoryId,
                zone?.Name ?? string.Empty,
                note);
        }

        // ------------------------------------------------------------------ rewards

        public void AddRewards(UniqueRewardCatalog rewards)
        {
            foreach (var quest in catalog.All)
            {
                foreach (var entry in rewards.ForQuest(quest.RowId))
                {
                    if (string.Equals(entry.Source, UniqueRewardCatalog.UserSource, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var source = entry.Confidence >= Confidence.Curated ? UnlockSource.Curated : UnlockSource.Sheet;
                    AddReward(quest, entry.Kind, entry.RewardId, entry.ItemId, entry.RewardName, source, 0, inRewards: false);
                }

                foreach (var reward in quest.Rewards)
                {
                    // Currency rewards (an item id) and unresolved slots (no name) are no unlock.
                    if (reward.Kind == RewardKind.Other && (reward.ItemId != 0 || reward.Name.Length == 0))
                    {
                        continue;
                    }

                    // The split (RewardSplit): a duty, a job, an action, flying or a feature is an Unlocks row and no
                    // Rewards tile; an emote is a Rewards tile, its row kept for the reverse lookup and never drawn. A
                    // soul crystal is a tile too, and the job it carries is the job row.
                    var kept = RewardSplit.IsReward(reward);
                    if (kept && reward.Kind == RewardKind.Other)
                    {
                        continue;
                    }

                    AddReward(quest, reward.Kind, reward.Id, reward.ItemId, reward.Name, UnlockSource.Sheet, reward.Icon, inRewards: kept);
                }
            }
        }

        private void AddReward(QuestRecord quest, RewardKind kind, uint id, uint itemId, string name, UnlockSource source, uint icon, bool inRewards)
        {
            if (icon == 0)
            {
                icon = IconFromRewards(quest, kind, id, itemId);
            }

            if (icon == 0 && id != 0)
            {
                // An action, trait or general action only the reward data names: the sheet's own icon.
                icon = links.ActionIcons.GetValueOrDefault((kind, id));
            }

            switch (kind)
            {
                case RewardKind.DutyUnlock:
                    AddDuty(quest, duties.TryGetCondition(id, out var duty) ? duty : null, id, name, source, kind, icon, inRewards);
                    return;

                case RewardKind.Instance:
                    if (duties.TryGetInstance(id, out var instance) || duties.TryGetByName(name, out instance))
                    {
                        AddDuty(quest, instance, instance.ContentFinderConditionId, name, source, RewardKind.DutyUnlock, icon, inRewards);
                    }
                    else if (name.Length > 0)
                    {
                        AddDuty(quest, null, 0, name, source, null, icon, inRewards);
                    }

                    return;

                case RewardKind.ClassJob:
                    if (name.Length > 0)
                    {
                        RowsOf(quest.RowId).Add(new UnlockEntry(UnlockTarget.Job, id, NameCase.Title(name), id != 0 ? JobIconBase + id : icon, source, quest.Expansion, id)
                        {
                            Reward = kind,
                            InRewards = inRewards,
                        });
                    }

                    return;

                case RewardKind.AetherCurrent:
                    var zoneName = CurrentZone(name);
                    if (zoneName.Length > 0)
                    {
                        // The reward kind lets UnlockRewards match the quest's "Aether Current" reward tile.
                        RowsOf(quest.RowId).Add(new UnlockEntry(UnlockTarget.Flying, 0, zoneName, AetherCurrentIcon, source, quest.Expansion, Detail: FlyingDetail)
                        {
                            Reward = kind,
                            InRewards = inRewards,
                        });
                    }

                    return;

                case RewardKind.SystemUnlock:
                    AddFeature(quest, name, source, icon, inRewards);
                    return;

                case RewardKind.Other when id == UnlockRewards.AetherCurrentOtherReward:
                    // The quest's "Aether Current" reward is flying in the zone the reward data names, whose row is
                    // already there; a quest the data names no zone for keeps the reward's own flying row.
                    if (name.Length > 0 && !RowsOf(quest.RowId).Has(static e => e.Target == UnlockTarget.Flying && e.Reward == RewardKind.AetherCurrent))
                    {
                        RowsOf(quest.RowId).Add(new UnlockEntry(UnlockTarget.Flying, 0, name, icon != 0 ? icon : AetherCurrentIcon, source, quest.Expansion, Detail: FlyingDetail)
                        {
                            Reward = RewardKind.AetherCurrent,
                            InRewards = inRewards,
                        });
                    }

                    return;

                case RewardKind.Other:
                    // A named Quest.OtherReward: Wondrous Tails, the Aether Compass, Spearfishing, with the reward's icon.
                    AddFeature(quest, name, UnlockSource.Sheet, icon, inRewards);
                    return;
            }

            if (UnlockTargets.FromReward(kind) is not { } target || name.Length == 0)
            {
                return;
            }

            RowsOf(quest.RowId).Add(new UnlockEntry(target, id, UnlockTags.Capitalize(name), icon, source, quest.Expansion, id)
            {
                Reward = kind,
                ItemId = itemId,
                InRewards = inRewards,
            });
        }

        private void AddDuty(QuestRecord quest, PlanDuty? duty, uint conditionId, string fallbackName, UnlockSource source, RewardKind? reward, uint icon, bool inRewards)
        {
            var name = UnlockTags.Capitalize(UnlockTags.ContentName(duty?.Name ?? fallbackName));
            if (name.Length == 0)
            {
                return;
            }

            var target = duty is null ? UnlockTarget.OtherDuty : UnlockTargets.FromDutyKind(duty.Kind);
            var info = conditionId != 0 ? dutyInfo.GetValueOrDefault(conditionId) : null;
            var detail = info is { Level: > 0 } ? string.Format(CultureInfo.CurrentCulture, LevelFormat, info.Level) : string.Empty;
            // The duty's own icon, else the reward tile's (an instance wears its duty kind's).
            var dutyIcon = info is { Icon: > 0 } ? info.Icon : icon;
            RowsOf(quest.RowId).Add(new UnlockEntry(target, conditionId, name, dutyIcon, source, info?.Expansion ?? quest.Expansion, info?.Level ?? 0, Detail: detail)
            {
                Reward = conditionId != 0 ? reward : null,
                InRewards = inRewards,
            });
        }

        private void AddFeature(QuestRecord quest, string label, UnlockSource source, uint icon, bool inRewards)
        {
            if (label.Length == 0)
            {
                return;
            }

            var target = UnlockTags.FlyingLabels.Contains(label) ? UnlockTarget.Flying
                : UnlockTags.FieldOperationLabels.Contains(label) ? UnlockTarget.FieldOperation
                : UnlockTarget.System;
            var note = curated is not null && curated.SystemUnlocks.TryGetValue(quest.RowId, out var system) && string.Equals(system.Label, label, StringComparison.Ordinal)
                ? system.Note
                : null;
            // The reward's own icon, else the Duty Finder tile or menu that stands for the feature (FeatureArt).
            var art = target == UnlockTarget.Flying ? AetherCurrentIcon : icon != 0 ? icon : links.FeatureIcons.For(label);
            RowsOf(quest.RowId).Add(new UnlockEntry(target, 0, label, art, source, quest.Expansion, Note: note)
            {
                InRewards = inRewards,
            });
        }

        private static uint IconFromRewards(QuestRecord quest, RewardKind kind, uint id, uint itemId)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Icon != 0 && ((itemId != 0 && reward.ItemId == itemId) || (reward.Kind == kind && reward.Id == id && id != 0)))
                {
                    return reward.Icon;
                }
            }

            return 0;
        }

        // ------------------------------------------------------------------ next quests

        public void AddNextQuests()
        {
            foreach (var quest in catalog.All)
            {
                if (quest.IsRemoved)
                {
                    continue;
                }

                foreach (var parent in catalog.PrerequisitesOf(quest).QuestIds)
                {
                    if (parent == quest.RowId || !catalog.ByRowId.ContainsKey(parent))
                    {
                        continue;
                    }

                    if (!nextQuests.TryGetValue(parent, out var list))
                    {
                        list = [];
                        nextQuests[parent] = list;
                    }

                    if (!list.Exists(e => e.TargetId == quest.RowId))
                    {
                        list.Add(new UnlockEntry(UnlockTarget.NextQuest, quest.RowId, quest.Name, 0, UnlockSource.Sheet, quest.Expansion, quest.DisplayLevel));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ finish

        public QuestUnlocks Finish()
        {
            var result = new Dictionary<uint, UnlockEntry[]>();
            var shown = new Dictionary<uint, UnlockEntry[]>();
            var extras = new Dictionary<uint, UnlockEntry[]>();
            var reverse = new Dictionary<(UnlockTarget, uint), List<uint>>();
            foreach (var quest in catalog.All)
            {
                byQuest.TryGetValue(quest.RowId, out var rows);
                nextQuests.TryGetValue(quest.RowId, out var next);
                if ((rows is null || rows.Count == 0) && next is null)
                {
                    continue;
                }

                var ordered = new List<UnlockEntry>((rows?.Count ?? 0) + (next?.Count ?? 0));
                var hidden = 0;
                List<UnlockEntry>? extra = null;
                if (rows is not null)
                {
                    foreach (var row in rows.Ordered())
                    {
                        // One split for every kind and every surface (RewardSplit): a reward-class row is the Rewards
                        // tiles', and so is a row one of the quest's reward-class rewards names; neither is an unlock row.
                        var named = UnlockRewards.Shown(quest.Rewards, row);
                        var kept = RewardSplit.Of(row.Target) == RewardOrUnlock.Reward;
                        var entry = row.InRewards || (!named && !kept) ? row : row with { InRewards = true };
                        hidden += entry.InRewards ? 1 : 0;
                        ordered.Add(entry);
                        if (kept && !named)
                        {
                            // No reward slot carries it (a title): the Rewards tiles draw it from here.
                            (extra ??= []).Add(entry);
                        }
                    }
                }

                if (next is not null)
                {
                    ordered.AddRange(next);
                }

                result[quest.RowId] = [.. ordered];
                if (extra is not null)
                {
                    extras[quest.RowId] = [.. extra];
                }

                if (hidden < ordered.Count)
                {
                    shown[quest.RowId] = hidden == 0 ? result[quest.RowId] : [.. ordered.Where(static e => !e.InRewards)];
                }

                foreach (var entry in ordered)
                {
                    if (entry.TargetId == 0)
                    {
                        continue;
                    }

                    var key = (entry.Target, entry.TargetId);
                    if (!reverse.TryGetValue(key, out var quests))
                    {
                        quests = [];
                        reverse[key] = quests;
                    }

                    quests.Add(quest.RowId);
                }
            }

            return result.Count == 0
                ? Empty
                : new QuestUnlocks(catalog, shown.ToFrozenDictionary(), result.ToFrozenDictionary(), extras.ToFrozenDictionary(), reverse.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
        }
    }

    /// <summary>One quest's rows, one per target; the most trusted claim wins and keeps the first claim's place.</summary>
    private sealed class Rows
    {
        private readonly List<UnlockEntry> entries = [];

        public int Count => entries.Count;

        public void Add(UnlockEntry entry)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var existing = entries[i];
                if (!SameTarget(existing, entry))
                {
                    continue;
                }

                if (existing.Target != entry.Target && existing.Group == UnlockGroup.Area)
                {
                    // A zone and the world map of its name ("The Tempest"): the zone's row (the map opens on it), as sure
                    // as the surer of the two.
                    var zone = existing.Target == UnlockTarget.Zone ? existing : entry;
                    var surer = UnlockTargets.Trust(entry.Source) > UnlockTargets.Trust(existing.Source) ? entry.Source : existing.Source;
                    entries[i] = zone with { Source = surer };
                    return;
                }

                if (UnlockTargets.Trust(entry.Source) > UnlockTargets.Trust(existing.Source))
                {
                    // The more trusted claim, keeping what the first knew that it does not (an icon, a note, a reward kind).
                    entries[i] = entry with
                    {
                        Icon = entry.Icon != 0 ? entry.Icon : existing.Icon,
                        Note = entry.Note ?? existing.Note,
                        Detail = entry.Detail.Length > 0 ? entry.Detail : existing.Detail,
                        TargetId = entry.TargetId != 0 ? entry.TargetId : existing.TargetId,
                        Reward = entry.Reward ?? existing.Reward,
                        ItemId = entry.ItemId != 0 ? entry.ItemId : existing.ItemId,
                        InRewards = entry.InRewards || existing.InRewards,
                    };
                }
                else
                {
                    entries[i] = existing with
                    {
                        Icon = existing.Icon != 0 ? existing.Icon : entry.Icon,
                        Note = existing.Note ?? entry.Note,
                        Detail = existing.Detail.Length > 0 ? existing.Detail : entry.Detail,
                        TargetId = existing.TargetId != 0 ? existing.TargetId : entry.TargetId,
                        Reward = existing.Reward ?? entry.Reward,
                        ItemId = existing.ItemId != 0 ? existing.ItemId : entry.ItemId,
                        InRewards = existing.InRewards || entry.InRewards,
                    };
                }

                return;
            }

            // A feature named like one of the quest's duties, jobs or actions is that duty, job or action ("Palace of
            // the Dead", "Blue Mage", "Aether Compass"), whichever came first; the row keeps the feature's icon and note.
            var named = entry.Target == UnlockTarget.System ? entries.FindIndex(e => NamesFeature(e, entry)) : -1;
            if (named >= 0)
            {
                entries[named] = Absorb(entries[named], entry);
                return;
            }

            var feature = entries.FindIndex(e => e.Target == UnlockTarget.System && NamesFeature(entry, e));
            if (feature >= 0)
            {
                entries[feature] = Absorb(entry, entries[feature]);
                return;
            }

            entries.Add(entry);
        }

        /// <summary>Whether any of the rows so far matches.</summary>
        public bool Has(Predicate<UnlockEntry> match) => entries.Exists(match);

        /// <summary>The duty, job or action row with what the feature row of its name knew that it does not.</summary>
        private static UnlockEntry Absorb(UnlockEntry row, UnlockEntry feature) => row with
        {
            Icon = row.Icon != 0 ? row.Icon : feature.Icon,
            Note = row.Note ?? feature.Note,
            Source = UnlockTargets.Trust(feature.Source) > UnlockTargets.Trust(row.Source) ? feature.Source : row.Source,
            InRewards = row.InRewards || feature.InRewards,
        };

        /// <summary>
        /// One row per target: the same sheet row of the same kind, else the same name: within the group for a duty (a
        /// duty's floor sets, a duty named only by the reward data) and an area (a zone and the world map of its name, a
        /// world map and its own place name), within the kind otherwise (two actions both named "Collect" read as one).
        /// </summary>
        private static bool SameTarget(UnlockEntry a, UnlockEntry b)
        {
            if (a.Group != b.Group)
            {
                return false;
            }

            if (a.Group == UnlockGroup.Duty)
            {
                return (a.TargetId != 0 && a.TargetId == b.TargetId) || SameName(a, b);
            }

            // A zone's id is a TerritoryType row and a world map's a PlaceName row: only the name joins the two.
            if (a.Group == UnlockGroup.Area)
            {
                return (a.Target == b.Target && a.TargetId != 0 && a.TargetId == b.TargetId) || SameName(a, b);
            }

            if (a.Target != b.Target)
            {
                return false;
            }

            return (a.TargetId != 0 && a.TargetId == b.TargetId) || SameName(a, b);
        }

        /// <summary>Whether <paramref name="row"/> is a duty, a job or an action named like the <paramref name="feature"/>.</summary>
        private static bool NamesFeature(UnlockEntry row, UnlockEntry feature) =>
            (row.Group == UnlockGroup.Duty || row.Target is UnlockTarget.Job or UnlockTarget.Action or UnlockTarget.GeneralAction or UnlockTarget.Trait or UnlockTarget.BlueMageSpell)
            && SameName(row, feature);

        private static bool SameName(UnlockEntry a, UnlockEntry b) =>
            string.Equals(PlanDuties.NameKey(a.Name), PlanDuties.NameKey(b.Name), StringComparison.Ordinal);

        public IEnumerable<UnlockEntry> Ordered() => entries
            .OrderBy(static e => e.Group)
            .ThenBy(static e => e.Target)
            .ThenBy(static e => e.Expansion)
            .ThenBy(static e => e.SortKey)
            .ThenBy(static e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>First job icon in the 062000 set, offset by ClassJob row id.</summary>
    public const uint JobIconBase = 62100;

    /// <summary>The aether current attunement crystal.</summary>
    public const uint AetherCurrentIcon = 60033;

    private static string LevelFormat => CoreText.T("Core.Unlock.LevelFormat", "Lv {0}");

    private static string FlyingDetail => CoreText.T("Core.Unlock.FlyingDetail", "aether current");

    /// <summary>"Aether Current (Coerthas Western Highlands)" names the zone flying opens in.</summary>
    private static string CurrentZone(string name) => UnlockRewards.CurrentZone(name);
}
