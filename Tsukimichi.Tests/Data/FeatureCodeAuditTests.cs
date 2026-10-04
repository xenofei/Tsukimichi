using Lumina.Data;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Sheets = Lumina.Excel.Sheets;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The feature-code audit (feature plan v7 K5, <c>docs/data/unlock-audit.md</c>): every <c>Quest.SystemReward</c> value
/// on a quest still in the game is mapped. The first slot is a Trait row whose own quest is that quest (the index shows
/// it as a trait); the second is an unlock link, the game's flag for a feature the quest opens, which
/// <c>curated/system_unlocks.json</c> must name unless <see cref="NotAFeature"/> says why not. A patch's new code fails
/// here with its quests until it is mapped.
/// </summary>
public sealed class FeatureCodeAuditTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    /// <summary>The class unlock every "Way of the &lt;class&gt;" quest sets: the index shows the class as a Job row.</summary>
    private const uint ClassUnlock = 21;

    /// <summary>Codes that are no feature of their own for the Unlocks section, each with why (the audit's table).</summary>
    private static readonly Dictionary<uint, string> NotAFeature = new()
    {
        [3] = "The Scions of the Seventh Dawn: no text says what it opens; the wiki names Retainers, which An Ill-conceived Venture (code 93) opens; most likely the flag that makes the retainer quest available",
        [6] = "the Armoury system: every class's level-10 quest sets it, but only the first one done opens it, so naming it on eight quests would mislead",
        [19] = "Austerities of Earth: Summon Titan, shown as its action",
        [20] = "Austerities of Wind: Summon Garuda, shown as its action",
        [ClassUnlock] = "the class unlock of a Way of the <class> quest, shown as its Job row",
        [94] = "Magiteknical Difficulties: the magitek armor's cannons on the pet hotbar, no feature of its own",
        [227] = "Yes We Cant: a seasonal pet-hotbar action used in one FATE",
        [247] = "Hearts on Fire: no text says what it opens (the wiki names a training hall)",
        [522] = "The Phantom Village: an Occult Record lore entry",
        [523] = "Unfamiliar Territory: an Occult Record lore entry (the field operation shows as its duty)",
        [524] = "Past and Crescent: an Occult Record lore entry",
        [536] = "The Ancient Arts of War: an Occult Record lore entry",
        [632] = "Wisdom's End: an Occult Record lore entry",
    };

    [GameDataFact]
    public void Every_feature_code_of_a_live_quest_is_mapped()
    {
        var catalog = game.Bundle.Catalog;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var traits = game.Game.GetExcelSheet<Sheets.Trait>(Language.English)!;
        var problems = new List<string>();
        var seen = new HashSet<uint>();
        var traitsChecked = 0;
        foreach (var quest in game.Game.GetExcelSheet<Sheets.Quest>(Language.English)!)
        {
            if (catalog.GetByRowId(quest.RowId) is not { IsRemoved: false } record)
            {
                continue;
            }

            var trait = (uint)quest.SystemReward[0];
            if (trait != 0)
            {
                traitsChecked++;
                if (traits.GetRowOrDefault(trait) is not { } row || row.Quest.RowId != quest.RowId)
                {
                    problems.Add($"{quest.RowId} {record.Name}: SystemReward[0] {trait} is no trait of this quest");
                }
            }

            var code = quest.SystemReward.Count > 1 ? (uint)quest.SystemReward[1] : 0u;
            if (code == 0)
            {
                continue;
            }

            seen.Add(code);
            if (code == ClassUnlock)
            {
                if (!unique.Entries.Any(e => e.QuestRowId == quest.RowId && e.Kind == RewardKind.ClassJob))
                {
                    problems.Add($"{quest.RowId} {record.Name}: feature code 21 (class unlock), but the reward data names no class");
                }
            }
            else if (!NotAFeature.ContainsKey(code) && !curated.SystemUnlocks.ContainsKey(quest.RowId))
            {
                problems.Add($"{quest.RowId} {record.Name}: feature code {code} maps to no curated/system_unlocks.json entry (add one, or a reason to NotAFeature)");
            }
        }

        var stale = NotAFeature.Keys.Where(code => !seen.Contains(code)).ToList();
        Assert.True(stale.Count == 0, "NotAFeature codes no live quest carries any more: " + string.Join(", ", stale));
        Assert.True(traitsChecked >= 40, $"only {traitsChecked} trait codes");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
