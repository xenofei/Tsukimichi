using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the first-run portrait pack offer's wiring (1.22.0; the rules themselves are tested in Core,
/// <see cref="Portraits.PortraitPackWelcomeTests"/>): the window asks <c>PortraitPackWelcome</c> when to show, when to
/// step aside and how the player answered; it opens without the focus and lets keys answer only once the player clicked
/// inside it; it starts the download only on the Download answer or a Try again click, keeps an accepted Enter from the
/// game, keeps any other close before Download as Not now, and goes after What's new, the tour and Settings' own
/// confirmation. The test project references Core and GameData only, so it reads the plugin's sources from the repository.
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
    public void It_opens_without_taking_the_focus()
    {
        Assert.Contains("ImGuiWindowFlags.NoFocusOnAppearing", Window, StringComparison.Ordinal);
        Assert.DoesNotContain("BringToFront", Window, StringComparison.Ordinal);
        Assert.DoesNotContain("SetNextWindowFocus", Window, StringComparison.Ordinal);

        // The close hotkey (Esc) is off until the player engaged, and follows the engagement.
        Assert.Contains("RespectCloseHotkey = false;", Window, StringComparison.Ordinal);
        Assert.Contains("RespectCloseHotkey = engaged;", Member(Window, "public override void Draw()"), StringComparison.Ordinal);
    }

    [Fact]
    public void Enter_and_download_start_the_download_and_esc_declines_only_once_engaged()
    {
        var offer = Member(Window, "private void DrawOffer(");
        Assert.Contains("PortraitPackWelcome.AnswerOf(download, notNow, enter, escape, engaged, sinceOpen)", offer, StringComparison.Ordinal);
        Assert.DoesNotContain("focused, sinceOpen", offer, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.Enter", offer, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.Escape", offer, StringComparison.Ordinal);

        // Engaged means a click inside the offer since it appeared, kept while it has the focus.
        var draw = Member(Window, "public override void Draw()");
        Assert.Contains("ImGui.IsMouseClicked(", draw, StringComparison.Ordinal);
        Assert.Contains("engaged = true;", draw, StringComparison.Ordinal);
        Assert.Contains("Disengage();", Member(Window, "public override void PreDraw()"), StringComparison.Ordinal);

        // The download starts only in the Download answer's branch.
        var download = offer.IndexOf("case PortraitPackWelcomeAnswer.Download:", StringComparison.Ordinal);
        var notNow = offer.IndexOf("case PortraitPackWelcomeAnswer.NotNow:", StringComparison.Ordinal);
        var start = offer.IndexOf(".StartDownload(", StringComparison.Ordinal);
        Assert.True(download >= 0 && notNow > download && start > download && start < notNow, "StartDownload belongs to the Download answer");
        Assert.Equal(start, offer.LastIndexOf(".StartDownload(", StringComparison.Ordinal));

        // Yes by default, but the keyboard focus goes to Download portraits only once the offer owns the keys.
        Assert.Contains("PortraitPackWelcome.OwnsKeys(engaged, sinceOpen, asking: true)", offer, StringComparison.Ordinal);
        Assert.Contains("if (focusPending && ownsKeys)", offer, StringComparison.Ordinal);
        Assert.Contains("ImGui.SetItemDefaultFocus();", offer, StringComparison.Ordinal);
        Assert.Contains("ImGui.SetKeyboardFocusHere(-1);", offer, StringComparison.Ordinal);
    }

    [Fact]
    public void An_accepted_enter_is_kept_from_the_game()
    {
        var offer = Member(Window, "private void DrawOffer(");
        Assert.Contains("ConsumeEnter();", offer[offer.IndexOf("case PortraitPackWelcomeAnswer.Download:", StringComparison.Ordinal)..], StringComparison.Ordinal);
        var consume = Member(Window, "private void ConsumeEnter()");
        Assert.Contains("keys[VirtualKey.RETURN] = false;", consume, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.RETURN, VirtualKey.ESCAPE", Window, StringComparison.Ordinal);
        Assert.Contains("keys[key] = false;", Member(Window, "public void ConsumeKeys("), StringComparison.Ordinal);

        var wiring = Source("Plugin.PortraitPackOffer.cs");
        Assert.Contains("KeyState = KeyState", wiring, StringComparison.Ordinal);
        Assert.Contains("Framework.Update += offer.ConsumeKeys;", wiring, StringComparison.Ordinal);
        Assert.Contains("Framework.Update -= offer.ConsumeKeys;", wiring, StringComparison.Ordinal);
        Assert.Contains("Unwind(\"portrait pack offer\", TearDownPortraitPackOffer);", Source("Plugin.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Try_again_starts_the_download_only_from_its_button()
    {
        var after = Member(Window, "private void DrawDownload(");
        var pill = after.IndexOf("var retry = Chrome.ActionPill(\"##packOfferRetry\"", StringComparison.Ordinal);
        var click = after.IndexOf("if (retry)", StringComparison.Ordinal);
        var start = after.IndexOf(".StartDownload(", StringComparison.Ordinal);
        Assert.True(pill >= 0 && click > pill && start > click, "Try again's StartDownload sits under its button click");
        Assert.Equal(start, after.LastIndexOf(".StartDownload(", StringComparison.Ordinal));

        // A removal in Settings since closes the window instead of reading as a failed download.
        Assert.Contains("PortraitPackWelcome.ViewAfterDownload(", after, StringComparison.Ordinal);
        Assert.Contains("phase == PortraitPackPhase.Removing", after, StringComparison.Ordinal);
        Assert.Contains("service.LastWasRemoval", after, StringComparison.Ordinal);
    }

    [Fact]
    public void Closing_it_before_download_counts_as_not_now()
    {
        var close = Member(Window, "public override void OnClose()");
        Assert.Contains("if (!downloading)", close, StringComparison.Ordinal);
        Assert.Contains("Record();", close, StringComparison.Ordinal);
    }

    [Fact]
    public void Stepping_aside_answers_nothing()
    {
        var conditions = Member(Window, "public override bool DrawConditions()");
        Assert.Contains("PortraitPackWelcome.Visible(moment, OtherFirst?.Invoke() ?? false)", conditions, StringComparison.Ordinal);
        Assert.DoesNotContain("Record(", conditions, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpen", conditions, StringComparison.Ordinal);
    }

    [Fact]
    public void It_goes_after_whats_new_the_tour_and_settings_confirmation()
    {
        var wiring = Source("Plugin.PortraitPackOffer.cs");
        Assert.Contains("whatsNewPopup?.Due == true", wiring, StringComparison.Ordinal);
        Assert.Contains("tutorial?.Active == true", wiring, StringComparison.Ordinal);
        Assert.Contains("settingsWindow.PackDialogShowing", wiring, StringComparison.Ordinal);
        Assert.Contains("Moment = WhatsNewMomentNow", wiring, StringComparison.Ordinal);

        // Wired after What's new exists.
        var plugin = Source("Plugin.cs");
        var whatsNew = plugin.IndexOf("InitializeWhatsNew(mainWindow, settingsWindow);", StringComparison.Ordinal);
        var offer = plugin.IndexOf("InitializePortraitPackOffer(", StringComparison.Ordinal);
        Assert.True(whatsNew >= 0 && offer > whatsNew);
    }
}
