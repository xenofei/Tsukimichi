using System;
using System.Collections.Generic;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Text;

namespace Tsukimichi.Game;

/// <summary>
/// "Say what's next in chat" (plan v7, 1.21.0 P8): after the logged-in character finishes a step ("Step done. Next:
/// step 4. …") or a quest ("Quest done. Next: …", the <c>/tsuki next</c> line), one plain line for text-to-speech, at
/// most one every 10 s (<see cref="GuidanceThrottle"/>), while <see cref="Config.Configuration.SayWhatsNext"/> is on (off
/// by default). The journal is compared capture to capture (<see cref="StepProgress"/>); the first capture of a character
/// is a baseline, and the baseline moves while the setting is off, so turning it on never replays old steps.
/// </summary>
public sealed partial class ChatNotifier
{
    private readonly GuidanceThrottle whatsNextThrottle = new();
    private CharacterSnapshot? whatsNextSnapshot;
    private IReadOnlyList<AcceptedQuest>? whatsNextJournal;
    private ulong whatsNextCharacter;

    /// <summary>The <c>/tsuki next</c> line (<c>GuidanceCommand.NextLine</c>); null prints nothing.</summary>
    public Func<GuidanceLine?>? NextLine { get; set; }

    /// <summary>The "Step done" line for a quest id (<c>GuidanceCommand.StepDoneLine</c>); null prints nothing.</summary>
    public Func<ushort, GuidanceLine?>? StepDoneLine { get; set; }

    private void AnnounceWhatsNext()
    {
        if (session.LiveSnapshot is not { } snapshot || ReferenceEquals(snapshot, whatsNextSnapshot))
        {
            return;
        }

        var before = snapshot.ContentId == whatsNextCharacter ? whatsNextJournal : null;
        whatsNextSnapshot = snapshot;
        whatsNextJournal = snapshot.Accepted;
        whatsNextCharacter = snapshot.ContentId;
        if (before is null || !config.SayWhatsNext
            || StepProgress.Finished(before, snapshot.Accepted, snapshot.IsCompleted) is not { } finish)
        {
            return;
        }

        var line = finish.QuestFinished
            ? NextLine?.Invoke() is { } next ? GuidanceText.QuestDone(next) : null
            : StepDoneLine?.Invoke(finish.QuestId);
        if (line is not null && whatsNextThrottle.TryTake(Environment.TickCount64))
        {
            links.PrintGuidance(line);
        }
    }
}
