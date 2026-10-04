using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Journal;

/// <summary>How much room the journal has left (feature plan v7, C9).</summary>
public enum JournalRoom : byte
{
    /// <summary>More than <see cref="JournalSlots.NearLeft"/> slots free.</summary>
    Room,

    /// <summary><see cref="JournalSlots.NearLeft"/> slots free or fewer: worth knowing before picking up more.</summary>
    Near,

    /// <summary>No slot free: the game refuses another quest until one is handed in or abandoned.</summary>
    Full,
}

/// <summary>
/// The quest journal's slots (feature plan v7, C9): the game holds at most <see cref="GameCap"/> accepted quests
/// (<c>QuestManager.NormalQuests</c> has that many slots; allied society dailies and levequests are kept apart). Said
/// as what is left ("3 journal slots left", "Journal full"), never as a tally. Pure.
/// </summary>
/// <param name="Used">Slots holding a quest.</param>
/// <param name="Cap">Slots the journal has.</param>
public readonly record struct JournalSlots(int Used, int Cap)
{
    /// <summary>The game's journal size: <c>QuestManager.NormalQuests</c> holds 30 quests.</summary>
    public const int GameCap = 30;

    /// <summary>At this many free slots or fewer the journal reads <see cref="JournalRoom.Near"/>.</summary>
    public const int NearLeft = 3;

    /// <summary>Free slots, never below 0.</summary>
    public int Left => Math.Max(0, Cap - Used);

    public JournalRoom Room => Left == 0 ? JournalRoom.Full : Left <= NearLeft ? JournalRoom.Near : JournalRoom.Room;

    /// <summary>
    /// The character's slots: the client's own count when the capture has it (<see cref="CharacterSnapshot.JournalSlotsUsed"/>),
    /// else the journal's quests less the allied society dailies, which the game keeps in their own array.
    /// </summary>
    public static JournalSlots Of(CharacterSnapshot snapshot, QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        if (snapshot.JournalSlotsUsed is { } used)
        {
            return new JournalSlots(used, GameCap);
        }

        var count = 0;
        foreach (var accepted in snapshot.Accepted)
        {
            if (catalog.GetByRowId(0x10000u | accepted.QuestId) is not { IsAlliedSocietyDaily: true })
            {
                count++;
            }
        }

        return new JournalSlots(count, GameCap);
    }

    /// <summary>"Journal full", "1 journal slot left", "12 journal slots left".</summary>
    public string Text => Left switch
    {
        0 => CoreText.T("Core.Journal.Full", "Journal full"),
        1 => CoreText.T("Core.Journal.OneLeft", "1 journal slot left"),
        var n => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Journal.Left", "{0} journal slots left"), n),
    };
}
