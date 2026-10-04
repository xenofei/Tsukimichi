using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the game-call kill switch (1.19.0 review): every <c>GameLinks</c> method that reaches into a game
/// agent (<c>Agent….Instance()</c>: the journal, the map, the Duty Finder, the Gathering Log) checks the switch
/// (<c>CallsAllowed</c>, directly or through a <c>CanOpen…</c> gate) before it does. 1.19's Open journal skipped it while
/// Flag, the map and Open Gathering Log took it. The test project references Core and GameData only, so it reads the
/// plugin's sources from the repository.
/// </summary>
public sealed class GameCallsLintTests
{
    // A method's signature line in a GameLinks partial (four-space indent), its body running to the next "    }" line.
    internal static readonly Regex Method = new(@"^    (?:public|private|internal)[^\n=;]*\b(\w+)\([^\n]*\)\s*\n    \{\n(.*?)\n    \}", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

    private static readonly Regex AgentCall = new(@"\bAgent\w+\.Instance\(\)", RegexOptions.Compiled);

    private static readonly Regex Gate = new(@"\bCallsAllowed\b|\bCanOpen\w*\b", RegexOptions.Compiled);

    [Fact]
    public void Every_game_agent_call_takes_the_kill_switch()
    {
        var ui = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui");
        var checkedCalls = 0;
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(ui, "GameLinks*.cs").OrderBy(static f => f, StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
            foreach (Match method in Method.Matches(source))
            {
                var body = method.Groups[2].Value;
                if (AgentCall.Match(body) is not { Success: true } call)
                {
                    continue;
                }

                checkedCalls++;
                var gate = Gate.Match(body);
                if (!gate.Success || gate.Index > call.Index)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {method.Groups[1].Value} calls a game agent without checking CallsAllowed first");
                }
            }
        }

        // The journal, the map, the Duty Finder and the Gathering Log: the lint must see them, or it checks nothing.
        Assert.True(checkedCalls >= 4, $"found {checkedCalls} agent calls in GameLinks");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }
}
