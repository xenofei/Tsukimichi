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
            quest = FindByName(bundle.Catalog, text);
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

        links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportCopiedChatFormat, quest.Name));
    }

    /// <summary>
    /// A quest by name: an exact match first (case-insensitive, listed quests before unlisted ones), else the first
    /// listed quest the search index matches in catalog order; null when nothing matches.
    /// </summary>
    internal static QuestRecord? FindByName(QuestCatalog catalog, string name)
    {
        QuestRecord? exact = null;
        foreach (var quest in catalog.All)
        {
            if (!string.Equals(quest.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!quest.IsUnlisted)
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
            if (!quest.IsUnlisted && index.Matches(quest.RowId, normalized))
            {
                return quest;
            }
        }

        return null;
    }
}
