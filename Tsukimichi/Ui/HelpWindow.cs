using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Help topics in the order the rail shows them.</summary>
public enum HelpTopic
{
    QuickStart,
    MoonPhases,
    Filters,
    ReadingAQuest,
    Moonlit,
    Characters,
    Flight,

    /// <summary>The My blues tab (P3): the unlock quests left and the Todo overlay's Clear my blues section.</summary>
    Plan,

    /// <summary>What Tsukimichi does beside the game (1.7.0): the overlay, Nearby, item hints, NPC menu, Duty Finder hint, routes, travel.</summary>
    WhilePlaying,

    /// <summary>The optional plugins Tsukimichi hands work to (feature plan v5, decision 1).</summary>
    Companions,
    Commands,
    CountsDiffer,
    KnownQuirks,
    Spoilers,

    /// <summary>What Tsukimichi reads and keeps (1.7.0, players research F10).</summary>
    Privacy,
    Tips,
}

/// <summary>
/// What the help window's "Try it" buttons do. Each action is expected to bring the main window to the front as
/// well as perform the change, so the reader sees the result next to the explanation.
/// </summary>
/// <param name="OpenFilters">Opens the filter panel.</param>
/// <param name="ShowTab">Switches the main window's navigation tab.</param>
/// <param name="StartTutorial">Starts the interactive tutorial from its first step.</param>
/// <param name="OpenSettings">Toggles the settings window.</param>
/// <param name="ToggleTodo">Turns the Todo overlay on or off (1.7.0).</param>
/// <param name="OpenNearby">Opens the Nearby quests window (1.7.0).</param>
/// <param name="ShowSetup">Opens the main window with the "Set up your road" card (1.7.0).</param>
public sealed record HelpActions(Action OpenFilters, Action<NavTab> ShowTab, Action StartTutorial, Action OpenSettings, Action ToggleTodo, Action OpenNearby, Action ShowSetup);

/// <summary>
/// Guided help (F: C1/G2/I12): a topic rail on the left, each topic with an icon and the active one marked with a
/// Moon bar, and the topic's page on a Night panel on the right. Pages are built from small blocks: cards, phase
/// rows, numbered steps with "Try it" buttons, tip callouts and key caps. The search box at the top of the rail
/// filters topics by their title and body text. Opened from the toolbar, Settings or <c>/tsukimichi help</c>.
/// Every string lives in <see cref="Strings.Help"/>; sizes go through <see cref="UiMetrics.Scale"/>, so the window follows
/// Settings › Display › Window scale like the main window (R3 #4).
/// </summary>
public sealed class HelpWindow : Window
{
    private const float RailWidth = 212f;
    private const float Rounding = 6f;
    private const float Pad = 10f;
    private const float BarWidth = 3f;
    private const float PhaseGlyphRadius = 18f;
    /// <summary>Halo half-size in the legend card: a 32 px box at scale 1, so the core and its moon show.</summary>
    private const float FillingGlyphRadius = 16f;
    private const float StepRadius = 11f;
    private const float TitleScale = 1.4f;
    private const int SearchMaxLength = 64;

    private static readonly HelpTopic[] Topics = Enum.GetValues<HelpTopic>();

    /// <summary>Body text: the primary tone at 85 %, in whichever palette is active.</summary>
    private static Vector4 BodyText => Theme.WithAlphaVector(Theme.Surface.Text, 0.85f);

    private Theme.StyleScope nightChrome;

    private readonly record struct CardItem(string Icon, string Title, string Body);

    private readonly record struct StepItem(string Number, string Title, string Body, Action? TryIt);

    private readonly record struct PhaseItem(QuestState State, string Name, string Meaning, string[] Chips);

    private readonly record struct BadgeItem(string Label, Vector4 Color, string Meaning);

    private static readonly string[] TopicIcons =
    [
        FontAwesomeIcon.Rocket.ToIconString(),
        FontAwesomeIcon.Moon.ToIconString(),
        FontAwesomeIcon.Filter.ToIconString(),
        FontAwesomeIcon.BookOpen.ToIconString(),
        FontAwesomeIcon.Gem.ToIconString(),
        FontAwesomeIcon.Users.ToIconString(),
        FontAwesomeIcon.Plane.ToIconString(),
        FontAwesomeIcon.ClipboardList.ToIconString(),
        FontAwesomeIcon.Tasks.ToIconString(),
        FontAwesomeIcon.PuzzlePiece.ToIconString(),
        FontAwesomeIcon.Terminal.ToIconString(),
        FontAwesomeIcon.Calculator.ToIconString(),
        FontAwesomeIcon.ExclamationTriangle.ToIconString(),
        FontAwesomeIcon.EyeSlash.ToIconString(),
        FontAwesomeIcon.ShieldAlt.ToIconString(),
        FontAwesomeIcon.Lightbulb.ToIconString(),
    ];

    private static readonly string LightbulbIcon = FontAwesomeIcon.Lightbulb.ToIconString();

    private static PhaseItem[] Phases => phasesCache.Value;

    private static readonly Localization.LocCache<PhaseItem[]> phasesCache = new(static () =>
        [
        Phase(QuestState.Completed, Strings.Help.PhaseCompletedMeaning, Strings.Help.ChipHideCompletedOff),
        Phase(QuestState.Accepted, Strings.Help.PhaseAcceptedMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.Ready, Strings.Help.PhaseReadyMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.ReadyOnOtherJob, Strings.Help.PhaseReadyOtherJobMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.DoneThisCycle, Strings.Help.PhaseDoneThisCycleMeaning, Strings.Help.ChipAvailableNowOff),
        Phase(QuestState.Blocked, Strings.Help.PhaseBlockedMeaning, Strings.Help.ChipAvailableNowOff),
        Phase(QuestState.Foreclosed, Strings.Help.PhaseForeclosedMeaning, Strings.Help.ChipHideCompletedOff, Strings.Help.ChipNotInTotals),
        Phase(QuestState.Unknown, Strings.Help.PhaseUnknownMeaning),
    ]);

    private static CardItem[] FilterCards => filterCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> filterCardsCache = new(static () =>
        Cards(
        Strings.Help.FilterCardTitles,
        Strings.Help.FilterCardBodies,
        FontAwesomeIcon.Eye,
        FontAwesomeIcon.Check,
        FontAwesomeIcon.Moon,
        FontAwesomeIcon.SlidersH,
        FontAwesomeIcon.BookOpen,
        FontAwesomeIcon.Tags,
        FontAwesomeIcon.Search));

    private static CardItem[] QuestCards => questCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> questCardsCache = new(static () =>
        Cards(
        Strings.Help.QuestCardTitles,
        Strings.Help.QuestCardBodies,
        FontAwesomeIcon.Check,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.Link,
        FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.History,
        FontAwesomeIcon.BookOpen));

    private static CardItem[] MoonlitCards => moonlitCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> moonlitCardsCache = new(static () =>
        [
        new(FontAwesomeIcon.Gem.ToIconString(), Strings.Help.UniqueTitle, Strings.Help.UniqueBody),
        new(FontAwesomeIcon.Certificate.ToIconString(), Strings.Help.ConfidenceTitle, Strings.Help.ConfidenceBody),
        new(FontAwesomeIcon.Check.ToIconString(), Strings.Help.HaveTitle, Strings.Help.HaveBody),
        new(FontAwesomeIcon.Adjust.ToIconString(), Strings.Help.OverridesTitle, Strings.Help.OverridesBody),
        new(FontAwesomeIcon.Undo.ToIconString(), Strings.Help.RestoreTitle, Strings.Help.RestoreBody),
    ]);

    private static BadgeItem[] Badges => badgesCache.Value;

    private static readonly Localization.LocCache<BadgeItem[]> badgesCache = new(static () =>
        [
        // The same tones as the Moonlit table's badge column (never gold: a badge is not a call to action).
        new(Strings.MoonlitConfidenceStatic, Theme.Surface.TextSecondary, Strings.Help.ConfidenceStaticMeaning),
        new(Strings.MoonlitConfidenceCommunity, Theme.UnknownText, Strings.Help.ConfidenceCommunityMeaning),
        new(Strings.MoonlitConfidenceCurated, Theme.Surface.Text, Strings.Help.ConfidenceCuratedMeaning),
        new(Strings.MoonlitConfidenceUser, Theme.DangerText, Strings.Help.ConfidenceUserMeaning),
    ]);

    private static CardItem[] CharacterCards => characterCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> characterCardsCache = new(static () =>
        Cards(
        Strings.Help.CharacterCardTitles,
        Strings.Help.CharacterCardBodies,
        FontAwesomeIcon.Camera,
        FontAwesomeIcon.Users,
        FontAwesomeIcon.Table,
        FontAwesomeIcon.LayerGroup,
        FontAwesomeIcon.Download,
        FontAwesomeIcon.Undo,
        FontAwesomeIcon.CalendarAlt,
        FontAwesomeIcon.MapSigns,
        FontAwesomeIcon.History));

    private static CardItem[] FlightCards => flightCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> flightCardsCache = new(static () =>
        Cards(
        Strings.Help.FlightCardTitles,
        Strings.Help.FlightCardBodies,
        FontAwesomeIcon.Plane,
        FontAwesomeIcon.Compass,
        FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.Moon,
        FontAwesomeIcon.Terminal));

    private static CardItem[] PlanCards => planCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> planCardsCache = new(static () =>
        [
        new(FontAwesomeIcon.ClipboardList.ToIconString(), Strings.PlanHelpTitle, Strings.PlanHelpBody),
    ]);

    private static CardItem[] CompanionCards => companionCardsCache.Value;

    /// <summary>The companion setup summary ("Ready for full automation", "2 plugins need setup"); null before the plugin set it up.</summary>
    private static string? CompanionSetupLine() =>
        Game.CompanionPlugins.Current?.Setup is { } setup ? Strings.CompanionSetupSummaryLine(setup.Summary) : null;

    /// <summary>One card per listed companion plugin (its name and what it unlocks), then how to add a custom repository.</summary>
    private static readonly Localization.LocCache<CardItem[]> companionCardsCache = new(static () =>
    {
        var cards = new List<CardItem>();
        var icon = FontAwesomeIcon.PuzzlePiece.ToIconString();
        foreach (var definition in Core.Companions.CompanionCatalog.All)
        {
            if (definition.Listed)
            {
                cards.Add(new CardItem(icon, definition.DisplayName, Strings.CompanionUnlocks(definition.Plugin)));
            }
        }

        cards.Add(new CardItem(FontAwesomeIcon.Link.ToIconString(), Strings.HelpCompanionsRepoTitle, Strings.CompanionAddRepoHowTo));
        return cards.ToArray();
    });

    private static CardItem[] CountsCards => countsCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> countsCardsCache = new(static () =>
        Cards(
        Strings.Help.CountsCardTitles,
        Strings.Help.CountsCardBodies,
        FontAwesomeIcon.EyeSlash,
        FontAwesomeIcon.Snowflake,
        FontAwesomeIcon.Lock,
        FontAwesomeIcon.Plus,
        FontAwesomeIcon.Redo));

    private static CardItem[] QuirkCards => quirkCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> quirkCardsCache = new(static () =>
        Cards(
        Strings.Help.QuirkCardTitles,
        Strings.Help.QuirkCardBodies,
        FontAwesomeIcon.StepForward,
        FontAwesomeIcon.Tag,
        FontAwesomeIcon.ExchangeAlt,
        FontAwesomeIcon.Clock,
        FontAwesomeIcon.ShoppingCart,
        FontAwesomeIcon.PauseCircle,
        FontAwesomeIcon.EyeSlash,
        FontAwesomeIcon.Percent));

    private static CardItem[] PlayCards => playCardsCache.Value;

    /// <summary>
    /// "While you play" (1.7.0): the overlay and Nearby first (each with a button), then item hints, the NPC menu, the
    /// Duty Finder hint, abandoned quests, routes, following a route, travel to a giver, the panels beside the game's
    /// windows, the clickable chat lines, and nameplate marks with quest banners.
    /// </summary>
    private static readonly Localization.LocCache<CardItem[]> playCardsCache = new(static () =>
        Cards(
        Strings.Help.PlayCardTitles,
        Strings.Help.PlayCardBodies,
        FontAwesomeIcon.Tasks,
        FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.Tag,
        FontAwesomeIcon.Comments,
        FontAwesomeIcon.Lock,
        FontAwesomeIcon.Undo,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.MapSigns,
        FontAwesomeIcon.Walking,
        FontAwesomeIcon.WindowRestore,
        FontAwesomeIcon.CommentDots,
        FontAwesomeIcon.IdBadge));

    private static CardItem[] PrivacyCards => privacyCardsCache.Value;

    /// <summary>
    /// What Tsukimichi reads and keeps (1.7.0): whose data, which files, no network, links, companions, deleting, and
    /// checking the build (feature plan v7 N2).
    /// </summary>
    private static readonly Localization.LocCache<CardItem[]> privacyCardsCache = new(static () =>
        Cards(
        Strings.Help.PrivacyCardTitles,
        Strings.Help.PrivacyCardBodies,
        FontAwesomeIcon.User,
        FontAwesomeIcon.FolderOpen,
        FontAwesomeIcon.Ban,
        FontAwesomeIcon.ExternalLinkAlt,
        FontAwesomeIcon.PuzzlePiece,
        FontAwesomeIcon.TrashAlt,
        FontAwesomeIcon.Fingerprint));

    private static CardItem[] SpoilerCards => spoilerCardsCache.Value;

    private static readonly Localization.LocCache<CardItem[]> spoilerCardsCache = new(static () =>
        Cards(
        Strings.Help.SpoilerCardTitles,
        Strings.Help.SpoilerCardBodies,
        FontAwesomeIcon.EyeSlash,
        FontAwesomeIcon.Image,
        FontAwesomeIcon.Seedling,
        FontAwesomeIcon.SlidersH,
        FontAwesomeIcon.HourglassHalf));

    private static readonly float[] FillingFractions = [0f, 0.03f, 0.25f, 0.66f, 1f];

    private readonly HelpActions actions;
    private readonly IFontHandle iconFont;
    private readonly IFontHandle monoFont;
    private StepItem[] steps = [];

    /// <summary>The UI language <see cref="steps"/> and the search text were built in.</summary>
    private int builtLanguage = -1;

    /// <summary>Lower-cased title, lede and every block's text per topic, for the rail's search box.</summary>
    private readonly string[] topicSearchText = new string[Topics.Length];

    private readonly bool[] topicVisible = new bool[Topics.Length];

    private HelpTopic topic = HelpTopic.QuickStart;
    private string searchText = string.Empty;

    /// <param name="actions">What the "Try it" buttons do.</param>
    /// <param name="pluginInterface">For the icon and monospace font handles.</param>
    public HelpWindow(HelpActions actions, IDalamudPluginInterface pluginInterface)
        : base(Strings.Help.WindowTitle)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        ArgumentNullException.ThrowIfNull(pluginInterface);
        iconFont = pluginInterface.UiBuilder.IconFontFixedWidthHandle;
        monoFont = pluginInterface.UiBuilder.MonoFontHandle;

        BuildForLanguage();
        Array.Fill(topicVisible, true);

        Size = new Vector2(780f, 600f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) };
    }

    /// <summary>Logical minimum size of the window, scaled by the UI scale each frame.</summary>
    private const float MinWidthLogical = 560f;
    private const float MinHeightLogical = 380f;

    /// <summary>The quick-start steps and the rail's search text in the current UI language (V2-19).</summary>
    private void BuildForLanguage()
    {
        builtLanguage = Localization.Loc.Version;
        steps =
        [
            // Find, Read, Beyond: the order of the tour's chapters (T14).
            new("1", Strings.Help.StepOpenTitle, Strings.Help.StepOpenBody, null),
            new("2", Strings.Help.StepFindTitle, Strings.Help.StepFindBody, () => actions.ShowTab(NavTab.Journal)),
            new("3", Strings.Help.StepFiltersTitle, Strings.Help.StepFiltersBody, actions.OpenFilters),
            new("4", Strings.Help.StepReadTitle, Strings.Help.StepReadBody, () => actions.ShowTab(NavTab.Journal)),
            new("5", Strings.Help.StepMoonlitTitle, Strings.Help.StepMoonlitBody, () => actions.ShowTab(NavTab.Moonlit)),
            new("6", Strings.Help.StepCharactersTitle, Strings.Help.StepCharactersBody, () => actions.ShowTab(NavTab.Characters)),
            new("7", Strings.Help.StepFlightTitle, Strings.Help.StepFlightBody, () => actions.ShowTab(NavTab.Flight)),
            new("8", Strings.Help.StepPlanTitle, Strings.Help.StepPlanBody, () => actions.ShowTab(NavTab.Plan)),
            new("9", Strings.Help.StepTourTitle, Strings.Help.StepTourBody, actions.StartTutorial),
            new("10", Strings.Help.StepSetupTitle, Strings.Help.StepSetupBody, actions.ShowSetup),
        ];

        BuildSearchText();
    }

    /// <summary>Opens the window (on its current topic) and brings it to the front.</summary>
    public void Show()
    {
        IsOpen = true;
        BringToFront();
    }

    /// <summary>Opens the window on a topic.</summary>
    public void Show(HelpTopic topic)
    {
        this.topic = topic;
        Show();
    }

    /// <summary>Night chrome around the whole window (rail included); nothing is pushed while following Dalamud's colours.</summary>
    public override void PreDraw()
    {
        if (builtLanguage != Localization.Loc.Version)
        {
            BuildForLanguage();
        }

        // The window is its own top level, so its minimum follows the UI scale like the main window's.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.UiScale,
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
        // The window scales itself (the rail and the content inherit it); the scale is reset before Begin lays the
        // title bar out again.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawWindow();
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawWindow()
    {
        var scale = UiMetrics.Scale;
        using (var rail = ImRaii.Child("##helpRail", new Vector2(RailWidth * scale, -1f), false))
        {
            if (rail)
            {
                DrawRail(scale);
            }
        }

        ImGui.SameLine();
        using var colors = Theme.PushNightPanel();
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, Rounding * scale);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * scale);
        using var content = ImRaii.Child("##helpContent", new Vector2(-1f, -1f), true);
        if (!content)
        {
            return;
        }

        DrawContent(scale);
    }

    // ------------------------------------------------------------------ rail

    private void DrawRail(float scale)
    {
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputTextWithHint("##helpSearch", Strings.Help.SearchHint, ref searchText, SearchMaxLength))
        {
            UpdateVisible();
        }

        ImGui.Spacing();
        var any = false;
        for (var i = 0; i < Topics.Length; i++)
        {
            if (!topicVisible[i])
            {
                continue;
            }

            any = true;
            DrawTopicRow(i, scale);
        }

        if (!any)
        {
            ImGui.TextDisabled(Strings.Help.NoTopicMatches);
        }
    }

    /// <summary>A full-width selectable with the topic's icon and title drawn over it and a Moon bar when active.</summary>
    private void DrawTopicRow(int index, float scale)
    {
        var t = Topics[index];
        var selected = topic == t;
        var rowHeight = ImGui.GetFrameHeight() * 1.35f;
        var min = ImGui.GetCursorScreenPos();

        // The selection wash comes from the Night chrome (neutral, never gold); the Moon bar is the selected row's rule.
        using var id = ImRaii.PushId(index);
        if (ImGui.Selectable("##topic", selected, ImGuiSelectableFlags.None, new Vector2(0f, rowHeight)))
        {
            topic = t;
        }

        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        if (selected)
        {
            var inset = 4f * scale;
            dl.AddRectFilled(new Vector2(min.X, min.Y + inset), new Vector2(min.X + BarWidth * scale, max.Y - inset), Theme.GoldU32, BarWidth * scale * 0.5f);
        }

        var textY = min.Y + (rowHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(min.X + 12f * scale, textY));
        using (iconFont.Push())
        using (Theme.PushText(selected ? Theme.Surface.Text : Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(TopicIcons[index]);
        }

        ImGui.SameLine(0f, 8f * scale);
        ImGui.TextUnformatted(Strings.Help.TopicName(t));
        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + ImGui.GetStyle().ItemSpacing.Y));
    }

    private void UpdateVisible()
    {
        var first = -1;
        for (var i = 0; i < Topics.Length; i++)
        {
            topicVisible[i] = searchText.Length == 0 || topicSearchText[i].Contains(searchText, StringComparison.OrdinalIgnoreCase);
            if (topicVisible[i] && first < 0)
            {
                first = i;
            }
        }

        // The content pane never shows a topic the rail no longer lists: move to the first match while there is one.
        if (first >= 0 && !topicVisible[Array.IndexOf(Topics, topic)])
        {
            topic = Topics[first];
        }
    }

    /// <summary>Concatenates each topic's visible text once, so the search box filters without allocating per frame.</summary>
    private void BuildSearchText()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Topics.Length; i++)
        {
            var t = Topics[i];
            sb.Clear();
            sb.Append(Strings.Help.TopicName(t)).Append('\n').Append(Strings.Help.TopicLede(t)).Append('\n');
            switch (t)
            {
                case HelpTopic.QuickStart:
                    foreach (var step in steps)
                    {
                        sb.Append(step.Title).Append('\n').Append(step.Body).Append('\n');
                    }

                    sb.Append(Strings.Help.QuickStartTip).Append('\n').Append(Strings.Help.QuickStartSettingsTip);
                    break;
                case HelpTopic.MoonPhases:
                    foreach (var phase in Phases)
                    {
                        sb.Append(Strings.StateName(phase.State)).Append('\n').Append(phase.Name).Append('\n').Append(phase.Meaning).Append('\n');
                        sb.Append(StateNames.HighContrastSubtitle(phase.State)).Append('\n');
                        foreach (var chip in phase.Chips)
                        {
                            sb.Append(chip).Append('\n');
                        }
                    }

                    sb.Append(Strings.Help.FillingTitle).Append('\n').Append(Strings.Help.FillingBody).Append('\n');
                    sb.Append(Strings.Help.StripeTitle).Append('\n').Append(Strings.Help.StripeBody).Append('\n');
                    foreach (var phase in Phases)
                    {
                        sb.Append(StripePattern.Name(phase.State)).Append('\n');
                    }

                    break;
                case HelpTopic.Filters:
                    AppendCards(sb, FilterCards);
                    sb.Append(Strings.Help.FiltersTip);
                    break;
                case HelpTopic.ReadingAQuest:
                    AppendCards(sb, QuestCards);
                    sb.Append(Strings.Help.QuestTip);
                    break;
                case HelpTopic.Moonlit:
                    AppendCards(sb, MoonlitCards);
                    foreach (var badge in Badges)
                    {
                        sb.Append(badge.Label).Append('\n').Append(badge.Meaning).Append('\n');
                    }

                    break;
                case HelpTopic.Characters:
                    AppendCards(sb, CharacterCards);
                    break;
                case HelpTopic.Flight:
                    AppendCards(sb, FlightCards);
                    break;
                case HelpTopic.Plan:
                    AppendCards(sb, PlanCards);
                    break;
                case HelpTopic.WhilePlaying:
                    AppendCards(sb, PlayCards);
                    sb.Append(Strings.Help.PlayTip);
                    break;
                case HelpTopic.Privacy:
                    AppendCards(sb, PrivacyCards);
                    break;
                case HelpTopic.Companions:
                    if (CompanionSetupLine() is { } setupLine)
                    {
                        sb.Append(setupLine).Append('\n');
                    }

                    AppendCards(sb, CompanionCards);
                    sb.Append(Strings.HelpCompanionsTip);
                    break;
                case HelpTopic.Commands:
                    for (var c = 0; c < Strings.Help.CommandKeys.Length; c++)
                    {
                        sb.Append(Strings.Help.CommandKeys[c]).Append('\n').Append(Strings.Help.CommandMeanings[c]).Append('\n');
                    }

                    break;
                case HelpTopic.CountsDiffer:
                    AppendCards(sb, CountsCards);
                    sb.Append(Strings.Help.CountsTip);
                    break;
                case HelpTopic.KnownQuirks:
                    AppendCards(sb, QuirkCards);
                    sb.Append(Strings.Help.QuirksTip);
                    break;
                case HelpTopic.Spoilers:
                    AppendCards(sb, SpoilerCards);
                    sb.Append(Strings.Help.SpoilersTip);
                    break;
                case HelpTopic.Tips:
                    foreach (var tip in Strings.Help.Tips)
                    {
                        sb.Append(tip).Append('\n');
                    }

                    break;
            }

            topicSearchText[i] = sb.ToString();
        }
    }

    private static void AppendCards(StringBuilder sb, CardItem[] cards)
    {
        foreach (var card in cards)
        {
            sb.Append(card.Title).Append('\n').Append(card.Body).Append('\n');
        }
    }

    // ------------------------------------------------------------------ content

    private void DrawContent(float scale)
    {
        ImGui.SetWindowFontScale(TitleScale);
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(Strings.Help.TopicName(topic));
        }

        ImGui.SetWindowFontScale(1f);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(Strings.Help.TopicLede(topic));
        }

        ImGui.Spacing();
        ImGui.Spacing();

        switch (topic)
        {
            case HelpTopic.QuickStart:
                DrawQuickStart();
                break;
            case HelpTopic.MoonPhases:
                DrawMoonPhases(scale);
                break;
            case HelpTopic.Filters:
                DrawCards(FilterCards);
                Tip(100, Strings.Help.FiltersTip);
                break;
            case HelpTopic.ReadingAQuest:
                DrawCards(QuestCards);
                Tip(100, Strings.Help.QuestTip);
                break;
            case HelpTopic.Moonlit:
                DrawMoonlit(scale);
                break;
            case HelpTopic.Characters:
                DrawCards(CharacterCards);
                break;
            case HelpTopic.Flight:
                DrawCards(FlightCards);
                break;
            case HelpTopic.Plan:
                DrawCards(PlanCards);
                break;
            case HelpTopic.WhilePlaying:
                DrawWhilePlaying();
                break;
            case HelpTopic.Privacy:
                DrawCards(PrivacyCards);
                break;
            case HelpTopic.Companions:
                if (CompanionSetupLine() is { } setupLine)
                {
                    using (Theme.PushText(Theme.Surface.Text))
                    {
                        ImGui.TextWrapped(setupLine);
                    }

                    ImGui.Spacing();
                }

                DrawCards(CompanionCards);
                Tip(100, Strings.HelpCompanionsTip, actions.OpenSettings, Strings.Help.OpenSettings);
                break;
            case HelpTopic.Commands:
                DrawCommands(scale);
                break;
            case HelpTopic.CountsDiffer:
                DrawCards(CountsCards);
                Tip(100, Strings.Help.CountsTip);
                break;
            case HelpTopic.KnownQuirks:
                DrawCards(QuirkCards);
                Tip(100, Strings.Help.QuirksTip);
                break;
            case HelpTopic.Spoilers:
                DrawCards(SpoilerCards);
                Tip(100, Strings.Help.SpoilersTip);
                break;
            case HelpTopic.Tips:
                for (var i = 0; i < Strings.Help.Tips.Length; i++)
                {
                    Tip(i, Strings.Help.Tips[i]);
                }

                break;
        }
    }

    private void DrawQuickStart()
    {
        for (var i = 0; i < steps.Length; i++)
        {
            Step(i, in steps[i]);
        }

        ImGui.Spacing();
        Tip(100, Strings.Help.QuickStartTip);
        Tip(101, Strings.Help.QuickStartSettingsTip, actions.OpenSettings, Strings.Help.OpenSettings);
    }

    private void DrawMoonPhases(float scale)
    {
        if (Theme.Glyphs.HighContrast)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(Strings.Help.HighContrastLegendNote);
            }

            ImGui.Spacing();
        }

        for (var i = 0; i < Phases.Length; i++)
        {
            PhaseRow(i, in Phases[i], scale);
        }

        ImGui.Spacing();
        FillingCard(scale);
        StripeCard(scale);
    }

    private void DrawMoonlit(float scale)
    {
        Card(0, in MoonlitCards[0]);
        Card(1, in MoonlitCards[1]);
        using (ImRaii.PushIndent(Pad * scale, false))
        {
            foreach (var badge in Badges)
            {
                Chrome.Pill(badge.Label, badge.Color);
                ImGui.SameLine(0f, 8f * scale);
                using (Theme.PushText(BodyText))
                {
                    ImGui.TextUnformatted(badge.Meaning);
                }
            }
        }

        ImGui.Spacing();
        for (var i = 2; i < MoonlitCards.Length; i++)
        {
            Card(i, in MoonlitCards[i]);
        }
    }

    private void DrawCommands(float scale)
    {
        for (var i = 0; i < Strings.Help.CommandKeys.Length; i++)
        {
            KeyCap(Strings.Help.CommandKeys[i], Strings.Help.CommandMeanings[i], scale);
        }
    }

    private void DrawCards(CardItem[] cards)
    {
        for (var i = 0; i < cards.Length; i++)
        {
            Card(i, in cards[i]);
        }
    }

    /// <summary>While you play: the overlay and Nearby cards carry a button that does it; the rest are plain cards.</summary>
    private void DrawWhilePlaying()
    {
        var cards = PlayCards;
        for (var i = 0; i < cards.Length; i++)
        {
            switch (i)
            {
                case 0:
                    Card(i, in cards[i], actions.ToggleTodo, Strings.Help.ToggleOverlay);
                    break;
                case 1:
                    Card(i, in cards[i], actions.OpenNearby, Strings.Help.OpenNearby);
                    break;
                default:
                    Card(i, in cards[i]);
                    break;
            }
        }

        Tip(100, Strings.Help.PlayTip);
    }

    // ------------------------------------------------------------------ blocks

    /// <summary>A Chrome card: the icon and title on the first line and the wrapped body under them; optionally a button under the body.</summary>
    private static void Card(int id, in CardItem card, Action? action = null, string? label = null)
    {
        Chrome.BeginCard(id, card.Title, card.Icon);
        using (Theme.PushText(BodyText))
        {
            ImGui.TextUnformatted(card.Body);
        }

        if (action is not null && label is not null && ImGui.Button(label))
        {
            action();
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    /// <summary>The glyph at <see cref="PhaseGlyphRadius"/>, the state name in its colour, the glyph name, the meaning and the "shown by" pills.</summary>
    private void PhaseRow(int id, in PhaseItem phase, float scale)
    {
        var radius = PhaseGlyphRadius * scale;
        var box = radius * 3.2f;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        using var idScope = ImRaii.PushId(id);
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.Draw(dl, pos + new Vector2(box * 0.5f), radius, phase.State);

        ImGui.SameLine(0f, 8f * scale);
        using (ImRaii.Group())
        {
            using (Theme.PushText(Theme.StateText(phase.State)))
            {
                ImGui.TextUnformatted(Strings.StateName(phase.State));
            }

            // The shape the moon beside it has: the high-contrast silhouette and mark when that palette is on.
            ImGui.SameLine(0f, 6f * scale);
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(Theme.Glyphs.HighContrast ? StateNames.HighContrastSubtitle(phase.State) : phase.Name);
            }

            using (Theme.PushText(BodyText))
            {
                ImGui.TextWrapped(phase.Meaning);
            }

            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(Strings.Help.ShownBy);
            }

            foreach (var chip in phase.Chips)
            {
                ImGui.SameLine(0f, 6f * scale);
                Chrome.Pill(chip, Theme.Surface.TextTertiary, wrap: true);
            }
        }

        ImGui.Spacing();
        Chrome.Hairline();
        ImGui.Spacing();
    }

    /// <summary>Five filling moons (new to full) beside the explanation of the progress moon, on a Chrome card.</summary>
    private static void FillingCard(float scale)
    {
        var radius = FillingGlyphRadius * scale;
        var box = radius * 2.4f;
        var dl = ImGui.GetWindowDrawList();

        Chrome.BeginCard("##filling");
        foreach (var fraction in FillingFractions)
        {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box, box));
            MoonGlyph.DrawHalo(dl, pos + new Vector2(box * 0.5f), radius, fraction, onCard: true);
            ImGui.SameLine(0f, 6f * scale);
        }

        ImGui.NewLine();
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(Strings.Help.FillingTitle);
        }

        using (Theme.PushText(BodyText))
        {
            ImGui.TextUnformatted(Strings.Help.FillingBody);
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    /// <summary>
    /// The quest table's state stripe as a legend (T15, accessibility A3): for each of the eight states a sample of its
    /// stripe as the table draws it (<see cref="StripePattern"/>'s runs in the table's colour), the state's name and the
    /// pattern's name, so the patterns can be learnt without colour.
    /// </summary>
    private static void StripeCard(float scale)
    {
        Chrome.BeginCard("##stripes", Strings.Help.StripeTitle);
        using (Theme.PushText(BodyText))
        {
            ImGui.TextUnformatted(Strings.Help.StripeBody);
        }

        ImGui.Spacing();
        var dl = ImGui.GetWindowDrawList();
        var height = MathF.Round(24f * scale);
        var thickness = MathF.Max(2f, MathF.Round(Theme.Glyphs.StripeWidth * scale));
        var line = ImGui.GetTextLineHeight();
        foreach (var phase in Phases)
        {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(thickness + (10f * scale), height));
            var color = TablePane.StripeColor(phase.State);
            foreach (var segment in StripePattern.Segments(phase.State))
            {
                var y0 = MathF.Round(pos.Y + (segment.Start * height));
                var y1 = MathF.Max(y0 + 1f, MathF.Round(pos.Y + (segment.End * height)));
                dl.AddRectFilled(new Vector2(pos.X, y0), new Vector2(pos.X + thickness, y1), color);
            }

            // A faint rail shows where the stripe would be, so "no bar" still reads as a pattern.
            dl.AddRect(new Vector2(pos.X - 1f, pos.Y), new Vector2(pos.X + thickness + 1f, pos.Y + height), Theme.U32(Theme.Surface.Line), 0f, ImDrawFlags.None, 1f);

            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((height - line) * 0.5f));
            using (Theme.PushText(Theme.StateText(phase.State)))
            {
                ImGui.TextUnformatted(Strings.StateName(phase.State));
            }

            ImGui.SameLine(0f, 6f * scale);
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(StripePattern.Name(phase.State));
            }
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    /// <summary>A Moon circle with the step number, the title with an optional "Try it" button on its right, and the body.</summary>
    private void Step(int id, in StepItem step)
    {
        var scale = UiMetrics.Scale;
        var radius = StepRadius * scale;
        var box = radius * 2.2f;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        using var idScope = ImRaii.PushId(id);
        ImGui.Dummy(new Vector2(box, box));
        var center = pos + new Vector2(box * 0.5f);
        dl.AddCircleFilled(center, radius, Theme.GoldU32);
        var numberSize = ImGui.CalcTextSize(step.Number);
        dl.AddText(center - numberSize * 0.5f, Theme.OnGoldU32, step.Number);

        ImGui.SameLine(0f, 10f * scale);
        using (ImRaii.Group())
        {
            using (Theme.PushText(Theme.Accent))
            {
                ImGui.TextUnformatted(step.Title);
            }

            if (step.TryIt is { } tryIt)
            {
                // SameLine(x) is relative to the group's start, so the button is placed in window coordinates instead.
                var buttonWidth = ImGuiHelpers.GetButtonSize(Strings.Help.TryIt).X;
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - buttonWidth);
                if (ImGui.Button(Strings.Help.TryIt))
                {
                    tryIt();
                }
            }

            using (Theme.PushText(BodyText))
            {
                ImGui.TextWrapped(step.Body);
            }
        }

        ImGui.Spacing();
    }

    /// <summary>
    /// A callout card (a faint wash with a neutral rule on the left: advice, not a call to action) holding a lightbulb
    /// and the text; optionally a button under it.
    /// </summary>
    private static void Tip(int id, string body, Action? action = null, string? label = null)
    {
        Chrome.BeginCard(id, kind: CardKind.Callout);
        ImGui.PushFont(UiBuilder.IconFont);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(LightbulbIcon);
        }

        ImGui.PopFont();
        ImGui.SameLine(0f, UiMetrics.Px(8f));
        using (ImRaii.Group())
        {
            using (Theme.PushText(BodyText))
            {
                ImGui.TextUnformatted(body);
            }

            if (action is not null && label is not null && ImGui.Button(label))
            {
                action();
            }
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    /// <summary>The command in a key cap (monospace on a raised box) with its meaning beside it.</summary>
    private void KeyCap(string command, string meaning, float scale)
    {
        var padX = 8f * scale;
        var padY = 3f * scale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = 4f * scale;

        Vector2 textSize;
        using (monoFont.Push())
        {
            textSize = ImGui.CalcTextSize(command);
        }

        var s = Theme.Surface;
        var size = textSize + new Vector2(padX * 2f, padY * 2f + 2f * scale);
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        dl.AddRectFilled(pos, pos + size, Theme.U32(s.Hover), rounding);
        dl.AddRect(pos, pos + size, Theme.U32(s.StrongLine), rounding);
        dl.AddLine(new Vector2(pos.X + rounding, pos.Y + size.Y - 1.5f * scale), new Vector2(pos.X + size.X - rounding, pos.Y + size.Y - 1.5f * scale), Theme.U32(s.TextTertiary), 2f * scale);
        using (monoFont.Push())
        {
            dl.AddText(pos + new Vector2(padX, padY), Theme.U32(s.Text), command);
        }

        ImGui.SameLine(0f, 10f * scale);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (size.Y - ImGui.GetTextLineHeight()) * 0.5f);
        using (Theme.PushText(BodyText))
        {
            ImGui.TextWrapped(meaning);
        }

        ImGui.Spacing();
    }

    // ------------------------------------------------------------------ static data helpers

    /// <summary>One legend row: the display name, the moon-phase subtitle from <see cref="Strings.StateGlyphSubtitle"/>, the meaning and its chips.</summary>
    private static PhaseItem Phase(QuestState state, string meaning, params string[] filterChips)
    {
        var chips = new string[filterChips.Length + 1];
        chips[0] = string.Format(CultureInfo.CurrentCulture, Strings.Help.ChipStateFormat, Strings.StateName(state));
        Array.Copy(filterChips, 0, chips, 1, filterChips.Length);
        return new PhaseItem(state, Strings.StateGlyphSubtitle(state), meaning, chips);
    }

    private static CardItem[] Cards(string[] titles, string[] bodies, params FontAwesomeIcon[] icons)
    {
        // A card list that grows without its icon must not stop the Help window (and so the plugin) from
        // loading: log it, show a plain icon for the missing ones and drop a title that has no body.
        if (titles.Length != bodies.Length || titles.Length != icons.Length)
        {
            Plugin.Log.Warning(
                "Help cards: {Titles} titles, {Bodies} bodies, {Icons} icons; a card list and its icons differ",
                titles.Length, bodies.Length, icons.Length);
        }

        var cards = new CardItem[Math.Min(titles.Length, bodies.Length)];
        for (var i = 0; i < cards.Length; i++)
        {
            var icon = i < icons.Length ? icons[i] : FontAwesomeIcon.InfoCircle;
            cards[i] = new CardItem(icon.ToIconString(), titles[i], bodies[i]);
        }

        return cards;
    }
}
