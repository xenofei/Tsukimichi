namespace Tsukimichi.Ui;

/// <summary>
/// "New chapters" in the Tonight card (spec-1.22 W4, decision 18): the 1.21 line of side stories that gained quests
/// this patch, moved here from the retired What's new card. One line in Tonight's lines block, once per patch and per
/// character, closed with ×; no new notice or float (<see cref="NewChaptersSource"/>).
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>The New chapters line; set by the plugin. Null draws none.</summary>
    public NewChaptersSource? NewChapters { get; set; }

    private void DrawNewChapters()
    {
        if (NewChapters is not { Visible: true } chapters)
        {
            return;
        }

        Chrome.Hairline();
        chapters.Draw();
    }
}
