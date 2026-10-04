using System;
using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Go with the game" and "Use Tsukimichi's answer" (feature plan v7 C1; spec-1.19 "When the game disagrees"): the
/// player's per-character choice, kept in <c>user/characters.json</c> beside the C3 gate marks
/// (<see cref="CharacterSettings.GoWithGame"/>), for the character on view. Either click takes effect at once (the
/// session re-resolves that character, live or stored) and offers the floating Undo for 8 s. Framework thread only.
/// </summary>
public sealed class GameAnswerActions
{
    private readonly SessionState session;
    private readonly CharacterSettingsBook book;

    public GameAnswerActions(SessionState session, CharacterSettingsBook book)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.book = book ?? throw new ArgumentNullException(nameof(book));
    }

    /// <summary>Whether the player went with the game on <paramref name="quest"/> for the character on view.</summary>
    public bool IsChosen(QuestRecord quest) =>
        session.ViewedContentId is { } contentId && book.IsGoWithGame(contentId, quest.RowId);

    /// <summary>"Use Tsukimichi's answer" applies: the quest on view reads Ready because the player went with the game.</summary>
    public bool CanTakeBack(QuestRecord quest, QuestEvaluation? evaluation) =>
        evaluation is { ByGame: GameAnswer.Override } || IsChosen(quest);

    /// <summary>Go with the game for the character on view, with Undo.</summary>
    public void GoWithGame(QuestRecord quest) => Set(quest, true, Strings.GoWithGameToastFormat);

    /// <summary>Use Tsukimichi's answer again for the character on view, with Undo.</summary>
    public void UseOwnAnswer(QuestRecord quest) => Set(quest, false, Strings.UseOwnAnswerToastFormat);

    private void Set(QuestRecord quest, bool withGame, string toastFormat)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (session.ViewedContentId is not { } contentId)
        {
            return;
        }

        var rowId = quest.RowId;
        book.Edit(CharacterSettingChange.GoWithGame(contentId, rowId, withGame));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, toastFormat, session.Spoilers.DisplayName(quest)),
            () => book.Edit(CharacterSettingChange.GoWithGame(contentId, rowId, !withGame)));
    }
}
