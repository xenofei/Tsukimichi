using Tsukimichi.Core.Jobs;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The capped turn-in chat line (feature plan v7, 1.19.0, C8): "Turn in on a job that isn't capped: [quest]", once per
/// quest each time it reaches its turn-in step on a capped job (<see cref="CappedTurnInNotice"/>), for the logged-in
/// character, while <see cref="Config.Configuration.ChatNoticeCappedTurnIn"/> is on (off by default). The quests are the
/// Todo overlay's row (<see cref="CappedTurnInScan"/>), so the line says what the row says.
/// </summary>
public sealed partial class ChatNotifier
{
    private readonly CappedTurnInNotice cappedTurnIns = new();

    // The session version last scanned, so a change that is not a new capture does not rescan the journal.
    private int cappedScannedVersion = -1;

    private void AnnounceCappedTurnIns()
    {
        if (!config.ChatNoticeCappedTurnIn || !session.IsLive || session.LiveContentId is not { } live || live == 0
            || session.ViewedSnapshot is not { } snapshot || session.Version == cappedScannedVersion)
        {
            return;
        }

        cappedScannedVersion = session.Version;
        var (before, after) = Strings.SplitAtLink(Strings.CappedTurnInChatFormat, Strings.LinkSlot);
        foreach (var turnIn in cappedTurnIns.Fresh(live, snapshot, CappedTurnInScan.Viewed(session)))
        {
            Print(before, turnIn.Quest, after);
        }
    }
}
