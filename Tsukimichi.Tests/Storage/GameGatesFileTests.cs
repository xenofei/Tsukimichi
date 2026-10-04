using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/game_gates.json</c>: the shape, each required field, and the gates the catalog builders take.</summary>
[Trait("Category", "Curated")]
public sealed class GameGatesFileTests : IDisposable
{
    private const string Good = """{ "gate": "a relic weapon nexus equipped", "evidence": "https://e.org/a", "note": "n" }""";

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.GameGatesFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Entries_parse_their_gate_after_quests_and_text_keys()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "note": "header",
              "entries": {
                "65897": { "gate": " a relic weapon nexus equipped ", "after": [65742], "gameTextKey": "TEXT_JOBREL015_00361_SYSTEM_000_000", "afterTextKey": "TEXT_JOBREL007_00206_SYSTEM_000_100", "evidence": "https://e.org/a", "note": "n" },
                "68478": { "gate": "Eureka progress", "evidence": "https://e.org/b", "note": "n2" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal([65897u, 68478u], data.GameGates.Keys.Order());
        var materia = data.GameGates[65897];
        Assert.Equal("a relic weapon nexus equipped", materia.Gate);
        Assert.Equal([65742u], materia.After);
        Assert.Equal("TEXT_JOBREL015_00361_SYSTEM_000_000", materia.GameTextKey);
        Assert.Equal("TEXT_JOBREL007_00206_SYSTEM_000_100", materia.AfterTextKey);
        Assert.Equal("https://e.org/a", materia.Evidence);
        Assert.Equal("n", materia.Note);

        var pagos = data.GameGates[68478];
        Assert.Empty(pagos.After);
        Assert.Null(pagos.GameTextKey);
        Assert.Null(pagos.AfterTextKey);

        // What the catalog builders take, and DataGen's copy without feature quests keeps them.
        Assert.Equal(new QuestGate("a relic weapon nexus equipped", [65742]).Gate, data.GameGateIds[65897].Gate);
        Assert.Equal([65742u], data.GameGateIds[65897].After);
        Assert.Equal(2, data.WithoutFeatureQuests().GameGates.Count);
    }

    [Fact]
    public void Gear_gates_parse_their_weapons_sorted_and_pass_them_to_the_catalog()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "entries": {
                "65897": { "gate": "a relic weapon nexus equipped", "equipped": { "sources": ["RelicItem#5"], "shield": "both", "items": [[8658, 8649], [8650]] }, "evidence": "https://e.org/a", "note": "n" },
                "67820": { "gate": "an anima weapon in your possession", "held": { "sources": ["QuestClassJobReward#6"], "items": [[13224], [13223, 13236]] }, "evidence": "https://e.org/b", "note": "n" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        var nexus = data.GameGates[65897].Items!;
        Assert.Equal(GateHold.Equipped, nexus.Hold);
        Assert.Equal(["RelicItem#5"], nexus.Sources);
        Assert.Equal("both", nexus.Shield);
        Assert.Equal([[8649u, 8658u], [8650u]], nexus.Groups);

        var anima = data.GameGates[67820].Items!;
        Assert.Equal(GateHold.Held, anima.Hold);
        Assert.Null(anima.Shield);
        Assert.Equal([[13223u, 13236u], [13224u]], anima.Groups);

        Assert.Equal(GateHold.Held, data.GameGateIds[67820].Items!.Hold);
        Assert.Equal(anima.Groups, data.GameGateIds[67820].Items!.Groups);
        Assert.Same(data.GameGates[65897].Items, data.WithoutFeatureQuests().GameGates[65897].Items);
    }

    [Theory]
    [InlineData("""{ "gate": "g", "equipped": { "sources": ["RelicItem#5"], "items": [[1]] }, "held": { "sources": ["RelicItem#5"], "items": [[2]] }, "evidence": "https://e.org", "note": "n" }""", "either equipped or held")]
    [InlineData("""{ "gate": "g", "equipped": { "items": [[1]] }, "evidence": "https://e.org", "note": "n" }""", "equipped must be")]
    [InlineData("""{ "gate": "g", "equipped": { "sources": ["RelicItem"], "items": [[1]] }, "evidence": "https://e.org", "note": "n" }""", "equipped must be")]
    [InlineData("""{ "gate": "g", "held": { "sources": ["RelicItem#5"], "items": [] }, "evidence": "https://e.org", "note": "n" }""", "held must be")]
    [InlineData("""{ "gate": "g", "held": { "sources": ["RelicItem#5"], "items": [[]] }, "evidence": "https://e.org", "note": "n" }""", "held must be")]
    [InlineData("""{ "gate": "g", "held": { "sources": ["RelicItem#5"], "items": [[1], [1]] }, "evidence": "https://e.org", "note": "n" }""", "no item twice")]
    [InlineData("""{ "gate": "g", "held": { "sources": ["RelicItem#5"], "shield": "sometimes", "items": [[1]] }, "evidence": "https://e.org", "note": "n" }""", "held must be")]
    public void A_bad_weapon_list_skips_the_entry_with_a_warning(string entry, string reason)
    {
        var data = Load($$"""{ "schema": 1, "entries": { "65897": {{entry}}, "68478": {{Good}} } }""");

        Assert.Equal([68478u], data.GameGates.Keys);
        Assert.Contains(reason, Assert.Single(data.Warnings), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"x\"", Good, "key is not a quest row id")]
    [InlineData("\"65897\"", "[65742]", "value is not an object")]
    [InlineData("\"65897\"", """{ "evidence": "https://e.org", "note": "n" }""", "gate must say what the game wants")]
    [InlineData("\"65897\"", """{ "gate": " ", "evidence": "https://e.org", "note": "n" }""", "gate must say what the game wants")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": 65742, "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "after must be an array")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65897], "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "names the quest itself or repeats")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742, 65742], "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "names the quest itself or repeats")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742], "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742], "afterTextKey": "SYSTEM_1", "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "gameTextKey": "SYSTEM_1", "evidence": "https://e.org", "note": "n" }""", "gameTextKey must be a TEXT_ key")]
    [InlineData("\"67086\"", """{ "gate": "g", "mounts": { "sources": ["Item#105"], "all": [75] }, "evidence": "https://e.org", "note": "n" }""", "mounts must be")]
    [InlineData("\"67086\"", """{ "gate": "g", "mounts": { "sources": ["Mount#105"], "all": [] }, "evidence": "https://e.org", "note": "n" }""", "mounts must be")]
    [InlineData("\"67086\"", """{ "gate": "g", "mounts": { "sources": ["Mount#105"], "all": [75, 75] }, "evidence": "https://e.org", "note": "n" }""", "mounts must be")]
    [InlineData("\"67086\"", """{ "gate": "g", "mounts": { "all": [75] }, "evidence": "https://e.org", "note": "n" }""", "mounts must be")]
    [InlineData("\"67086\"", """{ "gate": "g", "held": { "sources": ["RelicItem#5"], "items": [[1]] }, "mounts": { "sources": ["Mount#105"], "all": [75] }, "evidence": "https://e.org", "note": "n" }""", "either weapons or mounts")]
    [InlineData("\"70852\"", """{ "gate": "g", "unlockLinks": { "sources": ["QuestAcceptAdditionCondition#70852"], "all": [] }, "evidence": "https://e.org", "note": "n" }""", "unlockLinks must be")]
    [InlineData("\"70852\"", """{ "gate": "g", "unlockLinks": { "sources": ["QuestAcceptAdditionCondition#70852"], "all": [510, 510] }, "evidence": "https://e.org", "note": "n" }""", "unlockLinks must be")]
    [InlineData("\"70852\"", """{ "gate": "g", "unlockLinks": { "sources": ["QuestAcceptAdditionCondition#70852"], "all": [65536] }, "evidence": "https://e.org", "note": "n" }""", "unlockLinks must be")]
    [InlineData("\"70852\"", """{ "gate": "g", "unlockLinks": { "all": [510] }, "evidence": "https://e.org", "note": "n" }""", "unlockLinks must be")]
    [InlineData("\"70852\"", """{ "gate": "g", "held": { "sources": ["RelicItem#5"], "items": [[1]] }, "unlockLinks": { "sources": ["Action#1"], "all": [5] }, "evidence": "https://e.org", "note": "n" }""", "one of weapons, mounts or unlock links")]
    [InlineData("\"68667\"", """{ "gate": "g", "metBy": [68667], "evidence": "https://e.org", "note": "n" }""", "metBy must be")]
    [InlineData("\"68667\"", """{ "gate": "g", "metBy": [12], "evidence": "https://e.org", "note": "n" }""", "metBy must be")]
    [InlineData("\"67086\"", """{ "gate": "g", "mounts": { "sources": ["Mount#105"], "all": [75] }, "metBy": [67923], "evidence": "https://e.org", "note": "n" }""", "metBy must be")]
    [InlineData("\"68668\"", """{ "gate": "g", "acceptConditions": [68667], "evidence": "https://e.org", "note": "n" }""", "acceptConditions must be")]
    [InlineData("\"68668\"", """{ "gate": "g", "acceptConditions": 226, "evidence": "https://e.org", "note": "n" }""", "acceptConditions must be")]
    [InlineData("\"65897\"", """{ "gate": "g", "evidence": "https://e.org" }""", "note is missing")]
    [InlineData("\"65897\"", """{ "gate": "g", "note": "n" }""", "evidence is missing")]
    [InlineData("\"65897\"", """{ "gate": "g", "evidence": "http://e.org", "note": "n" }""", "is not an https URL")]
    public void A_bad_entry_is_skipped_with_a_warning_naming_the_file_and_the_key(string key, string entry, string reason)
    {
        var data = Load($$"""{ "schema": 1, "entries": { {{key}}: {{entry}}, "68478": {{Good}} } }""");

        Assert.Equal([68478u], data.GameGates.Keys);
        var warning = Assert.Single(data.Warnings);
        Assert.StartsWith(CuratedData.GameGatesFileName + ": entry " + key, warning, StringComparison.Ordinal);
        Assert.Contains(reason, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Mount_gates_parse_their_mounts_sorted_and_pass_them_to_the_catalog()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "entries": {
                "67086": { "gate": "all seven Heavensward Lanner mounts", "mounts": { "sources": ["Mount#105"], "all": [104, 75, 90] }, "evidence": "https://e.org/a", "note": "n" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        var gate = data.GameGates[67086];
        Assert.Null(gate.Items);
        Assert.Equal(["Mount#105"], gate.Mounts!.Sources);
        Assert.Equal([75u, 90u, 104u], gate.Mounts.All);
        Assert.Equal([75u, 90u, 104u], data.GameGateIds[67086].Mounts!);
        Assert.Null(data.GameGateIds[67086].Items);
    }

    [Fact]
    public void Unlock_links_met_by_quests_and_accept_conditions_parse_and_reach_the_catalog()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "entries": {
                "68668": { "gate": "floor 30 of Heaven-on-High cleared", "acceptConditions": [226], "evidence": "https://ffxiv.consolegameswiki.com/wiki/On_the_Shoulders_of_Giants", "note": "n" },
                "68667": { "gate": "floor 50 of the Palace of the Dead cleared", "after": [67092], "metBy": [67923], "gameTextKey": "TEXT_A", "afterTextKey": "TEXT_B", "evidence": "https://ffxiv.consolegameswiki.com/wiki/K", "note": "n" },
                "70852": { "gate": "the Occult Record entries unlocked", "unlockLinks": { "sources": ["QuestAcceptAdditionCondition#70852"], "all": [512, 510, 511] }, "gameTextKey": "TEXT_C", "evidence": "https://e.org/c", "note": "n" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        var occult = data.GameGates[70852];
        Assert.Equal(["QuestAcceptAdditionCondition#70852"], occult.UnlockLinks!.Sources);
        Assert.Equal([510u, 511u, 512u], occult.UnlockLinks.All);
        Assert.Equal([510u, 511u, 512u], data.GameGateIds[70852].UnlockLinks!);
        Assert.Equal([QuestGate.GameTextSource, QuestGate.SheetSource], occult.SourceKinds);

        var floor = data.GameGates[68667];
        Assert.Equal([67923u], floor.MetByIds);
        Assert.Equal([67923u], data.GameGateIds[68667].MetBy);
        Assert.Null(data.GameGateIds[68667].UnlockLinks);
        Assert.Equal([QuestGate.GameTextSource, QuestGate.WikiSource], data.GameGateIds[68667].Sources);

        Assert.Equal([226u], data.GameGates[68668].AcceptConditionIds);
        Assert.Equal([226u], data.GameGateIds[68668].AcceptConditions);
        Assert.Equal([QuestGate.SheetSource, QuestGate.WikiSource], data.GameGates[68668].SourceKinds);
        Assert.Empty(data.GameGates[68668].MetByIds);
    }

    [Fact]
    public void The_1_22_sources_parse_into_their_kinds()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "entries": {
                "69617": { "gate": "the achievement A Card in the Hand", "questionable": true, "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" },
                "70181": { "gate": "Island Sanctuary rank 9", "playerConfirmed": "the wiki alone states it", "evidence": "https://ffxiv.consolegameswiki.com/wiki/B", "note": "n" },
                "70200": { "gate": "floor 30 of Eureka Orthos cleared", "lodestone": "https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/0ca8e48fbc1/", "evidence": "https://ffxiv.consolegameswiki.com/wiki/C", "note": "n" },
                "70995": { "gate": "three unique final bosses defeated", "requiredTextKey": "TEXT_KINGVA101_05441_SYSTEM_101_202", "evidence": "https://ffxiv.consolegameswiki.com/wiki/D", "note": "n" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal([QuestGate.WikiSource, QuestGate.QuestionableSource], data.GameGates[69617].SourceKinds);
        Assert.Equal([QuestGate.WikiSource, QuestGate.PlayerSource], data.GameGates[70181].SourceKinds);
        Assert.Equal([QuestGate.WikiSource, QuestGate.LodestoneSource], data.GameGates[70200].SourceKinds);
        Assert.Equal([QuestGate.GameTextSource, QuestGate.WikiSource], data.GameGates[70995].SourceKinds);
        Assert.Equal([QuestGate.WikiSource, QuestGate.PlayerSource], data.GameGateIds[70181].Sources);
        Assert.True(data.GameGates[70181].NeverJudged);
    }

    [Theory]
    [InlineData("""{ "gate": "g", "lodestone": "https://example.org/quest/abc/", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "lodestone")]
    [InlineData("""{ "gate": "g", "lodestone": "https://na.finalfantasyxiv.com/lodestone/playguide/db/item/0ca8e48fbc1/", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "lodestone")]
    [InlineData("""{ "gate": "g", "questionable": false, "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "questionable")]
    [InlineData("""{ "gate": "g", "requiredTextKey": "SEQ_01", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "requiredTextKey")]
    // The player stands in only for a never-judged gate the wiki alone states: not on another evidence page, not beside
    // a second source, not on a judged gate, and never without a reason.
    [InlineData("""{ "gate": "g", "playerConfirmed": "why", "evidence": "https://e.org/a", "note": "n" }""", "playerConfirmed")]
    [InlineData("""{ "gate": "g", "playerConfirmed": "why", "gameTextKey": "TEXT_A", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "playerConfirmed")]
    [InlineData("""{ "gate": "g", "playerConfirmed": "why", "acceptConditions": [226], "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "playerConfirmed")]
    [InlineData("""{ "gate": "g", "playerConfirmed": "why", "after": [65742], "afterTextKey": "TEXT_B", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "playerConfirmed")]
    [InlineData("""{ "gate": "g", "playerConfirmed": " ", "evidence": "https://ffxiv.consolegameswiki.com/wiki/A", "note": "n" }""", "playerConfirmed")]
    public void A_malformed_1_22_source_skips_the_entry(string entry, string field)
    {
        var data = Load($$"""{ "schema": 1, "entries": { "70200": {{entry}} } }""");

        Assert.Empty(data.GameGates);
        Assert.Contains(data.Warnings, w => w.Contains(field, StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_file_adds_no_gate()
    {
        var dir = tmp.File("empty");
        Directory.CreateDirectory(dir);
        var data = CuratedData.Load(dir);

        Assert.Empty(data.Warnings);
        Assert.Empty(data.GameGates);
        Assert.Empty(CuratedData.Empty.GameGateIds);
    }
}
