using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.GameData;

/// <summary>
/// Pure mapping from Lumina sheets to <see cref="QuestRecord"/>s and the accompanying lookups.
/// Takes an <see cref="ExcelModule"/> so the same code runs inside Dalamud (IDataManager.Excel) and in tests
/// against a standalone <see cref="Lumina.GameData"/>. Sheet reads are thread-safe; call from any thread.
/// </summary>
public static class CatalogMapper
{
    /// <summary>Rows mapped between cancellation checks.</summary>
    public const int CancellationBatch = 256;

    /// <summary>Quest sheet SortKey occupies the low 16 bits of <see cref="JournalRef.SortKey"/>; the genre rank sits above it.</summary>
    private const int SortKeyGenreShift = 16;

    /// <summary>
    /// Maps every named quest. Rows with an empty name are skipped. Cancellation is honoured every
    /// <see cref="CancellationBatch"/> rows and between phases.
    /// </summary>
    /// <param name="excel">Excel module to read from.</param>
    /// <param name="language">Language for every localized string; non-localized sheets fall back to their neutral page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="log">Optional sink for one-line progress facts (row counts, skips).</param>
    /// <param name="filing">Whether the <see cref="JournalRefiler"/> runs over the mapped records (the default) or the sheet's genres stand.</param>
    /// <param name="curated">The curated overlay the refiler reads (<c>refile_overrides.json</c>, <c>retired_quests.json</c>) and the catalog takes its extra prerequisites from (<c>extra_prerequisites.json</c>); null runs the rules alone.</param>
    /// <param name="patches"><c>quest_patches.json</c>, which sets <see cref="QuestRecord.AddedIn"/> (P8); null leaves every patch unknown.</param>
    public static CatalogBundle Map(
        ExcelModule excel,
        Language language,
        CancellationToken ct = default,
        Action<string>? log = null,
        JournalFiling filing = JournalFiling.Refiled,
        CuratedData? curated = null,
        QuestPatches? patches = null)
    {
        ArgumentNullException.ThrowIfNull(excel);

        var quests = excel.GetSheet<Quest>(language);
        var journal = JournalIndex.Build(excel, language);
        var context = new Sheets(
            excel.GetSheet<Item>(language),
            excel.GetSubrowSheet<QuestClassJobReward>(language),
            excel.GetSheet<BeastRankBonus>(language),
            excel.GetSheet<QuestAcceptAdditionCondition>(language));
        ct.ThrowIfCancellationRequested();

        var records = new List<QuestRecord>(quests.Count);
        var skipped = 0;
        var seen = 0;
        foreach (var quest in quests)
        {
            if (++seen % CancellationBatch == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            var name = quest.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                continue;
            }

            records.Add(MapQuest(in quest, name, journal, context));
        }

        ct.ThrowIfCancellationRequested();
        IReadOnlyList<QuestRecord> dated = patches is null ? records : patches.Apply(records);
        IReadOnlyList<QuestRecord> filed = filing == JournalFiling.Refiled ? JournalRefiler.Apply(dated, curated ?? CuratedData.Empty) : dated;
        // The curated extra prerequisites join the sheet's at build, so every reader of PrerequisitesOf sees them.
        var catalog = QuestCatalog.Build(filed, curated?.ExtraPrerequisiteIds);
        // The choice groups' labels and guards (feature plan v4 D1); without curated data only the rule-found sets.
        Core.Evaluation.PathIndex.Attach(catalog, curated?.PathChoices);
        var jobs = ClassJobCategoryLookup.Build(excel, language);
        ct.ThrowIfCancellationRequested();
        var names = ReadNames(excel, language, jobs);

        log?.Invoke($"Quest sheet: {quests.Count} rows, {records.Count} named, {skipped} skipped; {journal.GenreCount} journal genres; {jobs.Count} class/job categories");
        if (filing == JournalFiling.Refiled)
        {
            var refiled = 0;
            var retired = 0;
            var unlisted = new List<uint>();
            foreach (var quest in filed)
            {
                if (quest.IsRetired)
                {
                    retired++;
                }
                else if (quest.IsUnlisted)
                {
                    // Rule 7, or a rule or override whose genre no listed quest holds: worth a row id in the log.
                    unlisted.Add(quest.RowId);
                }
                else if (quest.RefiledFrom != 0)
                {
                    refiled++;
                }
            }

            var unlistedIds = unlisted.Count == 0 ? string.Empty : " (" + string.Join(", ", unlisted) + ")";
            log?.Invoke($"Journal refiling: {refiled} quests filed into a genre, {retired} retired, {unlisted.Count} left unlisted{unlistedIds}");
        }

        return new CatalogBundle(catalog, names, jobs, language.ToString());
    }

    /// <summary>Sheet join byte to <see cref="JoinKind"/>: 2 means any, everything else (1, or 0 when unused) means all.</summary>
    public static JoinKind ToJoin(byte join) => join == 2 ? JoinKind.Any : JoinKind.All;

    private static QuestRecord MapQuest(in Quest quest, string name, JournalIndex journal, Sheets sheets)
    {
        var previous = NonZero(quest.PreviousQuest);
        var instances = NonZero(quest.InstanceContent);

        return new QuestRecord
        {
            RowId = quest.RowId,
            QuestId = QuestRecord.ToQuestId(quest.RowId),
            InternalId = quest.Id.ExtractText(),
            Name = name,
            Journal = journal.Resolve(quest.JournalGenre.RowId, quest.SortKey),

            Expansion = ToByte(quest.Expansion.RowId),
            Level = ToByte(quest.ClassJobLevel.Count > 0 ? quest.ClassJobLevel[0] : 0u),
            LevelMax = quest.LevelMax,
            LevelOffset = quest.QuestLevelOffset,
            StepCount = StepCountOf(in quest),

            ClassJobCategory = quest.ClassJobCategory0.RowId,
            ClassJobCategory1 = quest.ClassJobCategory1.RowId,
            ClassJobRequired = quest.ClassJobRequired.RowId,

            PreviousQuests = previous.Length == 0 ? Prereq.None : new Prereq(previous, ToJoin(quest.PreviousQuestJoin)),
            QuestLocks = NonZero(quest.QuestLock),
            InstanceContentRequired = instances,
            InstanceJoin = ToJoin(quest.InstanceContentJoin),

            GrandCompany = ToByte(quest.GrandCompany.RowId),
            GrandCompanyRank = ToByte(quest.GrandCompanyRank.RowId),
            BeastTribe = ToByte(quest.BeastTribe.RowId),
            BeastRank = ToByte(quest.BeastReputationRank.RowId),
            // With current data every quest carries 0 or 65535 here. 65535 is on the society story quests (the rank-up
            // quests among them) and means "this rank's reputation maxed" (BeastReputationMaxed; the amount is the
            // rank's BeastReputationRank.RequiredReputation); 0 is no reputation gate. A real number is kept for a
            // sheet that ever holds one.
            BeastValue = quest.BeastReputationValue == ushort.MaxValue ? (ushort)0 : quest.BeastReputationValue,
            BeastReputationMaxed = quest.BeastReputationValue == ushort.MaxValue && quest.BeastTribe.RowId != 0,
            RepeatFlag = ToByte(quest.QuestRepeatFlag.RowId),

            IsRepeatable = quest.IsRepeatable,
            RepeatInterval = quest.RepeatIntervalType,
            DailyPool = quest.DailyQuestPool,
            Festival = ToUInt16(quest.Festival.RowId),
            FestivalBegin = quest.FestivalBegin,
            FestivalEnd = quest.FestivalEnd,
            SatisfactionNpc = ToByte(quest.SatisfactionNpc.RowId),
            SatisfactionLevel = quest.SatisfactionLevel,
            CarrierLevel = ToByte(quest.DeliveryQuest.RowId),

            MountRequired = quest.MountRequired.RowId != 0,
            HouseRequired = quest.IsHouseRequired,
            AcceptConditions = MapAcceptConditions(quest.RowId, sheets.AcceptConditions),

            Issuer = MapIssuer(in quest),
            Icon = quest.Icon,
            IconSpecial = quest.IconSpecial,
            EventIconType = ToByte(quest.EventIconType.RowId),
            // The unnamed bool between HideOfferIcon and HideInScenarioGuide; Lumina numbers unknown columns per
            // release, so HiddenFlagColumnTests pins the column against the sheet's own header.
            IsHidden = quest.Unknown12,

            Rewards = MapRewards(in quest, sheets),
            ExpFactor = quest.ExpFactor,
            Gil = quest.GilReward,
        };
    }

    /// <summary>
    /// Journal steps: the distinct non-zero <c>ToDoCompleteSeq</c> values across the quest's objectives. Every quest
    /// with objectives runs 1, 2, … then 255 (checked against the 7.3 sheets: no gaps), so the count is the number of
    /// steps and an accepted quest's sequence is its current step, 255 the last. Objectives sharing a sequence (three
    /// people to talk to) are one step.
    /// </summary>
    private static byte StepCountOf(in Quest quest)
    {
        Span<bool> seen = stackalloc bool[256];
        var count = 0;
        foreach (var todo in quest.TodoParams)
        {
            var sequence = todo.ToDoCompleteSeq;
            if (sequence != 0 && !seen[sequence])
            {
                seen[sequence] = true;
                count++;
            }
        }

        return (byte)Math.Min(count, byte.MaxValue);
    }

    /// <summary>
    /// QuestAcceptAdditionCondition is keyed by quest row id and carries two quest references plus one unknown uint.
    /// The non-zero values are kept in slot order (Requirement0, Requirement1, Unknown0); empty when no row exists or
    /// every slot is zero, so a quest never shows an accept condition it does not have. Most values are Quest row ids
    /// (Unknown0 included: Endwalker, Dawntrail, Crossroads); a few are small ids of some other sheet. The catalog
    /// judges the quest ones as previous quests (<see cref="QuestCatalog.PrerequisitesOf"/>).
    /// </summary>
    private static uint[] MapAcceptConditions(uint questRowId, ExcelSheet<QuestAcceptAdditionCondition> sheet)
    {
        if (sheet.GetRowOrDefault(questRowId) is not { } row)
        {
            return [];
        }

        Span<uint> slots = [row.Requirement0.RowId, row.Requirement1.RowId, row.Unknown0];
        var count = 0;
        foreach (var slot in slots)
        {
            if (slot != 0)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var result = new uint[count];
        var i = 0;
        foreach (var slot in slots)
        {
            if (slot != 0)
            {
                result[i++] = slot;
            }
        }

        return result;
    }

    private static Issuer? MapIssuer(in Quest quest)
    {
        var start = quest.IssuerStart;
        var level = quest.IssuerLocation.RowId != 0 ? quest.IssuerLocation.ValueNullable : null;
        if (start.RowId == 0 && level is null)
        {
            return null;
        }

        var name = string.Empty;
        if (start.RowId != 0)
        {
            if (start.RowType == typeof(ENpcResident))
            {
                name = start.GetValueOrDefault<ENpcResident>()?.Singular.ExtractText() ?? string.Empty;
            }
            else if (start.RowType == typeof(EObjName))
            {
                name = start.GetValueOrDefault<EObjName>()?.Singular.ExtractText() ?? string.Empty;
            }
        }

        // Raw Level coordinates; the UI converts to map coordinates when it builds a map link.
        return new Issuer(
            start.RowId,
            name,
            level?.Territory.RowId ?? 0,
            level?.Map.RowId ?? 0,
            level?.X ?? 0f,
            level?.Y ?? 0f,
            level?.Z ?? 0f);
    }

    private static IReadOnlyList<RewardRef> MapRewards(in Quest quest, Sheets sheets)
    {
        var rewards = new List<RewardRef>();

        // Reward slots are untyped in the sheet; ItemRewardType picks the target sheet (1/3/5 Item, 6 QuestClassJobReward, 7 BeastRankBonus).
        for (var i = 0; i < quest.Reward.Count; i++)
        {
            var slot = quest.Reward[i];
            if (slot.RowId == 0)
            {
                continue;
            }

            var count = i < quest.ItemCountReward.Count ? quest.ItemCountReward[i] : (byte)0;
            if (slot.RowType == typeof(Item))
            {
                if (sheets.Items.GetRowOrDefault(slot.RowId) is { } item)
                {
                    rewards.Add(ItemReward(RewardKind.Item, in item, count));
                }
            }
            else if (slot.RowType == typeof(QuestClassJobReward))
            {
                AddClassJobRewards(rewards, slot.RowId, sheets);
            }
            else if (slot.RowType == typeof(BeastRankBonus))
            {
                if (sheets.BeastRankBonus.GetRowOrDefault(slot.RowId) is { } bonus && bonus.Item.RowId != 0 && bonus.Item.ValueNullable is { } bonusItem)
                {
                    // Quantity scales with reputation rank; the first non-zero entry is the base amount.
                    var quantity = bonus.ItemQuantity.FirstOrDefault(q => q != 0);
                    rewards.Add(ItemReward(RewardKind.Item, in bonusItem, quantity));
                }
            }
            else
            {
                rewards.Add(new RewardRef(RewardKind.Other, slot.RowId, 0, Math.Max(count, (byte)1), string.Empty, 0));
            }
        }

        for (var i = 0; i < quest.OptionalItemReward.Count; i++)
        {
            var slot = quest.OptionalItemReward[i];
            if (slot.RowId == 0 || slot.ValueNullable is not { } item)
            {
                continue;
            }

            var count = i < quest.OptionalItemCountReward.Count ? quest.OptionalItemCountReward[i] : (byte)0;
            rewards.Add(ItemReward(RewardKind.OptionalItem, in item, count));
        }

        // Currencies (tomestones, seals, scrips) are Item rows but not collectible items; Other keeps them out of
        // the Item reward filter while the item row still rides along in ItemId for the icon and name.
        if (quest.CurrencyReward.RowId != 0 && quest.CurrencyReward.ValueNullable is { } currency)
        {
            rewards.Add(new RewardRef(RewardKind.Other, currency.RowId, currency.RowId, Math.Max(quest.CurrencyRewardCount, 1u), currency.Name.ExtractText(), currency.Icon));
        }

        if (quest.EmoteReward.RowId != 0 && quest.EmoteReward.ValueNullable is { } emote)
        {
            rewards.Add(new RewardRef(RewardKind.Emote, emote.RowId, 0, 1, emote.Name.ExtractText(), emote.Icon));
        }

        if (quest.ActionReward.RowId != 0 && quest.ActionReward.ValueNullable is { } action)
        {
            rewards.Add(new RewardRef(RewardKind.Action, action.RowId, 0, 1, action.Name.ExtractText(), action.Icon));
        }

        foreach (var generalRef in quest.GeneralActionReward)
        {
            if (generalRef.RowId != 0 && generalRef.ValueNullable is { } general)
            {
                rewards.Add(new RewardRef(RewardKind.GeneralAction, general.RowId, 0, 1, general.Name.ExtractText(), general.Icon > 0 ? (uint)general.Icon : 0u));
            }
        }

        if (quest.InstanceContentUnlock.RowId != 0 && quest.InstanceContentUnlock.ValueNullable is { } instance)
        {
            var condition = instance.ContentFinderCondition.RowId != 0 ? instance.ContentFinderCondition.ValueNullable : null;
            rewards.Add(new RewardRef(
                RewardKind.Instance,
                instance.RowId,
                0,
                1,
                condition?.Name.ExtractText() ?? string.Empty,
                condition?.Icon ?? 0));
        }

        if (quest.ClassJobUnlock.RowId != 0 && quest.ClassJobUnlock.ValueNullable is { } classJob)
        {
            rewards.Add(new RewardRef(RewardKind.ClassJob, classJob.RowId, 0, 1, NameCase.Title(classJob.Name.ExtractText()), 0));
        }

        if (quest.OtherReward.RowId != 0 && quest.OtherReward.ValueNullable is { } other)
        {
            rewards.Add(new RewardRef(RewardKind.Other, other.RowId, 0, 1, other.Name.ExtractText(), other.Icon));
        }

        return rewards.Count == 0 ? [] : rewards.ToArray();
    }

    /// <summary>
    /// A QuestClassJobReward row lists, per class/job category, up to four items. The catalog flattens every distinct item:
    /// gear as <see cref="RewardKind.ArtifactGear"/> with the QuestClassJobReward row as <see cref="RewardRef.Id"/>, and
    /// anything else (crystals, Cordials, society currencies) as an ordinary <see cref="RewardKind.Item"/> reward
    /// (<see cref="ClassJobRewardItems"/>).
    /// </summary>
    private static void AddClassJobRewards(List<RewardRef> rewards, uint rowId, Sheets sheets)
    {
        if (sheets.ClassJobRewards.GetRowOrDefault(rowId) is not { } subrows)
        {
            return;
        }

        HashSet<uint>? seen = null;
        foreach (var subrow in subrows)
        {
            for (var k = 0; k < subrow.RewardItem.Count; k++)
            {
                var itemRef = subrow.RewardItem[k];
                if (itemRef.RowId == 0 || !(seen ??= []).Add(itemRef.RowId) || itemRef.ValueNullable is not { } item)
                {
                    continue;
                }

                var amount = k < subrow.RewardAmount.Count ? subrow.RewardAmount[k] : (byte)0;
                rewards.Add(ClassJobRewardItems.IsArtifactGear(in item)
                    ? new RewardRef(RewardKind.ArtifactGear, rowId, item.RowId, Math.Max(amount, (byte)1), item.Name.ExtractText(), item.Icon)
                    : ItemReward(RewardKind.Item, in item, amount));
            }
        }
    }

    private static RewardRef ItemReward(RewardKind kind, in Item item, byte count)
        => new(kind, item.RowId, item.RowId, Math.Max(count, (byte)1), item.Name.ExtractText(), item.Icon);

    /// <summary>ClassJobCategory rows "Disciples of the Land" and "Disciples of the Hand"; membership marks gatherers and crafters.</summary>
    private const uint DisciplesOfTheLandCategory = 32;
    private const uint DisciplesOfTheHandCategory = 33;

    private static GameNames ReadNames(ExcelModule excel, Language language, ClassJobCategoryLookup jobs)
    {
        var classJobs = new Dictionary<uint, string>();
        var abbreviations = new Dictionary<uint, string>();
        var infos = new List<ClassJobInfo>();
        foreach (var job in excel.GetSheet<ClassJob>(language))
        {
            // Rows past the last real job carry an abbreviation and an exp slot but no name; they are not jobs.
            // The sheet spells names in lower case ("paladin"); they are shown as titles everywhere.
            var abbreviation = job.Abbreviation.ExtractText();
            var name = NameCase.Title(job.Name.ExtractText());
            if (abbreviation.Length == 0 || name.Length == 0)
            {
                continue;
            }

            classJobs[job.RowId] = name;
            abbreviations[job.RowId] = abbreviation;

            var id = job.RowId <= byte.MaxValue ? (byte)job.RowId : (byte)0;
            infos.Add(new ClassJobInfo(
                job.RowId,
                name,
                abbreviation,
                job.ClassJobParent.RowId,
                job.UnlockQuest.RowId,
                job.Role,
                IsCrafter: id != 0 && jobs.Admits(DisciplesOfTheHandCategory, id),
                IsGatherer: id != 0 && jobs.Admits(DisciplesOfTheLandCategory, id),
                job.ExpArrayIndex,
                IsLimited: job.IsLimitedJob));
        }

        return new GameNames(
            Names(excel.GetSheet<BeastTribe>(language), static (in BeastTribe r) => r.Name),
            Names(excel.GetSheet<GrandCompany>(language), static (in GrandCompany r) => r.Name),
            Names(excel.GetSheet<ExVersion>(language), static (in ExVersion r) => r.Name),
            classJobs,
            abbreviations,
            Names(excel.GetSheet<BeastReputationRank>(language), static (in BeastReputationRank r) => r.Name),
            infos,
            Names(excel.GetSheet<ClassJobCategory>(language), static (in ClassJobCategory r) => r.Name),
            DutyNames(excel.GetSheet<ContentFinderCondition>(language)),
            SatisfactionNpcNames(excel.GetSheet<SatisfactionNpc>(language)));
    }

    /// <summary>
    /// Custom delivery client names keyed by SatisfactionNpc row id (what <see cref="QuestRecord.SatisfactionNpc"/>
    /// holds), from the ENpcResident row each client points at ("M'naago", "Kurenai"). Row 0 is empty and skipped.
    /// </summary>
    private static Dictionary<uint, string> SatisfactionNpcNames(ExcelSheet<SatisfactionNpc> sheet)
    {
        var result = new Dictionary<uint, string>();
        foreach (var row in sheet)
        {
            if (row.Npc.RowId == 0 || row.Npc.ValueNullable is not { } npc)
            {
                continue;
            }

            var text = npc.Singular.ExtractText();
            if (text.Length != 0)
            {
                result[row.RowId] = text;
            }
        }

        return result;
    }

    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    /// <summary>
    /// Duty names keyed by InstanceContent row id, from the Duty Finder entry that links the instance: what
    /// <see cref="QuestRecord.InstanceContentRequired"/> refers to. The first named entry per instance wins.
    /// </summary>
    private static Dictionary<uint, string> DutyNames(ExcelSheet<ContentFinderCondition> sheet)
    {
        var result = new Dictionary<uint, string>();
        foreach (var row in sheet)
        {
            if (row.ContentLinkType != InstanceContentLink || row.Content.RowId == 0 || result.ContainsKey(row.Content.RowId))
            {
                continue;
            }

            var text = row.Name.ExtractText();
            if (text.Length != 0)
            {
                // The sheet writes "the Vault"; a line opens with the name, so its first letter is raised.
                result[row.Content.RowId] = char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
            }
        }

        return result;
    }

    private delegate Lumina.Text.ReadOnly.ReadOnlySeString NameOf<T>(in T row);

    private static Dictionary<uint, string> Names<T>(ExcelSheet<T> sheet, NameOf<T> name)
        where T : struct, IExcelRow<T>
    {
        var result = new Dictionary<uint, string>(sheet.Count);
        foreach (var row in sheet)
        {
            var text = name(in row).ExtractText();
            if (text.Length != 0)
            {
                result[row.RowId] = text;
            }
        }

        return result;
    }

    private static uint[] NonZero<T>(Collection<RowRef<T>> refs)
        where T : struct, IExcelRow<T>
    {
        var count = 0;
        foreach (var r in refs)
        {
            if (r.RowId != 0)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var result = new uint[count];
        var i = 0;
        foreach (var r in refs)
        {
            if (r.RowId != 0)
            {
                result[i++] = r.RowId;
            }
        }

        return result;
    }

    private static byte ToByte(uint value) => value > byte.MaxValue ? byte.MaxValue : (byte)value;

    private static ushort ToUInt16(uint value) => value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;

    private sealed record Sheets(
        ExcelSheet<Item> Items,
        SubrowExcelSheet<QuestClassJobReward> ClassJobRewards,
        ExcelSheet<BeastRankBonus> BeastRankBonus,
        ExcelSheet<QuestAcceptAdditionCondition> AcceptConditions);

    /// <summary>
    /// Journal genre → (section, category, genre) names plus a rank that orders genres by section, then category, then
    /// genre row id, matching the in-game journal. The quest's own SortKey fills the low 16 bits of the composite key.
    /// Genre 0 (unlisted quests) sorts after every listed genre and keeps the section and category ids the sheet's
    /// row 0 points at (category 0 under section 255) but none of their names: row 0 is a placeholder whose category
    /// reads "Sephiroth Missions", and no quest is filed there. The tree and query layers never place unlisted quests
    /// under a journal node, so the ids cannot be mistaken for the real section 0.
    /// </summary>
    private sealed class JournalIndex
    {
        private readonly Dictionary<uint, JournalRef> templates;
        private readonly Dictionary<uint, int> ranks;
        private readonly JournalRef unlisted;
        private readonly int unlistedRank;

        private JournalIndex(Dictionary<uint, JournalRef> templates, Dictionary<uint, int> ranks, JournalRef unlisted, int unlistedRank)
        {
            this.templates = templates;
            this.ranks = ranks;
            this.unlisted = unlisted;
            this.unlistedRank = unlistedRank;
        }

        public int GenreCount => templates.Count;

        public static JournalIndex Build(ExcelModule excel, Language language)
        {
            var templates = new Dictionary<uint, JournalRef>();
            var unlisted = JournalRef.None;
            foreach (var genre in excel.GetSheet<JournalGenre>(language))
            {
                var template = Template(in genre);
                if (genre.RowId == 0)
                {
                    unlisted = template with { SectionName = string.Empty, CategoryName = string.Empty, GenreName = string.Empty };
                    continue;
                }

                templates[genre.RowId] = template;
            }

            var ordered = templates.Values
                .OrderBy(j => j.SectionId)
                .ThenBy(j => j.CategoryId)
                .ThenBy(j => j.GenreId)
                .ToArray();
            var ranks = new Dictionary<uint, int>(ordered.Length);
            for (var i = 0; i < ordered.Length; i++)
            {
                ranks[ordered[i].GenreId] = i;
            }

            return new JournalIndex(templates, ranks, unlisted, ordered.Length);
        }

        public JournalRef Resolve(uint genreId, ushort sortKey)
        {
            if (genreId != 0 && templates.TryGetValue(genreId, out var template))
            {
                return template with { SortKey = (ranks[genreId] << SortKeyGenreShift) | sortKey };
            }

            var unlistedKey = (unlistedRank << SortKeyGenreShift) | sortKey;
            return genreId == 0
                ? unlisted with { SortKey = unlistedKey }
                : JournalRef.None with { GenreId = genreId, SortKey = unlistedKey };
        }

        private static JournalRef Template(in JournalGenre genre)
        {
            var category = genre.JournalCategory.ValueNullable;
            var section = category?.JournalSection.ValueNullable;
            return new JournalRef(
                category?.JournalSection.RowId ?? 0,
                section?.Name.ExtractText() ?? string.Empty,
                genre.JournalCategory.RowId,
                category?.Name.ExtractText() ?? string.Empty,
                genre.RowId,
                genre.Name.ExtractText(),
                0);
        }
    }
}
