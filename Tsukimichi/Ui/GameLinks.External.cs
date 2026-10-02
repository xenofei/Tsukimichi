using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Links;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// "Open on…" (1.8.0, research C9 #1 and #2): browser links to a quest's page on the Lodestone, Garland Tools, the
/// Console Games Wiki and Teamcraft, and a Moonlit reward's page on FFXIV Collect and its item on Garland Tools. The
/// pages come from the shipped link table (<see cref="ExternalIds"/>), else the site's search; the browser opens them
/// through Dalamud's <see cref="Util.OpenLink"/> and Tsukimichi itself never fetches anything (decision 8). A quest the
/// spoiler shield masks asks first ("This page may show spoilers. Open anyway?", <see cref="ConfirmLink"/>).
/// </summary>
public sealed partial class GameLinks
{
    private static readonly (ExternalSite Site, Func<string> Label)[] QuestSites =
    [
        (ExternalSite.Lodestone, static () => Strings.LinksLodestone),
        (ExternalSite.GarlandTools, static () => Strings.LinksGarland),
        (ExternalSite.ConsoleGamesWiki, static () => Strings.LinksWiki),
        (ExternalSite.Teamcraft, static () => Strings.LinksTeamcraft),
    ];

    /// <summary>The shipped link table, set by the plugin at load; empty (every link a search) until then.</summary>
    public ExternalIds ExternalIds { get; set; } = ExternalIds.Empty;

    /// <summary>The sites' language code ("en", "ja", "de", "fr"), from the catalog's client language; English until attached.</summary>
    public Func<string>? SiteLanguage { get; set; }

    /// <summary>Asks before opening a page that may show spoilers (url, then the name the question shows); null opens at once.</summary>
    public Action<string, string>? ConfirmLink { get; set; }

    // English quest names for the wiki's search on another client, read from the English Quest sheet once per row.
    private readonly Dictionary<uint, string?> englishQuestNames = [];

    private string Language => SiteLanguage?.Invoke() ?? "en";

    /// <summary>
    /// The quest's page on <paramref name="site"/>, from the link table or the site's search. The wiki is English, so
    /// on another client its search uses the quest's English name (the English Quest sheet), else the quest's
    /// Lodestone or Garland Tools page opens instead.
    /// </summary>
    public string? QuestLink(ExternalSite site, QuestRecord quest)
    {
        var language = Language;
        return ExternalLinks.Quest(site, quest.RowId, quest.Name, ExternalIds, language, WikiEnglishName(site, quest.RowId, language));
    }

    /// <summary>The English name the wiki's search needs: only for the wiki, on another client, when the table has no page.</summary>
    private string? WikiEnglishName(ExternalSite site, uint rowId, string language) =>
        site == ExternalSite.ConsoleGamesWiki && language != "en" && ExternalIds.WikiTitle(rowId) is null ? EnglishQuestName(rowId) : null;

    /// <summary>The quest's name on the English Quest sheet (cleaned of sheet glyphs), read once; null when it cannot be read.</summary>
    private string? EnglishQuestName(uint rowId)
    {
        if (englishQuestNames.TryGetValue(rowId, out var cached))
        {
            return cached;
        }

        string? name = null;
        try
        {
            var text = data.GetExcelSheet<Quest>(ClientLanguage.English)?.GetRowOrDefault(rowId)?.Name.ExtractText();
            name = string.IsNullOrWhiteSpace(text) ? null : UiFormat.CleanSheetText(text);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not read the English name of quest {RowId}", rowId);
        }

        englishQuestNames[rowId] = name;
        return name;
    }

    /// <summary>The one link a copied line or row carries: the Lodestone page when known, else Garland Tools.</summary>
    public string PreferredLink(QuestRecord quest) => PreferredLink(quest.RowId);

    /// <inheritdoc cref="PreferredLink(QuestRecord)"/>
    public string PreferredLink(uint rowId) => ExternalLinks.Preferred(rowId, ExternalIds, Language);

    /// <summary>Opens <paramref name="url"/> in the browser; with <paramref name="mayShowSpoilers"/> asks first.</summary>
    public void OpenExternal(string url, string name, bool mayShowSpoilers)
    {
        if (mayShowSpoilers && ConfirmLink is { } confirm)
        {
            confirm(url, name);
            return;
        }

        OpenUrl(url);
    }

    /// <summary>Opens a URL in the default browser; a failure logs one warning.</summary>
    public void OpenUrl(string url)
    {
        try
        {
            Util.OpenLink(url);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not open {Url} in the browser", url);
        }
    }

    /// <summary>
    /// The "Open on…" submenu of a quest menu: Lodestone, Garland Tools, Console Games Wiki and Teamcraft. A site the
    /// link table has no page for says its search opens instead. <paramref name="masked"/> (the spoiler shield hides
    /// the quest's name) asks before opening.
    /// </summary>
    public void DrawOpenOnMenu(QuestRecord quest, bool masked, string displayName)
    {
        using var menu = ImRaii.Menu(Strings.LinksOpenOn);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LinksOpenOnTooltip);
        }

        if (!menu)
        {
            return;
        }

        // A submenu is its own popup window: it scales itself.
        UiMetrics.ApplyFontScale();
        foreach (var (site, label) in QuestSites)
        {
            if (ImGui.MenuItem(label()) && QuestLink(site, quest) is { } url)
            {
                OpenExternal(url, displayName, masked);
            }

            var searches = site switch
            {
                ExternalSite.Lodestone => ExternalIds.LodestoneId(quest.RowId) is null,
                ExternalSite.ConsoleGamesWiki => ExternalIds.WikiTitle(quest.RowId) is null
                                                 && ExternalLinks.WikiSearches(Language, WikiEnglishName(site, quest.RowId, Language)),
                _ => false,
            };
            if (searches && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.LinksSearchTooltip);
            }
        }

        ImGui.Separator();
        if (ImGui.MenuItem(Strings.LinksCopyLink))
        {
            ImGui.SetClipboardText(PreferredLink(quest));
        }
    }

    /// <summary>
    /// A Moonlit row's links: "Open on FFXIV Collect" for a kind the site lists (its page when the link table knows
    /// it, else its list filtered by name) and "Open item on Garland Tools" for a reward that comes as an item. Both
    /// pages name the quest that gives the reward, so a reward of a quest the spoiler shield masks
    /// (<paramref name="masked"/>) asks first.
    /// </summary>
    public void DrawRewardLinks(UniqueRewardEntry entry, bool masked, string displayName)
    {
        if (ExternalLinks.Collect(entry, ExternalIds) is { } collect)
        {
            if (ImGui.MenuItem(Strings.LinksOpenOnCollect))
            {
                OpenExternal(collect, displayName, masked);
            }

            if (ExternalIds.CollectId(entry.Kind, entry.RewardId) is null && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.LinksCollectSearchTooltip);
            }
        }

        if (entry.ItemId != 0 && ImGui.MenuItem(Strings.LinksOpenItemOnGarland))
        {
            OpenExternal(ExternalLinks.GarlandItem(entry.ItemId), displayName, masked);
        }
    }
}
