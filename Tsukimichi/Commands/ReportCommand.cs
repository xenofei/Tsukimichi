using System;
using System.Globalization;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki report [quest name]</c>: copies the diagnostic block of the named quest (the selected one when no name
/// is given) to the clipboard and prints one chat line saying so. The same block the detail pane's Report button copies.
/// </summary>
public sealed class ReportCommand(SessionState session, UiState ui, GameLinks links, DiagnosticBuilder diagnostics, IPluginLog log)
{
    public void Run(string name)
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        var text = (name ?? string.Empty).Trim();
        QuestRecord? quest;
        if (text.Length == 0)
        {
            quest = ui.SelectedRowId is { } rowId ? bundle.Catalog.GetByRowId(rowId) : null;
            if (quest is null)
            {
                links.PrintText(Strings.ReportNoSelection);
                return;
            }
        }
        else
        {
            quest = FindByName(bundle.Catalog, text, session.Spoilers);
            if (quest is null)
            {
                links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportNoMatchFormat, text));
                return;
            }
        }

        var block = diagnostics.Compose(quest);
        if (!DiagnosticBuilder.TryCopy(block, log))
        {
            links.PrintText(Strings.ReportClipboardFailed);
            return;
        }

        links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportCopiedChatFormat, links.NameOf(quest)));
    }

    /// <summary>
    /// A quest by name: an exact match first (case-insensitive, live quests before removed ones), else the first
    /// live quest the search index matches in catalog order; null when nothing matches. A name the spoiler shield
    /// hides matches neither way (its placeholder does), as in the table's search.
    /// </summary>
    internal static QuestRecord? FindByName(QuestCatalog catalog, string name, SpoilerMask? spoilers = null)
    {
        spoilers ??= SpoilerMask.None;
        QuestRecord? exact = null;
        foreach (var quest in catalog.All)
        {
            if (!string.Equals(spoilers.DisplayName(quest), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!quest.IsRemoved)
            {
                return quest;
            }

            exact ??= quest;
        }

        if (exact is not null)
        {
            return exact;
        }

        var index = SearchIndex.For(catalog);
        var normalized = SearchIndex.Normalize(name);
        foreach (var quest in catalog.All)
        {
            if (!quest.IsRemoved && index.Matches(quest.RowId, normalized, spoilers))
            {
                return quest;
            }
        }

        return null;
    }
}
