using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// The Settings window's frame (feature plan v6 U7, the 1.13.0 rebuild): a full-width search box, a section index on
/// the left with the version and "Help &amp; tour" at its foot (wrapped section tabs above the page when the window is
/// narrow), and the chosen page on a Night panel: its title, a one-line intro, then its blocks, each a heading over a
/// card of settings. Typing in the search box shows every matching setting across the pages instead, under each
/// page's title and its block's heading, with an empty state when nothing matches. The page open last is remembered
/// (<see cref="Config.Configuration.SettingsPage"/>).
///
/// <para><b>One row design for every setting</b>: the label (Body, primary tone), a short plain hint
/// under it (Caption, secondary tone, wrapped in the font in use, never clipped), the control in its own
/// column on the right (under the hint when the page is narrower than <see cref="RowWideLogical"/>), 10 px above and
/// below, and a hairline between rows. Write a row with <see cref="Toggle"/>, <see cref="Choice"/>,
/// <see cref="ButtonRow"/> or, for any other control, <c>if (Setting(label, hint)) { …control…; EndSetting(); }</c>
/// (<see cref="SettingBelow"/> moves under the row for a table or a status line). Labels and hints keep the copy rules
/// (<see cref="SettingsCopy"/>; a test holds every <c>Strings</c> pair passed to these helpers to them).</para>
///
/// <para><b>Adding a block</b>: write <c>private void DrawSomething()</c> in its own partial file that starts with
/// <c>Header(Strings.SomethingHeading)</c> and draws its rows, then add one line to <see cref="BuildBlocks"/>, such as
/// <c>new(SettingsSection.InGame, DrawSomething),</c>. The rows' card opens with the first row that shows and closes
/// after the block (or at the next <see cref="Header"/>). A block other windows open Settings on takes an anchor, e.g.
/// <c>anchor: SettingsAnchor.Nearby</c>, for <see cref="OpenAt(SettingsSection, SettingsAnchor)"/>. The older
/// <see cref="Row"/> still registers a free-form setting with the search and opens the card.</para>
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Logical minimum size of the window, scaled by the UI scale each frame.</summary>
    private const float MinWidthLogical = 400f;
    private const float MinHeightLogical = 320f;

    /// <summary>The section index's width, and the least body width beside it before the index turns into tabs.</summary>
    private const float IndexWidthLogical = 160f;
    private const float IndexBodyMinLogical = 420f;
    private const float PanelRounding = 6f;
    private const float IndexBarWidth = 3f;
    private const int SearchMaxLength = 128;

    // The row design (feature plan v6 U7).
    private const float RowPadLogical = 10f;
    private const float RowWideLogical = 440f;
    private const float ControlLogical = 200f;
    private const float GutterLogical = 24f;
    private const float HintGapLogical = 3f;
    private const float SubIndentLogical = 28f;
    private const float CardPadXLogical = 14f;
    private const float CardPadYLogical = 4f;
    private const float CardGapLogical = 20f;
    private const float PageMarginLogical = 16f;

    /// <summary>
    /// The most lines a label or hint wraps to before it ends in an ellipsis (the whole hint then on hover). The copy
    /// rules keep a hint to two lines at the page's usual width; a narrow page or a large text size wraps it further,
    /// measured in the font in use, rather than cutting it.
    /// </summary>
    private const int MaxRowLines = 6;

    /// <summary>The window's size at first use, and once after the 1.13 rebuild: room for the index and wide rows.</summary>
    private static readonly Vector2 DefaultSizeLogical = new(880f, 720f);

    // The index bar's motion key ("IDX").
    private const uint IndexBarTag = 0x0049_4458;

    /// <summary>What the Settings window turns off while the portrait pack's confirmation is up.</summary>
    private const ImGuiWindowFlags PackBlockedWindow = ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;

    private readonly SettingsFilter filter = new();
    private readonly SettingsBlock[] blocks;
    private readonly SaveDebounce pendingSave = new();
    private Theme.StyleScope nightChrome;
    private SettingsSection section = SettingsSection.General;
    private string searchText = string.Empty;
    private bool scrollToTop;
    private SettingsAnchor anchor;
    private bool focusSearch;

    // The index's match counts as text, per row.
    private readonly string?[] indexCounts = new string?[SettingsSections.Count];
    private readonly int[] indexCountValues = new int[SettingsSections.Count];
    private bool headingDrawn;

    // Narrow tabs' labels with their ImGui ids, rebuilt when the language changes.
    private string[] tabLabels = [];
    private int tabLabelsLanguage = -1;

    // The card the rows draw on, painted behind them through a splitter of its own (so a block may split its draw list
    // too, as the Decoration previews do), and the body panel's rectangle the card may paint into.
    private ImDrawListSplitterPtr cardSplitter;
    private bool cardOpen;
    private bool nextCardDanger;
    private bool cardDanger;
    private float cardTop;
    private float cardLeft;
    private float cardRight;
    private int cardRows;
    private Vector2 bodyMin;
    private Vector2 bodyMax;

    // The row being drawn (between Setting and EndSetting).
    private RowLayout row;

    // Set until the window takes its new size after the 1.13 rebuild (see PreDraw).
    private bool widenOnce;

    // Where the window stood when it last drew (null before its first draw this session), and the frames it last went
    // through PreDraw and Draw: a frame with the one and not the other was drawn collapsed.
    private Vector2? drawnPos;
    private int preDrawnFrame = -10;
    private int drawnFrame = -10;

    /// <summary>
    /// The blocks of settings in the order they draw: grouped by section in <see cref="SettingsSections.Order"/>, and
    /// in table order within a section. To add a block, add one line here (see the class remarks).
    /// </summary>
    private SettingsBlock[] BuildBlocks() =>
    [
        new(SettingsSection.General, DrawSize),
        new(SettingsSection.General, DrawLook),
        new(SettingsSection.General, DrawMainWindow),
        new(SettingsSection.General, DrawHelp),
        new(SettingsSection.Themes, DrawThemeCards, ThemeCardsKeywords),
        new(SettingsSection.Themes, DrawThemePreview),
        new(SettingsSection.Themes, DrawThemeColours, ThemeColoursKeywords),
        new(SettingsSection.Themes, DrawThemeMix, ThemeMixKeywords),
        new(SettingsSection.Themes, DrawThemeShare, ThemeShareKeywords, SettingsAnchor.ThemeShare),
        new(SettingsSection.Themes, DrawThemeReset),
        new(SettingsSection.Journal, DrawJournalTable),
        new(SettingsSection.Journal, DrawJournal),
        new(SettingsSection.Journal, DrawJournalText),
        new(SettingsSection.Journal, DrawCollectorSettings),
        new(SettingsSection.TodoOverlay, DrawTodoOverlay),
        new(SettingsSection.TodoOverlay, DrawTodoSections),
        new(SettingsSection.TodoOverlay, DrawRoutes),
        new(SettingsSection.Alerts, DrawNotices),
        new(SettingsSection.Alerts, DrawChatActions),
        new(SettingsSection.Alerts, DrawNeedsYou),
        new(SettingsSection.Alerts, DrawWelcomeBack),
        new(SettingsSection.Spoilers, DrawSpoilers),
        new(SettingsSection.InGame, DrawGamePanels),
        new(SettingsSection.InGame, DrawMenusAndTooltips),
        new(SettingsSection.InGame, DrawNearbySettings, anchor: SettingsAnchor.Nearby),
        new(SettingsSection.InGame, DrawWotsit),
        new(SettingsSection.Automation, DrawAutomationLevel, AutomationKeywords, SettingsAnchor.AutomationLevel),
        new(SettingsSection.Automation, DrawCompanionPlugins, anchor: SettingsAnchor.CompanionPlugins),
        new(SettingsSection.Automation, DrawTravelPreflight),
        new(SettingsSection.Automation, DrawTravelSettings),
        new(SettingsSection.Automation, DrawQuestionableSettings),
        new(SettingsSection.Automation, DrawAutoDutySettings),
        new(SettingsSection.Automation, DrawHandInIntegrations),
        new(SettingsSection.Characters, DrawCharacters),
        new(SettingsSection.Characters, DrawDashboard),
        new(SettingsSection.Characters, DrawData),
        new(SettingsSection.Characters, DrawDangerZone),
        new(SettingsSection.Advanced, DrawKeyboard),
        new(SettingsSection.Advanced, DrawCommandAliases),
        new(SettingsSection.Advanced, DrawSafety),
        new(SettingsSection.Advanced, DrawPolling),
        new(SettingsSection.Advanced, DrawJournalFiling),
        new(SettingsSection.Advanced, DrawHookGate),
        new(SettingsSection.Advanced, DrawQuestionableStopConfirm),
        new(SettingsSection.Advanced, DrawDiagnostics),
        new(SettingsSection.Advanced, DrawPrivacy),
    ];

    /// <summary>The section's title: the index entry, the page title and the search results' heading.</summary>
    public static string SectionTitle(SettingsSection settingsSection) => settingsSection switch
    {
        SettingsSection.Journal => Strings.SettingsPageJournal,
        SettingsSection.TodoOverlay => Strings.SettingsPageOverlay,
        SettingsSection.Alerts => Strings.SettingsPageAlerts,
        SettingsSection.Spoilers => Strings.SettingsSpoilers,
        SettingsSection.InGame => Strings.SettingsPageInGame,
        SettingsSection.Automation => Strings.SettingsPageAutomation,
        SettingsSection.Characters => Strings.SettingsPageCharacters,
        SettingsSection.Advanced => Strings.ConfigSectionAdvanced,
        SettingsSection.Themes => Strings.SettingsPageThemes,
        _ => Strings.SettingsPageGeneral,
    };

    /// <summary>The one line under a page's title saying what the page is for.</summary>
    private static string SectionIntro(SettingsSection settingsSection) => settingsSection switch
    {
        SettingsSection.Journal => Strings.SettingsIntroJournal,
        SettingsSection.TodoOverlay => Strings.SettingsIntroOverlay,
        SettingsSection.Alerts => Strings.SettingsIntroAlerts,
        SettingsSection.Spoilers => Strings.SettingsIntroSpoilers,
        SettingsSection.InGame => Strings.SettingsIntroInGame,
        SettingsSection.Automation => Strings.SettingsIntroAutomation,
        SettingsSection.Characters => Strings.SettingsIntroCharacters,
        SettingsSection.Advanced => Strings.SettingsIntroAdvanced,
        SettingsSection.Themes => Strings.SettingsIntroThemes,
        _ => Strings.SettingsIntroGeneral,
    };

    /// <summary>
    /// Opens the window on <paramref name="settingsSection"/> with the search cleared, and brings it to the front. With
    /// an <paramref name="settingsAnchor"/> in that section the page is scrolled so the block's heading is at the top;
    /// otherwise the page starts at its top.
    /// </summary>
    public void OpenAt(SettingsSection settingsSection, SettingsAnchor settingsAnchor = SettingsAnchor.None)
    {
        Select(settingsSection);
        anchor = settingsAnchor;
        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw()
    {
        // Collapsed last frame, the window did not draw, so a change waiting to save would wait until it is expanded
        // again (or be lost at unload): it is saved now.
        var frame = ImGui.GetFrameCount();
        if (preDrawnFrame == frame - 1 && drawnFrame != frame - 1 && pendingSave.Flush())
        {
            Save();
        }

        preDrawnFrame = frame;

        // Opened for the first time since the rebuild (no page remembered yet): the window takes the new size once, so
        // rows that were laid out for the old 680 px window get their control column; afterwards it is the player's. It
        // waits for one draw to know where the window stands, and never reaches past the screen's work area from there.
        var minimum = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.UiScale;
        if (widenOnce && drawnPos is { } pos)
        {
            widenOnce = false;
            var viewport = ImGuiHelpers.MainViewport;
            Size = Vector2.Max(ScaleMetrics.FitFromPosition(DefaultSizeLogical, ImGuiHelpers.GlobalScale, pos, viewport.WorkPos, viewport.WorkSize), minimum);
            SizeCondition = ImGuiCond.Always;
            settings.SettingsPage = SettingsSections.Name(section);
            SaveSoon();
        }
        else if (SizeCondition == ImGuiCond.Always)
        {
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        // The window is its own top level, so its minimum follows the UI scale like Nearby's.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = minimum,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        // While the portrait pack's confirmation is up, Settings takes no input (spec-1.20 F4): BeginDisabled (in Draw)
        // stops its items, and these stop what it does not: the mouse wheel, collapsing, and the title bar's buttons
        // (close, pin, click-through). Esc belongs to the dialog.
        var blocked = packDialogOpen;
        Flags = blocked ? Flags | PackBlockedWindow : Flags & ~PackBlockedWindow;
        ShowCloseButton = !blocked;
        AllowPinning = !blocked;
        AllowClickthrough = !blocked;
        RespectCloseHotkey = !blocked;

        CaptureHostStyle();
        nightChrome = Theme.PushNightWindow();
    }

    /// <summary>The mouse wheel off for Settings' index and page while the portrait pack's confirmation is up.</summary>
    private ImGuiWindowFlags PackBlockedScroll => packDialogOpen ? ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None;

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        drawnFrame = ImGui.GetFrameCount();
        drawnPos = ImGui.GetWindowPos();

        // The window scales itself (its two children inherit it); the scale is reset before Begin lays the title bar
        // out again.
        UiMetrics.ApplyFontScale();

        // While the portrait pack's confirmation is up, Settings takes no input; only the dialog's own scrim dims it
        // (spec-1.20 F4), so the disabled alpha stays 1.
        var blocked = packDialogOpen;
        if (blocked)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);
            ImGui.BeginDisabled();
        }

        try
        {
            DrawFrame();
        }
        finally
        {
            if (blocked)
            {
                ImGui.EndDisabled();
                ImGui.PopStyleVar();
            }

            ImGui.SetWindowFontScale(1f);
        }

        if (packDialogOpen)
        {
            DrawPackDialog(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
        }

        // Live settings (the scales, the sliders) save once they have been still a moment and nothing is held.
        if (pendingSave.Due(ImGui.GetTime(), ImGui.IsAnyItemActive()))
        {
            Save();
        }
    }

    /// <summary>Notes a change that already applies; it is saved once the value has been still a moment (<see cref="SaveDebounce"/>).</summary>
    private void SaveSoon() => pendingSave.Changed(ImGui.GetTime());

    private void DrawFrame()
    {
        DrawSearchBar();
        ImGui.Spacing();
        var wide = ImGui.GetContentRegionAvail().X >= UiMetrics.Px(IndexWidthLogical + IndexBodyMinLogical);
        if (wide)
        {
            using (var index = ImRaii.Child("##settingsIndex", new Vector2(UiMetrics.Px(IndexWidthLogical), -1f), false, PackBlockedScroll))
            {
                if (index)
                {
                    DrawIndex();
                    DrawIndexFooter();
                }
            }

            ImGui.SameLine();
        }
        else
        {
            DrawTabs();
            ImGui.Spacing();
        }

        using var colors = Theme.PushNightPanel();
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, UiMetrics.Px(PanelRounding));

        // The cards reach CardPadX past the text on each side, inside the page margin.
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(PageMarginLogical + CardPadXLogical), UiMetrics.Px(16f)));
        using var body = ImRaii.Child("##settingsBody", new Vector2(-1f, -1f), true, PackBlockedScroll);
        if (!body)
        {
            return;
        }

        bodyMin = ImGui.GetWindowPos();
        bodyMax = bodyMin + ImGui.GetWindowSize();
        if (scrollToTop)
        {
            scrollToTop = false;
            ImGui.SetScrollY(0f);
        }

        try
        {
            DrawBody();
        }
        finally
        {
            // A card left open by a block that threw is closed so the draw list's channels merge.
            CloseCard();
        }
    }

    /// <summary>The search box, across the whole window.</summary>
    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(MathF.Max(UiMetrics.Px(120f), ImGui.GetContentRegionAvail().X));
        if (focusSearch)
        {
            focusSearch = false;
            ImGui.SetKeyboardFocusHere();
        }

        if (ImGui.InputTextWithHint("##settingsSearch", Strings.SettingsSearchHint, ref searchText, SearchMaxLength) && filter.SetText(searchText))
        {
            scrollToTop = true;
        }

        HintOnHover(Strings.SettingsSearchTooltip);
    }

    /// <summary>
    /// The section index: one row per page, with the Moon bar gliding to the chosen one. While searching no row is
    /// chosen; each shows how many settings matched in it and the ones without a match are dimmed.
    /// </summary>
    private void DrawIndex()
    {
        var order = SettingsSections.Order;
        var rowHeight = MathF.Max(UiMetrics.MinTarget, ImGui.GetFrameHeight() * 1.3f);
        var searching = filter.Active;
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var chosenIndex = -1;
        for (var i = 0; i < order.Count; i++)
        {
            var s = order[i];
            var chosen = !searching && s == section;
            var matches = searching ? filter.Shown(s) : 0;
            var min = ImGui.GetCursorScreenPos();
            using var id = ImRaii.PushId(i);
            if (ImGui.Selectable("##section", chosen, ImGuiSelectableFlags.None, new Vector2(0f, rowHeight)))
            {
                Select(s);
            }

            var max = ImGui.GetItemRectMax();
            if (chosen)
            {
                chosenIndex = i;
            }

            var tone = chosen ? Theme.Surface.Text : searching && matches == 0 ? Theme.Surface.TextDisabled : Theme.Surface.TextSecondary;
            var textY = min.Y + ((rowHeight - ImGui.GetTextLineHeight()) * 0.5f);
            var left = min.X + UiMetrics.Px(14f);
            var right = max.X - UiMetrics.Px(6f);
            if (searching && matches > 0)
            {
                var count = IndexCount(i, matches);
                var countWidth = ImGui.CalcTextSize(count).X;
                dl.AddText(new Vector2(right - countWidth, textY), Theme.U32(Theme.Surface.TextSecondary), count);
                right -= countWidth + UiMetrics.Px(6f);
            }

            Chrome.EllipsisTextAt(dl, new Vector2(left, textY), MathF.Max(0f, right - left), SectionTitle(s), Theme.U32(tone));
        }

        if (chosenIndex < 0)
        {
            return;
        }

        // The Moon bar glides to the chosen page (at once under Reduce motion).
        var step = rowHeight + ImGui.GetStyle().ItemSpacing.Y;
        var at = Motion.Lerp(Motion.Key(IndexBarTag, 0), chosenIndex, MotionMath.SelectRate);
        var inset = UiMetrics.Px(6f);
        var bar = UiMetrics.Px(IndexBarWidth);
        var top = origin.Y + (at * step);
        dl.AddRectFilled(new Vector2(origin.X, top + inset), new Vector2(origin.X + bar, top + rowHeight - inset), Theme.GoldU32, bar * 0.5f);
    }

    /// <summary>The index's foot: the plugin's version (the data stamp on hover) and "Help &amp; tour", About folded in.</summary>
    private void DrawIndexFooter()
    {
        var style = ImGui.GetStyle();
        var helpHeight = ShowHelp is null ? 0f : ImGui.GetFrameHeight() + style.ItemSpacing.Y;
        var footer = ImGui.GetTextLineHeight() + helpHeight + UiMetrics.Px(10f) + style.ItemSpacing.Y;
        var y = ImGui.GetWindowHeight() - style.WindowPadding.Y - footer;
        if (y > ImGui.GetCursorPosY())
        {
            ImGui.SetCursorPosY(y);
        }

        Chrome.Hairline();
        ImGui.Dummy(new Vector2(0f, UiMetrics.Px(4f)));
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(pluginVersionLine.Value);
        }

        HintOnHover(dataStampLine);
        if (ShowHelp is not { } showHelp)
        {
            return;
        }

        if (ImGui.Button(Strings.SettingsHelpAndTour))
        {
            showHelp();
        }

        HintOnHover(Strings.SettingsHelpAndTourTooltip);
    }

    /// <summary>The index row's match count as text, cached per row until the count changes.</summary>
    private string IndexCount(int index, int count)
    {
        if (indexCountValues[index] != count || indexCounts[index] is null)
        {
            indexCountValues[index] = count;
            indexCounts[index] = count.ToString(CultureInfo.CurrentCulture);
        }

        return indexCounts[index]!;
    }

    /// <summary>The narrow window's section tabs: each section's title as a tab, wrapped onto as many lines as it needs.</summary>
    private void DrawTabs()
    {
        var order = SettingsSections.Order;
        if (tabLabelsLanguage != Loc.Version || tabLabels.Length != order.Count)
        {
            tabLabelsLanguage = Loc.Version;
            tabLabels = new string[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                tabLabels[i] = SectionTitle(order[i]) + "##settingsTab" + i.ToString(CultureInfo.InvariantCulture);
            }
        }

        var searching = filter.Active;
        var padding = ImGui.GetStyle().FramePadding;
        var dl = ImGui.GetWindowDrawList();
        for (var i = 0; i < order.Count; i++)
        {
            var s = order[i];
            var chosen = !searching && s == section;
            var title = SectionTitle(s);
            var size = ImGui.CalcTextSize(title) + (padding * 2f);
            if (i > 0)
            {
                Chrome.SameLineOrWrap(size.X);
            }

            using (Theme.PushText(chosen ? Theme.Surface.Text : searching && filter.Shown(s) == 0 ? Theme.Surface.TextDisabled : Theme.Surface.TextSecondary))
            {
                if (ImGui.Selectable(tabLabels[i], chosen, ImGuiSelectableFlags.None, size with { Y = 0f }))
                {
                    Select(s);
                }
            }

            if (chosen)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var bar = MathF.Max(1f, UiMetrics.Px(2f));
                dl.AddRectFilled(new Vector2(min.X, max.Y - bar), new Vector2(max.X, max.Y), Theme.GoldU32);
            }
        }
    }

    /// <summary>The chosen section's page, or every match across the sections while searching.</summary>
    private void DrawBody()
    {
        filter.BeginFrame();
        headingDrawn = false;
        if (!filter.Active)
        {
            filter.BeginSection(section, SectionTitle(section));
            SectionHeading.Title(SectionTitle(section), ImGui.GetContentRegionAvail().X);
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(SectionIntro(section));
            }

            ImGui.Dummy(new Vector2(0f, UiMetrics.Px(8f)));
            DrawBlocks(section);

            // An anchor outside the page (or on a block that did not draw) is dropped rather than kept for later.
            anchor = SettingsAnchor.None;
        }
        else
        {
            foreach (var s in SettingsSections.Order)
            {
                filter.BeginSection(s, SectionTitle(s));
                DrawBlocks(s);
            }

            if (filter.VisibleThisFrame == 0)
            {
                DrawNoMatch();
            }
        }

        filter.EndFrame();
    }

    private void DrawBlocks(SettingsSection settingsSection)
    {
        for (var i = 0; i < blocks.Length; i++)
        {
            var block = blocks[i];
            if (block.Section != settingsSection)
            {
                continue;
            }

            filter.BeginBlock(block.Keywords);
            if (!filter.DrawsBlock(block.RowAware))
            {
                continue;
            }

            if (anchor != SettingsAnchor.None && block.Anchor == anchor && !filter.Active)
            {
                // Opened on this block (OpenAt): its heading lands at the top of the page.
                anchor = SettingsAnchor.None;
                ImGui.SetScrollHereY(0f);
            }

            using (ImRaii.PushId(i))
            {
                block.Draw();
                CloseCard();
                nextCardDanger = false;
            }

            block.RowAware = filter.LearnRowAware(block.RowAware);
        }
    }

    private void DrawNoMatch()
    {
        if (EmptyState.DrawWithAction(Strings.SettingsNoMatchHeading, Strings.SettingsNoMatchBody, Strings.SettingsClearSearch, moon: QuestState.Blocked) == EmptyState.ActionClicked)
        {
            ClearSearch();
            focusSearch = true;
        }
    }

    /// <summary>Shows <paramref name="settingsSection"/>'s page: the search is cleared, the page starts at its top and is remembered.</summary>
    private void Select(SettingsSection settingsSection)
    {
        section = settingsSection;
        anchor = SettingsAnchor.None;
        ClearSearch();
        scrollToTop = true;
        var name = SettingsSections.Name(settingsSection);
        if (!string.Equals(settings.SettingsPage, name, StringComparison.Ordinal))
        {
            settings.SettingsPage = name;
            SaveSoon();
        }
    }

    private void ClearSearch()
    {
        searchText = string.Empty;
        filter.SetText(string.Empty);
        scrollToTop = true;
    }

    /// <summary>
    /// Starts a block of settings under <paramref name="title"/> (a Moon Road heading over the block's card). On a
    /// section's page the heading draws at once, except one equal to the section's title, which the page title already
    /// says; while searching it waits for the block's first matching setting, or draws at once when the title itself
    /// matches and the whole block shows. A heading in the middle of a block closes the card above it.
    /// </summary>
    private void Header(string title)
    {
        if (filter.Heading(title))
        {
            DrawPendingHeadings();
        }
    }

    /// <summary>The next card drawn in this block is the Danger zone: its border in the Eclipse tone.</summary>
    private void DangerCard() => nextCardDanger = true;

    /// <summary>
    /// One free-form setting, for the search box: true when it shows (always on a section's page; while searching when
    /// <paramref name="label"/>, <paramref name="hint"/> or the English <paramref name="keywords"/> hold every word
    /// typed, or its block or section matched whole). Draws the waiting section title and block heading before the
    /// first setting that shows, and opens the block's card. Rows with a label and a control use <see cref="Setting"/>.
    /// </summary>
    private bool Row(string label, string? hint = null, string? keywords = null)
    {
        if (!filter.Row(label, hint, keywords))
        {
            return false;
        }

        DrawPendingHeadings();
        OpenCard();
        if (cardRows++ > 0)
        {
            RowDivider(ImGui.GetCursorScreenPos().Y);
        }

        ImGui.Dummy(new Vector2(0f, MathF.Max(0f, UiMetrics.Px(RowPadLogical) - ImGui.GetStyle().ItemSpacing.Y)));
        return true;
    }

    /// <summary>
    /// A setting drawn without the block's card, for a surface of its own (the Themes page's cards and preview): it
    /// registers with the search as <see cref="Row"/> does and draws the waiting headings, closes any open card, and
    /// leaves the cursor at the row's left. Returns false, drawing nothing, when the search hides it. End it with
    /// <see cref="EndBareRow"/>.
    /// </summary>
    private bool BareRow(string label, string? hint = null, string? keywords = null)
    {
        if (!filter.Row(label, hint, keywords))
        {
            return false;
        }

        DrawPendingHeadings();
        CloseCard();
        return true;
    }

    /// <summary>The gap a card leaves before whatever follows, after a <see cref="BareRow"/>.</summary>
    private static void EndBareRow() =>
        ImGui.Dummy(new Vector2(0f, MathF.Max(1f, UiMetrics.Px(CardGapLogical) - ImGui.GetStyle().ItemSpacing.Y)));

    /// <summary>
    /// A sub-setting's indent, greyed out while <paramref name="parentOn"/> is false, for a free-form <see cref="Row"/>.
    /// Rows drawn with <see cref="Setting"/> say <c>sub: true</c> instead.
    /// </summary>
    private static SubSettingScope SubSetting(bool parentOn) => new(ImRaii.PushIndent(), ImRaii.Disabled(!parentOn));

    /// <summary>
    /// Starts one setting row (see the class remarks): registers it with the search, draws the waiting headings and
    /// opens the card, then lays out the label and the hint on the left and leaves the cursor where the control goes:
    /// in the control column, vertically centred on the label's first line, or under the hint on a narrow page. Draw the
    /// control (sized <see cref="ControlWidth"/>), then call <see cref="EndSetting"/>. Returns false, drawing nothing,
    /// when the search hides the row.
    /// </summary>
    /// <param name="label">The label; also what the search reads. At most 40 characters.</param>
    /// <param name="hint">One plain line under the label; at most 110 characters. Null for none.</param>
    /// <param name="keywords">English words that find the row besides its label and hint.</param>
    /// <param name="controlWidth">The control's width in pixels; negative for the standard 200 logical px, 0 for none.</param>
    /// <param name="controlHeight">The control's height, to centre it on the label line; 0 for a frame's height.</param>
    /// <param name="enabled">False dims the row and disables its control; <paramref name="reason"/> says why on the hint line.</param>
    /// <param name="sub">A sub-setting: indented under its parent with a guide line.</param>
    /// <param name="clickable">The label column acts as the control (a toggle): <see cref="RowLayout.LabelClicked"/>.</param>
    /// <param name="shown">Text drawn in place of <paramref name="label"/> (a label with a count in it).</param>
    /// <param name="reason">Shown in place of the hint while the row is disabled.</param>
    private bool Setting(string label, string? hint, string? keywords = null, float controlWidth = -1f, float controlHeight = 0f, bool enabled = true, bool sub = false, bool clickable = false, string? shown = null, string? reason = null)
    {
        if (!filter.Row(label, hint, keywords))
        {
            return false;
        }

        DrawPendingHeadings();
        OpenCard();

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        if (cardRows++ > 0)
        {
            RowDivider(start.Y);
        }

        var left = start.X + (sub ? UiMetrics.Px(SubIndentLogical) : 0f);
        var right = start.X + avail;
        var width = MathF.Max(1f, right - left);
        var wide = avail >= UiMetrics.Px(RowWideLogical);
        var control = controlWidth < 0f ? UiMetrics.Px(ControlLogical) : controlWidth;
        control = MathF.Min(control, wide ? MathF.Max(UiMetrics.Px(120f), width * 0.6f) : width);
        var gutter = UiMetrics.Px(GutterLogical);
        var labelWidth = wide && control > 0f ? MathF.Max(UiMetrics.Px(80f), width - control - gutter) : width;
        var pad = UiMetrics.Px(RowPadLogical);
        var top = start.Y + pad;

        // Measured first, in the font in use, so a clickable label covers the whole label column and nothing clips.
        var text = shown ?? label;
        var line = ImGui.GetTextLineHeight();
        var labelHeight = MathF.Min(TextFlow.Height(text, labelWidth), MaxRowLines * line);
        var note = enabled ? hint : reason ?? hint;
        var hintHeight = 0f;
        if (!string.IsNullOrEmpty(note))
        {
            using (Typography.Caption())
            {
                hintHeight = MathF.Min(TextFlow.Height(note, labelWidth), MaxRowLines * ImGui.GetTextLineHeight());
            }
        }

        var hintTop = top + labelHeight + UiMetrics.Px(HintGapLogical);
        var labelBottom = hintHeight > 0f ? hintTop + hintHeight : top + labelHeight;

        ImGui.PushID(label);
        var labelClicked = false;
        if (clickable && enabled)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, start.Y));
            // Down to the row's foot beside a control column; on a narrow page only to the hint, above the control.
            var clickBottom = wide ? labelBottom + pad : labelBottom;
            labelClicked = ImGui.InvisibleButton("##rowLabel", new Vector2(labelWidth, MathF.Max(1f, clickBottom - start.Y)));
        }

        TextFlow.DrawClamped(dl, new Vector2(left, top), text, labelWidth, MaxRowLines, Theme.U32(enabled ? s.Text : s.TextDisabled));
        var hintCut = false;
        if (hintHeight > 0f)
        {
            using (Typography.Caption())
            {
                hintCut = TextFlow.DrawClamped(dl, new Vector2(left, hintTop), note!, labelWidth, MaxRowLines, Theme.U32(enabled ? s.TextSecondary : s.TextTertiary));
            }
        }

        var height = controlHeight > 0f ? controlHeight : ImGui.GetFrameHeight();
        var controlPos = wide
            ? new Vector2(right - control, top + ((line - height) * 0.5f))
            : new Vector2(left, labelBottom + UiMetrics.Px(6f));
        ImGui.SetCursorScreenPos(controlPos);
        if (!enabled)
        {
            ImGui.BeginDisabled();
        }

        row = new RowLayout(start, start.X, left, right, labelWidth, labelBottom, control, enabled, sub, hintCut ? note : null, labelClicked);
        return true;
    }

    /// <summary>The control column's width for the row being drawn (pass to <c>SetNextItemWidth</c>).</summary>
    private float ControlWidth => row.Control;

    /// <summary>Moves under the row's label, hint and control, at the row's left and full width: for a table, a list or a status line.</summary>
    private void SettingBelow()
    {
        var y = MathF.Max(row.LabelBottom + UiMetrics.Px(8f), ImGui.GetCursorScreenPos().Y);
        ImGui.SetCursorScreenPos(new Vector2(row.Left, y));
    }

    /// <summary>A status line under the row, in the caption role and <paramref name="color"/> (the secondary tone by default), wrapped.</summary>
    private void SettingNote(string text, Vector4? color = null)
    {
        SettingBelow();
        using (Typography.Caption())
        using (Theme.PushText(color ?? Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(text);
        }
    }

    /// <summary>Ends the row <see cref="Setting"/> started: the hint in full on hover when it was cut, the sub-setting guide, and the row's bottom padding.</summary>
    private void EndSetting()
    {
        if (!row.Enabled)
        {
            ImGui.EndDisabled();
        }

        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var pad = UiMetrics.Px(RowPadLogical);
        var contentBottom = MathF.Max(row.LabelBottom, ImGui.GetCursorScreenPos().Y - spacing);
        var bottom = contentBottom + pad;
        if (row.Sub)
        {
            var x = row.ContentLeft + MathF.Round(UiMetrics.Px(SubIndentLogical) * 0.4f);
            ImGui.GetWindowDrawList().AddLine(new Vector2(x, row.Start.Y + (pad * 0.5f)), new Vector2(x, bottom - (pad * 0.5f)), Theme.WithAlpha(Theme.Surface.Line, 0.8f), UiMetrics.Hairline);
        }

        if (row.CutHint is { } hint && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(row.Left, row.Start.Y), new Vector2(row.Left + row.LabelWidth, bottom)))
        {
            UiMetrics.Tooltip(hint);
        }

        ImGui.PopID();
        ImGui.SetCursorScreenPos(new Vector2(row.ContentLeft, row.Start.Y));
        ImGui.Dummy(new Vector2(MathF.Max(1f, row.Right - row.ContentLeft), MathF.Max(1f, bottom - row.Start.Y - spacing)));
    }

    /// <summary>
    /// A setting with a moon toggle (feature plan v6 decision 7); a click anywhere on the label flips it too. Returns
    /// true on the frame <paramref name="value"/> changed (already written); the caller saves.
    /// </summary>
    private bool Toggle(string label, string? hint, ref bool value, string? keywords = null, bool enabled = true, bool sub = false, string? reason = null)
    {
        if (!ToggleSetting(label, hint, keywords, enabled, sub, reason))
        {
            return false;
        }

        var changed = RowToggle(ref value);
        EndSetting();
        return changed;
    }

    /// <summary>
    /// The moon toggle of a row <see cref="Setting"/> started with <c>clickable: true</c> (for a toggle with more under
    /// it): a click on the toggle or on the label flips <paramref name="value"/>. Returns true when it flipped.
    /// </summary>
    private bool RowToggle(ref bool value)
    {
        var changed = Chrome.MoonToggle("##toggle", ref value);
        if (!changed && row.LabelClicked)
        {
            value = !value;
            changed = true;
        }

        return changed;
    }

    /// <summary>A row for a toggle with more under it: <see cref="Setting"/> sized and clickable for <see cref="RowToggle"/>.</summary>
    private bool ToggleSetting(string label, string? hint, string? keywords = null, bool enabled = true, bool sub = false, string? reason = null)
    {
        var size = Chrome.ToggleSize;
        return Setting(label, hint, keywords, size.X, size.Y, enabled, sub, clickable: true, reason: reason);
    }

    /// <summary>
    /// A setting with a segmented picker of <paramref name="options"/> (feature plan v6 decision 7) in place of a
    /// sentence of radio buttons. Returns true on the frame <paramref name="selected"/> changed.
    /// </summary>
    private bool Choice(string label, string? hint, ref int selected, string[] options, string? keywords = null, bool enabled = true, bool sub = false, string? reason = null)
    {
        if (!Setting(label, hint, keywords, Chrome.SegmentedWidth(options), UiMetrics.MinTarget, enabled, sub, reason: reason))
        {
            return false;
        }

        var changed = Chrome.Segmented("##choice", ref selected, options, ControlWidth);
        EndSetting();
        return changed;
    }

    /// <summary>A setting whose control is one button, right-aligned in the control column. Returns true when it was pressed.</summary>
    private bool ButtonRow(string label, string? hint, string button, string? keywords = null, bool enabled = true, bool sub = false, string? reason = null)
    {
        var width = ImGui.CalcTextSize(button, true).X + (ImGui.GetStyle().FramePadding.X * 2f);
        if (!Setting(label, hint, keywords, width, 0f, enabled, sub, reason: reason))
        {
            return false;
        }

        var pressed = ImGui.Button(button);
        EndSetting();
        return pressed;
    }

    /// <summary>A setting that only says something (no control): the label and its hint.</summary>
    private void Note(string label, string? hint, string? keywords = null)
    {
        if (Setting(label, hint, keywords, 0f))
        {
            EndSetting();
        }
    }

    /// <summary>The hairline between two rows of a card.</summary>
    private void RowDivider(float y)
    {
        ImGui.GetWindowDrawList().AddLine(new Vector2(cardLeft, y), new Vector2(cardRight, y), Theme.WithAlpha(Theme.Surface.Line, 0.5f), UiMetrics.Hairline);
    }

    /// <summary>Opens the block's card at the cursor (nothing when one is open): its surface is painted when it closes.</summary>
    private void OpenCard()
    {
        if (cardOpen)
        {
            return;
        }

        if (cardSplitter.IsNull)
        {
            cardSplitter = ImGui.ImDrawListSplitter();
        }

        var dl = ImGui.GetWindowDrawList();
        cardSplitter.Split(dl, 2);
        cardSplitter.SetCurrentChannel(dl, 1);
        var start = ImGui.GetCursorScreenPos();
        cardOpen = true;
        cardDanger = nextCardDanger;
        nextCardDanger = false;
        cardTop = start.Y;
        cardLeft = start.X;
        cardRight = start.X + MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        cardRows = 0;
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + UiMetrics.Px(CardPadYLogical)));
    }

    /// <summary>
    /// Closes the open card: paints its surface (a raised wash, 6 px corners, a hairline border; the Danger zone's in the
    /// Eclipse tone) behind its rows, reaching <see cref="CardPadXLogical"/> past the text on both sides, then leaves the
    /// gap before whatever follows.
    /// </summary>
    private void CloseCard()
    {
        if (!cardOpen)
        {
            return;
        }

        cardOpen = false;
        var dl = ImGui.GetWindowDrawList();
        var bottom = ImGui.GetCursorScreenPos().Y + UiMetrics.Px(CardPadYLogical);
        var padX = UiMetrics.Px(CardPadXLogical);
        var min = new Vector2(cardLeft - padX, cardTop);
        var max = new Vector2(cardRight + padX, bottom);
        var s = Theme.Surface;
        var rounding = UiMetrics.Px(PanelRounding);
        cardSplitter.SetCurrentChannel(dl, 0);
        dl.PushClipRect(bodyMin, bodyMax, false);
        dl.AddRectFilled(min, max, Theme.WithAlpha(s.Raised, 0.55f), rounding);
        dl.AddRect(min, max, cardDanger ? Theme.WithAlpha(Theme.Danger, 0.7f) : Theme.WithAlpha(s.Line, 0.8f), rounding, ImDrawFlags.None, cardDanger ? MathF.Max(1f, UiMetrics.Px(1.5f)) : UiMetrics.Hairline);
        dl.PopClipRect();
        cardSplitter.Merge(dl);
        cardDanger = false;
        ImGui.SetCursorScreenPos(new Vector2(cardLeft, bottom));
        ImGui.Dummy(new Vector2(0f, MathF.Max(1f, UiMetrics.Px(CardGapLogical) - ImGui.GetStyle().ItemSpacing.Y)));
    }

    /// <summary>Saves a change still waiting to (the plugin unloads before the window closes) and frees the card splitter.</summary>
    public void Dispose()
    {
        if (pendingSave.Flush())
        {
            Save();
        }

        if (!cardSplitter.IsNull)
        {
            cardSplitter.Destroy();
            cardSplitter = default;
        }
    }

    private void DrawPendingHeadings()
    {
        if (filter.TakeSectionHeading())
        {
            CloseCard();
            if (headingDrawn)
            {
                ImGui.Dummy(new Vector2(0f, UiMetrics.Px(12f)));
            }

            headingDrawn = true;
            SectionHeading.Title(SectionTitle(filter.Section), ImGui.GetContentRegionAvail().X);
            ImGui.Spacing();
        }

        if (filter.TakeBlockHeading() is { } heading)
        {
            CloseCard();
            SectionHeading.Draw(heading);
            ImGui.Dummy(new Vector2(0f, MathF.Max(0f, UiMetrics.Px(8f) - ImGui.GetStyle().ItemSpacing.Y)));
        }
    }

    /// <summary>The indent and the disabled state <see cref="SubSetting"/> opened, closed in reverse order.</summary>
    private readonly struct SubSettingScope(IDisposable indent, IDisposable disabled) : IDisposable
    {
        public void Dispose()
        {
            disabled.Dispose();
            indent.Dispose();
        }
    }

    /// <summary>Where the row being drawn sits: what <see cref="EndSetting"/> and <see cref="SettingBelow"/> need.</summary>
    private readonly record struct RowLayout(
        Vector2 Start,
        float ContentLeft,
        float Left,
        float Right,
        float LabelWidth,
        float LabelBottom,
        float Control,
        bool Enabled,
        bool Sub,
        string? CutHint,
        bool LabelClicked);

    /// <summary>
    /// One entry of the section table: where a block draws, how, the keywords that show it whole, and the anchor
    /// <see cref="OpenAt"/> can scroll its page to.
    /// </summary>
    private sealed class SettingsBlock(SettingsSection section, Action draw, string? keywords = null, SettingsAnchor anchor = SettingsAnchor.None)
    {
        public SettingsSection Section { get; } = section;

        public Action Draw { get; } = draw;

        public string? Keywords { get; } = keywords;

        public SettingsAnchor Anchor { get; } = anchor;

        /// <summary>
        /// Learnt from its draws (<see cref="SettingsFilter.LearnRowAware"/>): true when the block registers its settings
        /// through <see cref="Row"/> or <see cref="Setting"/>, false when it drew without registering any, null until it
        /// first draws. An unknown block draws while searching, so its settings are found before its page was ever opened.
        /// </summary>
        public bool? RowAware { get; set; }
    }
}
