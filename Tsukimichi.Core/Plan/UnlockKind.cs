using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// What an unlock quest opens, as the "Clear my blues" plan (P3) tags it. The declaration order is the precedence: a
/// quest carrying several kinds lists them in this order, and the first is its primary kind (the pill drawn first, the
/// kind a one-word hint names). Duty content comes first because it is what a player queues for and what roulettes
/// need (Dungeon, Trial, NormalRaid, AllianceRaid, then FieldOperation, the instanced zones Eureka, Bozja and the
/// Occult Crescent); then a class or job, an allied society, flying, a game system, and <see cref="Other"/> last,
/// only when nothing more specific is known.
/// </summary>
public enum UnlockKind : byte
{
    Dungeon,
    Trial,
    NormalRaid,
    AllianceRaid,
    FieldOperation,
    Job,
    Society,
    Flying,
    System,
    Other,
}

/// <summary>Names and bit masks over <see cref="UnlockKind"/>; the English names are what the checklist prints.</summary>
public static class UnlockKinds
{
    /// <summary>Every kind, in precedence order.</summary>
    public static readonly UnlockKind[] All =
    [
        UnlockKind.Dungeon, UnlockKind.Trial, UnlockKind.NormalRaid, UnlockKind.AllianceRaid, UnlockKind.FieldOperation,
        UnlockKind.Job, UnlockKind.Society, UnlockKind.Flying, UnlockKind.System, UnlockKind.Other,
    ];

    /// <summary>The mask with every kind set: no kind filter.</summary>
    public const ushort AllMask = (1 << 10) - 1;

    /// <summary>The kind's bit in a mask.</summary>
    public static ushort Bit(UnlockKind kind) => (ushort)(1 << (int)kind);

    /// <summary>A mask of the given kinds.</summary>
    public static ushort Mask(params UnlockKind[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ushort mask = 0;
        foreach (var kind in kinds)
        {
            mask |= Bit(kind);
        }

        return mask;
    }

    /// <summary>The display name: "Dungeon", "Alliance raid", "Field operation", ….</summary>
    public static string Name(UnlockKind kind) => kind switch
    {
        UnlockKind.Dungeon => CoreText.T("Core.Unlock.Dungeon", "Dungeon"),
        UnlockKind.Trial => CoreText.T("Core.Unlock.Trial", "Trial"),
        UnlockKind.NormalRaid => CoreText.T("Core.Unlock.NormalRaid", "Raid"),
        UnlockKind.AllianceRaid => CoreText.T("Core.Unlock.AllianceRaid", "Alliance raid"),
        UnlockKind.FieldOperation => CoreText.T("Core.Unlock.FieldOperation", "Field operation"),
        UnlockKind.Job => CoreText.T("Core.Unlock.Job", "Job"),
        UnlockKind.Society => CoreText.T("Core.Unlock.Society", "Allied society"),
        UnlockKind.Flying => CoreText.T("Core.Unlock.Flying", "Flying"),
        UnlockKind.System => CoreText.T("Core.Unlock.System", "System"),
        _ => CoreText.T("Core.Unlock.Other", "Other"),
    };
}
