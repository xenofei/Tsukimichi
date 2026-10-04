using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Ui;

/// <summary>
/// The alts work of 1.8.0 on the Characters tab (R7 B, E, G, H): the left column's state (search box, data center
/// runs, hidden characters), the switch between the dashboard and "Collection by character", and the dashboard's
/// status lines for a character that is not tracked or whose file another client saved and this one cannot read.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The list shows its search box from this many characters on.</summary>
    private const int ListSearchFrom = 5;

    // The left column (RefreshList): the search text, the key it was built for, the runs to draw, and the counts and
    // label of the "Show hidden" toggle.
    private string listSearch = string.Empty;
    private ListKey listKey;
    private List<ListGroup> listGroups = [];
    private int listTotal;
    private int listHiddenCount;
    private string listShowHiddenLabel = string.Empty;

    // The centre column's view: -1 the dashboard, 0 the collection grid, 1 All characters (1.21.0 P3).
    private int view = -1;

    /// <summary>
    /// The Dashboard / Collection by character / All characters switch at the top of the centre column, with the
    /// roster's caption on its right while the roster shows; true while the grid is chosen.
    /// </summary>
    private bool DrawViewSwitch()
    {
        Chrome.SegmentedControl("##charactersView", ref view, Strings.AltsViewDashboard, [Strings.AltsViewCollection, Strings.RosterView]);
        DrawRosterCaption();
        ImGui.Spacing();
        return view == 0;
    }

    /// <summary>
    /// Under the dashboard header, one line that is always reserved (feature plan v6, U4): "Not tracked" for a character
    /// the player chose not to track, and "not updating" with its reason for one whose file another game client saved
    /// and this client cannot read (1.8.0, R7 G), each cut to the room left with the whole text on hover. Ticking "Don't
    /// track" in the Actions row below therefore never moves the dashboard, or the box under the pointer.
    /// </summary>
    private void DrawStatusNotices(ulong contentId)
    {
        var origin = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var right = origin.X + room;
        var x = origin.X;
        if (!roster.Settings.IsTracked(contentId))
        {
            x = Chrome.StripSegment(x, origin.X, right, origin.Y, Strings.AltsUntrackedNotice, Theme.U32(Theme.Surface.TextSecondary), Strings.AltsDontTrackTooltip);
        }

        if (session.NotUpdating.TryGetValue(contentId, out var problem))
        {
            Chrome.StripSegment(x, origin.X, right, origin.Y, NotUpdatingTooltip(problem), Theme.U32(Theme.Surface.Text));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(room, ImGui.GetTextLineHeight()));
    }

    /// <summary>The character's not-updating reason as a <see cref="SharedLoad"/>, for the switcher in the main window too.</summary>
    internal static string NotUpdatingText(SharedLoad problem) => NotUpdatingTooltip(problem);
}
