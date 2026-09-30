using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Journal text (P9): "Search journal text of completed quests", off by default. Ticking it has the journal
/// text service load or build its word index in the background; the line under the box says where that stands
/// (building with a percentage, ready with the index's size, or why it failed). Unticking drops the index from memory
/// and the search box goes back to names, rewards and ids.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The journal text reader and index; set by the plugin. Null hides the section.</summary>
    public QuestTextService? QuestText { get; set; }

    private void DrawJournalText()
    {
        if (QuestText is not { } service)
        {
            return;
        }

        Header(Strings.JournalTextSettingsSection);
        var search = settings.JournalTextSearch;
        if (ImGui.Checkbox(Strings.JournalTextSearchSetting, ref search))
        {
            settings.JournalTextSearch = search;
            Save();
            service.SetEnabled(search);
        }

        ImGui.TextDisabled(Strings.JournalTextSearchHint);
        if (!settings.JournalTextSearch)
        {
            return;
        }

        // The main window's query runner starts the build too; this starts it while only Settings is open.
        service.Update(session.Bundle?.Catalog);

        var line = service.Status switch
        {
            JournalIndexStatus.Waiting => Strings.JournalTextStatusWaiting,
            JournalIndexStatus.Loading => Strings.JournalTextStatusLoading,
            JournalIndexStatus.Building => BuildingLine((int)(service.Progress * 100f)),
            JournalIndexStatus.Ready => ReadyLine(service),
            JournalIndexStatus.Failed => FailedLine(service.Error),
            _ => null,
        };

        if (service.Status == JournalIndexStatus.Building)
        {
            ImGui.ProgressBar(service.Progress, new System.Numerics.Vector2(-1f, 0f), line);
        }
        else if (line is not null)
        {
            ImGui.TextWrapped(line);
        }
    }

    // The ready line, rebuilt only when the index or the language changes.
    private int journalTextLineVersion = -1;
    private int journalTextLineLanguage = -1;
    private string journalTextLine = string.Empty;

    // The building and failed lines, rebuilt only when the percentage, the error or the language changes.
    private int journalBuildingPercent = -1;
    private int journalBuildingLanguage = -1;
    private string journalBuildingLine = string.Empty;
    private string? journalFailedError;
    private int journalFailedLanguage = -1;
    private string journalFailedLine = string.Empty;

    private string BuildingLine(int percent)
    {
        if (percent != journalBuildingPercent || journalBuildingLanguage != Localization.Loc.Version)
        {
            journalBuildingPercent = percent;
            journalBuildingLanguage = Localization.Loc.Version;
            journalBuildingLine = string.Format(CultureInfo.CurrentCulture, Strings.JournalTextStatusBuildingFormat, percent);
        }

        return journalBuildingLine;
    }

    private string FailedLine(string? error)
    {
        if (!string.Equals(error, journalFailedError, System.StringComparison.Ordinal) || journalFailedLanguage != Localization.Loc.Version)
        {
            journalFailedError = error;
            journalFailedLanguage = Localization.Loc.Version;
            journalFailedLine = string.Format(CultureInfo.CurrentCulture, Strings.JournalTextStatusFailedFormat, error ?? Strings.UnknownError);
        }

        return journalFailedLine;
    }

    private string ReadyLine(QuestTextService service)
    {
        if (journalTextLineVersion == service.Version && journalTextLineLanguage == Localization.Loc.Version)
        {
            return journalTextLine;
        }

        journalTextLineVersion = service.Version;
        journalTextLineLanguage = Localization.Loc.Version;
        var index = service.Index;
        journalTextLine = string.Format(CultureInfo.CurrentCulture, Strings.JournalTextStatusReadyFormat, index?.QuestCount ?? 0, (service.FileBytes + 1023) / 1024);
        if (service.BuildTime is { } built)
        {
            journalTextLine += string.Format(CultureInfo.CurrentCulture, Strings.JournalTextStatusBuiltFormat, built.TotalSeconds);
        }

        return journalTextLine;
    }
}
