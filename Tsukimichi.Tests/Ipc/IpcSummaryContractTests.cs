using System.Reflection;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// The 1.22.0 summary gates are a contract Tsukimichi for Umbra is built against (plan v8 M2; docs/ipc.md "The summary"):
/// their names, signatures, release, the summary's own version and the OpenAt places are pinned here, and the docs and the
/// drop-in client name every one. Changing what a shipped gate takes or returns bumps <see cref="IpcChannels.SummaryVersion"/>
/// and this test with it; adding a gate keeps both versions.
/// </summary>
public sealed partial class IpcSummaryContractTests
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
        ("Tsukimichi.GetReadyTonight", "(int max) -> (uint rowId, string name, string place)[]", "1.22.0", false),
        ("Tsukimichi.SummaryChanged", "message ()", "1.22.0", true),
    ];

    /// <summary>
    /// The generic types each pinned signature is registered with: the arguments, then the answer, without the names
    /// (<c>ICallGateProvider&lt;…&gt;</c>'s type arguments); a message is <c>object</c> plus its arguments.
    /// </summary>
    private static string GenericTypes(string signature)
    {
        if (signature.StartsWith("message (", StringComparison.Ordinal))
        {
            var args = TypesOf(signature["message (".Length..^1]);
            return args.Length == 0 ? "object" : args + ",object";
        }

        var arrow = signature.IndexOf(" -> ", StringComparison.Ordinal);
        var parameters = TypesOf(signature[1..(arrow - 1)]);
        var answer = signature[(arrow + 4)..];
        answer = answer.StartsWith('(') ? "(" + TypesOf(answer[1..answer.LastIndexOf(')')]) + ")" + answer[(answer.LastIndexOf(')') + 1)..] : answer;
        return parameters.Length == 0 ? answer : parameters + "," + answer;
    }

    /// <summary>"uint rowId, string name" as "uint,string".</summary>
    private static string TypesOf(string list) =>
        string.Join(',', list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static p => p.Split(' ')[0]));

    /// <summary>The gate constants of <see cref="IpcChannels"/> by name ("GetUpNextGate" → "Tsukimichi.GetUpNext").</summary>
    private static Dictionary<string, string> GateConstants() =>
        typeof(IpcChannels).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(static f => f.Name, static f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Each_summary_gate_is_registered_and_wrapped_with_the_types_its_signature_pins()
    {
        // The provider's own registration and the drop-in client's subscriber, read from source (the test project does
        // not load the plugin): a type changed on either side without the signature fails here.
        var root = ResxFiles.RepositoryRoot();
        var provider = File.ReadAllText(Path.Combine(root, "Tsukimichi", "Game", "IpcProvider.Summary.cs"));
        var client = File.ReadAllText(Path.Combine(root, "docs", "TsukimichiIpc.cs"));
        var constants = GateConstants();
        var registrations = ProviderRegistration().Matches(provider);
        var registered = registrations
            .ToDictionary(m => constants[m.Groups["gate"].Value], m => Regex.Replace(m.Groups["types"].Value, @"\s+", string.Empty));
        var fields = registrations.ToDictionary(m => constants[m.Groups["gate"].Value], m => m.Groups["field"].Value);
        var wrapped = ClientSubscription().Matches(client)
            .ToDictionary(m => m.Groups["gate"].Value, m => Regex.Replace(m.Groups["types"].Value, @"\s+", string.Empty));

        foreach (var (name, signature, _, _) in Shipped)
        {
            var expected = GenericTypes(signature);
            Assert.True(registered.TryGetValue(name, out var types), name + " is not registered");
            Assert.Equal(expected, types);
            Assert.True(wrapped.TryGetValue(name, out var clientTypes), name + " is not in the client");
            Assert.Equal(expected, clientTypes);
        }

        // And each one with an answer is registered as a function on that provider, and unregistered with it.
        foreach (var (name, _, _, _) in Shipped.Where(static g => !g.Message))
        {
            Assert.Contains(fields[name] + ".RegisterFunc(", provider, StringComparison.Ordinal);
            Assert.Contains("Unregister(" + fields[name] + ");", provider, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_type_reading_is_strict()
    {
        Assert.Equal("(uint,string,string)", GenericTypes("() -> (uint rowId, string name, string step)"));
        Assert.Equal("int,(uint,string,string)[]", GenericTypes("(int max) -> (uint rowId, string name, string place)[]"));
        Assert.Equal("string,string,int", GenericTypes("(string addon, string version) -> int"));
        Assert.Equal("object", GenericTypes("message ()"));
        Assert.NotEqual("(int,string,string)", GenericTypes("() -> (uint rowId, string name, string step)"));
    }

    [GeneratedRegex(@"(?<field>\w+) = pluginInterface\.GetIpcProvider<(?<types>[^;]+?)>\(IpcChannels\.(?<gate>\w+)\)")]
    private static partial Regex ProviderRegistration();

    [GeneratedRegex(@"GetIpcSubscriber<(?<types>[^;]+?)>\(""(?<gate>Tsukimichi\.\w+)""\)")]
    private static partial Regex ClientSubscription();

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
    public void OpenAt_knows_six_places_and_nothing_else()
    {
        // 1.22.0's five, then Make room appended (the words stay stable within the summary version).
        Assert.Equal(["main", "tonight", "upnext", "route", "settings", "makeroom"], IpcPlaces.All);
        Assert.Equal("tonight", IpcPlaces.Parse(" Tonight "));
        Assert.Equal("upnext", IpcPlaces.Parse("UPNEXT"));
        Assert.Equal("makeroom", IpcPlaces.Parse(" MakeRoom "));
        Assert.Null(IpcPlaces.Parse("make room"));
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
        Assert.Empty(empty.ReadyTonightAnswer(5));
    }

    [Fact]
    public void GetReadyTonight_answers_the_first_max_a_fresh_array_each_call()
    {
        var summary = Sample() with
        {
            ReadyTonight = [(70100u, "The Long Road to Xak Tural", "Tuliyollal"), (70200u, "Sidequest (Lv 92)", "Dawntrail area 3"), (70300u, "A Fresh Start", "Gridania")],
        };
        Assert.Equal([(70100u, "The Long Road to Xak Tural", "Tuliyollal"), (70200u, "Sidequest (Lv 92)", "Dawntrail area 3")], summary.ReadyTonightAnswer(2));
        Assert.Equal(3, summary.ReadyTonightAnswer(50).Length);
        Assert.Empty(summary.ReadyTonightAnswer(0));
        Assert.Empty(summary.ReadyTonightAnswer(-1));

        var first = summary.ReadyTonightAnswer(1);
        first[0] = (1u, "Changed", string.Empty);
        Assert.Equal(70100u, summary.ReadyTonightAnswer(1)[0].RowId);

        // A changed list is a changed summary (SummaryChanged).
        Assert.False(summary.Same(Sample()));
        Assert.True(summary.Same(summary with { ReadyTonight = [.. summary.ReadyTonight] }));
    }

    [Fact]
    public void The_first_ready_summary_after_loading_is_a_change_so_SummaryChanged_is_sent()
    {
        // SummarySource starts from Empty and raises Changed (wired to SummaryChanged) for the first capture that differs.
        Assert.False(Sample().Same(TsukimichiSummary.Empty));
        Assert.False(TsukimichiSummary.Empty.Same(Sample()));
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
