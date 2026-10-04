using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source checks for the 1.22.0 review's plugin-side fixes, whose code lives in the plugin (not loaded by the tests):
/// Tonight from the moon icon, the server info bar and IPC opens on the logged-in character the card describes; IPC
/// <c>OpenAt("route")</c> never takes the route window's Undo; Make room opens through the status bar's scope; the
/// summary is announced from its first capture; and every collection in the configuration survives a saved <c>null</c>.
/// </summary>
public sealed partial class Release122FixesWiringTests
{
    private static string Source(params string[] path) => File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), .. path]));

    /// <summary>The body of the method or local function <paramref name="name"/> (from its line to the closing brace at its indent).</summary>
    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature + " not found");
        var lineStart = source.LastIndexOf('\n', start) + 1;
        var indent = source[lineStart..start];
        var end = source.IndexOf("\n" + indent + "}", start, StringComparison.Ordinal);
        Assert.True(end > start, signature + " has no end");
        return source[start..end];
    }

    [Fact]
    public void Tonight_from_the_moon_icon_opens_on_the_logged_in_character()
    {
        var open = Body(Source("Tsukimichi", "Plugin.MoonIcon.cs"), "void OpenTonight()");
        Assert.True(open.IndexOf("ViewLiveCharacter();", StringComparison.Ordinal) is >= 0 and var view
            && view < open.IndexOf("ui.Tab = NavTab.Journal;", StringComparison.Ordinal));
    }

    [Fact]
    public void Tonight_Up_next_and_Make_room_from_IPC_and_the_server_info_bar_open_on_the_logged_in_character()
    {
        var source = Source("Tsukimichi", "Plugin.Welcome22.cs");
        var place = Body(source, "private void OpenWelcomePlace(string place)");
        Assert.Matches(TonightCase(), place);
        Assert.Matches(MakeRoomCase(), place);

        var live = Body(source, "private void ViewLiveCharacter()");
        Assert.Contains("Session.LiveContentId is { } live && Session.ViewedContentId != live", live, StringComparison.Ordinal);
        Assert.Contains("Session.ViewCharacter(live);", live, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenAt_route_never_drops_the_route_windows_Undo()
    {
        var place = Body(Source("Tsukimichi", "Plugin.Welcome22.cs"), "private void OpenWelcomePlace(string place)");
        Assert.Contains("routeWindow?.RevealFollowed();", place, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowFollowed", place, StringComparison.Ordinal);

        // On the followed route already, or holding an Undo: only to the front, before anything that drops the Undo.
        var reveal = Body(Source("Tsukimichi", "Ui", "RouteWindow.cs"), "public bool RevealFollowed()");
        Assert.DoesNotContain("DropUndo", reveal, StringComparison.Ordinal);
        var guard = reveal.IndexOf("saved.Matches(shown) || undo.Added.Count > 0", StringComparison.Ordinal);
        Assert.True(guard >= 0);
        Assert.True(guard < reveal.IndexOf("Show(saved.ToTarget());", StringComparison.Ordinal));
    }

    [Fact]
    public void Make_room_from_IPC_opens_in_the_status_bars_scope_before_the_popover_draws()
    {
        var draw = Source("Tsukimichi", "Ui", "MainWindow.cs");
        var open = draw.IndexOf("OpenRequestedMakeRoom();", StringComparison.Ordinal);
        Assert.True(open > draw.IndexOf("DrawStatusBar(session, bundle);", StringComparison.Ordinal));
        Assert.True(open < draw.IndexOf("DrawMakeRoomPopover();", StringComparison.Ordinal));
    }

    [Fact]
    public void The_summary_is_announced_from_its_first_capture()
    {
        // Subscribed before the tick that builds the first capture, so the first Ready summary sends SummaryChanged.
        var source = Source("Tsukimichi", "Plugin.Welcome22.cs");
        var subscribe = source.IndexOf("summary.Changed += ipc.AnnounceSummary;", StringComparison.Ordinal);
        Assert.True(subscribe >= 0);
        Assert.True(subscribe < source.IndexOf("Framework.Update += WelcomeHomeTick;", StringComparison.Ordinal));
        Assert.Contains("private volatile TsukimichiSummary current = TsukimichiSummary.Empty;", Source("Tsukimichi", "Ui", "SummarySource.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_collection_in_the_configuration_survives_a_saved_null()
    {
        var root = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Config");
        var load = File.ReadAllText(Path.Combine(root, "Configuration.cs"));
        var collections = Directory.GetFiles(root, "Configuration*.cs")
            .SelectMany(file => CollectionProperty().Matches(File.ReadAllText(file)))
            .Select(static m => m.Groups["name"].Value)
            .ToList();
        Assert.Contains("UmbraPlaces", collections);
        Assert.All(collections, name => Assert.True(
            load.Contains("config." + name + " ??= ", StringComparison.Ordinal) || load.Contains("config." + name + " = Sanitize", StringComparison.Ordinal),
            name + " is not reset when it loads as null"));
    }

    [GeneratedRegex(@"case IpcPlaces\.Tonight or IpcPlaces\.UpNext:\s*(//[^\n]*\s*)*ViewLiveCharacter\(\);")]
    private static partial Regex TonightCase();

    [GeneratedRegex(@"case IpcPlaces\.MakeRoom:\s*(//[^\n]*\s*)*ViewLiveCharacter\(\);\s*mainWindow\.RequestMakeRoom\(\);")]
    private static partial Regex MakeRoomCase();

    [GeneratedRegex(@"public (?:Dictionary|List|HashSet)<[^\n]*> (?<name>\w+) \{ get; set; \}")]
    private static partial Regex CollectionProperty();
}
