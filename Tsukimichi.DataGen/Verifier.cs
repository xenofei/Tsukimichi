using System.Text;
using System.Text.Json.Nodes;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

internal sealed record VerifyOptions(
    string Game,
    string DataPath,
    string ReportPath,
    string? NotesPath,
    bool UseXivApi,
    int SampleSize,
    int Seed);

/// <summary>
/// <c>--verify</c> mode: checks a shipped unique_quests.json against the local game files (structure, reward ids, icons,
/// known answers, exclusivity) and, optionally, against xivapi v2 (a deterministic sample of entries). Writes
/// docs/data/verification-report.md and returns 1 when a hard check fails. Meant to run after every regeneration.
/// </summary>
internal sealed class Verifier
{
    private const string UserAgent = "Tsukimichi.DataGen verify (https://github.com/xenofei/Tsukimichi)";

    /// <summary>Kinds whose entry is delivered through an Item row, so <c>itemId</c> must be a real item.</summary>
    private static readonly HashSet<RewardKind> ItemKinds =
    [
        RewardKind.Item, RewardKind.OptionalItem, RewardKind.ArtifactGear, RewardKind.Mount, RewardKind.Minion,
        RewardKind.Orchestrion, RewardKind.TripleTriadCard, RewardKind.Ornament, RewardKind.Barding, RewardKind.Hairstyle,
    ];

    /// <summary>ItemAction type expected per collectible kind (docs/feasibility-report.md section 4).</summary>
    private static readonly Dictionary<RewardKind, uint> ItemActionByKind = new()
    {
        [RewardKind.Mount] = 1322,
        [RewardKind.Minion] = 853,
        [RewardKind.Orchestrion] = 25183,
        [RewardKind.TripleTriadCard] = 3357,
        [RewardKind.Ornament] = 20086,
        [RewardKind.Barding] = 1013,
        [RewardKind.Hairstyle] = 2633,
    };

    private readonly GameSheets g;
    private readonly VerifyOptions o;
    private readonly StringBuilder md = new();
    private readonly List<string> failures = new();
    private readonly List<string> notes = new();

    private Verifier(GameSheets sheets, VerifyOptions options)
    {
        g = sheets;
        o = options;
    }

    public static int Run(VerifyOptions options)
    {
        var sheets = new GameSheets(options.Game);
        var v = new Verifier(sheets, options);
        return v.Execute();
    }

    private int Execute()
    {
        var data = UniqueRewardsFile.Load(o.DataPath);
        var entries = data.Entries;
        Console.WriteLine($"data:    {o.DataPath} ({entries.Count} entries, game {data.GameVersion})");

        md.AppendLine("# Unique reward verification report");
        md.AppendLine();
        md.AppendLine($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `Tsukimichi.DataGen --verify` against game version `{g.GameVersion}`.");
        md.AppendLine($"Data file: `{Path.GetFileName(o.DataPath)}` generated {data.GeneratedUtc:yyyy-MM-dd HH:mm} UTC from game version `{data.GameVersion}`; **{entries.Count}** entries.");
        if (data.GameVersion != g.GameVersion)
            failures.Add($"data file game version {data.GameVersion} differs from local game {g.GameVersion}; regenerate.");
        foreach (var w in data.Warnings)
            failures.Add($"loader: {w}");
        md.AppendLine();

        if (o.NotesPath is not null)
        {
            md.AppendLine(File.ReadAllText(o.NotesPath).Trim());
            md.AppendLine();
        }

        var summaryStart = md.Length;

        Structural(entries);
        var bundle = CatalogMapper.Map(g.Data.Excel, Language.English);
        Icons(entries, bundle);
        KnownAnswers(entries);
        if (o.UseXivApi)
            SpotChecks(entries);
        else
            md.AppendLine("## Semantic spot checks\n\nSkipped (`--no-xivapi`).\n");
        Exclusivity(entries);
        GeneratorDiagnostics();

        // Summary goes to the top, after the header.
        var summary = new StringBuilder();
        summary.AppendLine("## Summary");
        summary.AppendLine();
        summary.AppendLine(failures.Count == 0 ? "- Hard checks: **all passed**." : $"- Hard checks: **{failures.Count} failed**.");
        foreach (var f in failures)
            summary.AppendLine($"  - {Md(f)}");
        if (notes.Count > 0)
        {
            summary.AppendLine($"- Observations ({notes.Count}):");
            foreach (var n in notes)
                summary.AppendLine($"  - {Md(n)}");
        }
        summary.AppendLine();
        md.Insert(summaryStart, summary.ToString());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(o.ReportPath))!);
        File.WriteAllText(o.ReportPath, md.ToString());
        Console.WriteLine($"wrote:   {o.ReportPath}");

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"VERIFY FAILED ({failures.Count}):");
            foreach (var f in failures)
                Console.Error.WriteLine($"  - {f}");
            return 1;
        }

        Console.WriteLine($"verify passed ({notes.Count} observations)");
        return 0;
    }

    // ----------------------------------------------------------------------------------------------------------
    // 1. Structure

    private void Structural(IReadOnlyList<UniqueRewardEntry> entries)
    {
        md.AppendLine("## 1. Structural checks");
        md.AppendLine();

        var problems = new List<string>();
        var seenKeys = new HashSet<(uint, RewardKind, uint)>();
        var seenExact = new HashSet<UniqueRewardEntry>();
        var keyDuplicates = 0;
        var exactDuplicates = 0;

        foreach (var e in entries)
        {
            var q = g.Quests.GetRowOrDefault(e.QuestRowId);
            var questName = UniqueRewardGenerator.Text(q?.Name);
            if (q is null)
                problems.Add($"{Describe(e)}: quest row {e.QuestRowId} does not exist");
            else if (questName.Length == 0)
                problems.Add($"{Describe(e)}: quest row {e.QuestRowId} has an empty name");

            if (!Enum.IsDefined(e.Kind))
                problems.Add($"{Describe(e)}: kind {e.Kind} is not a RewardKind");
            if (!Enum.IsDefined(e.Confidence))
                problems.Add($"{Describe(e)}: confidence {e.Confidence} is not a Confidence");
            if (string.IsNullOrWhiteSpace(e.RewardName))
                problems.Add($"{Describe(e)}: rewardName is empty");
            if (string.IsNullOrWhiteSpace(e.Source))
                problems.Add($"{Describe(e)}: source is empty");

            if (ItemKinds.Contains(e.Kind))
            {
                if (e.ItemId == 0)
                    problems.Add($"{Describe(e)}: item-based kind with itemId 0");
                else if (g.Items.GetRowOrDefault(e.ItemId) is not { } item)
                    problems.Add($"{Describe(e)}: item {e.ItemId} does not exist");
                else if (UniqueRewardGenerator.Text(item.Name).Length == 0)
                    problems.Add($"{Describe(e)}: item {e.ItemId} has an empty name");
                else if (LocalIdMismatch(e, item) is { } mismatch)
                    problems.Add($"{Describe(e)}: {mismatch}");
            }

            if (e.Kind != RewardKind.SystemUnlock && e.RewardId == 0)
                problems.Add($"{Describe(e)}: rewardId 0");
            else if (ResolveRewardRow(e) is { } why)
                problems.Add($"{Describe(e)}: {why}");

            if (!seenExact.Add(e))
                exactDuplicates++;
            if (!seenKeys.Add((e.QuestRowId, e.Kind, e.RewardId)))
                keyDuplicates++;
        }

        if (exactDuplicates > 0) problems.Add($"{exactDuplicates} exact duplicate entries");
        if (keyDuplicates > 0) problems.Add($"{keyDuplicates} entries share a (questRowId, kind, rewardId) key");

        md.AppendLine($"Entries: **{entries.Count}** across **{entries.Select(e => e.QuestRowId).Distinct().Count()}** quests. Problems found: **{problems.Count}**.");
        md.AppendLine();
        if (problems.Count > 0)
        {
            foreach (var p in problems.Take(60))
                md.AppendLine($"- {Md(p)}");
            if (problems.Count > 60)
                md.AppendLine($"- ... {problems.Count - 60} more");
            md.AppendLine();
            failures.Add($"structural: {problems.Count} problem(s), first: {problems[0]}");
        }

        md.AppendLine("### Counts per kind and confidence");
        md.AppendLine();
        var confidences = Enum.GetValues<Confidence>();
        md.Append("| Kind |");
        foreach (var c in confidences) md.Append($" {c} |");
        md.AppendLine(" Total | With `otherSource` |");
        md.Append("|---|");
        foreach (var _ in confidences) md.Append("---:|");
        md.AppendLine("---:|---:|");
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            var ofKind = entries.Where(e => e.Kind == kind).ToList();
            if (ofKind.Count == 0) continue;
            md.Append($"| {kind} |");
            foreach (var c in confidences) md.Append($" {ofKind.Count(e => e.Confidence == c)} |");
            md.AppendLine($" {ofKind.Count} | {ofKind.Count(e => e.Source.Contains(";otherSource="))} |");
        }
        md.Append("| **Total** |");
        foreach (var c in confidences) md.Append($" {entries.Count(e => e.Confidence == c)} |");
        md.AppendLine($" {entries.Count} | {entries.Count(e => e.Source.Contains(";otherSource="))} |");
        md.AppendLine();

        md.AppendLine("### `otherSource` breakdown");
        md.AppendLine();
        md.AppendLine("| Kind | otherSource | Entries |");
        md.AppendLine("|---|---|---:|");
        foreach (var grp in entries
                     .Where(e => e.Source.Contains(";otherSource="))
                     .GroupBy(e => (e.Kind, Other: OtherSource(e)))
                     .OrderBy(x => x.Key.Kind).ThenByDescending(x => x.Count()))
            md.AppendLine($"| {grp.Key.Kind} | {grp.Key.Other} | {grp.Count()} |");
        md.AppendLine();
    }

    /// <summary>
    /// Offline consistency between the item and the reward id: the ItemAction type the kind implies, and the id the runtime
    /// will check (ItemAction.Data[0], Item.AdditionalData for orchestrion rolls, Emote.UnlockLink for emote items).
    /// </summary>
    private string? LocalIdMismatch(UniqueRewardEntry e, Item item)
    {
        var action = item.ItemAction.RowId == 0 ? null : g.ItemActions.GetRowOrDefault(item.ItemAction.RowId);
        var type = action?.Action.RowId ?? 0;
        uint data0 = action is { } a && a.Data.Count > 0 ? a.Data[0] : 0u;
        if (ItemActionByKind.TryGetValue(e.Kind, out var expected) && type != expected)
            return $"item {e.ItemId} has ItemAction type {type}, kind {e.Kind} expects {expected}";
        if (e.Kind == RewardKind.Emote && e.ItemId != 0 && type != 2633)
            return $"item {e.ItemId} has ItemAction type {type}, an emote item expects 2633";
        return e.Kind switch
        {
            RewardKind.Orchestrion => item.AdditionalData.RowId != e.RewardId
                ? $"item {e.ItemId} AdditionalData {item.AdditionalData.RowId} differs from rewardId {e.RewardId}"
                : null,
            RewardKind.Mount or RewardKind.Minion or RewardKind.TripleTriadCard or RewardKind.Ornament or RewardKind.Barding or RewardKind.Hairstyle
                => data0 != e.RewardId ? $"item {e.ItemId} ItemAction.Data[0] {data0} differs from rewardId {e.RewardId}" : null,
            RewardKind.Emote => data0 != (g.Emotes.GetRowOrDefault(e.RewardId)?.UnlockLink ?? 0)
                ? $"item {e.ItemId} ItemAction.Data[0] {data0} differs from Emote {e.RewardId}.UnlockLink"
                : null,
            _ => null,
        };
    }

    /// <summary>Null when rewardId points at a real row of the sheet the kind implies; else the reason.</summary>
    private string? ResolveRewardRow(UniqueRewardEntry e)
    {
        var id = e.RewardId;
        return e.Kind switch
        {
            RewardKind.Emote => Named(g.Emotes.GetRowOrDefault(id)?.Name, "Emote", id),
            RewardKind.Mount => Named(g.Mounts.GetRowOrDefault(id)?.Singular, "Mount", id),
            RewardKind.Minion => Named(g.Companions.GetRowOrDefault(id)?.Singular, "Companion", id),
            RewardKind.Orchestrion => Named(g.Orchestrions.GetRowOrDefault(id)?.Name, "Orchestrion", id),
            RewardKind.TripleTriadCard => Named(g.TripleTriadCards.GetRowOrDefault(id)?.Name, "TripleTriadCard", id),
            RewardKind.Ornament => Named(g.Ornaments.GetRowOrDefault(id)?.Singular, "Ornament", id),
            RewardKind.Barding => Named(g.BuddyEquips.GetRowOrDefault(id)?.Name, "BuddyEquip", id),
            RewardKind.Hairstyle => g.CharaMakeCustomizes.Any(c => c.UnlockLink == id) ? null : $"no CharaMakeCustomize row has unlock link {id}",
            RewardKind.Action => Named(g.Actions.GetRowOrDefault(id)?.Name, "Action", id),
            RewardKind.GeneralAction => Named(g.GeneralActions.GetRowOrDefault(id)?.Name, "GeneralAction", id),
            RewardKind.Trait => Named(g.Traits.GetRowOrDefault(id)?.Name, "Trait", id),
            RewardKind.ClassJob => Named(g.ClassJobs.GetRowOrDefault(id)?.Name, "ClassJob", id),
            RewardKind.AetherCurrent => g.AetherCurrents.GetRowOrDefault(id) is null ? $"AetherCurrent {id} does not exist" : null,
            RewardKind.BlueMageSpell => g.AozActions.GetRowOrDefault(id) is null ? $"AozAction {id} does not exist" : null,
            RewardKind.Achievement => Named(g.Achievements.GetRowOrDefault(id)?.Name, "Achievement", id),
            RewardKind.Title => Named(g.Titles.GetRowOrDefault(id)?.Masculine, "Title", id),
            RewardKind.DutyUnlock => Named(g.ContentFinderConditions.GetRowOrDefault(id)?.Name, "ContentFinderCondition", id),
            RewardKind.Instance => g.InstanceContents.GetRowOrDefault(id) is null ? $"InstanceContent {id} does not exist" : null,
            RewardKind.Other => Named(g.QuestRewardOthers.GetRowOrDefault(id)?.Name, "QuestRewardOther", id),
            RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear => id != e.ItemId ? $"rewardId {id} differs from itemId {e.ItemId}" : null,
            _ => null,
        };

        static string? Named(Lumina.Text.ReadOnly.ReadOnlySeString? name, string sheet, uint id)
            => name is null ? $"{sheet} {id} does not exist"
                : UniqueRewardGenerator.Text(name).Length == 0 ? $"{sheet} {id} has an empty name"
                : null;
    }

    // ----------------------------------------------------------------------------------------------------------
    // 2. Icons

    private void Icons(IReadOnlyList<UniqueRewardEntry> entries, CatalogBundle bundle)
    {
        md.AppendLine("## 2. Icon checks");
        md.AppendLine();

        // Reward icons exactly as the Moonlit pane resolves them: the catalog's RewardRef list for the quest,
        // matched by item id first, then by (kind, id).
        var iconOfEntry = new Dictionary<UniqueRewardEntry, uint>();
        var noIconByKind = new Dictionary<RewardKind, int>();
        foreach (var e in entries)
        {
            var icon = FindIcon(bundle.Catalog.GetByRowId(e.QuestRowId), e);
            iconOfEntry[e] = icon;
            if (icon == 0)
                noIconByKind[e.Kind] = noIconByKind.GetValueOrDefault(e.Kind) + 1;
        }

        var rewardIcons = iconOfEntry.Values.Where(i => i != 0).Distinct().OrderBy(i => i).ToList();
        var missingReward = rewardIcons.Where(i => !IconExists(i, hr1: false)).ToList();
        var missingRewardHr1 = rewardIcons.Where(i => !IconExists(i, hr1: true)).ToList();

        md.AppendLine($"Reward icons the Moonlit pane will draw: **{rewardIcons.Count}** distinct ids over {iconOfEntry.Count(kv => kv.Value != 0)} entries.");
        md.AppendLine($"Missing `ui/icon/{{folder}}/{{id}}.tex`: **{missingReward.Count}**; missing `_hr1` variant: **{missingRewardHr1.Count}**.");
        if (missingReward.Count > 0) md.AppendLine($"Missing: {string.Join(", ", missingReward.Take(50))}");
        if (missingRewardHr1.Count > 0) md.AppendLine($"Missing hr1: {string.Join(", ", missingRewardHr1.Take(50))}");
        md.AppendLine();
        if (missingReward.Count > 0)
            failures.Add($"icons: {missingReward.Count} reward icon(s) missing from the game files");
        if (missingRewardHr1.Count > 0)
            failures.Add($"icons: {missingRewardHr1.Count} reward icon(s) missing the _hr1 variant");

        md.AppendLine("Entries with no drawable icon (no catalog reward matches by item id or by kind and id), per kind:");
        md.AppendLine();
        md.AppendLine("| Kind | Entries | Without icon |");
        md.AppendLine("|---|---:|---:|");
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            var total = entries.Count(e => e.Kind == kind);
            if (total == 0) continue;
            md.AppendLine($"| {kind} | {total} | {noIconByKind.GetValueOrDefault(kind)} |");
        }
        md.AppendLine();
        var dutyNoIcon = noIconByKind.GetValueOrDefault(RewardKind.DutyUnlock);
        var dutyTotal = entries.Count(e => e.Kind == RewardKind.DutyUnlock);
        if (dutyNoIcon == dutyTotal && dutyTotal > 0)
            notes.Add("DutyUnlock entries never get an icon: the catalog emits RewardKind.Instance with the InstanceContent id, the data file DutyUnlock with the ContentFinderCondition id, so MoonlitPane.FindIcon's (kind, id) match never hits (UI/GameData side, not fixed here).");

        // Quest banners: Quest.Icon (journal banner) and Quest.IconSpecial.
        var named = g.Quests.Where(q => q.RowId != 0 && UniqueRewardGenerator.Text(q.Name).Length > 0).ToList();
        var banners = named.Where(q => q.Icon != 0).ToList();
        var special = named.Where(q => q.IconSpecial != 0).ToList();
        md.AppendLine($"### Quest banners");
        md.AppendLine();
        md.AppendLine($"Named quests: **{named.Count}**; with a non-zero `Quest.Icon` banner: **{banners.Count}** ({banners.Select(q => q.Icon).Distinct().Count()} distinct ids); with a non-zero `Quest.IconSpecial`: **{special.Count}** ({special.Select(q => q.IconSpecial).Distinct().Count()} distinct ids).");
        md.AppendLine();

        var bannerIds = banners.Select(q => q.Icon).Concat(special.Select(q => q.IconSpecial)).Distinct().OrderBy(i => i).ToList();
        var missingBanner = bannerIds.Where(i => !IconExists(i, hr1: false)).ToList();
        var missingBannerHr1 = bannerIds.Where(i => !IconExists(i, hr1: true)).ToList();
        md.AppendLine($"Distinct banner ids: **{bannerIds.Count}**; missing `.tex`: **{missingBanner.Count}**; missing `_hr1`: **{missingBannerHr1.Count}**.");
        if (missingBanner.Count > 0) md.AppendLine($"Missing: {string.Join(", ", missingBanner.Take(50))}");
        if (missingBannerHr1.Count > 0) md.AppendLine($"Missing hr1: {string.Join(", ", missingBannerHr1.Take(50))}");
        md.AppendLine();
        if (missingBanner.Count > 0)
            failures.Add($"icons: {missingBanner.Count} quest banner(s) missing from the game files");
        if (missingBannerHr1.Count > 0)
            notes.Add($"{missingBannerHr1.Count} quest banner(s) have no _hr1 variant (Dalamud falls back to the base texture)");

        md.AppendLine("Banner id distribution (folder = id / 1000; top 25 by quest count):");
        md.AppendLine();
        md.AppendLine("| Banner id | Quests | Example |");
        md.AppendLine("|---:|---:|---|");
        foreach (var grp in banners.GroupBy(q => q.Icon).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).Take(25))
        {
            var ex = grp.OrderBy(q => q.RowId).First();
            md.AppendLine($"| {grp.Key} | {grp.Count()} | {ex.RowId} {Md(UniqueRewardGenerator.Text(ex.Name))} |");
        }
        md.AppendLine();
        md.AppendLine("Banner folders (id / 1000) by quest count:");
        md.AppendLine();
        md.AppendLine("| Folder | Quests | Distinct ids |");
        md.AppendLine("|---:|---:|---:|");
        foreach (var grp in banners.GroupBy(q => q.Icon / 1000).OrderBy(x => x.Key))
            md.AppendLine($"| {grp.Key:D6} | {grp.Count()} | {grp.Select(q => q.Icon).Distinct().Count()} |");
        md.AppendLine();

        md.AppendLine("Banner dimensions for well-known quests (the journal banner is a wide image, 376x120 at base resolution):");
        md.AppendLine();
        md.AppendLine("| Quest | Banner id | Base tex | hr1 tex | Plausible |");
        md.AppendLine("|---|---:|---|---|---|");
        var wellKnown = new List<(string Label, uint RowId)>();
        foreach (var q in named.Where(q => UniqueRewardGenerator.Text(q.Name) == "Close to Home").OrderBy(q => q.RowId))
            wellKnown.Add(("Close to Home", q.RowId));
        wellKnown.Add(("The Ultimate Weapon", 70058));
        foreach (var q in named.Where(q => UniqueRewardGenerator.Text(q.Name) == "Her Last Vow").OrderBy(q => q.RowId))
            wellKnown.Add(("Her Last Vow", q.RowId));
        foreach (var grp in banners.GroupBy(q => q.Icon).OrderByDescending(x => x.Count()).Take(3))
            wellKnown.Add(("(most common banner)", grp.OrderBy(q => q.RowId).First().RowId));
        var plausibleAll = true;
        foreach (var (label, rowId) in wellKnown)
        {
            var q = g.Quests.GetRowOrDefault(rowId);
            if (q is null) continue;
            var id = q.Value.Icon;
            var dims = TexDims(id, hr1: false);
            var dimsHr = TexDims(id, hr1: true);
            var plausible = dims is { } d && d.Width > d.Height && d.Width >= 256;
            if (id != 0 && !plausible) plausibleAll = false;
            md.AppendLine($"| {rowId} {Md(label)} | {id} | {Fmt(dims)} | {Fmt(dimsHr)} | {(id == 0 ? "no banner" : plausible ? "yes" : "NO")} |");
        }
        md.AppendLine();
        if (!plausibleAll)
            failures.Add("icons: a well-known quest banner does not look like a wide banner texture");

        static string Fmt((ushort Width, ushort Height)? d) => d is { } x ? $"{x.Width}x{x.Height}" : "missing";
    }

    /// <summary>Copy of MoonlitPane.FindIcon: same item, else same kind and id, from the catalog's reward list.</summary>
    private static uint FindIcon(QuestRecord? quest, UniqueRewardEntry entry)
    {
        if (quest is null)
            return 0;
        if (entry.ItemId != 0)
        {
            foreach (var reward in quest.Rewards)
                if (reward.ItemId == entry.ItemId && reward.Icon != 0)
                    return reward.Icon;
        }
        if (entry.RewardId != 0)
        {
            foreach (var reward in quest.Rewards)
                if (reward.Kind == entry.Kind && reward.Id == entry.RewardId && reward.Icon != 0)
                    return reward.Icon;
        }
        return 0;
    }

    private static string IconPath(uint id, bool hr1) => $"ui/icon/{id / 1000 * 1000:D6}/{id:D6}{(hr1 ? "_hr1" : string.Empty)}.tex";

    private bool IconExists(uint id, bool hr1) => g.Data.FileExists(IconPath(id, hr1));

    private (ushort Width, ushort Height)? TexDims(uint id, bool hr1)
    {
        if (id == 0 || !IconExists(id, hr1))
            return null;
        var tex = g.Data.GetFile<TexFile>(IconPath(id, hr1));
        return tex is null ? null : (tex.Header.Width, tex.Header.Height);
    }

    // ----------------------------------------------------------------------------------------------------------
    // 3. Known answers

    private void KnownAnswers(IReadOnlyList<UniqueRewardEntry> entries)
    {
        md.AppendLine("## 3. Known-answer checks");
        md.AppendLine();
        md.AppendLine("| Check | Result | Detail |");
        md.AppendLine("|---|---|---|");

        void Check(string label, bool ok, string detail)
        {
            md.AppendLine($"| {Md(label)} | {(ok ? "pass" : "FAIL")} | {Md(detail)} |");
            if (!ok) failures.Add($"known answer: {label} ({detail})");
        }

        var herLastVow = entries.Where(e => e.QuestRowId == 66038).ToList();
        Check("Her Last Vow (66038) -> Emote 114 Most Gentlemanly",
            herLastVow.Any(e => e is { Kind: RewardKind.Emote, RewardId: 114 } && e.RewardName.Equals("Most Gentlemanly", StringComparison.OrdinalIgnoreCase)),
            Join(herLastVow.Where(e => e.Kind == RewardKind.Emote)));
        Check("Her Last Vow (66038) -> Minion Wind-up Gentleman",
            herLastVow.Any(e => e.Kind == RewardKind.Minion && e.RewardName.Contains("gentleman", StringComparison.OrdinalIgnoreCase)),
            Join(herLastVow.Where(e => e.Kind == RewardKind.Minion)));

        var ultimate = entries.Where(e => e.QuestRowId == 70058).ToList();
        Check("The Ultimate Weapon (70058) -> Mount 6 Magitek Armor (item 6008)",
            ultimate.Any(e => e is { Kind: RewardKind.Mount, RewardId: 6, ItemId: 6008 } && e.RewardName.Contains("magitek armor", StringComparison.OrdinalIgnoreCase)),
            Join(ultimate.Where(e => e.Kind == RewardKind.Mount)));
        Check("The Ultimate Weapon (70058) does not list Fantasia as a unique Item",
            !ultimate.Any(e => e.Kind is RewardKind.Item or RewardKind.OptionalItem && e.RewardName.Equals("Fantasia", StringComparison.OrdinalIgnoreCase)),
            Join(ultimate.Where(e => e.Kind is RewardKind.Item or RewardKind.OptionalItem)));

        var pledge = entries.Where(e => e.QuestRowId == 66591).ToList();
        Check("Paladin's Pledge (66591) -> ClassJob 19 paladin",
            pledge.Any(e => e is { Kind: RewardKind.ClassJob, RewardId: 19 }),
            Join(pledge.Where(e => e.Kind == RewardKind.ClassJob)));

        // A Dawntrail aether current quest: Quest.Expansion 4 and the AetherCurrent sheet links back to it.
        var dawntrail = entries
            .Where(e => e.Kind == RewardKind.AetherCurrent && g.Quests.GetRowOrDefault(e.QuestRowId)?.Expansion.RowId == 4)
            .OrderBy(e => e.QuestRowId)
            .ToList();
        var dtExample = dawntrail.FirstOrDefault();
        var dtLinksBack = dtExample is not null && g.AetherCurrents.GetRowOrDefault(dtExample.RewardId)?.Quest.RowId == dtExample.QuestRowId;
        Check("Dawntrail aether current quests present and linked from AetherCurrent.Quest",
            dawntrail.Count >= 20 && dtLinksBack,
            dtExample is null ? "none" : $"{dawntrail.Count} entries; e.g. {dtExample.QuestRowId} {QuestName(dtExample.QuestRowId)} -> {dtExample.RewardName} (current {dtExample.RewardId})");

        // The curated retainer quests (An Ill-conceived Venture, three city variants) -> SystemUnlock "Retainers".
        var retainers = entries.Where(e => e.Kind == RewardKind.SystemUnlock && e.RewardName.Equals("Retainers", StringComparison.OrdinalIgnoreCase)).ToList();
        Check("Curated retainer quests -> SystemUnlock Retainers (three city variants, quest named An Ill-conceived Venture)",
            retainers.Count == 3 && retainers.All(e => e.Confidence == Confidence.Curated && QuestName(e.QuestRowId) == "An Ill-conceived Venture"),
            Join(retainers));

        Check("Sastasha (CFC 4) is a DutyUnlock of It's Probably Pirates (65781 and 66211)",
            entries.Count(e => e is { Kind: RewardKind.DutyUnlock, RewardId: 4 } && e.QuestRowId is 65781 or 66211) == 2,
            Join(entries.Where(e => e is { Kind: RewardKind.DutyUnlock, RewardId: 4 })));

        md.AppendLine();
        md.AppendLine("Ye Olde Faux Hollows: skipped (unsure of the expected entry).");
        md.AppendLine();

        string Join(IEnumerable<UniqueRewardEntry> es)
        {
            var list = es.Select(e => $"{e.Kind} {e.RewardId} '{e.RewardName}' item {e.ItemId} [{e.Confidence}]").ToList();
            return list.Count == 0 ? "(none)" : string.Join("; ", list);
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // 4. Semantic spot checks against xivapi v2

    private void SpotChecks(IReadOnlyList<UniqueRewardEntry> entries)
    {
        md.AppendLine("## 4. Semantic spot checks (xivapi v2)");
        md.AppendLine();

        var sample = Sample(entries);
        using var api = new XivApi(UserAgent);

        // Batched lookups per sheet.
        var questNames = api.Rows("Quest", sample.Select(e => e.QuestRowId), "Name");
        var items = api.Rows("Item", sample.Where(e => e.ItemId != 0).Select(e => e.ItemId), "Name,ItemAction.Action,ItemAction.Data,AdditionalData");
        var bySheet = new Dictionary<string, IReadOnlyDictionary<uint, JsonObject>>();
        foreach (var grp in sample.GroupBy(e => SheetOf(e.Kind)))
        {
            if (grp.Key is null) continue;
            var fields = FieldsOf(grp.Key);
            bySheet[grp.Key] = api.Rows(grp.Key, grp.Select(e => e.RewardId), fields);
        }
        var achievementIds = sample.Where(e => e.Kind == RewardKind.Title).Select(e => AchievementId(e.Source)).Where(a => a != 0).ToList();
        var achievements = achievementIds.Count == 0 ? new Dictionary<uint, JsonObject>() : api.Rows("Achievement", achievementIds, "Name,Title,Key,Data");

        var rows = new List<(UniqueRewardEntry Entry, string Check, string Expected, string Actual, bool Pass)>();
        foreach (var e in sample)
        {
            var localQuest = QuestName(e.QuestRowId);
            var apiQuest = XivApi.Str(questNames.GetValueOrDefault(e.QuestRowId), "Name");
            rows.Add((e, "Quest name", localQuest, apiQuest, Same(localQuest, apiQuest)));

            var sheet = SheetOf(e.Kind);
            var fields = sheet is null ? null : bySheet[sheet].GetValueOrDefault(e.RewardId);
            switch (e.Kind)
            {
                case RewardKind.Emote:
                case RewardKind.Orchestrion:
                case RewardKind.TripleTriadCard:
                case RewardKind.Barding:
                case RewardKind.Action:
                case RewardKind.GeneralAction:
                case RewardKind.Trait:
                case RewardKind.ClassJob:
                case RewardKind.Achievement:
                case RewardKind.DutyUnlock:
                case RewardKind.Other:
                    rows.Add((e, $"{sheet}.Name", e.RewardName, XivApi.Str(fields, "Name"), Same(e.RewardName, XivApi.Str(fields, "Name"))));
                    break;
                case RewardKind.Mount:
                case RewardKind.Minion:
                case RewardKind.Ornament:
                    rows.Add((e, $"{sheet}.Singular", e.RewardName, XivApi.Str(fields, "Singular"), Same(e.RewardName, XivApi.Str(fields, "Singular"))));
                    break;
                case RewardKind.Title:
                    rows.Add((e, "Title.Masculine", e.RewardName, XivApi.Str(fields, "Masculine"), Same(e.RewardName, XivApi.Str(fields, "Masculine"))));
                    var achId = AchievementId(e.Source);
                    var ach = achievements.GetValueOrDefault(achId);
                    var achTitle = XivApi.Ref(ach?["Title"]);
                    rows.Add((e, $"Achievement {achId}.Title", e.RewardId.ToString(), achTitle.ToString(), achTitle == e.RewardId));
                    var linked = XivApi.Ref(ach?["Key"]) == e.QuestRowId
                                 || (ach?["Data"] as JsonArray)?.Any(d => XivApi.Ref(d) == e.QuestRowId) == true;
                    rows.Add((e, $"Achievement {achId} links quest", e.QuestRowId.ToString(), linked ? "linked" : "not linked", linked));
                    break;
                case RewardKind.AetherCurrent:
                    var zoneOk = e.RewardName.StartsWith("Aether Current (", StringComparison.Ordinal) && ZoneNames().Contains(ZoneOf(e.RewardName));
                    rows.Add((e, "Zone is a real AetherCurrentCompFlgSet territory", ZoneOf(e.RewardName), zoneOk ? "found" : "not found", zoneOk));
                    var acQuest = XivApi.Ref(fields?["Quest"]);
                    rows.Add((e, "AetherCurrent.Quest", e.QuestRowId.ToString(), acQuest.ToString(), acQuest == e.QuestRowId));
                    break;
                case RewardKind.BlueMageSpell:
                    var spell = XivApi.Str(XivApi.Nested(fields, "Action"), "Name");
                    rows.Add((e, "AozAction.Action.Name", e.RewardName, spell, Same(e.RewardName, spell)));
                    break;
                case RewardKind.Hairstyle:
                    rows.Add((e, "CharaMakeCustomize unlock link exists locally", e.RewardId.ToString(), g.CharaMakeCustomizes.Any(c => c.UnlockLink == e.RewardId) ? "found" : "not found", g.CharaMakeCustomizes.Any(c => c.UnlockLink == e.RewardId)));
                    break;
                case RewardKind.SystemUnlock:
                    rows.Add((e, "Curated label non-empty", "non-empty", e.RewardName, e.RewardName.Length > 0));
                    break;
            }

            if (e.ItemId != 0)
            {
                var item = items.GetValueOrDefault(e.ItemId);
                var itemName = XivApi.Str(item, "Name");
                if (e.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear or RewardKind.Hairstyle)
                    rows.Add((e, "Item.Name", e.RewardName, itemName, Same(e.RewardName, itemName)));
                else
                    rows.Add((e, "Item exists", $"item {e.ItemId}", itemName.Length == 0 ? "missing" : itemName, itemName.Length > 0));

                var itemAction = XivApi.Nested(item, "ItemAction");
                var actionType = XivApi.Ref(itemAction?["Action"]);
                var data0 = (itemAction?["Data"] as JsonArray)?.FirstOrDefault() is JsonValue dv && dv.TryGetValue<uint>(out var d0) ? d0 : 0;
                if (ItemActionByKind.TryGetValue(e.Kind, out var expectedType))
                {
                    rows.Add((e, "ItemAction type", expectedType.ToString(), actionType.ToString(), actionType == expectedType));
                    if (e.Kind == RewardKind.Orchestrion)
                    {
                        var additional = XivApi.Ref(item?["AdditionalData"]);
                        rows.Add((e, "Item.AdditionalData = rewardId", e.RewardId.ToString(), additional.ToString(), additional == e.RewardId));
                    }
                    else
                    {
                        rows.Add((e, "ItemAction.Data[0] = rewardId", e.RewardId.ToString(), data0.ToString(), data0 == e.RewardId));
                    }
                }
                else if (e.Kind == RewardKind.Emote)
                {
                    var unlockLink = g.Emotes.GetRowOrDefault(e.RewardId)?.UnlockLink ?? 0;
                    rows.Add((e, "ItemAction type", "2633", actionType.ToString(), actionType == 2633));
                    rows.Add((e, "ItemAction.Data[0] = Emote.UnlockLink", unlockLink.ToString(), data0.ToString(), data0 == unlockLink));
                }
            }
        }

        var failed = rows.Count(r => !r.Pass);
        md.AppendLine($"Sample: **{sample.Count}** entries (seed {o.Seed}, up to 3 per kind then filled at random), **{rows.Count}** checks, **{failed}** failed. xivapi version `{api.Version}`, {api.Requests} requests, {api.Errors.Count} request errors.");
        md.AppendLine();
        foreach (var err in api.Errors.Take(10))
            md.AppendLine($"- request error: {Md(err)}");
        if (api.Errors.Count > 0) md.AppendLine();
        if (api.Errors.Count > 0)
            failures.Add($"xivapi: {api.Errors.Count} request error(s)");
        if (failed > 0)
            failures.Add($"xivapi: {failed} spot check(s) failed");

        md.AppendLine("| Quest | Kind | Reward id | Item id | Check | Expected (local) | Actual (xivapi) | Result |");
        md.AppendLine("|---|---|---:|---:|---|---|---|---|");
        foreach (var r in rows)
            md.AppendLine($"| {r.Entry.QuestRowId} {Md(QuestName(r.Entry.QuestRowId))} | {r.Entry.Kind} | {r.Entry.RewardId} | {r.Entry.ItemId} | {Md(r.Check)} | {Md(r.Expected)} | {Md(r.Actual)} | {(r.Pass ? "pass" : "FAIL")} |");
        md.AppendLine();

        // xivapi keeps the Square Enix private-use glyphs some quest names start with; the data file strips them.
        static bool Same(string a, string b) => string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
        static string Clean(string s) => new string(s.Where(c => c < '\uE000' || c > '\uF8FF').ToArray()).Trim();
        static string ZoneOf(string name) => name.Length > 17 ? name["Aether Current (".Length..^1] : string.Empty;
    }

    private HashSet<string>? zoneNames;

    private HashSet<string> ZoneNames()
    {
        if (zoneNames is not null) return zoneNames;
        zoneNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in g.AetherCurrentSets)
        {
            var zone = set.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText().Trim();
            if (!string.IsNullOrEmpty(zone)) zoneNames.Add(zone);
        }
        return zoneNames;
    }

    /// <summary>Deterministic sample: up to three per kind, then random fill to the requested size.</summary>
    private List<UniqueRewardEntry> Sample(IReadOnlyList<UniqueRewardEntry> entries)
    {
        var rng = new Random(o.Seed);
        var ordered = entries.OrderBy(e => e.QuestRowId).ThenBy(e => e.Kind).ThenBy(e => e.RewardId).ToList();
        var picked = new List<UniqueRewardEntry>();
        var pickedSet = new HashSet<UniqueRewardEntry>();
        foreach (var grp in ordered.GroupBy(e => e.Kind).OrderBy(x => x.Key))
        {
            var pool = grp.ToList();
            for (var i = 0; i < 3 && pool.Count > 0; i++)
            {
                var idx = rng.Next(pool.Count);
                var e = pool[idx];
                pool.RemoveAt(idx);
                if (pickedSet.Add(e)) picked.Add(e);
            }
        }
        var rest = ordered.Where(e => !pickedSet.Contains(e)).ToList();
        while (picked.Count < o.SampleSize && rest.Count > 0)
        {
            var idx = rng.Next(rest.Count);
            picked.Add(rest[idx]);
            rest.RemoveAt(idx);
        }
        return picked.OrderBy(e => e.QuestRowId).ThenBy(e => e.Kind).ThenBy(e => e.RewardId).ToList();
    }

    private static string? SheetOf(RewardKind kind) => kind switch
    {
        RewardKind.Emote => "Emote",
        RewardKind.Mount => "Mount",
        RewardKind.Minion => "Companion",
        RewardKind.Orchestrion => "Orchestrion",
        RewardKind.TripleTriadCard => "TripleTriadCard",
        RewardKind.Ornament => "Ornament",
        RewardKind.Barding => "BuddyEquip",
        RewardKind.Action => "Action",
        RewardKind.GeneralAction => "GeneralAction",
        RewardKind.Trait => "Trait",
        RewardKind.ClassJob => "ClassJob",
        RewardKind.AetherCurrent => "AetherCurrent",
        RewardKind.BlueMageSpell => "AozAction",
        RewardKind.Achievement => "Achievement",
        RewardKind.Title => "Title",
        RewardKind.DutyUnlock => "ContentFinderCondition",
        RewardKind.Other => "QuestRewardOther",
        _ => null,
    };

    private static string FieldsOf(string sheet) => sheet switch
    {
        "Mount" or "Companion" or "Ornament" => "Singular",
        "Title" => "Masculine",
        "AetherCurrent" => "Quest",
        "AozAction" => "Action.Name",
        _ => "Name",
    };

    private static uint AchievementId(string source)
    {
        const string marker = "achievement=";
        var idx = source.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return 0;
        var start = idx + marker.Length;
        var end = start;
        while (end < source.Length && char.IsDigit(source[end])) end++;
        return uint.TryParse(source.AsSpan(start, end - start), out var v) ? v : 0;
    }

    // ----------------------------------------------------------------------------------------------------------
    // 5. Exclusivity

    private void Exclusivity(IReadOnlyList<UniqueRewardEntry> entries)
    {
        md.AppendLine("## 5. Exclusivity of Item entries");
        md.AppendLine();

        var items = entries.Where(e => e.Kind is RewardKind.Item or RewardKind.OptionalItem && e.Confidence == Confidence.Static).ToList();
        var questsPerItem = items.GroupBy(e => e.ItemId).ToDictionary(x => x.Key, x => x.Select(e => e.QuestRowId).Distinct().Count());

        md.AppendLine($"Static Item/OptionalItem entries: **{items.Count}**.");
        md.AppendLine();
        md.AppendLine("### ItemUICategory distribution");
        md.AppendLine();
        md.AppendLine("| ItemUICategory | Entries | Consumable-like | Of which unlock items | Examples |");
        md.AppendLine("|---|---:|---|---:|---|");
        foreach (var grp in items.GroupBy(e => CategoryName(e.ItemId)).OrderByDescending(x => x.Count()).ThenBy(x => x.Key))
        {
            var examples = grp.Select(e => e.RewardName).Distinct().Take(4);
            var consumable = UniqueRewardGenerator.NonExclusiveCategories.Contains(grp.Key);
            md.AppendLine($"| {Md(grp.Key)} | {grp.Count()} | {(consumable ? "yes" : string.Empty)} | {(consumable ? grp.Count(e => IsUnlockItem(e.ItemId)) : 0)} | {Md(string.Join(", ", examples))} |");
        }
        md.AppendLine();

        // Which gil shop menus sell the items flagged GilShopItem, and which NPCs host those menus. The Calamity Salvager's
        // quest-reward menus only re-sell what the character already earned; every other menu is a real vendor.
        var shopByItem = new Dictionary<uint, List<uint>>();
        var shopIds = new HashSet<uint>();
        foreach (var row in g.GilShopItems.Flatten())
        {
            if (row.Item.RowId == 0 || !questsPerItem.ContainsKey(row.Item.RowId)) continue;
            if (!shopByItem.TryGetValue(row.Item.RowId, out var list)) shopByItem[row.Item.RowId] = list = new List<uint>();
            if (!list.Contains(row.RowId)) list.Add(row.RowId);
            shopIds.Add(row.RowId);
        }
        var npcByShop = new Dictionary<uint, SortedSet<string>>();
        foreach (var npc in g.ENpcBases)
        {
            foreach (var slot in npc.ENpcData)
            {
                if (slot.RowId == 0 || !shopIds.Contains(slot.RowId)) continue;
                var name = UniqueRewardGenerator.Text(g.ENpcResidents.GetRowOrDefault(npc.RowId)?.Singular);
                if (name.Length == 0) continue;
                if (!npcByShop.TryGetValue(slot.RowId, out var set)) npcByShop[slot.RowId] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(name);
            }
        }
        string ShopName(uint id)
        {
            var n = UniqueRewardGenerator.Text(g.GilShops.GetRowOrDefault(id)?.Name);
            return n.Length == 0 ? $"GilShop {id}" : n;
        }
        bool SoldByVendor(uint itemId) => shopByItem.TryGetValue(itemId, out var shops) && shops.Any(s => !UniqueRewardGenerator.IsReacquisitionMenu(ShopName(s)));

        md.AppendLine("### Gil shop menus selling flagged items");
        md.AppendLine();
        md.AppendLine($"Items with a GilShopItem row: **{shopByItem.Count}**; sold only through quest-reward reacquisition menus: **{shopByItem.Count(kv => !SoldByVendor(kv.Key))}**; sold by a real vendor menu: **{shopByItem.Count(kv => SoldByVendor(kv.Key))}**.");
        md.AppendLine();
        md.AppendLine("| Menu | NPC(s) | Reacquisition | Items | Examples |");
        md.AppendLine("|---|---|---|---:|---|");
        foreach (var grp in shopByItem.SelectMany(kv => kv.Value.Select(s => (Shop: s, Item: kv.Key))).GroupBy(x => x.Shop).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).Take(25))
        {
            var name = ShopName(grp.Key);
            var npcs = npcByShop.TryGetValue(grp.Key, out var set) ? string.Join(", ", set.Take(3)) : "(no ENpc)";
            var examples = string.Join(", ", grp.Select(x => UniqueRewardGenerator.Text(g.Items.GetRowOrDefault(x.Item)?.Name)).Distinct().Take(3));
            md.AppendLine($"| {Md(name)} | {Md(npcs)} | {(UniqueRewardGenerator.IsReacquisitionMenu(name) ? "yes" : "no")} | {grp.Count()} | {Md(examples)} |");
        }
        md.AppendLine();

        md.AppendLine("### 20 most suspicious Static entries");
        md.AppendLine();
        md.AppendLine("Score: +100 consumable-like ItemUICategory, +40 stack size above 1, +25 each for SpecialShop / Recipe / GatheringItem, +10 sold through a gil shop menu that is not a quest-reward reacquisition menu, +5 per additional quest handing out the same item.");
        md.AppendLine();
        md.AppendLine("| Score | Quest | Kind | Item | Category | Stack | Quests | Source |");
        md.AppendLine("|---:|---|---|---|---|---:|---:|---|");
        var scored = items.Select(e =>
        {
            var item = g.Items.GetRowOrDefault(e.ItemId);
            var cat = CategoryName(e.ItemId);
            var stack = item?.StackSize ?? 0;
            var score = 0;
            if (UniqueRewardGenerator.NonExclusiveCategories.Contains(cat) && !IsUnlockItem(e.ItemId)) score += 100;
            if (stack > 1) score += 40;
            if (e.Source.Contains("SpecialShop")) score += 25;
            if (e.Source.Contains("Recipe")) score += 25;
            if (e.Source.Contains("GatheringItem")) score += 25;
            if (SoldByVendor(e.ItemId)) score += 10;
            score += 5 * (questsPerItem[e.ItemId] - 1);
            return (Entry: e, Score: score, Category: cat, Stack: stack);
        })
        .OrderByDescending(x => x.Score).ThenBy(x => x.Entry.ItemId).ThenBy(x => x.Entry.QuestRowId)
        .Take(20);
        foreach (var s in scored)
            md.AppendLine($"| {s.Score} | {s.Entry.QuestRowId} {Md(QuestName(s.Entry.QuestRowId))} | {s.Entry.Kind} | {s.Entry.ItemId} {Md(s.Entry.RewardName)} | {Md(s.Category)} | {s.Stack} | {questsPerItem[s.Entry.ItemId]} | `{s.Entry.Source}` |");
        md.AppendLine();

        var stillSuspicious = items.Count(e => UniqueRewardGenerator.NonExclusiveCategories.Contains(CategoryName(e.ItemId)) && !IsUnlockItem(e.ItemId));
        if (stillSuspicious > 0)
            failures.Add($"exclusivity: {stillSuspicious} Static Item/OptionalItem entries sit in a consumable-like ItemUICategory without an unlock ItemAction (generator run with --keep-nonexclusive-items?)");
        var unlockItems = items.Count(e => IsUnlockItem(e.ItemId));
        if (unlockItems > 0)
            notes.Add($"{unlockItems} Item entries are unlock items kept despite a consumable-like category (framer's kits, field notes)");
    }

    /// <summary>Framer's kits, field notes and facewear: ItemAction unlocks the generator keeps whatever the item's category.</summary>
    private bool IsUnlockItem(uint itemId)
    {
        var item = g.Items.GetRowOrDefault(itemId);
        var action = item is { } i && i.ItemAction.RowId != 0 ? g.ItemActions.GetRowOrDefault(i.ItemAction.RowId) : null;
        return action is { } a && (UniqueRewardGenerator.UnlockItemActions.Contains(a.Action.RowId) || a.Action.RowId == 37312); // 37312: facewear, shipped as an Item
    }

    private string CategoryName(uint itemId)
    {
        var item = g.Items.GetRowOrDefault(itemId);
        var name = UniqueRewardGenerator.Text(item?.ItemUICategory.ValueNullable?.Name);
        return name.Length == 0 ? "(none)" : name;
    }

    // ----------------------------------------------------------------------------------------------------------
    // 6. Generator diagnostics

    private void GeneratorDiagnostics()
    {
        md.AppendLine("## 6. Generator diagnostics");
        md.AppendLine();
        md.AppendLine("Which sheet `Quest.Reward[0]` points at per `ItemRewardType` (Lumina resolves the union from the schema). The generator's rules must agree with this table.");
        md.AppendLine();
        md.AppendLine("| ItemRewardType | Reward[0] row type | Quests | Example |");
        md.AppendLine("|---:|---|---:|---|");
        var groups = g.Quests
            .Where(q => q.RowId != 0 && q.Reward.Count > 0 && q.Reward[0].RowId != 0)
            .GroupBy(q => (q.ItemRewardType, Type: q.Reward[0].RowType?.Name ?? "(untyped)"))
            .OrderBy(x => x.Key.ItemRewardType).ThenBy(x => x.Key.Type);
        foreach (var grp in groups)
        {
            var ex = grp.OrderBy(q => q.RowId).First();
            md.AppendLine($"| {grp.Key.ItemRewardType} | {grp.Key.Type} | {grp.Count()} | {ex.RowId} {Md(UniqueRewardGenerator.Text(ex.Name))} |");
        }
        md.AppendLine();
    }

    // ----------------------------------------------------------------------------------------------------------
    // Helpers

    private string QuestName(uint rowId) => UniqueRewardGenerator.Text(g.Quests.GetRowOrDefault(rowId)?.Name);

    private static string OtherSource(UniqueRewardEntry e)
    {
        const string marker = ";otherSource=";
        var idx = e.Source.IndexOf(marker, StringComparison.Ordinal);
        return idx < 0 ? string.Empty : e.Source[(idx + marker.Length)..];
    }

    private static string Describe(UniqueRewardEntry e) => $"{e.QuestRowId} {e.Kind} {e.RewardId} '{e.RewardName}'";

    private static string Md(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
