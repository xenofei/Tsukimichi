using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the first-run portrait pack offer's wiring (1.22.0; the rules themselves are tested in Core,
/// <see cref="Portraits.PortraitPackWelcomeTests"/>): the window asks <c>PortraitPackWelcome</c> when to show and how the
/// player answered, starts the download only on the Download answer (Enter or the button), keeps any other close as
/// Not now, and goes after What's new, the tour and Settings' own confirmation. The test project references Core and
/// GameData only, so it reads the plugin's sources from the repository.
/// </summary>
public sealed class PortraitPackOfferLintTests
{
    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), "Tsukimichi", .. path]));

    private static string Window => Source("Ui", "PortraitPackOfferWindow.cs");

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace at the same indent.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    [Fact]
    public void It_shows_only_when_the_core_rule_says_so()
    {
        var check = Member(Window, "public override void PreOpenCheck()");
        Assert.Contains("PortraitPackWelcome.Next(", check, StringComparison.Ordinal);
        Assert.Contains("settings.PortraitPackOfferAnswered", check, StringComparison.Ordinal);
        Assert.Contains("service.Loaded", check, StringComparison.Ordinal);
    }

    [Fact]
    public void Enter_and_download_start_the_download_and_esc_declines()
    {
        var offer = Member(Window, "private void DrawOffer(");
        Assert.Contains("PortraitPackWelcome.AnswerOf(download, notNow, enter, escape, focused, sinceOpen)", offer, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.Enter", offer, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.Escape", offer, StringComparison.Ordinal);

        // The download starts only in the Download answer's branch.
        var download = offer.IndexOf("case PortraitPackWelcomeAnswer.Download:", StringComparison.Ordinal);
        var notNow = offer.IndexOf("case PortraitPackWelcomeAnswer.NotNow:", StringComparison.Ordinal);
        var start = offer.IndexOf(".StartDownload(", StringComparison.Ordinal);
        Assert.True(download >= 0 && notNow > download && start > download && start < notNow, "StartDownload belongs to the Download answer");
        Assert.Equal(start, offer.LastIndexOf(".StartDownload(", StringComparison.Ordinal));

        // Yes by default: the focus goes to Download portraits, not before Enter may accept it.
        Assert.Contains("sinceOpen >= PortraitPackWelcome.KeySettleSeconds", offer, StringComparison.Ordinal);
        Assert.Contains("ImGui.SetItemDefaultFocus();", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void Closing_it_before_download_counts_as_not_now()
    {
        var close = Member(Window, "public override void OnClose()");
        Assert.Contains("if (!downloading)", close, StringComparison.Ordinal);
        Assert.Contains("Record();", close, StringComparison.Ordinal);
        Assert.Contains("RespectCloseHotkey = true;", Window, StringComparison.Ordinal);
    }

    [Fact]
    public void It_goes_after_whats_new_the_tour_and_settings_confirmation()
    {
        var wiring = Source("Plugin.PortraitPackOffer.cs");
        Assert.Contains("whatsNewPopup?.Due == true", wiring, StringComparison.Ordinal);
        Assert.Contains("tutorial?.Active == true", wiring, StringComparison.Ordinal);
        Assert.Contains("TutorialOverlay { Offering: true }", wiring, StringComparison.Ordinal);
        Assert.Contains("settingsWindow.PackDialogShowing", wiring, StringComparison.Ordinal);
        Assert.Contains("Moment = WhatsNewMomentNow", wiring, StringComparison.Ordinal);

        // Wired after What's new exists.
        var plugin = Source("Plugin.cs");
        var whatsNew = plugin.IndexOf("InitializeWhatsNew(mainWindow, settingsWindow);", StringComparison.Ordinal);
        var offer = plugin.IndexOf("InitializePortraitPackOffer(", StringComparison.Ordinal);
        Assert.True(whatsNew >= 0 && offer > whatsNew);
    }
}
