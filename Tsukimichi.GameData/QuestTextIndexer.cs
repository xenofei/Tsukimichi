using System.Text;
using Lumina.Data;
using Tsukimichi.Core.Text;

namespace Tsukimichi.GameData;

/// <summary>
/// Builds the journal search index (P9, <see cref="JournalTextIndex"/>) from every quest's text sheet: the words of
/// its journal entries and objectives (every branch of every fork, no names; <see cref="QuestTextNeutral.Words"/>),
/// never its dialogue. Each sheet is read through <see cref="QuestTextFiles"/> and dropped once its words are taken,
/// so memory stays at the index's own size however many sheets are read. Meant for a background task: cancellation is
/// checked between quests and progress is reported every <see cref="ProgressEvery"/> quests.
/// </summary>
public static class QuestTextIndexer
{
    public const int ProgressEvery = 64;

    /// <summary>Indexes <paramref name="quests"/> (row id and script id) in <paramref name="language"/>.</summary>
    /// <param name="progress">Called with (quests done, quests in all) now and then, from the building thread.</param>
    public static JournalTextIndex Build(
        QuestTextFiles files,
        IReadOnlyList<(uint RowId, string InternalId)> quests,
        Language language,
        string gameVersion,
        Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(quests);
        var builder = new JournalTextIndex.Builder();
        var text = new StringBuilder(4096);
        var words = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < quests.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i % ProgressEvery == 0)
            {
                progress?.Invoke(i, quests.Count);
            }

            var (rowId, internalId) = quests[i];
            if (QuestTextReader.Read(files, internalId, language) is not { } questText || questText.IsEmpty)
            {
                continue;
            }

            text.Clear();
            foreach (var line in questText.Journal)
            {
                QuestTextNeutral.Words(line.Text, text);
            }

            foreach (var line in questText.Objectives)
            {
                QuestTextNeutral.Words(line.Text, text);
            }

            words.Clear();
            JournalTokenizer.AddWords(text.ToString(), words);
            builder.Add(rowId, words);
        }

        progress?.Invoke(quests.Count, quests.Count);
        return builder.Build(gameVersion, QuestTextFiles.LanguageSuffix(language));
    }
}
