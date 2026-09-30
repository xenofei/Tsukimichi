using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Payoff;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Before you continue" (P5), drawn under the MSQ line on the Characters dashboard and in the Tonight card: one line
/// per gate speaking for the viewed character ("Before you continue: Finish the Eden raid series first."), shown only
/// while the gate's milestone is Ready or in the journal and its content is not done (<see cref="PayoffGates"/>). The
/// line is the curated instruction and nothing else, so it names the optional content and never the payoff or a main
/// scenario quest past the character's position. Under it a closed "why? (spoiler)" button reveals the reason on click;
/// the open state is per gate and per character, remembered in the configuration, so the reason stays hidden for an
/// alt that has not asked. Hover the line for the progress; click it to select the first content quest left.
/// </summary>
public sealed class PayoffGateLines
{
    private readonly PayoffGateSource source;
    private readonly SessionState session;
    private readonly Configuration config;
    private readonly Action save;

    /// <summary>Open disclosures of a character without a content id (never persisted).</summary>
    private readonly HashSet<string> transientOpen = new(StringComparer.Ordinal);

    // Lines and tooltips for the last gate list drawn, so a frame allocates nothing.
    private IReadOnlyList<ActivePayoffGate>? builtFor;
    private string[] lines = [];
    private string[] tooltips = [];

    public PayoffGateLines(PayoffGateSource source, SessionState session, Configuration config, Action save)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <summary>Whether any gate speaks for the viewed character now; never while Settings › Spoilers hides the notes.</summary>
    public bool Any => config.ShowPayoffGates && source.Viewed().Count > 0;

    /// <summary>Draws the gates speaking for the viewed character; nothing when none does or the notes are hidden.</summary>
    /// <param name="scope">An id scope, so the dashboard and the Tonight card never share ImGui ids.</param>
    public void Draw(UiState ui, string scope)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (!config.ShowPayoffGates)
        {
            return;
        }

        var gates = source.Viewed();
        if (gates.Count == 0)
        {
            return;
        }

        Refresh(gates);
        using var id = ImRaii.PushId(scope);
        for (var i = 0; i < gates.Count; i++)
        {
            var gate = gates[i];
            using var gateId = ImRaii.PushId(gate.Gate.Id);
            using (Theme.PushText(Theme.Accent))
            {
                ImGui.TextWrapped(lines[i]);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(tooltips[i]);
                if (ImGui.IsItemClicked() && FirstLeft(gate) is { } quest)
                {
                    ui.Reveal(quest);
                }
            }

            var open = IsOpen(gate.Gate.Id);
            if (ImGui.SmallButton(open ? Strings.PayoffWhyOpen : Strings.PayoffWhyClosed))
            {
                SetOpen(gate.Gate.Id, !open);
                open = !open;
            }

            if (!open && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.PayoffWhyTooltip);
            }

            if (open)
            {
                using var mist = Theme.PushText(Theme.Surface.TextSecondary);
                ImGui.TextWrapped(gate.Gate.Why);
            }
        }
    }

    private int builtLanguage = -1;

    private void Refresh(IReadOnlyList<ActivePayoffGate> gates)
    {
        if (ReferenceEquals(gates, builtFor) && builtLanguage == Localization.Loc.Version)
        {
            return;
        }

        builtLanguage = Localization.Loc.Version;
        builtFor = gates;
        lines = new string[gates.Count];
        tooltips = new string[gates.Count];
        for (var i = 0; i < gates.Count; i++)
        {
            lines[i] = string.Format(CultureInfo.CurrentCulture, Strings.PayoffFormat, gates[i].Gate.Instruction);
            tooltips[i] = string.Format(CultureInfo.CurrentCulture, Strings.PayoffProgressFormat, gates[i].Done, gates[i].Total);
        }
    }

    /// <summary>The first quest of the gate's content the viewed character has not completed.</summary>
    private QuestRecord? FirstLeft(ActivePayoffGate gate)
    {
        if (session.Bundle is not { } bundle)
        {
            return null;
        }

        foreach (var rowId in gate.Resolved.Content)
        {
            if (!(session.States.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed))
            {
                return bundle.Catalog.GetByRowId(rowId);
            }
        }

        return null;
    }

    private bool IsOpen(string gateId) =>
        session.ViewedContentId is { } contentId
            ? config.PayoffWhyOpenByCharacter.TryGetValue(contentId, out var open) && open is not null && open.Contains(gateId)
            : transientOpen.Contains(gateId);

    private void SetOpen(string gateId, bool open)
    {
        if (session.ViewedContentId is not { } contentId)
        {
            if (open)
            {
                transientOpen.Add(gateId);
            }
            else
            {
                transientOpen.Remove(gateId);
            }

            return;
        }

        if (!config.PayoffWhyOpenByCharacter.TryGetValue(contentId, out var set) || set is null)
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            config.PayoffWhyOpenByCharacter[contentId] = set;
        }

        var changed = open ? set.Add(gateId) : set.Remove(gateId);
        if (set.Count == 0)
        {
            config.PayoffWhyOpenByCharacter.Remove(contentId);
        }

        if (changed)
        {
            save();
        }
    }
}
