using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;

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
    public static CatalogBundle Map(ExcelModule excel, Language language, CancellationToken ct = default, Action<string>? log = null)
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
        var catalog = QuestCatalog.Build(records);
        var jobs = ClassJobCategoryLookup.Build(excel, language);
        ct.ThrowIfCancellationRequested();
        var names = ReadNames(excel, language, jobs);

        log?.Invoke($"Quest sheet: {quests.Count} rows, {records.Count} named, {skipped} skipped; {journal.GenreCount} journal genres; {jobs.Count} class/job categories");
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
            BeastValue = quest.BeastReputationValue,

            IsRepeatable = quest.IsRepeatable,
            RepeatInterval = quest.RepeatIntervalType,
            DailyPool = quest.DailyQuestPool,
            Festival = ToUInt16(quest.Festival.RowId),

            MountRequired = quest.MountRequired.RowId != 0,
            HouseRequired = quest.IsHouseRequired,
            AcceptConditions = MapAcceptConditions(quest.RowId, sheets.AcceptConditions),

            Issuer = MapIssuer(in quest),
            Icon = quest.Icon,
            IconSpecial = quest.IconSpecial,
            EventIconType = ToByte(quest.EventIconType.RowId),

            Rewards = MapRewards(in quest, sheets),
            ExpFactor = quest.ExpFactor,
            Gil = quest.GilReward,
        };
    }

    /// <summary>
    /// QuestAcceptAdditionCondition is keyed by quest row id and carries two quest references plus one unknown uint.
    /// The non-zero values are kept in slot order (Requirement0, Requirement1, Unknown0); empty when no row exists or
    /// every slot is zero, so a quest never shows an accept condition it does not have.
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
    /// A QuestClassJobReward row lists, per class/job category, up to four items. The catalog flattens every distinct item
    /// as <see cref="RewardKind.ArtifactGear"/> with the QuestClassJobReward row as <see cref="RewardRef.Id"/>.
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
                rewards.Add(new RewardRef(RewardKind.ArtifactGear, rowId, item.RowId, Math.Max(amount, (byte)1), item.Name.ExtractText(), item.Icon));
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
                job.ExpArrayIndex));
        }

        return new GameNames(
            Names(excel.GetSheet<BeastTribe>(language), static (in BeastTribe r) => r.Name),
            Names(excel.GetSheet<GrandCompany>(language), static (in GrandCompany r) => r.Name),
            Names(excel.GetSheet<ExVersion>(language), static (in ExVersion r) => r.Name),
            classJobs,
            abbreviations,
            Names(excel.GetSheet<BeastReputationRank>(language), static (in BeastReputationRank r) => r.Name),
            infos);
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
    /// Genre 0 (unlisted quests) sorts after every listed genre and keeps whatever section and category the sheet's
    /// row 0 names (usually none); the tree and query layers never place unlisted quests under a journal node, so a
    /// zero there cannot be mistaken for the real section 0.
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
                    unlisted = template;
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
