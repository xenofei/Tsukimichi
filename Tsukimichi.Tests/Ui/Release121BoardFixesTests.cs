using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The 1.21.0 UI review's fixes on the roster, the Triple Triad card, Side stories, Loose ends, the goal card, the
/// Duties board and Nearby's Everywhere: the wiring lives in plugin code the tests cannot load, so these read its
/// source (the logic itself is tested in Core: ZoneBoardTests, StorylinesTests, AltGoalTests, LinkedFoldersTests).
/// </summary>
public sealed class Release121BoardFixesTests
{
    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui", file)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string GameSource(string file) =>
        File.ReadAllText(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Game", file)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The text from <paramref name="start"/> up to <paramref name="end"/> after it.</summary>
    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"no \"{start}\"");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"no \"{end}\" after \"{start}\"");
        return source[from..to];
    }

    // ------------------------------------------------------------------ 1. Set aside in Everywhere

    [Fact]
    public void Everywhere_leaves_set_aside_quests_out_of_the_board_and_the_zone_menu()
    {
        var refresh = Between(Source("DiscoveryWindow.Everywhere.cs"), "private void RefreshBoard()", "private bool IsReady(");
        Assert.Contains("var setAside = session.ViewedSetAside;", refresh, StringComparison.Ordinal);
        Assert.Contains("setAside);", Between(refresh, "board = ZoneBoard.Build(", "// Each zone's quests left"), StringComparison.Ordinal);
        Assert.Contains("setAside.Contains(quest.RowId)", Between(refresh, "foreach (var quest in catalog.All)", "list.Add(quest);"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 2. The shield on 1.21's placeholders

    [Fact]
    public void Board_rows_take_the_shields_hover_and_right_click()
    {
        var row = Source("BoardRow.cs");
        Assert.Contains("BoardShield? shield = null", row, StringComparison.Ordinal);
        Assert.Contains("ShieldText.Interact(min, new Vector2(textRight, max.Y), hidden.Session", row, StringComparison.Ordinal);

        Assert.Contains("shield: row.Shield is { } hidden ? new BoardShield(", Source("CharactersPane.Triad.cs"), StringComparison.Ordinal);
        Assert.Contains("{ Shield = ShieldOf(row) }", Source("TriadBoardSource.cs"), StringComparison.Ordinal);

        var zone = Between(Source("DiscoveryWindow.Everywhere.cs"), "if (zone.Masked)", "return;");
        Assert.Contains("shield: new BoardShield(session, SpoilerKind.Area, zone.Zone.Name", zone, StringComparison.Ordinal);
        Assert.Contains("ShieldText.DrawMenu(nameof(DiscoveryWindow), session);", Source("DiscoveryWindow.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Storyline_placeholders_take_the_shields_hover_and_right_click()
    {
        var draw = Between(Source("CharactersPane.StoryRows.cs"), "private void DrawStoryRow(", "private static float DrawSemiboldAt(");
        Assert.Contains("row.Shield is { OnName: true } named", draw, StringComparison.Ordinal);
        Assert.Contains("row.Shield is { OnName: false } hidden", draw, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(draw, "ShieldText.Interact("));

        var side = Source("CharactersPane.SideStories.cs");
        Assert.Contains("Shield = first is null ? null : new StoryShield(chain.Name, first, OnName: true)", side, StringComparison.Ordinal);
        Assert.Contains("Shield = veiled ? new StoryShield(next.Name, next, OnName: false) : null", side, StringComparison.Ordinal);
        Assert.Contains("Shield = ahead ? new StoryShield(end.Next.Name, end.Next, OnName: false) : null", Source("CharactersPane.LooseEnds.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_goal_cards_and_the_never_cleared_duties_placeholders_take_the_shield()
    {
        var rows = Between(Source("CharactersPane.Goal.cs"), "private void DrawGoalRows(", "var more = progress.Quests.Count - shown;");
        Assert.Contains("if (masked)", rows, StringComparison.Ordinal);
        Assert.Contains("ShieldText.InteractItem(session,", rows, StringComparison.Ordinal);

        Assert.Contains("string? RealName = null", Source("DutyBoardSource.cs"), StringComparison.Ordinal);
        Assert.Contains("string.Empty, string.Empty, null, d.Name)", Source("DutyBoardSource.cs"), StringComparison.Ordinal);
        var hidden = Between(Source("CharactersPane.Duties.cs"), "private (string Name, bool StandIn)? HiddenDuty(", "private void DrawBlockActions(");
        Assert.Contains("row.Quest is null && row.RealName is { } real", hidden, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 3. and 6. The roster off the frame

    [Fact]
    public void The_roster_never_reads_a_save_or_works_out_facts_on_the_frame()
    {
        var source = Source("RosterSource.cs");
        Assert.DoesNotContain("private CharacterSnapshot? Load(", source, StringComparison.Ordinal);
        var attach = Between(source, "private void Attach(", "private (CharacterSnapshot Snapshot");

        // Every save read and every FactsFor runs inside a lambda handed to Schedule (a worker), none on the frame.
        Assert.DoesNotContain("member.Facts = FactsFor(", attach, StringComparison.Ordinal);
        var own = attach.IndexOf("Schedule(member, ownKey, () => FactsFor(", StringComparison.Ordinal);
        var stored = attach.IndexOf("Schedule(member, key, () =>", StringComparison.Ordinal);
        Assert.True(own > 0 && stored > own, "both facts are scheduled");
        Assert.True(attach.IndexOf("load(id)", StringComparison.Ordinal) > stored, "the save is read inside the worker");
        Assert.True(attach.LastIndexOf("FactsFor(", StringComparison.Ordinal) > stored, "the stored facts are worked out inside the worker");
        Assert.Contains("resolving[id] = (key, Task.Run(work));", Between(source, "private void Schedule(", "private void TakeLanded()"), StringComparison.Ordinal);

        // The context a stored save resolves with is taken on the frame thread and handed to the worker.
        Assert.Contains("var contextFor = session.StoredContextFactory();", attach, StringComparison.Ordinal);
        Assert.Contains("public System.Func<CharacterSnapshot, EvalContext> StoredContextFactory()", GameSource("SessionState.Roster.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_reset_cycle_rebuilds_the_roster()
    {
        var source = Source("RosterSource.cs");
        Assert.Contains("(DateTime Daily, DateTime Weekly) Cycle);", Between(source, "private readonly record struct BuildKey(", "private sealed record GoalKey("), StringComparison.Ordinal);
        Assert.Contains("Localization.Loc.Version,\n            cycle);", Between(source, "var key = new BuildKey(", "if (key == builtKey)"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 4. No hand-offs for a character not logged in here

    [Fact]
    public void Travel_and_hand_offs_are_offered_only_for_the_character_logged_in_here()
    {
        var triad = Source("CharactersPane.Triad.cs");
        Assert.Contains("actions && ViewedLiveHere && links!.TeleportShown", triad, StringComparison.Ordinal);
        Assert.Contains("if (ViewedLiveHere)", Between(triad, "private void DrawTriadMenu(", "private void DrawTriadActions("), StringComparison.Ordinal);

        var story = Source("CharactersPane.StoryRows.cs");
        Assert.Contains("private bool ViewedLiveHere => session.ViewedContentId is { } viewed && viewed == session.LiveContentId;", story, StringComparison.Ordinal);
        Assert.Contains("Links is { TeleportShown: true } links && ViewedLiveHere", Between(story, "private void DrawStoryRow(", "private static float DrawSemiboldAt("), StringComparison.Ordinal);
        var menu = Between(story, "private void DrawStoryRowMenu(", "private SetAsideActions SetAside");
        Assert.Contains("if (ViewedLiveHere)\n            {\n                TravelControls.MenuItems(", menu, StringComparison.Ordinal);
        Assert.Contains("row.RowIds.Count > 0 && ViewedLiveHere && AutomationGate.Questionable(", menu, StringComparison.Ordinal);

        var zone = Source("DiscoveryWindow.Everywhere.cs");
        Assert.Contains("links.TeleportShown && ViewedLiveHere ?", zone, StringComparison.Ordinal);
        var zoneMenu = Between(zone, "private void DrawZoneMenu(", "private QuestRecord? NearestReady(");
        Assert.Contains("links.GoToShown && live", zoneMenu, StringComparison.Ordinal);
        Assert.Contains("quests.Count > 0 && live", zoneMenu, StringComparison.Ordinal);

        Assert.Contains("rowIds.Count > 0 && ViewedLiveHere ? AutomationGate.Questionable(", Source("CharactersPane.cs"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 5. Roster rows keyed by the character

    [Fact]
    public void Roster_rows_and_their_menu_are_keyed_by_the_character_not_the_sort()
    {
        var row = Between(Source("CharactersPane.Roster.cs"), "private void DrawRosterRow(", "private static void TwoLines(");
        Assert.DoesNotContain("PushId(index)", row, StringComparison.Ordinal);
        Assert.Contains("ImRaii.PushId(unchecked((int)row.ContentId));", row, StringComparison.Ordinal);
        Assert.Contains("ImRaii.PushId(unchecked((int)(row.ContentId >> 32)));", row, StringComparison.Ordinal);
        Assert.Contains("rosterMenuFor = row.ContentId;", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("PushId(unchecked", StringComparison.Ordinal) < row.IndexOf("##rosterMenu", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ 7. No per-frame allocation

    [Fact]
    public void The_triad_card_the_kind_chips_and_the_goal_popover_allocate_nothing_per_frame()
    {
        Assert.DoesNotContain("new List<TriadBoardSource.Row>(", Between(Source("CharactersPane.Triad.cs"), "private void DrawTriadBoard(", "private void DrawTriadChips("), StringComparison.Ordinal);
        Assert.DoesNotContain("\"##kind\" +", Source("DiscoveryWindow.Everywhere.cs"), StringComparison.Ordinal);

        var popover = Between(Source("CharactersPane.Goal.cs"), "private void DrawGoalPopover(", "private static void PickerCombo(");
        Assert.DoesNotContain(".ToArray()", popover, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToList()", popover, StringComparison.Ordinal);
        Assert.DoesNotContain("index.ToString(", popover, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToList()", Between(Source("CharactersPane.Goal.cs"), "private AltGoal? ChosenGoal(", "private void RefreshGoalPickers("), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 8. Remove a linked folder with Undo

    [Fact]
    public void Removing_a_linked_folder_offers_the_undo()
    {
        var source = Source("ConfigWindow.LinkedFolders.cs");
        var remove = Between(source, "if (remove >= 0)", "ImGui.SetNextItemWidth(");
        Assert.Contains("UndoToast.Show(", remove, StringComparison.Ordinal);
        Assert.Contains("Relink(linked, folder, at)", remove, StringComparison.Ordinal);
        Assert.Contains("folders.Insert(", Between(source, "private void Relink(", "\n}\n"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 9. Not for me in Loose ends

    [Fact]
    public void Loose_ends_offers_not_for_me_and_the_line_leaves_the_card()
    {
        var menu = Between(Source("CharactersPane.StoryRows.cs"), "private void DrawStoryRowMenu(", "private SetAsideActions? setAsideActions;");
        Assert.Contains("if (row.NotForMe && SetAside.CanSetAside)", menu, StringComparison.Ordinal);
        Assert.Contains("ImGui.MenuItem(Strings.BluesNotForMe)", menu, StringComparison.Ordinal);
        Assert.Contains("SetAside.SetAside(quest, notForMe: true);", menu, StringComparison.Ordinal);
        Assert.Contains("NotForMe = true,", Source("CharactersPane.LooseEnds.cs"), StringComparison.Ordinal);

        var source = Source("LooseEndsSource.cs");
        Assert.Contains("Find(all, session.States, session.ViewedSetAside)", source, StringComparison.Ordinal);
        Assert.Contains("LooseEnds.Find(all, states, bundle.Catalog, setAside)", source, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 10. The slot on keyboard focus

    [Fact]
    public void Board_row_slots_show_on_keyboard_focus()
    {
        var draw = Between(Source("BoardRow.cs"), "public static BoardRowResult Draw(", "public const string MenuId");
        Assert.Contains("var shown = rowHovered || menuOpen || focusedRow == hoverKey;", draw, StringComparison.Ordinal);
        Assert.Contains("focused |= ImGui.IsItemFocused();", draw, StringComparison.Ordinal);

        // The buttons are submitted whether shown or not (so Tab reaches them), only painted away while hidden.
        Assert.DoesNotContain("if (shown && teleport is", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("if (shown)\n                {\n                    var more", draw, StringComparison.Ordinal);
        Assert.Contains("dl.PushClipRect(min, min, false);", draw, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 11. Opponents through the people mask

    [Fact]
    public void Triad_opponents_go_through_the_people_and_place_mask()
    {
        var build = Between(Source("TriadBoardSource.cs"), "private Row Build(TriadRow row,", "private (SpoilerKind Kind, string Name, QuestRecord? Quest, bool StandIn)? ShieldOf(");
        Assert.Contains("Capitalize(spoilers.Name(SpoilerKind.Npc, opponent.Name))", build, StringComparison.Ordinal);
        Assert.Contains("spoilers.Name(SpoilerKind.Area, opponent.Zone)", build, StringComparison.Ordinal);
        Assert.DoesNotContain("var place = opponent.Zone;", build, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ Core review follow-ups

    [Fact]
    public void Linked_folders_are_scanned_one_by_one_and_labels_have_room_for_their_bytes()
    {
        var pass = Between(GameSource("LinkedFolderService.cs"), "private void Pass()", "private void Warn(");
        Assert.Contains("folders.ScanAll(list, folder =>", pass, StringComparison.Ordinal);
        Assert.DoesNotContain("folders.Take(", pass, StringComparison.Ordinal);

        Assert.Contains("ref labelText, CharacterSettings.MaxLabelBytes,", Source("CharactersPane.Roster.cs"), StringComparison.Ordinal);
        Assert.Contains("board.IsForgotten(other)", Source("CharactersPane.Goal.cs"), StringComparison.Ordinal);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
