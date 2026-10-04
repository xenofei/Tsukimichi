namespace Tsukimichi.Ui;

/// <summary>
/// "Before Evercold" on the Characters dashboard (feature plan v7, 1.20.0, N7): the viewed character's checklist for
/// Patch 8.0 (<see cref="BeforeEvercoldCard"/>) as a section above the journal's room, kept while the card is hidden
/// from Tonight so it can be shown again there. Gone by itself on Evercold's data or date.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The Before Evercold card; set by the plugin. Null draws nothing.</summary>
    public BeforeEvercoldCard? BeforeEvercold { get; set; }

    private void DrawBeforeEvercold(UiState ui)
    {
        if (BeforeEvercold is not { Active: true } card)
        {
            return;
        }

        card.DrawSection(ui);
        Gap();
    }
}
