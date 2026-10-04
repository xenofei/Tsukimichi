using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// Copy report (plan v7, 1.18.0, A2; spec-1.18 "Copy report"): plain lines for a bug report, each left out when its facts
/// are unknown, and never the character's name, world, Free Company or chat: private words are scrubbed from every field
/// and every field is one line. The per-step stop count is capped.
/// </summary>
public class RunReportTests
{
    private static readonly string[] Private = ["Aria Moonsong", "Ultros", "MOON"];

    private static RunReportFacts Full => new()
    {
        PluginVersion = "1.18.0",
        ApiLevel = "15",
        GameVersion = "2026.09.15",
        HandOff = "Questionable",
        HandOffVersion = "7.4.12",
        StartedBy = "started by Tsukimichi, \"this quest only\"",
        Stopped = "error · missing sequence",
        QuestName = "Into the Aery",
        QuestId = 67187 & 0xFFFF,
        Sequence = 3,
        Step = 2,
        Job = "PLD",
        Level = 57,
        Zone = "Coerthas Central Highlands",
        TerritoryId = 155,
        MapCoordinates = "18.2, 24.7",
        Travel = "vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard",
        StopsHere = 2,
    };

    [Fact]
    public void The_report_reads_as_the_spec_shows()
    {
        var text = RunReport.Build(Full, Private);
        var lines = text.Split('\n');
        Assert.Equal("Tsukimichi 1.18.0 · Dalamud API 15 · game 2026.09.15", lines[0]);
        Assert.Equal("Hand-off: Questionable 7.4.12 (started by Tsukimichi, \"this quest only\")", lines[1]);
        Assert.Equal("Stopped: error · missing sequence", lines[2]);
        Assert.Equal($"Quest: Into the Aery (#{67187 & 0xFFFF}) · step 3 · sequence 3", lines[3]);
        Assert.Equal("Job: PLD 57 · Zone: Coerthas Central Highlands (155) · at 18.2, 24.7", lines[4]);
        Assert.Equal("Travel: vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard", lines[5]);
        Assert.Equal("Stops at this step on this computer: 2", lines[6]);
        Assert.Equal(7, lines.Length);
    }

    [Fact]
    public void Unknown_facts_leave_their_lines_out()
    {
        var text = RunReport.Build(new RunReportFacts { PluginVersion = "1.18.0", HandOff = "Travel", Stopped = "stuck" }, Private);
        Assert.Equal("Tsukimichi 1.18.0\nHand-off: Travel\nStopped: stuck", text);
    }

    [Fact]
    public void Private_words_never_reach_the_report_whatever_field_carries_them()
    {
        var facts = Full with
        {
            QuestName = "A favour for Aria moonsong",
            Zone = "Ultros's hideout",
            HandOffVersion = "7.4.12 (MOON build)",
            Stopped = "error · aria moonsong said hi",
        };
        var text = RunReport.Build(facts, Private);
        Assert.DoesNotContain("Aria", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ultros", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MOON build", text, StringComparison.Ordinal);
        Assert.Contains(RunReport.Hidden, text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_cannot_add_lines()
    {
        var facts = Full with { QuestName = "Line one\r\n[Say] Aria Moonsong: hello\tthere" };
        var text = RunReport.Build(facts, Private);
        Assert.Equal(7, text.Split('\n').Length);
        Assert.Contains("Quest: Line one [Say] [hidden]: hello there", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_flattens_and_ignores_tiny_or_empty_words()
    {
        Assert.Equal("a b c", RunReport.Scrub("  a\n\nb \t c  ", []));
        Assert.Equal("Into the Aery", RunReport.Scrub("Into the Aery", ["", " ", "A"]));
        Assert.Equal(string.Empty, RunReport.Scrub(null, Private));
    }

    [Fact]
    public void The_per_step_count_counts_and_stays_capped()
    {
        var counts = new Dictionary<string, int>();
        Assert.Equal(1, RunStopCounts.Note(counts, 65964, 3));
        Assert.Equal(2, RunStopCounts.Note(counts, 65964, 3));
        Assert.Equal(1, RunStopCounts.Note(counts, 65964, null));
        Assert.Equal(0, RunStopCounts.Note(counts, 0, 1));
        Assert.Equal("65964:3", RunStopCounts.Key(65964, 3));
        for (uint i = 1; i <= RunStopCounts.Max + 50; i++)
        {
            RunStopCounts.Note(counts, 70000 + i, 1);
        }

        Assert.Equal(RunStopCounts.Max, counts.Count);
        Assert.True(counts.ContainsKey(RunStopCounts.Key(70000 + RunStopCounts.Max + 50, 1)));
    }

    [Fact]
    public void The_cap_evicts_the_oldest_step_every_time_never_the_newest()
    {
        var counts = new Dictionary<string, int>();
        for (uint i = 1; i <= RunStopCounts.Max; i++)
        {
            RunStopCounts.Note(counts, i, 1);
        }

        // Two new steps past the cap: the first two noted go, in order. (Removing one key left a hole the next new key
        // filled, so the second eviction took the step just added.)
        RunStopCounts.Note(counts, 9001, 1);
        Assert.False(counts.ContainsKey(RunStopCounts.Key(1, 1)));
        Assert.True(counts.ContainsKey(RunStopCounts.Key(9001, 1)));

        RunStopCounts.Note(counts, 9002, 1);
        Assert.False(counts.ContainsKey(RunStopCounts.Key(2, 1)));
        Assert.True(counts.ContainsKey(RunStopCounts.Key(9001, 1)));
        Assert.True(counts.ContainsKey(RunStopCounts.Key(9002, 1)));
        Assert.Equal(RunStopCounts.Max, counts.Count);

        // The map's order is the order the steps were first noted; counting a kept step again does not move it.
        Assert.Equal(2, RunStopCounts.Note(counts, 3, 1));
        Assert.Equal(RunStopCounts.Key(3, 1), counts.Keys.First());
        Assert.Equal(RunStopCounts.Key(9002, 1), counts.Keys.Last());
        RunStopCounts.Note(counts, 9003, 1);
        Assert.False(counts.ContainsKey(RunStopCounts.Key(3, 1)));
        Assert.Equal(RunStopCounts.Key(4, 1), counts.Keys.First());
    }
}
