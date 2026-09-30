using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Text;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Journal card (P9): the quest's journal entries as the game's journal showed them, read from the
/// client's own quest text sheet. Offered only for a quest the viewed character completed, or has in its journal (then
/// up to the step it is on, never beyond: <see cref="JournalVisibility"/>). Closed by default behind "Read the
/// journal", and closed again when the selection moves, so nothing is read or shown until asked. The logged-in
/// character's entries are evaluated by the game (its name, its gender); a stored character's are shown neutrally.
/// Each entry has "Copy entry"; there is no bulk copy.
/// </summary>
public sealed partial class DetailPane
{
    private const double JournalCopiedSeconds = 2.0;

    private static readonly string JournalTextIcon = FontAwesomeIcon.BookOpen.ToIconString();

    private static readonly Localization.LocText JournalReadLabel = new(static () => Strings.JournalTextRead + "##journalRead");
    private static readonly Localization.LocText JournalHideLabel = new(static () => Strings.JournalTextHide + "##journalHide");

    // The quest whose journal is open; uint.MaxValue when none. A new selection starts closed.
    private uint journalOpenRowId = uint.MaxValue;

    // "Copied" in place of an entry's Copy button for a moment.
    private uint journalCopiedRowId = uint.MaxValue;
    private int journalCopiedIndex = -1;
    private double journalCopiedUntil;

    /// <summary>The journal text reader; null until the plugin attaches it, which hides the card.</summary>
    public QuestTextService? QuestText { get; set; }

    private void DrawJournalCard(SessionState session, QuestRecord quest)
    {
        if (QuestText is not { } service || !JournalVisibility.IsReadable(model.State))
        {
            return;
        }

        Gap();
        Chrome.BeginCard("##journalText", Strings.JournalTextCard, JournalTextIcon);
        if (journalOpenRowId != quest.RowId)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(model.State == QuestState.Accepted ? Strings.JournalTextClosedAccepted : Strings.JournalTextClosedCompleted);
            }

            if (ImGui.SmallButton(JournalReadLabel.Value))
            {
                journalOpenRowId = quest.RowId;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.JournalTextReadTooltip);
            }

            Chrome.EndCard();
            return;
        }

        var live = session.IsLive;
        var view = service.Journal(quest, model.State, model.Evaluation?.Sequence, live, live ? null : session.ViewedSnapshot?.Name, session.ViewedContentId ?? 0);
        switch (view.Availability)
        {
            case JournalAvailability.Available:
                DrawJournalEntries(quest.RowId, view, live);
                break;
            default:
                using (Theme.PushText(Theme.Surface.TextSecondary))
                {
                    ImGui.TextWrapped(Strings.JournalTextNoText);
                }

                break;
        }

        if (ImGui.SmallButton(JournalHideLabel.Value))
        {
            journalOpenRowId = uint.MaxValue;
        }

        Chrome.EndCard();
    }

    private void DrawJournalEntries(uint rowId, JournalView view, bool live)
    {
        var now = ImGui.GetTime();
        for (var i = 0; i < view.Entries.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            ImGui.TextWrapped(view.Entries[i]);
            var copied = journalCopiedRowId == rowId && journalCopiedIndex == i && now < journalCopiedUntil;
            if (copied)
            {
                using var mist = Theme.PushText(Theme.Surface.TextSecondary);
                ImGui.TextUnformatted(Strings.JournalTextCopied);
            }
            else if (ImGui.SmallButton(Strings.JournalTextCopy))
            {
                ImGui.SetClipboardText(view.Entries[i]);
                journalCopiedRowId = rowId;
                journalCopiedIndex = i;
                journalCopiedUntil = now + JournalCopiedSeconds;
            }

            if (!copied && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.JournalTextCopyTooltip);
            }

            if (i + 1 < view.Entries.Count)
            {
                Chrome.Hairline();
            }
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            if (view.Withheld > 0)
            {
                ImGui.TextWrapped(Strings.JournalTextLater);
            }

            if (!live)
            {
                ImGui.TextWrapped(Strings.JournalTextNeutral);
            }
        }

        if (view.Objectives.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        using (Typography.Caption())
        {
            ImGui.TextUnformatted(Strings.JournalTextObjectives);
        }

        foreach (var objective in view.Objectives)
        {
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextWrapped(objective);
        }
    }
}
