using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// How a placeholder of the spoiler shield looks and answers (spec-1.20 N6, "How a placeholder looks"): it keeps the
/// slot, size and weight of the name it hides and only turns Secondary, and a string that holds one is Secondary as a
/// whole (<see cref="Tone"/>); no glyph. Its hover says what it is in three lines (<see cref="Hover"/>), and its
/// right-click offers "Reveal this name" for the session, "Reveal names in this quest" inside one quest's context, and
/// any "Open on…" link behind the spoiler question (<see cref="Menu"/>).
/// </summary>
internal static class ShieldText
{
    private const string MenuId = "##shieldMenu";

    // The placeholder whose menu is open: one popup at a time.
    private static SpoilerKind menuKind;
    private static string menuName = string.Empty;
    private static string menuShown = string.Empty;
    private static QuestRecord? menuQuest;
    private static string? menuLink;
    private static int menuFrame = -1;

    /// <summary>Whether <paramref name="text"/> is or holds a placeholder (<see cref="SpoilerMask.HoldsPlaceholder"/>).</summary>
    public static bool Holds(string? text) => SpoilerMask.HoldsPlaceholder(text);

    /// <summary><paramref name="normal"/>, or Secondary for a string that is or holds a placeholder.</summary>
    public static Vector4 Tone(string? text, Vector4 normal) => Holds(text) ? Theme.Surface.TextSecondary : normal;

    /// <summary><see cref="Tone"/> packed for a draw list.</summary>
    public static uint U32(string? text, Vector4 normal) => Theme.U32(Tone(text, normal));

    /// <summary>
    /// The hover on any placeholder: "Hidden by the spoiler shield" (Text, semibold), "It's from the story past yours."
    /// (Secondary), "Right-click to reveal it for this session." (Tertiary). <paramref name="lead"/>, when given, is the
    /// placeholder itself over them, Secondary: a slot that cut it short, or one that shows no words (a reward tile).
    /// </summary>
    public static void Hover(string? lead = null)
    {
        using var tooltip = Theme.Tooltip();
        using var body = Typography.Body();
        UiMetrics.ApplyFontScale();
        using (UiMetrics.TooltipWrap())
        {
            if (!string.IsNullOrEmpty(lead))
            {
                using (Theme.PushText(Theme.Surface.TextSecondary))
                {
                    ImGui.TextUnformatted(lead);
                }

                ImGui.Spacing();
            }

            Chrome.SemiboldText(Strings.SpoilerHoverTitle, Theme.Surface.Text);

            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(Strings.SpoilerHoverWhy);
            }

            using (Theme.PushText(Theme.Surface.TextTertiary))
            {
                ImGui.TextUnformatted(Strings.SpoilerHoverHint);
            }
        }
    }

    /// <summary>
    /// The hover and the right-click of a placeholder drawn on the draw list over <paramref name="min"/>–<paramref name="max"/>
    /// (no item of its own): while the pointer is on it the three-line hover shows, and a right-click opens its menu.
    /// Draws the menu while it is open; call every frame the placeholder is drawn. Returns whether the pointer is on it.
    /// </summary>
    /// <param name="session">The session whose reveals the menu adds to.</param>
    /// <param name="kind">The hidden name's kind.</param>
    /// <param name="name">The hidden name itself (what the reveal keys on), never shown.</param>
    /// <param name="shown">The placeholder as printed, which the spoiler question names.</param>
    /// <param name="quest">The quest whose context the placeholder sits in; null for none.</param>
    /// <param name="links">For the quest's place in "Reveal names in this quest" and the "Open on…" question; null for neither.</param>
    /// <param name="link">An "Open on Garland Tools" page of the hidden thing; null for none.</param>
    /// <param name="lead">The placeholder as the hover's first line (<see cref="Hover"/>); null for none.</param>
    public static bool Interact(Vector2 min, Vector2 max, SessionState session, SpoilerKind kind, string name, string shown, QuestRecord? quest = null, GameLinks? links = null, string? link = null, string? lead = null)
    {
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup) && ImGui.IsMouseHoveringRect(min, max);
        if (hovered && !ImGui.IsPopupOpen(MenuId))
        {
            Hover(lead);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                OpenMenu(kind, name, shown, quest, link);
            }
        }

        DrawMenu(session, links);
        return hovered;
    }

    /// <summary>As <see cref="Interact(Vector2, Vector2, SessionState, SpoilerKind, string, string, QuestRecord?, GameLinks?, string?, string?)"/>, for the last item.</summary>
    public static bool InteractItem(SessionState session, SpoilerKind kind, string name, string shown, QuestRecord? quest = null, GameLinks? links = null, string? link = null, string? lead = null) =>
        Interact(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), session, kind, name, shown, quest, links, link, lead);

    /// <summary>Opens the menu for one placeholder; it draws through <see cref="DrawMenu"/>.</summary>
    private static void OpenMenu(SpoilerKind kind, string name, string shown, QuestRecord? quest, string? link)
    {
        menuKind = kind;
        menuName = name;
        menuShown = shown;
        menuQuest = quest;
        menuLink = link;
        ImGui.OpenPopup(MenuId);
    }

    /// <summary>The open placeholder's menu, if any: Reveal this name, Reveal names in this quest, Open on Garland Tools….</summary>
    private static void DrawMenu(SessionState session, GameLinks? links)
    {
        // Several placeholders of one window share the popup: it is drawn once a frame, by the first that reaches it.
        var frame = ImGui.GetFrameCount();
        if (frame == menuFrame)
        {
            return;
        }

        using var popup = ImRaii.Popup(MenuId);
        if (!popup)
        {
            return;
        }

        menuFrame = frame;

        UiMetrics.ApplyFontScale();
        RevealItems(session, links, menuKind, menuName, menuQuest);
        if (menuLink is { } url && links is not null)
        {
            ImGui.Separator();
            if (ImGui.MenuItem(Strings.LinksOpenItemOnGarland))
            {
                // Asks first, naming the thing by its placeholder (spec-1.20; LinkConfirmWindow).
                links.OpenExternal(url, menuShown, mayShowSpoilers: true);
            }
        }
    }

    /// <summary>
    /// The reveal items of a placeholder's menu, for a menu of its own (a row's right-click): "Reveal this name" with
    /// "this session" trailing in the disabled (Tertiary) tone, then "Reveal names in this quest" when
    /// <paramref name="quest"/> is given.
    /// </summary>
    public static void RevealItems(SessionState session, GameLinks? links, SpoilerKind kind, string name, QuestRecord? quest, IEnumerable<string>? duties = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (ImGui.MenuItem(Strings.SpoilerRevealName, Strings.SpoilerRevealThisSession))
        {
            session.RevealName(kind, name);
        }

        if (quest is null)
        {
            return;
        }

        if (ImGui.MenuItem(Strings.SpoilerRevealQuestNames))
        {
            RevealQuest(session, links, quest, duties);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerRevealQuestNamesTooltip);
        }
    }

    /// <summary>
    /// "Reveal names in this quest": the quest's own name, its giver, place, duties, rewards and unlocks, for the
    /// session (<see cref="SpoilerNames.NamesIn"/>).
    /// </summary>
    public static void RevealQuest(SessionState session, GameLinks? links, QuestRecord quest, IEnumerable<string>? duties = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(quest);
        session.RevealQuestNames(quest.RowId, QuestNames(links, session.Unlocks, quest, duties));
    }

    /// <summary>The names <see cref="RevealQuest"/> reveals.</summary>
    public static List<(SpoilerKind Kind, string Name)> QuestNames(GameLinks? links, QuestUnlocks? unlocks, QuestRecord quest, IEnumerable<string>? duties = null)
    {
        var map = quest.Issuer is { } issuer ? links?.Map(issuer.MapId) : null;
        return SpoilerNames.NamesIn(quest, unlocks ?? QuestUnlocks.Empty, map?.PlaceName, map?.Region, duties);
    }

    /// <summary>Whether the shield hides anything of the quest: its name, or one of <paramref name="names"/>.</summary>
    public static bool HidesAny(SpoilerMask spoilers, QuestRecord quest, IReadOnlyList<(SpoilerKind Kind, string Name)> names)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        if (spoilers.IsMasked(quest))
        {
            return true;
        }

        if (!spoilers.MasksNames)
        {
            return false;
        }

        foreach (var (kind, name) in names)
        {
            if (spoilers.IsNameMasked(kind, name))
            {
                return true;
            }
        }

        return false;
    }
}
