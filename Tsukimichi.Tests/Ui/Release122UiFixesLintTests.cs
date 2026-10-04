using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for the wiring the 1.22.0 UI review found wrong (the logic itself is tested in Core,
/// <see cref="Release122UiFixesTests"/>): What's new is placed under Reduce motion and Plain, opens at its top, keeps
/// the outgoing picture for the cross-fade and fades its scrollbar; a theme pick, Reset and a share code leave Follow
/// Umbra with Undo; the moon icon's menu has its padding and closes when the icon hides, its card never blinks, its
/// Hide toast is instant at Plain and a failure retries; the update note never runs into the version; and the per-frame
/// paths of 1.22.0 build no strings. The test project references Core and GameData only, so it reads the plugin's
/// sources from the repository.
/// </summary>
public sealed class Release122UiFixesLintTests
{
    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), "Tsukimichi", .. path]));

    private static string Ui(string name) => Source("Ui", name);

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace at the same indent.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    [Fact]
    public void Whats_new_is_placed_after_measuring_with_motion_off_too()
    {
        var preDraw = Member(Ui("WhatsNewPopup.cs"), "public override void PreDraw()");
        Assert.Contains("placement.Next(", preDraw, StringComparison.Ordinal);
        Assert.DoesNotContain("Rising(", preDraw, StringComparison.Ordinal);
        Assert.Contains("placement.Reset()", Member(Ui("WhatsNewPopup.cs"), "private void Place()"), StringComparison.Ordinal);
        Assert.Contains("WhatsNewLayout.Clamp(", Member(Ui("WhatsNewPopup.cs"), "private void Place()"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ConfigWindow.Themes.cs", "private void ApplyThemeCard(")]
    [InlineData("ConfigWindow.Themes.cs", "private void DrawThemeReset()")]
    [InlineData("ConfigWindow.Themes.Share.cs", "private void ApplySharePreview(")]
    public void A_theme_reset_or_share_code_leaves_follow_umbra_and_undo_brings_it_back(string file, string member)
    {
        var body = Member(Ui(file), member);
        Assert.Contains("var wasUmbra = LeaveFollowUmbra();", body, StringComparison.Ordinal);
        Assert.Contains("RestoreAppearance(before, wasUmbra)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_counts_follow_umbra_as_not_default_and_a_theme_card_click_leaves_it_even_on_the_same_theme()
    {
        Assert.Contains("AppearanceEdits.IsDefault(saved, GlyphSeam.Appearance, settings.FollowUmbraPalette)", Member(Ui("ConfigWindow.Themes.cs"), "private void DrawThemeReset()"), StringComparison.Ordinal);
        Assert.Contains("&& !settings.FollowUmbraPalette", Member(Ui("ConfigWindow.Themes.cs"), "private void ApplyThemeCard("), StringComparison.Ordinal);
    }

    [Fact]
    public void The_moon_icons_menu_gets_the_padding_back()
    {
        var menu = Member(Ui("MoonIconWindow.cs"), "private void Menu(");
        var padding = menu.IndexOf("ImGuiStyleVar.WindowPadding, menuPadding", StringComparison.Ordinal);
        Assert.True(padding >= 0, "the menu must push the window padding the icon took away");
        Assert.True(padding < menu.IndexOf("ImGui.BeginPopup(MenuId)", StringComparison.Ordinal));
        Assert.Contains("Theme.PushPopup()", menu, StringComparison.Ordinal);
        Assert.Contains("menuPadding = ImGui.GetStyle().WindowPadding", Member(Ui("MoonIconWindow.cs"), "private void DrawIcon("), StringComparison.Ordinal);
    }

    [Fact]
    public void Whats_new_opens_at_the_top_of_its_notes()
    {
        Assert.Contains("scrollToTop = true;", Member(Ui("WhatsNewPopup.cs"), "private void Show("), StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_change_cross_fades_the_outgoing_picture()
    {
        var art = Member(Ui("WhatsNewPopup.cs"), "private void DrawArt(");
        Assert.Contains("art.Leaving(", art, StringComparison.Ordinal);
        Assert.Contains("1f - swap", art, StringComparison.Ordinal);

        // The outgoing picture is no longer released the moment another is wanted.
        var want = Member(Ui("ReleaseArtTexture.cs"), "public void Want(");
        Assert.DoesNotContain("Release();", want, StringComparison.Ordinal);
        Assert.Contains("slots.Replace()", want, StringComparison.Ordinal);
    }

    [Fact]
    public void The_quick_card_measures_only_on_open()
    {
        var card = Ui("MoonIconCard.cs");
        Assert.DoesNotContain("MathF.Abs(measured - height) > 0.5f", card, StringComparison.Ordinal);
        var content = Member(card, "private CardSettle Content(");
        Assert.Contains("MoonIconRules.Settle(", content, StringComparison.Ordinal);
        Assert.Contains("Chrome.ShiftVertices(", content, StringComparison.Ordinal);
        Assert.Contains("CardSettle.Measure", Member(card, "public void Draw("), StringComparison.Ordinal);
    }

    [Fact]
    public void The_notes_scrollbar_is_in_the_fade_and_hidden_while_measuring()
    {
        var body = Member(Ui("WhatsNewPopup.cs"), "private void DrawBody(");
        Assert.Contains("Chrome.FadeVertices(dl, 0, alpha)", body, StringComparison.Ordinal);
        Assert.Contains("ImGuiWindowFlags.NoScrollbar", body, StringComparison.Ordinal);
        Assert.DoesNotContain("var start = dl.VtxBuffer.Size;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hide_toast_is_instant_at_plain()
    {
        var toast = Member(Ui("MoonIconWindow.cs"), "private void DrawHiddenToast(");
        Assert.Contains("MoonIconRules.ToastFade(", toast, StringComparison.Ordinal);
        Assert.DoesNotContain("UiMetrics.ReduceMotion ? 1f", toast, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hidden_icon_closes_its_menu()
    {
        var window = Ui("MoonIconWindow.cs");
        Assert.Contains("CloseMenu();", Member(window, "private void Rest()"), StringComparison.Ordinal);
        Assert.Contains("ImGuiP.ClosePopupToLevel(", Member(window, "private void CloseMenu()"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_update_note_is_fitted_before_the_version()
    {
        var bar = Member(Ui("MainWindow.cs"), "private void DrawStatusBar(");
        Assert.DoesNotContain("MathF.Max(x, versionX - gap - noteWidth)", bar, StringComparison.Ordinal);
        Assert.Contains("UpdateNoteFit.TextRoom(", bar, StringComparison.Ordinal);
        Assert.Contains("Chrome.EllipsisText", Member(Ui("MainWindow.Update.cs"), "private float DrawUpdateNote("), StringComparison.Ordinal);
    }

    [Fact]
    public void A_moon_icon_failure_retries_and_its_card_and_particles_are_guarded()
    {
        var draw = Member(Source("Plugin.MoonIcon.cs"), "private void DrawMoonIcon()");
        Assert.Contains("moonIconRetry.Ready(", draw, StringComparison.Ordinal);
        Assert.Contains("moonIconRetry.Failed(", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("until the plugin reloads", draw, StringComparison.Ordinal);

        var icon = Member(Ui("MoonIconWindow.cs"), "private void DrawIcon(");
        Assert.Contains("cardRetry.Ready(", icon, StringComparison.Ordinal);
        Assert.Contains("particleRetry.Ready(", icon, StringComparison.Ordinal);
    }

    [Fact]
    public void The_122_settings_and_notes_build_no_strings_per_frame()
    {
        var updates = Member(Ui("ConfigWindow.Updates.cs"), "private void DrawUpdates()");
        Assert.DoesNotContain("UpdateNotes.Plain(", updates, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Format(", updates, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\+ ""##"), updates);

        var umbra = Member(Ui("ConfigWindow.Umbra.cs"), "private void DrawUmbraAbout()");
        Assert.DoesNotContain("string.Format(", umbra, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\+ ""##"), umbra);

        Assert.DoesNotContain("+ \"\\n\\n\" +", Member(Ui("MainWindow.Update.cs"), "private float DrawUpdateNote("), StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\+ ""##"), Member(Ui("WhatsNewPopup.cs"), "private void DrawFooter("));
    }

    [Fact]
    public void The_assumed_height_slider_saves_on_release()
    {
        var umbra = Member(Ui("ConfigWindow.Umbra.cs"), "private void DrawUmbraAbout()");
        var slider = umbra.IndexOf("ImGui.SliderInt(\"##umbraAssumed\"", StringComparison.Ordinal);
        Assert.True(slider >= 0);
        var release = umbra.IndexOf("ImGui.IsItemDeactivatedAfterEdit()", slider, StringComparison.Ordinal);
        Assert.True(release > slider, "the slider must save when it is let go");
        Assert.DoesNotContain("Save();", umbra[slider..release], StringComparison.Ordinal);
    }
}
