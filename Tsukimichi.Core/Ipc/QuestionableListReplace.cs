namespace Tsukimichi.Core.Ipc;

/// <summary>How "Replace Questionable's list…" ended.</summary>
public enum QuestionableReplaceOutcome
{
    /// <summary>The list was emptied and the new quests imported.</summary>
    Replaced,

    /// <summary>Questionable's list could not be read first, so it was left as it was and nothing was sent.</summary>
    NotRead,

    /// <summary>The import failed; the list as it was before has been imported again.</summary>
    FailedRestored,

    /// <summary>The import failed and putting the old list back failed too.</summary>
    FailedNotRestored,
}

/// <summary>
/// The order of "Replace Questionable's list…" (feature plan v5, 1.6.0), kept apart from the IPC so it can be tested:
/// read the list (<c>ExportQuestPriority</c>), empty it (<c>ClearQuestPriority</c>), import the new quests
/// (<c>ImportQuestPriority</c>). The list is read before anything is cleared, and nothing is cleared when it cannot be
/// read; when the clear or the import throws or answers false, the list is emptied again and the saved one imported,
/// so the player's list is never lost silently. Questionable's import appends what is not on the list yet
/// (<c>QuestPriorityManager.Import</c>), so the restore keeps the old order; its accept-only marks are not part of the
/// exported text and cannot come back.
/// </summary>
public static class QuestionableListReplace
{
    /// <summary>
    /// Runs the steps. <paramref name="export"/> returns the encoded list (null when it cannot be read),
    /// <paramref name="clear"/> and <paramref name="import"/> whether Questionable took the call. Every exception is
    /// handed to <paramref name="failed"/> and never escapes.
    /// </summary>
    public static QuestionableReplaceOutcome Run(string encoded, Func<string?> export, Func<bool> clear, Func<string, bool> import, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(clear);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(failed);

        string? saved;
        try
        {
            saved = export();
        }
        catch (Exception ex)
        {
            failed(ex);
            saved = null;
        }

        if (saved is null)
        {
            return QuestionableReplaceOutcome.NotRead;
        }

        bool replaced;
        try
        {
            replaced = clear() && import(encoded);
        }
        catch (Exception ex)
        {
            failed(ex);
            replaced = false;
        }

        if (replaced)
        {
            return QuestionableReplaceOutcome.Replaced;
        }

        try
        {
            // Empty first so a half-done import does not stay mixed into the old list. A clear that fails again is no
            // reason to skip the import: it appends only what is missing, so an untouched list stays as it was.
            clear();
        }
        catch (Exception ex)
        {
            failed(ex);
        }

        try
        {
            return string.IsNullOrWhiteSpace(saved) || import(saved) ? QuestionableReplaceOutcome.FailedRestored : QuestionableReplaceOutcome.FailedNotRestored;
        }
        catch (Exception ex)
        {
            failed(ex);
            return QuestionableReplaceOutcome.FailedNotRestored;
        }
    }
}
