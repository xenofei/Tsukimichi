using System;
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
    Commands,
    CountsDiffer,
    KnownQuirks,
    Spoilers,
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
public sealed record HelpActions(Action OpenFilters, Action<NavTab> ShowTab, Action StartTutorial, Action OpenSettings);

/// <summary>
/// Guided help (F: C1/G2/I12): a topic rail on the left, each topic with an icon and the active one marked with a
/// Moon bar, and the topic's page on a Night panel on the right. Pages are built from small blocks: cards, phase
/// rows, numbered steps with "Try it" buttons, tip callouts and key caps. The search box at the top of the rail
/// filters topics by their title and body text. Opened from the toolbar, Settings or <c>/tsukimichi help</c>.
/// Every string lives in <see cref="Strings.Help"/>; sizes go through <see cref="ImGuiHelpers.GlobalScale"/>.
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
        FontAwesomeIcon.Terminal.ToIconString(),
        FontAwesomeIcon.Calculator.ToIconString(),
        FontAwesomeIcon.ExclamationTriangle.ToIconString(),
        FontAwesomeIcon.EyeSlash.ToIconString(),
        FontAwesomeIcon.Lightbulb.ToIconString(),
    ];

    private static readonly string LightbulbIcon = FontAwesomeIcon.Lightbulb.ToIconString();

    private static readonly PhaseItem[] Phases =
    [
        Phase(QuestState.Completed, Strings.Help.PhaseCompletedMeaning, Strings.Help.ChipHideCompletedOff),
        Phase(QuestState.Accepted, Strings.Help.PhaseAcceptedMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.Ready, Strings.Help.PhaseReadyMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.ReadyOnOtherJob, Strings.Help.PhaseReadyOtherJobMeaning, Strings.Help.ChipAvailableNow),
        Phase(QuestState.DoneThisCycle, Strings.Help.PhaseDoneThisCycleMeaning, Strings.Help.ChipAvailableNowOff),
        Phase(QuestState.Blocked, Strings.Help.PhaseBlockedMeaning, Strings.Help.ChipAvailableNowOff),
        Phase(QuestState.Foreclosed, Strings.Help.PhaseForeclosedMeaning, Strings.Help.ChipHideCompletedOff, Strings.Help.ChipNotInTotals),
        Phase(QuestState.Unknown, Strings.Help.PhaseUnknownMeaning),
    ];

    private static readonly CardItem[] FilterCards = Cards(
        Strings.Help.FilterCardTitles,
        Strings.Help.FilterCardBodies,
        FontAwesomeIcon.Eye,
        FontAwesomeIcon.Check,
        FontAwesomeIcon.Moon,
        FontAwesomeIcon.SlidersH,
        FontAwesomeIcon.BookOpen,
        FontAwesomeIcon.Tags,
        FontAwesomeIcon.Search);

    private static readonly CardItem[] QuestCards = Cards(
        Strings.Help.QuestCardTitles,
        Strings.Help.QuestCardBodies,
        FontAwesomeIcon.Check,
        FontAwesomeIcon.Route,
        FontAwesomeIcon.Link,
        FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.History);

    private static readonly CardItem[] MoonlitCards =
    [
        new(FontAwesomeIcon.Gem.ToIconString(), Strings.Help.UniqueTitle, Strings.Help.UniqueBody),
        new(FontAwesomeIcon.Certificate.ToIconString(), Strings.Help.ConfidenceTitle, Strings.Help.ConfidenceBody),
        new(FontAwesomeIcon.Check.ToIconString(), Strings.Help.HaveTitle, Strings.Help.HaveBody),
        new(FontAwesomeIcon.Adjust.ToIconString(), Strings.Help.OverridesTitle, Strings.Help.OverridesBody),
        new(FontAwesomeIcon.Undo.ToIconString(), Strings.Help.RestoreTitle, Strings.Help.RestoreBody),
    ];

    private static readonly BadgeItem[] Badges =
    [
        // The same tones as the Moonlit table's badge column (never gold: a badge is not a call to action).
        new(Strings.MoonlitConfidenceStatic, Theme.Mist, Strings.Help.ConfidenceStaticMeaning),
        new(Strings.MoonlitConfidenceCommunity, Theme.VeilText, Strings.Help.ConfidenceCommunityMeaning),
        new(Strings.MoonlitConfidenceCurated, Theme.Silver, Strings.Help.ConfidenceCuratedMeaning),
        new(Strings.MoonlitConfidenceUser, Theme.EclipseText, Strings.Help.ConfidenceUserMeaning),
    ];

    private static readonly CardItem[] CharacterCards = Cards(
        Strings.Help.CharacterCardTitles,
        Strings.Help.CharacterCardBodies,
        FontAwesomeIcon.Camera,
        FontAwesomeIcon.Users,
        FontAwesomeIcon.Table,
        FontAwesomeIcon.LayerGroup,
        FontAwesomeIcon.Download,
        FontAwesomeIcon.Undo,
        FontAwesomeIcon.CalendarAlt);

    private static readonly CardItem[] FlightCards = Cards(
        Strings.Help.FlightCardTitles,
        Strings.Help.FlightCardBodies,
        FontAwesomeIcon.Plane,
        FontAwesomeIcon.Compass,
        FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.Moon,
        FontAwesomeIcon.Terminal);

    private static readonly CardItem[] CountsCards = Cards(
        Strings.Help.CountsCardTitles,
        Strings.Help.CountsCardBodies,
        FontAwesomeIcon.EyeSlash,
        FontAwesomeIcon.Snowflake,
        FontAwesomeIcon.Lock,
        FontAwesomeIcon.Plus,
        FontAwesomeIcon.Redo);

    private static readonly CardItem[] QuirkCards = Cards(
        Strings.Help.QuirkCardTitles,
        Strings.Help.QuirkCardBodies,
        FontAwesomeIcon.StepForward,
        FontAwesomeIcon.Tag,
        FontAwesomeIcon.ExchangeAlt,
        FontAwesomeIcon.Clock,
        FontAwesomeIcon.ShoppingCart,
        FontAwesomeIcon.PauseCircle);

    private static readonly CardItem[] SpoilerCards = Cards(
        Strings.Help.SpoilerCardTitles,
        Strings.Help.SpoilerCardBodies,
        FontAwesomeIcon.EyeSlash,
        FontAwesomeIcon.Image,
        FontAwesomeIcon.Seedling,
        FontAwesomeIcon.SlidersH);

    private static readonly float[] FillingFractions = [0f, 0.03f, 0.25f, 0.66f, 1f];

    private readonly HelpActions actions;
    private readonly IFontHandle iconFont;
    private readonly IFontHandle monoFont;
    private readonly StepItem[] steps;

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
            new("8", Strings.Help.StepTourTitle, Strings.Help.StepTourBody, actions.StartTutorial),
        ];

        BuildSearchText();
        Array.Fill(topicVisible, true);

        Size = new Vector2(780f, 600f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(560f, 380f) };
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
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
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
            dl.AddRectFilled(new Vector2(min.X, min.Y + inset), new Vector2(min.X + BarWidth * scale, max.Y - inset), Theme.MoonU32, BarWidth * scale * 0.5f);
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
                        foreach (var chip in phase.Chips)
                        {
                            sb.Append(chip).Append('\n');
                        }
                    }

                    sb.Append(Strings.Help.FillingTitle).Append('\n').Append(Strings.Help.FillingBody);
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
        for (var i = 0; i < Phases.Length; i++)
        {
            PhaseRow(i, in Phases[i], scale);
        }

        ImGui.Spacing();
        FillingCard(scale);
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

    // ------------------------------------------------------------------ blocks

    /// <summary>A Chrome card: the icon and title on the first line and the wrapped body under them.</summary>
    private static void Card(int id, in CardItem card)
    {
        Chrome.BeginCard(id, card.Title, card.Icon);
        using (Theme.PushText(BodyText))
        {
            ImGui.TextUnformatted(card.Body);
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
            using (Theme.PushText(Theme.StateColor(phase.State)))
            {
                ImGui.TextUnformatted(Strings.StateName(phase.State));
            }

            ImGui.SameLine(0f, 6f * scale);
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(phase.Name);
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

    /// <summary>A Moon circle with the step number, the title with an optional "Try it" button on its right, and the body.</summary>
    private void Step(int id, in StepItem step)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = StepRadius * scale;
        var box = radius * 2.2f;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        using var idScope = ImRaii.PushId(id);
        ImGui.Dummy(new Vector2(box, box));
        var center = pos + new Vector2(box * 0.5f);
        dl.AddCircleFilled(center, radius, Theme.MoonU32);
        var numberSize = ImGui.CalcTextSize(step.Number);
        dl.AddText(center - numberSize * 0.5f, Theme.NightU32, step.Number);

        ImGui.SameLine(0f, 10f * scale);
        using (ImRaii.Group())
        {
            using (Theme.PushText(Theme.Moon))
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
        chips[0] = Strings.Help.ChipStatePrefix + Strings.StateName(state);
        Array.Copy(filterChips, 0, chips, 1, filterChips.Length);
        return new PhaseItem(state, Strings.StateGlyphSubtitle(state), meaning, chips);
    }

    private static CardItem[] Cards(string[] titles, string[] bodies, params FontAwesomeIcon[] icons)
    {
        if (titles.Length != bodies.Length || titles.Length != icons.Length)
        {
            throw new InvalidOperationException("Help card titles, bodies and icons must have the same length.");
        }

        var cards = new CardItem[titles.Length];
        for (var i = 0; i < cards.Length; i++)
        {
            cards[i] = new CardItem(icons[i].ToIconString(), titles[i], bodies[i]);
        }

        return cards;
    }
}
