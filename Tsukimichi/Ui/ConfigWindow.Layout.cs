using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// The Settings window's frame (feature plan v5, 1.7.0 "Settings"; R1 proposal 5, R3 proposals 4 and 7): a search box
/// with "Help &amp; tour" beside it, a section index on the left (wrapped section tabs above the page when the window is
/// narrow), and the chosen section's page on a Night panel. Typing in the search box shows every matching setting
/// across all sections instead, each under its section's title and its block's heading, with an empty state when
/// nothing matches. The window wears the Night chrome and follows Settings › Display › Window scale.
///
/// <para><b>Adding a block of settings</b> (one entry in the section table): write <c>private void DrawSomething()</c>
/// in its own partial file (<c>ConfigWindow.Something.cs</c>) that starts with <c>Header(Strings.SomethingHeading)</c>
/// and wraps each setting in <c>if (Row(label, hint)) { … }</c>; <see cref="Row"/> takes the setting's label and hint
/// (and optional English search keywords) so the search box can find it, and draws the waiting headings before the
/// first setting that shows. Then add one line to <see cref="BuildBlocks"/> where the block belongs, such as
/// <c>new(SettingsSection.Integrations, DrawSomething),</c>. A block whose draw never calls <see cref="Row"/> still
/// works on its section's page; while searching it shows (whole) only when the entry's keywords match, e.g.
/// <c>new(SettingsSection.Notices, DrawSomething, "chat line notice")</c>, once its first draw has shown it registers
/// none (every block draws the first time, so a block nobody opened yet is still searched). A block other windows
/// open Settings on takes an anchor, e.g. <c>anchor: SettingsAnchor.Nearby</c>, for
/// <see cref="OpenAt(SettingsSection, SettingsAnchor)"/>.</para>
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Logical minimum size of the window, scaled by the UI scale each frame.</summary>
    private const float MinWidthLogical = 400f;
    private const float MinHeightLogical = 320f;

    /// <summary>The section index's width, and the least body width beside it before the index turns into tabs.</summary>
    private const float IndexWidthLogical = 156f;
    private const float IndexBodyMinLogical = 420f;
    private const float PanelRounding = 6f;
    private const float IndexBarWidth = 3f;
    private const int SearchMaxLength = 128;

    private readonly SettingsFilter filter = new();
    private readonly SettingsBlock[] blocks;
    private Theme.StyleScope nightChrome;
    private SettingsSection section = SettingsSection.Display;
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

    /// <summary>
    /// The blocks of settings in the order they draw: grouped by section in <see cref="SettingsSections.Order"/>, and
    /// in table order within a section. To add a block, add one line here (see the class remarks).
    /// </summary>
    private SettingsBlock[] BuildBlocks() =>
    [
        new(SettingsSection.Display, DrawDisplay),
        new(SettingsSection.Display, DrawLook),
        new(SettingsSection.Display, DrawJournal),
        new(SettingsSection.Display, DrawJournalText),
        new(SettingsSection.TodoOverlay, DrawTodoOverlay),
        new(SettingsSection.Routes, DrawRoutes),
        new(SettingsSection.Notices, DrawNotices),
        new(SettingsSection.Notices, DrawChatActions),
        new(SettingsSection.Spoilers, DrawSpoilers),
        new(SettingsSection.Keyboard, DrawKeyboard),
        new(SettingsSection.Integrations, DrawCompanionPlugins, anchor: SettingsAnchor.CompanionPlugins),
        new(SettingsSection.Integrations, DrawQuestionableSettings),
        new(SettingsSection.Integrations, DrawHandInIntegrations),
        new(SettingsSection.Integrations, DrawAutoDutySettings),
        new(SettingsSection.Integrations, DrawTravelSettings),
        new(SettingsSection.Integrations, DrawWotsit),
        new(SettingsSection.Integrations, DrawNearbySettings, anchor: SettingsAnchor.Nearby),
        new(SettingsSection.Integrations, DrawIntegrations),
        new(SettingsSection.Integrations, DrawGamePanels),
        new(SettingsSection.Integrations, DrawItemHints),
        new(SettingsSection.Integrations, DrawChatTwoAndNamePlates),
        new(SettingsSection.Data, DrawCharacters),
        new(SettingsSection.Data, DrawData),
        new(SettingsSection.Advanced, DrawPolling),
        new(SettingsSection.Advanced, DrawJournalFiling),
        new(SettingsSection.Advanced, DrawHookGate),
        new(SettingsSection.About, DrawAbout),
        new(SettingsSection.About, DrawHelp),
    ];

    /// <summary>The section's title: the index entry, the page title and the search results' heading.</summary>
    public static string SectionTitle(SettingsSection settingsSection) => settingsSection switch
    {
        SettingsSection.Display => Strings.ConfigSectionDisplay,
        SettingsSection.TodoOverlay => Strings.TodoConfigSection,
        SettingsSection.Routes => Strings.ConfigSectionRoutes,
        SettingsSection.Notices => Strings.ConfigSectionNotices,
        SettingsSection.Spoilers => Strings.SettingsSpoilers,
        SettingsSection.Keyboard => Strings.ConfigSectionKeyboard,
        SettingsSection.Integrations => Strings.ConfigSectionIntegrations,
        SettingsSection.Data => Strings.ConfigSectionData,
        SettingsSection.Advanced => Strings.ConfigSectionAdvanced,
        _ => Strings.ConfigSectionAbout,
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
        // The window is its own top level, so its minimum follows the UI scale like Nearby's.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.FontScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        // The window scales itself (its two children inherit it); the scale is reset before Begin lays the title bar
        // out again.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawFrame();
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawFrame()
    {
        DrawSearchBar();
        ImGui.Spacing();
        var wide = ImGui.GetContentRegionAvail().X >= UiMetrics.Px(IndexWidthLogical + IndexBodyMinLogical);
        if (wide)
        {
            using (var index = ImRaii.Child("##settingsIndex", new Vector2(UiMetrics.Px(IndexWidthLogical), -1f), false))
            {
                if (index)
                {
                    DrawIndex();
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
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(14f), UiMetrics.Px(12f)));
        using var body = ImRaii.Child("##settingsBody", new Vector2(-1f, -1f), true);
        if (!body)
        {
            return;
        }

        if (scrollToTop)
        {
            scrollToTop = false;
            ImGui.SetScrollY(0f);
        }

        DrawBody();
    }

    /// <summary>The search box, and "Help &amp; tour" on its right when the help window is wired.</summary>
    private void DrawSearchBar()
    {
        var style = ImGui.GetStyle();
        var helpLabel = Strings.SettingsHelpAndTour;
        var helpWidth = ShowHelp is null ? 0f : ImGui.CalcTextSize(helpLabel).X + (style.FramePadding.X * 2f) + style.ItemSpacing.X;
        ImGui.SetNextItemWidth(MathF.Max(UiMetrics.Px(120f), ImGui.GetContentRegionAvail().X - helpWidth));
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
        if (ShowHelp is not { } showHelp)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button(helpLabel))
        {
            showHelp();
        }

        HintOnHover(Strings.SettingsHelpAndTourTooltip);
    }

    /// <summary>
    /// The section index: one row per section with the Moon bar on the chosen one (Help's rail). While searching no
    /// row is chosen; each shows how many settings matched in it and the ones without a match are dimmed.
    /// </summary>
    private void DrawIndex()
    {
        var order = SettingsSections.Order;
        var rowHeight = ImGui.GetFrameHeight() * 1.25f;
        var searching = filter.Active;
        var dl = ImGui.GetWindowDrawList();
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
                var inset = UiMetrics.Px(4f);
                var bar = UiMetrics.Px(IndexBarWidth);
                dl.AddRectFilled(new Vector2(min.X, min.Y + inset), new Vector2(min.X + bar, max.Y - inset), Theme.MoonU32, bar * 0.5f);
            }

            var tone = chosen ? Theme.Surface.Text : searching && matches == 0 ? Theme.Surface.TextDisabled : Theme.Surface.TextSecondary;
            var textY = min.Y + ((rowHeight - ImGui.GetTextLineHeight()) * 0.5f);
            var left = min.X + UiMetrics.Px(12f);
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
    }

    /// <summary>The index row's match count as text, cached per row until the count changes.</summary>
    private string IndexCount(int row, int count)
    {
        if (indexCountValues[row] != count || indexCounts[row] is null)
        {
            indexCountValues[row] = count;
            indexCounts[row] = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        return indexCounts[row]!;
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
                tabLabels[i] = SectionTitle(order[i]) + "##settingsTab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                dl.AddRectFilled(new Vector2(min.X, max.Y - bar), new Vector2(max.X, max.Y), Theme.MoonU32);
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
            ImGui.Spacing();
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

            using var id = ImRaii.PushId(i);
            block.Draw();
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

    /// <summary>Shows <paramref name="settingsSection"/>'s page: the search is cleared and the page starts at its top.</summary>
    private void Select(SettingsSection settingsSection)
    {
        section = settingsSection;
        anchor = SettingsAnchor.None;
        ClearSearch();
        scrollToTop = true;
    }

    private void ClearSearch()
    {
        searchText = string.Empty;
        filter.SetText(string.Empty);
        scrollToTop = true;
    }

    /// <summary>
    /// Starts a block of settings under <paramref name="title"/> (a Moon Road heading). On a section's page the heading
    /// draws at once, except one equal to the section's title, which the page title already says; while searching it
    /// waits for the block's first matching setting (<see cref="Row"/>), or draws at once when the title itself matches
    /// and the whole block shows.
    /// </summary>
    private void Header(string title)
    {
        if (filter.Heading(title))
        {
            DrawPendingHeadings();
        }
    }

    /// <summary>
    /// One setting, for the search box: true when it shows (always on a section's page; while searching when
    /// <paramref name="label"/>, <paramref name="hint"/> or the English <paramref name="keywords"/> hold every word
    /// typed, or its block or section matched whole). Draws the waiting section title and block heading before the
    /// first setting that shows. Wrap everything the setting draws (its control, its hint, its sub-settings' indent) in
    /// the <c>if</c>.
    /// </summary>
    private bool Row(string label, string? hint = null, string? keywords = null)
    {
        if (!filter.Row(label, hint, keywords))
        {
            return false;
        }

        DrawPendingHeadings();
        return true;
    }

    /// <summary>
    /// A sub-setting's indent, greyed out while <paramref name="parentOn"/> is false. Open it inside the setting's
    /// <c>if (Row(…))</c>, never around several rows: the headings a search result waits to draw come out of
    /// <see cref="Row"/>, and inside the scope they would be indented and greyed too.
    /// </summary>
    private static SubSettingScope SubSetting(bool parentOn) => new(ImRaii.PushIndent(), ImRaii.Disabled(!parentOn));

    private void DrawPendingHeadings()
    {
        if (filter.TakeSectionHeading())
        {
            if (headingDrawn)
            {
                ImGui.Spacing();
                ImGui.Spacing();
            }

            headingDrawn = true;
            SectionHeading.Title(SectionTitle(filter.Section), ImGui.GetContentRegionAvail().X);
            ImGui.Spacing();
        }

        if (filter.TakeBlockHeading() is { } heading)
        {
            ImGui.Spacing();
            SectionHeading.Draw(heading);
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
        /// through <see cref="Row"/>, false when it drew without registering any, null until it first draws. An unknown
        /// block draws while searching, so its settings are found before its page was ever opened.
        /// </summary>
        public bool? RowAware { get; set; }
    }
}
