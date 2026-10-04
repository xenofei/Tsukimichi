namespace Tsukimichi;

/// <summary>
/// 1.20.0 "Before Evercold" wiring (feature plan v7, N7): one <see cref="Ui.BeforeEvercoldCard"/> for the viewed
/// character, shared by the Tonight card and the Characters dashboard. Called once from the constructor after both exist.
/// </summary>
public sealed partial class Plugin
{
    private void InitializeBeforeEvercold(Ui.CharactersPane charactersPane)
    {
        var card = new Ui.BeforeEvercoldCard(Session, CharacterBook);
        charactersPane.BeforeEvercold = card;
        mainWindow.AttachBeforeEvercold(card);
    }
}
