using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the three Decoration looks of 1.13.0 (docs/design/flair-v13): the ledger's labels, the looks' one-line
/// descriptions in Settings and the live preview's sample. English only until localization reopens.
/// </summary>
static partial class Strings
{
    /// <summary>The ledger's first key (Plain's detail pane): the quest's state.</summary>
    public static string LedgerState => Loc.Get("LedgerState");

    /// <summary>The ledger's second key: the expansion, level and job line.</summary>
    public static string LedgerLevel => Loc.Get("LedgerLevel");

    /// <summary>Settings › General › Look › Decoration: what Full draws.</summary>
    public static string ConfigFlairFullNote => Loc.Get("ConfigFlairFullNote");

    /// <summary>Settings › General › Look › Decoration: what Quiet draws.</summary>
    public static string ConfigFlairQuietNote => Loc.Get("ConfigFlairQuietNote");

    /// <summary>Settings › General › Look › Decoration: what Plain draws.</summary>
    public static string ConfigFlairPlainNote => Loc.Get("ConfigFlairPlainNote");

    /// <summary>The live preview's sample quests, one per row (names of real quests, as the chosen look lists them).</summary>
    public static string ConfigFlairPreviewReady => Loc.Get("ConfigFlairPreviewReady");

    public static string ConfigFlairPreviewJournal => Loc.Get("ConfigFlairPreviewJournal");

    public static string ConfigFlairPreviewDone => Loc.Get("ConfigFlairPreviewDone");

    /// <summary>The live preview's sample card heading.</summary>
    public static string ConfigFlairPreviewCard => Loc.Get("ConfigFlairPreviewCard");

    /// <summary>The live preview's sample card line.</summary>
    public static string ConfigFlairPreviewCardLine => Loc.Get("ConfigFlairPreviewCardLine");
}
