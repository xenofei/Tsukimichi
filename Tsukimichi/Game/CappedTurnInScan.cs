using System;
using System.Collections.Generic;
using Tsukimichi.Core.Jobs;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// The viewed character's journal quests to turn in on a job that isn't capped (feature plan v7, 1.19.0, C8;
/// <see cref="CappedTurnIns"/>), read by the Todo overlay's row and the optional chat line. A stored character gets
/// them too, from the job, levels and cap its last capture holds (the job it logged out on is the one it would hand in
/// on); a capture without the level cap or the job gives none. Framework thread.
/// </summary>
public static class CappedTurnInScan
{
    /// <summary>The viewed character's capped turn-ins in journal order; empty before the catalog or a snapshot.</summary>
    public static List<CappedTurnIn> Viewed(SessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Bundle is not { } bundle || session.ViewedSnapshot is not { } snapshot
            ? []
            : CappedTurnIns.Find(bundle.Catalog, snapshot, bundle.ExpTable, session.Context, IsLimited(bundle));
    }

    /// <summary>The bundle's limited jobs (Blue Mage, Beastmaster), which are never the job to hand in on.</summary>
    public static Func<byte, bool> IsLimited(CatalogBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return job =>
        {
            foreach (var info in bundle.Names.ClassJobInfos)
            {
                if (info.RowId == job)
                {
                    return info.IsLimited;
                }
            }

            return false;
        };
    }
}
