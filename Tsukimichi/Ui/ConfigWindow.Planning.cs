using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Journal › Table and Tree (feature plan v6 U7; the columns from 1.9.0, R6 E and G): the quest table's row
/// height, the EXP column (off by default), the Unlocks column (on by default), and the Removed from the game node; and
/// Settings › Characters &amp; data › Dashboard: the allied society board (on by default).
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray DensityOptions = new(static () => [Strings.ConfigDensityComfortable, Strings.ConfigDensityDense]);

    private void DrawJournalTable()
    {
        Header(Strings.SettingsTableHeading);

        // Density: the quest table's row height only (T12).
        var density = settings.Density == RowDensity.Dense ? 1 : 0;
        if (Choice(Strings.ConfigDensity, Strings.ConfigDensityHint, ref density, DensityOptions.Value, "comfortable dense row height compact density"))
        {
            settings.Density = density == 1 ? RowDensity.Dense : RowDensity.Comfortable;
            Save();
        }

        var exp = settings.JournalShowExpColumn;
        if (Toggle(Strings.PlanningConfigExpColumn, Strings.PlanningConfigExpColumnHint, ref exp, "exp experience column journal table reward"))
        {
            settings.JournalShowExpColumn = exp;
            Save();
        }

        var opens = settings.JournalShowOpensColumn;
        if (Toggle(Strings.PlanningConfigOpensColumn, Strings.PlanningConfigOpensColumnHint, ref opens, "opens unlocks column journal table area duty aetheryte"))
        {
            settings.JournalShowOpensColumn = opens;
            Save();
        }
    }

    /// <summary>Settings › Journal › Tree: the Removed from the game node.</summary>
    private void DrawJournal()
    {
        Header(Strings.SettingsTreeHeading);
        var unlisted = settings.ShowUnlisted;
        if (Toggle(Strings.ConfigShowUnlisted, Strings.ConfigShowUnlistedHint, ref unlisted, "removed deleted quests tree"))
        {
            settings.ShowUnlisted = unlisted;
            Save();
            onShowUnlistedChanged(unlisted);
        }
    }

    /// <summary>Settings › Characters &amp; data › Dashboard: the allied society board.</summary>
    private void DrawDashboard()
    {
        Header(Strings.PlanningConfigHeading);
        var board = settings.ShowAlliedSocietyBoard;
        if (Toggle(Strings.PlanningConfigBoard, Strings.PlanningConfigBoardHint, ref board, "allied society beast tribe daily board reset allowances dashboard"))
        {
            settings.ShowAlliedSocietyBoard = board;
            Save();
        }
    }
}
