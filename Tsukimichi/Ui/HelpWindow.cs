using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>Help topics in the order the left list shows them.</summary>
public enum HelpTopic
{
    GettingStarted,
    MoonPhases,
    Filters,
    TableAndDetail,
    Moonlit,
    Characters,
    Commands,
    Tips,
}

/// <summary>
/// Guided help (F: C1/G2/I12): a list of topics on the left and the topic's text on the right, with the eight moon
/// phases drawn large on their own page. Opens once by itself the first time the main window opens
/// (<see cref="Configuration.ShowHelpOnFirstRun"/>), and any time from Settings or <c>/tsukimichi help</c>.
/// Every string lives in <see cref="Strings.Help"/>.
/// </summary>
public sealed class HelpWindow : Window
{
    private static readonly HelpTopic[] Topics = Enum.GetValues<HelpTopic>();

    /// <summary>The eight glyphs on the Moon phases page: state, glyph name, meaning and the filters that include it.</summary>
    private static readonly (QuestState State, string Name, string Meaning, string Filters)[] Phases =
    [
        (QuestState.Completed, Strings.Help.PhaseCompletedName, Strings.Help.PhaseCompletedMeaning, Strings.Help.PhaseCompletedFilters),
        (QuestState.Accepted, Strings.Help.PhaseAcceptedName, Strings.Help.PhaseAcceptedMeaning, Strings.Help.PhaseAcceptedFilters),
        (QuestState.Ready, Strings.Help.PhaseReadyName, Strings.Help.PhaseReadyMeaning, Strings.Help.PhaseReadyFilters),
        (QuestState.ReadyOnOtherJob, Strings.Help.PhaseReadyOtherJobName, Strings.Help.PhaseReadyOtherJobMeaning, Strings.Help.PhaseReadyOtherJobFilters),
        (QuestState.DoneThisCycle, Strings.Help.PhaseDoneThisCycleName, Strings.Help.PhaseDoneThisCycleMeaning, Strings.Help.PhaseDoneThisCycleFilters),
        (QuestState.Blocked, Strings.Help.PhaseBlockedName, Strings.Help.PhaseBlockedMeaning, Strings.Help.PhaseBlockedFilters),
        (QuestState.Foreclosed, Strings.Help.PhaseForeclosedName, Strings.Help.PhaseForeclosedMeaning, Strings.Help.PhaseForeclosedFilters),
        (QuestState.Unknown, Strings.Help.PhaseUnknownName, Strings.Help.PhaseUnknownMeaning, Strings.Help.PhaseUnknownFilters),
    ];

    private const float TopicListWidth = 170f;
    private const float PhaseGlyphRadius = 16f;

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Window mainWindow;

    private HelpTopic topic = HelpTopic.GettingStarted;
    private bool mainWasOpen;

    /// <param name="mainWindow">Watched by <see cref="CheckFirstRun"/> for its first opening.</param>
    public HelpWindow(Configuration settings, IDalamudPluginInterface pluginInterface, Window mainWindow)
        : base(Strings.Help.WindowTitle)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        Size = new Vector2(640f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(480f, 360f) };
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

    /// <summary>
    /// <c>UiBuilder.Draw</c> handler: the first time the main window is seen open while
    /// <see cref="Configuration.ShowHelpOnFirstRun"/> is set, opens the help, clears the flag and saves.
    /// </summary>
    public void CheckFirstRun()
    {
        var open = mainWindow.IsOpen;
        if (open && !mainWasOpen && settings.ShowHelpOnFirstRun)
        {
            settings.ShowHelpOnFirstRun = false;
            settings.Save(pluginInterface);
            Show(HelpTopic.GettingStarted);
        }

        mainWasOpen = open;
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (var list = ImRaii.Child("##helpTopics", new Vector2(TopicListWidth * scale, -1f), true))
        {
            if (list)
            {
                DrawTopics();
            }
        }

        ImGui.SameLine();
        using var colors = Theme.PushNightPanel();
        using var content = ImRaii.Child("##helpContent", new Vector2(-1f, -1f), true);
        if (!content)
        {
            return;
        }

        using var wrap = ImRaii.TextWrapPos(0f);
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(Strings.Help.TopicName(topic));
        }

        ImGui.Separator();
        ImGui.Spacing();

        switch (topic)
        {
            case HelpTopic.MoonPhases:
                Paragraphs(Strings.Help.MoonPhasesIntro);
                ImGui.Spacing();
                DrawPhases();
                break;
            case HelpTopic.GettingStarted:
                Paragraphs(Strings.Help.GettingStarted);
                break;
            case HelpTopic.Filters:
                Paragraphs(Strings.Help.Filters);
                break;
            case HelpTopic.TableAndDetail:
                Paragraphs(Strings.Help.TableAndDetail);
                break;
            case HelpTopic.Moonlit:
                Paragraphs(Strings.Help.Moonlit);
                break;
            case HelpTopic.Characters:
                Paragraphs(Strings.Help.Characters);
                break;
            case HelpTopic.Commands:
                Paragraphs(Strings.Help.Commands);
                break;
            case HelpTopic.Tips:
                Paragraphs(Strings.Help.Tips);
                break;
        }
    }

    private void DrawTopics()
    {
        foreach (var t in Topics)
        {
            if (ImGui.Selectable(Strings.Help.TopicName(t), topic == t))
            {
                topic = t;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        var again = settings.ShowHelpOnFirstRun;
        if (ImGui.Checkbox(Strings.Help.ShowAgain, ref again))
        {
            settings.ShowHelpOnFirstRun = again;
            settings.Save(pluginInterface);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.Help.ShowAgainHint);
        }
    }

    /// <summary>Every phase as a row: the glyph at <see cref="PhaseGlyphRadius"/>, then name, meaning and filters.</summary>
    private static void DrawPhases()
    {
        using var table = ImRaii.Table("##phases", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var radius = PhaseGlyphRadius * scale;
        var box = radius * 3.4f;
        ImGui.TableSetupColumn("##glyph", ImGuiTableColumnFlags.WidthFixed, box);
        ImGui.TableSetupColumn("##text", ImGuiTableColumnFlags.WidthStretch);

        foreach (var (state, name, meaning, filters) in Phases)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box, box));
            MoonGlyph.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(box * 0.5f), radius, state);

            ImGui.TableNextColumn();
            using (Theme.PushText(Theme.StateColor(state)))
            {
                ImGui.TextUnformatted(Strings.MoonlitStateName(state));
            }

            ImGui.SameLine();
            ImGui.TextDisabled(name);
            ImGui.TextWrapped(meaning);
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextWrapped(filters);
            }
        }
    }

    /// <summary>Paragraphs separated by a little space; lines starting with the bullet keep their indent.</summary>
    private static void Paragraphs(string[] paragraphs)
    {
        foreach (var paragraph in paragraphs)
        {
            ImGui.TextWrapped(paragraph);
            ImGui.Spacing();
        }
    }
}
