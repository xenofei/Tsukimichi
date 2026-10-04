using System;
using System.Collections.Generic;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// The current step's objectives in the game's own words (plan v7, 1.21.0 P2 and P8): the <c>TEXT_&lt;ID&gt;_TODO_nn</c>
/// lines whose step (<c>Quest.TodoParams[nn].ToDoCompleteSeq</c>) is the sequence the quest is on, and no other, so a
/// later step's text is never shown before the character reaches it. Rendered like the Journal card's (the game's
/// evaluator for the logged-in character, neutrally for a stored one) and kept for the last few quests asked.
/// </summary>
public sealed partial class QuestTextService
{
    private const int StepCacheSize = 8;

    // Rendered step objectives, most recent last; keyed by everything the lines depend on.
    private readonly List<(StepKey Key, IReadOnlyList<(int Index, string Text)> Lines)> stepLines = [];

    private readonly record struct StepKey(uint RowId, byte Sequence, bool Live, string? Name, ulong ContentId);

    /// <summary>
    /// The objectives of step <paramref name="sequence"/> of <paramref name="quest"/> (its journal sequence, 255 the
    /// last step), each with its objective number (<c>TODO_nn</c>), in number order; empty for sequence 0, without text,
    /// or when the sheet cannot be read. The caller asks only for the sequence the character's quest is on.
    /// </summary>
    public IReadOnlyList<(int Index, string Text)> StepObjectives(QuestRecord quest, byte sequence, bool live, string? storedName, ulong contentId)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (sequence == 0)
        {
            return [];
        }

        var key = new StepKey(quest.RowId, sequence, live, live ? null : storedName, contentId);
        for (var i = stepLines.Count - 1; i >= 0; i--)
        {
            if (stepLines[i].Key == key)
            {
                return stepLines[i].Lines;
            }
        }

        var lines = ReadStep(quest, sequence, live, storedName);
        stepLines.Add((key, lines));
        if (stepLines.Count > StepCacheSize)
        {
            stepLines.RemoveAt(0);
        }

        return lines;
    }

    private IReadOnlyList<(int Index, string Text)> ReadStep(QuestRecord quest, byte sequence, bool live, string? storedName)
    {
        try
        {
            var sequences = QuestTextReader.ObjectiveSequences(data.Excel, quest.RowId, language);
            if (QuestTextReader.Read(files, quest.InternalId, language, sequences) is not { } text)
            {
                return [];
            }

            var lines = new List<(int Index, string Text)>();
            foreach (var line in text.Objectives)
            {
                if (line.Sequence != sequence)
                {
                    continue;
                }

                var rendered = Render(line.Text, live, storedName);
                if (rendered.Length > 0)
                {
                    lines.Add((line.Index, rendered));
                }
            }

            return lines;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Step objectives of quest {RowId} could not be read", quest.RowId);
            return [];
        }
    }
}
