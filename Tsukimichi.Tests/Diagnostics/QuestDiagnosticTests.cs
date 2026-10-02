using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Evaluation;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// <see cref="QuestDiagnostic"/> on synthetic catalogs: every requirement kind's line format, the header lines, and
/// the rule that nothing identifying the character reaches the block. The fixture-backed case (Brotherhood of Ash)
/// is in <see cref="QuestDiagnosticFixtureTests"/>.
/// </summary>
public class QuestDiagnosticTests
{
    private const byte Pelupelu = 3;
    private const byte WhiteMage = 24;
    private const uint DisciplesOfTheHand = 33;
    private const uint TheVault = 7;
    private const uint UnnamedDuty = 8;
    private const ushort Starlight = 1;
    private const byte Mnaago = 2;

    private static readonly BlockerNames Names = new()
    {
        Tribe = id => id == Pelupelu ? "Pelupelu" : string.Empty,
        SatisfactionNpc = id => id == Mnaago ? "M'naago" : string.Empty,
        JobAbbreviation = id => id switch { Gladiator => "GLA", Conjurer => "CNJ", Paladin => "PLD", WhiteMage => "WHM", _ => string.Empty },
        ClassJobCategory = id => id == DisciplesOfTheHand ? "Disciple of the Hand" : string.Empty,
        Duty = id => id == TheVault ? "The Vault" : string.Empty,
    };

    /// <summary>A quest with one gate of every kind.</summary>
    private static QuestRecord Everything() => Quest(Target, "Every Gate") with
    {
        Journal = new JournalRef(3, "Side Quests", 1, "Category", 89, "Genre", 1),
        Level = 90,
        LevelOffset = 2,
        QuestLocks = [A],
        Expansion = 5,
        ClassJobCategory = DisciplesOfTheHand,
        PreviousQuests = new Prereq([B, C], JoinKind.All),
        GrandCompany = 1,
        GrandCompanyRank = 3,
        BeastTribe = Pelupelu,
        BeastRank = 4,
        BeastValue = 100,
        IsRepeatable = true,
        RepeatInterval = 1,
        InstanceContentRequired = [TheVault, UnnamedDuty],
        InstanceJoin = JoinKind.Any,
        Festival = Starlight,
        AcceptConditions = [12, 34],
        MountRequired = true,
        HouseRequired = true,
        SatisfactionNpc = Mnaago,
        SatisfactionLevel = 4,
        CarrierLevel = 7,
    };

    /// <param name="gameGate">Also give the target a game gate (curated/game_gates.json), the one kind the sheet cannot carry.</param>
    private static QuestCatalog EverythingCatalog(QuestRecord target, bool gameGate = false) =>
        QuestCatalog.Build(
            [target, Quest(A, "Lock A"), Quest(B, "Prereq B"), Quest(C, "Prereq C")],
            null,
            gameGate ? new Dictionary<uint, QuestGate> { [target.RowId] = new("a relic weapon nexus equipped", []) } : null);

    private static CharacterSnapshot Character() => Snapshot(A, B) with
    {
        ContentId = 4242424242424242,
        Name = "Michiru Tsukikage",
        World = 55,
        TakenUtc = new DateTime(2026, 9, 28, 21, 14, 2, DateTimeKind.Utc),
        CurrentJob = Gladiator,
        JobLevels = Levels((Gladiator, 50)),
        MaxExpansion = 4,
        LevelCap = 80,
        GrandCompany = 1,
        GcRanks = [0, 5],
        Tribes = new Dictionary<byte, TribeStanding> { [Pelupelu] = new(4, 120) },
        TribeAllowance = 3,
        UnlockedInstances = [TheVault],
        ActiveFestivals = [],
        AchievementsLoaded = false,
        SatisfactionRanks = new Dictionary<byte, byte> { [Mnaago] = 3 },
        CarrierLevel = 5,
    };

    private static EvalContext Context(ushort offeredQuestId) => new()
    {
        ClassJobs = new Jobs((DisciplesOfTheHand, [Conjurer])),
        IsAchievementGated = id => id == Target,
        TodaysDailyOffer = new HashSet<ushort> { offeredQuestId },
        HasMount = null,
        HasHouse = false,
    };

    private static DiagnosticInputs Inputs(QuestRecord quest, QuestCatalog catalog, CharacterSnapshot snapshot, EvalContext ctx) => new()
    {
        PluginVersion = "0.6.0.0",
        ClientGameVersion = "2026.09.15.0000.0000",
        DataGameVersion = "2026.09.15.0000.0000",
        DataGeneratedUtc = new DateTime(2026, 9, 28, 22, 39, 14, DateTimeKind.Utc),
        CuratedRevision = "573d225",
        Quest = quest,
        Evaluation = new QuestEvaluation(QuestState.Blocked, RequirementEvaluator.Evaluate(quest, snapshot, catalog, ctx), null, null, null),
        Names = Names with { Catalog = catalog },
        Snapshot = snapshot,
        Context = ctx,
        IsLive = true,
    };

    private static string[] Lines(string block) => block.Split('\n');

    [Fact]
    public void Every_requirement_kind_has_a_line_with_its_verdict_and_the_values_compared()
    {
        var quest = Everything();
        var catalog = EverythingCatalog(quest, gameGate: true);
        var block = QuestDiagnostic.Compose(Inputs(quest, catalog, Character(), Context(quest.QuestId)));
        var lines = Lines(block);

        // One line per kind, in the evaluator's order, and every kind of the enum is represented; Retired is the one
        // gate a live quest cannot carry and OtherPath needs a choice the character made elsewhere, so each has its own
        // test below.
        var requirementLines = lines.Where(l => l.StartsWith("  - ", StringComparison.Ordinal)).ToArray();
        var kinds = requirementLines.Select(l => l[4..l.IndexOf(':', StringComparison.Ordinal)]).ToArray();
        Assert.Equal(Enum.GetValues<RequirementKind>().Where(k => k is not (RequirementKind.Retired or RequirementKind.OtherPath)).Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal), kinds.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(kinds, RequirementEvaluator.Evaluate(quest, Character(), catalog, Context(quest.QuestId)).Select(r => r.Req.Kind.ToString()));

        Assert.Contains("  - Foreclosure: unmet (locks 65600 Lock A; completed 65600 Lock A)", requirementLines);
        Assert.Contains("  - ExpansionCap: unmet (Dawntrail (5) > cap 4)", requirementLines);
        Assert.Contains("  - LevelCap: unmet (90 > cap 80)", requirementLines);
        Assert.Contains("  - ClassJob: unmet (category 33 Disciple of the Hand, you are GLA)", requirementLines);
        Assert.Contains("  - Level: unmet (90 > 50)", requirementLines);
        Assert.Contains("  - PreviousQuests: unmet (65601 Prereq B: done, 65602 Prereq C: not done)", requirementLines);
        Assert.Contains("  - GrandCompany: met (Maelstrom required, you are Maelstrom)", requirementLines);
        Assert.Contains("  - GrandCompanyRank: met (Maelstrom rank 5 ≥ 3)", requirementLines);
        Assert.Contains("  - TribeRank: met (Pelupelu Trusted ≥ Trusted)", requirementLines);
        Assert.Contains("  - TribeReputation: met (Pelupelu 120 ≥ 100)", requirementLines);
        Assert.Contains("  - TribeAllowance: met (3 left today)", requirementLines);
        Assert.Contains("  - TribeDailyOffer: met (quest 164 offered today)", requirementLines);
        Assert.Contains("  - CustomDeliveryRank: unmet (M'naago rank 3 < 4)", requirementLines);
        Assert.Contains("  - CarrierLevel: unmet (carrier level 5 < 7)", requirementLines);
        Assert.Contains("  - DutyCompletion: met (1 of 2 cleared, one needed: 7 The Vault, 8)", requirementLines);
        Assert.Contains("  - Seasonal: unmet (festival 1 not active)", requirementLines);
        Assert.Contains("  - AcceptCondition: not checked (conditions 12, 34; listed, not judged)", requirementLines);
        Assert.Contains("  - Mount: not checked (has mount unknown)", requirementLines);
        Assert.Contains("  - House: unmet (has house no)", requirementLines);
        Assert.Contains("  - Achievement: not checked (achievements not loaded, quest 65700)", requirementLines);
        Assert.Contains("  - GameGate: not checked (game gate \"a relic weapon nexus equipped\"; listed, not judged)", requirementLines);
    }

    [Fact]
    public void Delivery_gates_the_capture_did_not_read_are_not_checked_and_a_phased_event_prints_its_window()
    {
        var quest = Everything() with { FestivalBegin = 2, FestivalEnd = 5 };

        // A second window among the festival's quests makes it a phased event, whose windows are judged.
        var catalog = QuestCatalog.Build([.. EverythingCatalog(quest).All, Quest(E, "Chapter 1") with { Festival = Starlight, FestivalBegin = 1, FestivalEnd = 1 }]);
        var unread = Character() with { SatisfactionRanks = new Dictionary<byte, byte>(), CarrierLevel = null, ActiveFestivals = [Starlight], ActiveFestivalPhases = [1] };
        var lines = Lines(QuestDiagnostic.Compose(Inputs(quest, catalog, unread, Context(quest.QuestId))));
        var requirementLines = lines.Where(l => l.StartsWith("  - ", StringComparison.Ordinal)).ToArray();

        Assert.Contains("  - CustomDeliveryRank: not checked (M'naago rank unknown, needs 4)", requirementLines);
        Assert.Contains("  - CarrierLevel: not checked (carrier level unknown, needs 7)", requirementLines);
        Assert.Contains("  - Seasonal: unmet (festival 1 active, window 2-5, phase 1)", requirementLines);
        Assert.Contains("festivals [1/1], delivery client 2 rank unknown, carrier level unknown", lines[^3]);

        var noPhase = unread with { ActiveFestivalPhases = [] };
        var unknown = Lines(QuestDiagnostic.Compose(Inputs(quest, catalog, noPhase, Context(quest.QuestId))));
        Assert.Contains("  - Seasonal: met (festival 1 active, window 2-5, phase unknown)", unknown);
        Assert.Contains("festivals [1],", unknown[^3]);
    }

    [Fact]
    public void A_quest_on_another_path_lists_the_path_gate_first_with_the_choice_and_the_quest_that_made_it()
    {
        // Two quests that lock each other are a choice; the character did the other one.
        var quest = Quest(Target, "Heads") with { QuestLocks = [A] };
        var other = Quest(A, "Tails") with { QuestLocks = [Target] };
        var catalog = Catalog(quest, other);
        var snapshot = Character() with { CompletedBits = Bits(A) };
        var evaluation = StateResolver.Resolve(quest, snapshot, catalog, Context(quest.QuestId));
        var lines = Lines(QuestDiagnostic.Compose(Inputs(quest, catalog, snapshot, Context(quest.QuestId)) with { Evaluation = evaluation }));

        Assert.Equal(QuestState.Foreclosed, evaluation.State);
        Assert.True(evaluation.IsOtherPath);
        Assert.Contains("state: Locked out · Another choice (Tails)", lines);
        Assert.Equal("requirements:", lines[Array.IndexOf(lines, "state: Locked out · Another choice (Tails)") + 1]);
        Assert.Equal("  - OtherPath: unmet (Choice Heads, chosen Tails; by 65600 Tails)", lines[Array.IndexOf(lines, "requirements:") + 1]);
        Assert.Contains("  - Foreclosure: unmet (locks 65600 Tails; completed 65600 Tails)", lines);
    }

    [Fact]
    public void A_retired_quest_lists_the_removed_gate_first_and_names_its_filing()
    {
        var quest = Quest(Target, "Old Story") with { IsRetired = true, RefiledFrom = 1 };
        var catalog = Catalog(quest);
        var inputs = Inputs(quest, catalog, Character(), Context(quest.QuestId)) with { FilingRule = "rule 1 (retired)" };
        var lines = Lines(QuestDiagnostic.Compose(inputs));

        Assert.Equal("quest: 65700 \"Old Story\" (genre 1, lvl 1 / display 1, filing rule 1 (retired))", lines[4]);
        var requirementLines = lines.Where(l => l.StartsWith("  - ", StringComparison.Ordinal)).ToArray();
        Assert.Equal("  - Retired: unmet (removed from the game)", requirementLines[0]);
    }

    [Fact]
    public void The_header_quest_state_inputs_and_capture_lines_read_as_the_plan_example()
    {
        var quest = Everything();
        var catalog = EverythingCatalog(quest);
        var lines = Lines(QuestDiagnostic.Compose(Inputs(quest, catalog, Character(), Context(quest.QuestId))));

        Assert.Equal("```tsukimichi-diagnostic", lines[0]);
        Assert.Equal("plugin: 0.6.0.0", lines[1]);
        Assert.Equal("game: 2026.09.15.0000.0000 (client) / 2026.09.15.0000.0000 (data)", lines[2]);
        Assert.Equal("data: unique_quests generated 2026-09-28T22:39:14Z, curated 573d225, schema 1", lines[3]);
        Assert.Equal("quest: 65700 \"Every Gate\" (genre 89, lvl 90 / display 92, filing n/a)", lines[4]);
        Assert.Equal("state: Blocked · closed by: Lock A", lines[5]);
        Assert.Equal("requirements:", lines[6]);
        // Only what the listed gates read: the caps, the main scenario position (none in this catalog), the Grand
        // Company, the society standing, the allowances and the offer, the festivals, mount, house, achievements.
        Assert.Equal(
            "inputs: job GLA 50, cap expansion 4 lv 80, gc 1 rank 5, tribe 3 rank 4 rep 120, dailies 3/12, offer [164], festivals [-], delivery client 2 rank 3, carrier level 5, mount unknown, house no, achievements not loaded",
            lines[^3]);
        Assert.Equal("captured: 2026-09-28T21:14:02Z live", lines[^2]);
        Assert.Equal("```", lines[^1]);
    }

    [Fact]
    public void Inputs_carry_only_what_the_evaluation_used()
    {
        var quest = Quest(Target, "Plain") with { Level = 24 };
        var catalog = Catalog(quest);
        var snapshot = Character() with { CurrentJob = WhiteMage, JobLevels = Levels((WhiteMage, 31)) };
        var evaluation = StateResolver.Resolve(quest, snapshot, catalog, EvalContext.Default);
        var block = QuestDiagnostic.Compose(Inputs(quest, catalog, snapshot, EvalContext.Default) with { Evaluation = evaluation, IsLive = false });
        var lines = Lines(block);

        Assert.Equal("state: Ready", lines[5]);
        Assert.Equal("  - Level: met (24 ≤ 31)", lines[7]);
        Assert.Equal("inputs: job WHM 31", lines[^3]);
        Assert.Equal("captured: 2026-09-28T21:14:02Z stored", lines[^2]);
    }

    [Fact]
    public void Questionables_answer_is_a_line_before_the_inputs_only_when_it_is_loaded()
    {
        var quest = Quest(Target, "Plain") with { Level = 24 };
        var catalog = Catalog(quest);
        var snapshot = Character() with { CurrentJob = WhiteMage, JobLevels = Levels((WhiteMage, 31)) };
        var evaluation = StateResolver.Resolve(quest, snapshot, catalog, EvalContext.Default);
        var inputs = Inputs(quest, catalog, snapshot, EvalContext.Default) with { Evaluation = evaluation };
        var check = Core.Ipc.QuestionableCrossCheck.Compare(evaluation, new Core.Ipc.QuestionableAnswer(true, "Prev quest (1)"));

        var without = Lines(QuestDiagnostic.Compose(inputs));
        var with = Lines(QuestDiagnostic.Compose(inputs with { Questionable = check }));

        Assert.DoesNotContain(without, line => line.StartsWith("questionable:", StringComparison.Ordinal));
        Assert.Equal(without.Length + 1, with.Length);
        Assert.Equal("questionable: disagrees; locked: Prev quest (1); tsukimichi Ready", with[^4]);
        Assert.StartsWith("inputs: ", with[^3], StringComparison.Ordinal);
    }

    [Fact]
    public void Without_an_evaluation_or_a_character_the_block_says_so()
    {
        var quest = Quest(Target, "Plain");
        var block = QuestDiagnostic.Compose(new DiagnosticInputs { Quest = quest });
        var lines = Lines(block);

        Assert.Equal("plugin: unknown", lines[1]);
        Assert.Equal("game: unknown (client) / unknown (data)", lines[2]);
        Assert.Equal("data: unique_quests generated unknown, curated unknown, schema 1", lines[3]);
        Assert.Equal("state: Not checked · no evaluation", lines[5]);
        Assert.Equal("requirements: none", lines[6]);
        Assert.Equal("inputs: no character", lines[7]);
        Assert.Equal("captured: no character", lines[8]);
    }

    [Fact]
    public void Ready_on_another_job_names_the_job_and_any_join_prerequisites_say_so()
    {
        var quest = Quest(Target, "Any") with
        {
            ClassJobCategory = DisciplesOfTheHand,
            PreviousQuests = new Prereq([B, C], JoinKind.Any),
        };
        var catalog = EverythingCatalog(quest);
        var snapshot = Character() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 50)) };
        var ctx = new EvalContext { ClassJobs = new Jobs((DisciplesOfTheHand, [Conjurer])) };
        var evaluation = StateResolver.Resolve(quest, snapshot, catalog, ctx);
        Assert.Equal(QuestState.ReadyOnOtherJob, evaluation.State);

        var lines = Lines(QuestDiagnostic.Compose(Inputs(quest, catalog, snapshot, ctx) with { Evaluation = evaluation }));
        Assert.Equal("state: Ready on another job (CNJ)", lines[5]);
        Assert.Contains("  - PreviousQuests: met (any of 65601 Prereq B: done, 65602 Prereq C: not done)", lines);
    }

    [Fact]
    public void The_block_never_carries_the_content_id_the_name_or_the_world()
    {
        var quest = Everything();
        var catalog = EverythingCatalog(quest);
        var snapshot = Character();
        Assert.Equal(4242424242424242ul, snapshot.ContentId);
        Assert.Equal("Michiru Tsukikage", snapshot.Name);
        Assert.Equal(55u, snapshot.World);

        var block = QuestDiagnostic.Compose(Inputs(quest, catalog, snapshot, Context(quest.QuestId)));

        Assert.DoesNotContain("4242424242424242", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Michiru", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tsukikage", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("world", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_block_is_the_same_string_for_the_same_inputs()
    {
        var quest = Everything();
        var catalog = EverythingCatalog(quest);
        var inputs = Inputs(quest, catalog, Character(), Context(quest.QuestId));

        Assert.Equal(QuestDiagnostic.Compose(inputs), QuestDiagnostic.Compose(inputs));
    }
}
