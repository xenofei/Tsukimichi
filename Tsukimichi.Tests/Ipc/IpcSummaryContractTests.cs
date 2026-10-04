using Tsukimichi.Core.Ipc;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// The 1.22.0 summary gates are a contract Tsukimichi for Umbra is built against (plan v8 M2; docs/ipc.md "The summary"):
/// their names, signatures, release, the summary's own version and the OpenAt places are pinned here, and the docs and the
/// drop-in client name every one. Changing what a shipped gate takes or returns bumps <see cref="IpcChannels.SummaryVersion"/>
/// and this test with it; adding a gate keeps both versions.
/// </summary>
public sealed class IpcSummaryContractTests
{
    /// <summary>The summary gates as shipped in 1.22.0: name, signature, release.</summary>
    private static readonly (string Name, string Signature, string Since, bool Message)[] Shipped =
    [
        ("Tsukimichi.GetSummaryVersion", "() -> int", "1.22.0", false),
        ("Tsukimichi.GetCharacter", "() -> (string name, string job, int level)", "1.22.0", false),
        ("Tsukimichi.GetUpNext", "() -> (uint rowId, string name, string step)", "1.22.0", false),
        ("Tsukimichi.GetReadyCount", "() -> (int ready, int here, string job)", "1.22.0", false),
        ("Tsukimichi.GetJournalRoom", "() -> (int used, int cap)", "1.22.0", false),
        ("Tsukimichi.GetEndingSoon", "() -> (string name, int daysLeft)[]", "1.22.0", false),
        ("Tsukimichi.GetStoryMeter", "() -> (string part, int leftToLatest, bool caughtUp)", "1.22.0", false),
        ("Tsukimichi.GetTheme", "() -> string", "1.22.0", false),
        ("Tsukimichi.OpenAt", "(string place) -> bool", "1.22.0", false),
        ("Tsukimichi.AddonHello", "(string addon, string version) -> int", "1.22.0", false),
        ("Tsukimichi.SummaryChanged", "message ()", "1.22.0", true),
    ];

    [Fact]
    public void The_summary_gates_are_pinned_by_name_signature_and_release()
    {
        var added = IpcChannels.All.Where(static g => g.Since == "1.22.0").Select(static g => (g.Name, g.Signature, g.Since, g.IsMessage)).ToArray();
        Assert.Equal(Shipped, added);
    }

    [Fact]
    public void The_versions_are_pinned()
    {
        // The summary has its own version; the 1.22 gates are additive, so the API version stays 1.
        Assert.Equal(1, IpcChannels.SummaryVersion);
        Assert.Equal(1, IpcChannels.ApiVersion);
    }

    [Fact]
    public void OpenAt_knows_five_places_and_nothing_else()
    {
        Assert.Equal(["main", "tonight", "upnext", "route", "settings"], IpcPlaces.All);
        Assert.Equal("tonight", IpcPlaces.Parse(" Tonight "));
        Assert.Equal("upnext", IpcPlaces.Parse("UPNEXT"));
        Assert.Null(IpcPlaces.Parse("travel"));
        Assert.Null(IpcPlaces.Parse("teleport"));
        Assert.Null(IpcPlaces.Parse(string.Empty));
        Assert.Null(IpcPlaces.Parse(null));
    }

    [Fact]
    public void Before_a_character_is_ready_every_answer_is_the_not_ready_one()
    {
        var empty = TsukimichiSummary.Empty;
        Assert.False(empty.Ready);
        Assert.Equal((string.Empty, string.Empty, 0), empty.CharacterAnswer());
        Assert.Equal((0u, string.Empty, string.Empty), empty.UpNextAnswer());
        Assert.Equal((0, 0, string.Empty), empty.ReadyAnswer());
        Assert.Equal((-1, 0), empty.JournalAnswer());
        Assert.Empty(empty.EndingSoonAnswer());
        Assert.Equal((string.Empty, 0, false), empty.StoryAnswer());
    }

    [Fact]
    public void A_summary_answers_each_gate_from_one_capture()
    {
        var summary = Sample();
        Assert.Equal(("Kiri", "WHM", 100), summary.CharacterAnswer());
        Assert.Equal((70100u, "The Long Road to Xak Tural", "Step 3: Speak with Erenville. · Shaaloani"), summary.UpNextAnswer());
        Assert.Equal((12, 3, "WHM"), summary.ReadyAnswer());
        Assert.Equal((27, 30), summary.JournalAnswer());
        Assert.Equal([("The Rising", 2)], summary.EndingSoonAnswer());
        Assert.Equal(("Dawntrail", 91, false), summary.StoryAnswer());

        // Each call is a fresh array: a caller can't change the capture.
        var first = summary.EndingSoonAnswer();
        first[0] = ("Changed", 9);
        Assert.Equal([("The Rising", 2)], summary.EndingSoonAnswer());
    }

    [Fact]
    public void SummaryChanged_follows_what_the_summary_says()
    {
        var a = Sample();
        Assert.True(a.Same(Sample()));
        Assert.False(a.Same(a with { ReadyCount = 11 }));
        Assert.False(a.Same(a with { EndingSoon = [("The Rising", 1)] }));
        Assert.False(a.Same(null));
    }

    [Fact]
    public void The_docs_and_the_client_name_every_summary_gate()
    {
        var root = ResxFiles.RepositoryRoot();
        var docs = File.ReadAllText(Path.Combine(root, "docs", "ipc.md"));
        var client = File.ReadAllText(Path.Combine(root, "docs", "TsukimichiIpc.cs"));
        foreach (var (name, signature, _, message) in Shipped)
        {
            Assert.Contains("`" + name + "`", docs, StringComparison.Ordinal);
            Assert.Contains("\"" + name + "\"", client, StringComparison.Ordinal);
            if (!message)
            {
                Assert.Contains("`" + signature + "`", docs, StringComparison.Ordinal);
            }
        }

        foreach (var place in IpcPlaces.All)
        {
            Assert.Contains("`" + place + "`", docs, StringComparison.Ordinal);
        }
    }

    private static TsukimichiSummary Sample() => new(
        Ready: true,
        Character: "Kiri",
        Job: "WHM",
        Level: 100,
        UpNextRowId: 70100,
        UpNextName: "The Long Road to Xak Tural",
        UpNextStep: "Step 3: Speak with Erenville. · Shaaloani",
        ReadyCount: 12,
        HereCount: 3,
        JournalUsed: 27,
        JournalCap: 30,
        EndingSoon: [("The Rising", 2)],
        StoryPart: "Dawntrail",
        StoryLeft: 91,
        CaughtUp: false);
}
