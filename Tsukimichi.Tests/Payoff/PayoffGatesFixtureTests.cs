using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Payoff;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Payoff;

/// <summary>
/// "Before you continue" payoff gates (P5) over the frozen catalog and the shipped <c>curated/payoff_gates.json</c>:
/// every starter pair speaks at its milestone and nowhere else, its content is never already required by the
/// milestone, and no instruction names what the milestone reveals.
/// </summary>
[Trait("Category", "Curated")]
public sealed class PayoffGatesFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    /// <summary>The starter set (docs/research/player-gripes-2026.md §3 P5) with the milestone each one speaks at.</summary>
    public static readonly TheoryData<string, uint> StarterPairs = new()
    {
        { "eden", 70286 },              // Growing Light (6.5)
        { "shb-role-quests", 69543 },   // Alisaie's Quest (5.4)
        { "pilgrims-traverse", 70943 }, // A Branch and Their Sapling (Pilgrim's Traverse floor 100)
        { "eureka", 70490 },            // The Taste of Family (7.0)
        { "bozja", 69919 },             // A Frosty Reception (6.0, Garlemald)
    };

    /// <summary>
    /// Names the whys reveal, reviewed by hand: an instruction naming one of them would spoil the payoff whatever the
    /// derived tokens say.
    /// </summary>
    private static readonly string[] PayoffNames = ["Gaia", "Ryne", "Krile", "Galuf", "Alayla", "Robor", "Cyella", "Unukalhai", "Pilgrim's Answer", "Garlemald", "Ilsabard", "Thirteenth", "Crystarium"];

    /// <summary>Capitalised words too common to mark a spoiler ("Quest" in Alisaie's Quest).</summary>
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase) { "Quest", "Quests", "Main", "Scenario", "With", "From", "Into", "Their", "Your", "What", "When" };

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private PayoffGates Gates() => PayoffGates.Build(Catalog, fixture.Curated);

    private ResolvedPayoffGate Gate(string id) => Assert.Single(Gates().Gates, g => g.Gate.Id == id);

    /// <summary>Every quest reachable backwards from <paramref name="rowId"/> through previous quests, whatever the join.</summary>
    private HashSet<uint> Ancestors(uint rowId)
    {
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>();
        stack.Push(rowId);
        while (stack.Count > 0)
        {
            if (Catalog.GetByRowId(stack.Pop()) is not { } quest)
            {
                continue;
            }

            foreach (var previous in quest.PreviousQuests.QuestIds)
            {
                if (seen.Add(previous))
                {
                    stack.Push(previous);
                }
            }
        }

        return seen;
    }

    /// <summary>A character standing at the milestone: every quest it requires completed, the milestone in <paramref name="state"/>.</summary>
    private Dictionary<uint, QuestState> AtMilestone(ResolvedPayoffGate gate, QuestState state)
    {
        var states = Ancestors(gate.Milestone.RowId).ToDictionary(id => id, _ => QuestState.Completed);
        states[gate.Milestone.RowId] = state;
        return states;
    }

    /// <summary>Main scenario quests in story order: sections 0 then 1, journal order, removed rows left out.</summary>
    private List<QuestRecord> StoryOrder() =>
        MsqProgress.MainScenarioSections
            .SelectMany(section => Catalog.BySection.GetValueOrDefault(section) ?? [])
            .Where(q => !q.IsRemoved)
            .ToList();

    [Fact]
    public void The_shipped_starter_set_resolves_against_the_fixture()
    {
        var gates = Gates();

        Assert.Empty(gates.Warnings);
        Assert.Equal(fixture.Curated.PayoffGates.Count, gates.Gates.Count);
        Assert.Equal(StarterPairs.Select(row => (string)row[0]).Order(StringComparer.Ordinal), gates.Gates.Select(g => g.Gate.Id).Order(StringComparer.Ordinal));
        foreach (var row in StarterPairs)
        {
            Assert.Equal((uint)row[1], Gate((string)row[0]).Milestone.RowId);
        }

        Assert.Equal("Growing Light", Gate("eden").Milestone.Name);
        Assert.Equal("Alisaie's Quest", Gate("shb-role-quests").Milestone.Name);
        Assert.Equal("A Branch and Their Sapling", Gate("pilgrims-traverse").Milestone.Name);
        Assert.Equal("The Taste of Family", Gate("eureka").Milestone.Name);
        Assert.Equal("A Frosty Reception", Gate("bozja").Milestone.Name);

        // The Eden gate names a chain: the whole series, ending with the epilogue the cameo checks.
        var eden = Gate("eden");
        Assert.Equal(18, eden.Content.Count);
        Assert.Equal(69515u, eden.Content[^1]);
        Assert.Equal("Where I Belong", Catalog.GetByRowId(69515)!.Name);
    }

    [Theory]
    [MemberData(nameof(StarterPairs))]
    public void A_gate_is_shown_at_its_milestone_ready_or_in_the_journal(string id, uint milestone)
    {
        var gate = Gate(id);
        Assert.Equal(milestone, gate.Milestone.RowId);
        var gates = Gates();

        var ready = Assert.Single(gates.Active(AtMilestone(gate, QuestState.Ready)), g => g.Gate.Id == id);
        Assert.Equal(QuestState.Ready, ready.MilestoneState);
        Assert.Equal(gate.Content.Count, ready.Total);
        Assert.True(ready.Done < ready.Total);

        var accepted = Assert.Single(gates.Active(AtMilestone(gate, QuestState.Accepted)), g => g.Gate.Id == id);
        Assert.Equal(QuestState.Accepted, accepted.MilestoneState);

        // Ready on another job is still the milestone reached (a crafter logged in at the Pilgrim's Traverse quest).
        Assert.Contains(gates.Active(AtMilestone(gate, QuestState.ReadyOnOtherJob)), g => g.Gate.Id == id);
    }

    [Theory]
    [MemberData(nameof(StarterPairs))]
    public void A_gate_is_hidden_before_the_milestone_and_after_it(string id, uint milestone)
    {
        var gate = Gate(id);
        var gates = Gates();

        // Before: the milestone is not open yet (Blocked, foreclosed, or never evaluated).
        foreach (var state in new[] { QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown })
        {
            Assert.DoesNotContain(gates.Active(AtMilestone(gate, state)), g => g.Gate.Id == id);
        }

        // Before, with the story one quest short of the milestone.
        var before = AtMilestone(gate, QuestState.Blocked);
        var previous = Catalog.GetByRowId(milestone)!.PreviousQuests.QuestIds;
        Assert.NotEmpty(previous);
        before[previous[0]] = QuestState.Ready;
        Assert.DoesNotContain(gates.Active(before), g => g.Gate.Id == id);

        // After: the milestone is turned in.
        Assert.DoesNotContain(gates.Active(AtMilestone(gate, QuestState.Completed)), g => g.Gate.Id == id);

        // No states at all (browse mode) shows nothing.
        Assert.Empty(gates.Active(new Dictionary<uint, QuestState>()));
    }

    [Theory]
    [MemberData(nameof(StarterPairs))]
    public void A_gate_is_hidden_once_its_content_is_done(string id, uint milestone)
    {
        var gate = Gate(id);
        var gates = Gates();
        var states = AtMilestone(gate, QuestState.Ready);
        Assert.Equal(milestone, gate.Milestone.RowId);

        // All but the last content quest: still shown, with the count.
        foreach (var rowId in gate.Content.SkipLast(1))
        {
            states[rowId] = QuestState.Completed;
        }

        var partial = Assert.Single(gates.Active(states), g => g.Gate.Id == id);
        Assert.Equal(gate.Content.Count - 1, partial.Done);

        // In the journal is not done.
        states[gate.Content[^1]] = QuestState.Accepted;
        Assert.Contains(gates.Active(states), g => g.Gate.Id == id);

        states[gate.Content[^1]] = QuestState.Completed;
        Assert.DoesNotContain(gates.Active(states), g => g.Gate.Id == id);
    }

    [Fact]
    public void No_gate_content_is_already_a_prerequisite_of_its_milestone()
    {
        // A gate whose content the milestone already requires could never show: the milestone is not Ready until the
        // content is done. (Hildibrand before the Manderville line was left out for exactly this reason.)
        foreach (var gate in Gates().Gates)
        {
            var ancestors = Ancestors(gate.Milestone.RowId);
            var required = gate.Content.Where(ancestors.Contains).ToList();
            Assert.True(required.Count == 0, $"payoff gate {gate.Gate.Id}: {string.Join(", ", required)} already required by milestone {gate.Milestone.RowId}");
        }

        var mandervilleStart = Catalog.GetByRowId(70188)!;
        Assert.Contains(68704u, Ancestors(mandervilleStart.RowId)); // Don't Do the Dewprism: the game enforces Hildibrand first.
    }

    [Fact]
    public void The_crystal_tower_is_left_out_because_the_main_scenario_requires_it()
    {
        // The Light of Hope (66031) is an accept condition of the main scenario quest A Time to Every Purpose.
        var timeToEveryPurpose = Catalog.GetByRowId(65961)!;
        Assert.True(FeaturePresets.IsMainScenario(timeToEveryPurpose));
        Assert.Contains(66031u, timeToEveryPurpose.AcceptConditions);
        Assert.DoesNotContain(fixture.Curated.PayoffGates, g => g.BeforeChain == "Crystal Tower");
    }

    [Fact]
    public void No_instruction_names_a_spoiler_sensitive_token()
    {
        var story = StoryOrder();
        var index = story.Select((q, i) => (q.RowId, i)).ToDictionary(p => p.RowId, p => p.i);
        foreach (var gate in Gates().Gates)
        {
            var instruction = gate.Gate.Instruction;

            // Where the character stands: the milestone itself, or for a side milestone the furthest main scenario
            // quest it requires.
            var anchor = index.TryGetValue(gate.Milestone.RowId, out var at)
                ? at
                : Ancestors(gate.Milestone.RowId).Where(index.ContainsKey).Select(id => index[id]).DefaultIfEmpty(-1).Max();

            // The derived tokens: the milestone's own name, and every main scenario genre from the anchor's on.
            var names = new List<string> { gate.Milestone.Name };
            if (anchor >= 0)
            {
                names.AddRange(story.Skip(anchor).Select(q => q.Journal.GenreName).Distinct(StringComparer.Ordinal));
            }

            var tokens = names.Concat(names.SelectMany(Words)).Concat(PayoffNames).Distinct(StringComparer.Ordinal).ToList();
            Assert.NotEmpty(tokens);
            foreach (var token in tokens)
            {
                Assert.False(ContainsWord(instruction, token), $"payoff gate {gate.Gate.Id}: instruction \"{instruction}\" names \"{token}\"");
            }

            // And no main scenario quest past the anchor, by its full name (the spoiler shield's rule).
            foreach (var quest in story.Skip(anchor + 1))
            {
                Assert.False(ContainsWord(instruction, quest.Name), $"payoff gate {gate.Gate.Id}: instruction names main scenario quest {quest.RowId} {quest.Name}");
            }

            // The reason is not repeated in the instruction.
            Assert.DoesNotContain(gate.Gate.Why, instruction, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_file_carries_its_review_status_https_evidence_and_spoiler_free_ids()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureCatalog.CuratedDir(), CuratedData.PayoffGatesFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int?)root["schema"]);
        var review = (string?)root["review"];
        Assert.False(string.IsNullOrWhiteSpace(review), "payoff_gates.json has no review status");
        Assert.True(review!.StartsWith("pending:", StringComparison.Ordinal) || review.StartsWith("confirmed:", StringComparison.Ordinal), $"review \"{review}\" is neither pending: nor confirmed:");

        Assert.NotEmpty(fixture.Curated.PayoffGates);
        foreach (var gate in fixture.Curated.PayoffGates)
        {
            Assert.Matches("^[a-z][a-z0-9-]*$", gate.Id);
            Assert.NotEmpty(gate.Evidence);
            Assert.All(gate.Evidence, url => Assert.StartsWith("https://", url, StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(gate.Note));
            Assert.EndsWith(".", gate.Instruction, StringComparison.Ordinal);
            Assert.True(gate.Instruction.Length <= 100, $"payoff gate {gate.Id}: the instruction fits one dashboard line");
        }
    }

    [Fact]
    public void The_chat_notice_is_taken_once_per_gate()
    {
        var gates = Gates();
        var active = gates.Active(AtMilestone(Gate("eden"), QuestState.Ready));
        var noticed = new HashSet<string>(StringComparer.Ordinal);

        var first = PayoffGates.TakeNotices(active, noticed);
        Assert.Contains(first, g => g.Gate.Id == "eden");
        Assert.Contains("eden", noticed);

        Assert.Empty(PayoffGates.TakeNotices(active, noticed));
    }

    /// <summary>Capitalised words of four letters or more, a possessive "'s" dropped, common words left out.</summary>
    private static IEnumerable<string> Words(string name) =>
        Regex.Matches(name, @"\p{Lu}[\p{L}'’]{3,}")
            .Select(m => Regex.Replace(m.Value, "['’]s$", string.Empty))
            .Where(w => w.Length >= 4 && !CommonWords.Contains(w));

    private static bool ContainsWord(string text, string word) =>
        word.Length > 0 && Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
