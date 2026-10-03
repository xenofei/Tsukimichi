namespace Tsukimichi.Core.Companions;

/// <summary>
/// When the companion plugins' settings are read again (feature plan v6 A11): only while someone wants them, and then
/// when the plugin list changed, when a companion's file changed and has been quiet for <c>settleMs</c> (a plugin that
/// saves several times in a row, or through a temporary file, costs one read and is never read half-written; an
/// explicit invalidation has no change time and is due at once), or when the last read is older than the backstop
/// interval (the settings read through IPC, which no watcher sees). Times are <see cref="Environment.TickCount64"/>
/// milliseconds. Pure, so the plugin's service and the tests share it.
/// </summary>
public static class SetupReadSchedule
{
    /// <param name="wanted">Someone read the settings since the last read.</param>
    /// <param name="listMoved">Dalamud's plugin list changed since the last read.</param>
    /// <param name="stale">A companion's file changed (or the settings were invalidated) since the last read.</param>
    /// <param name="now">Now.</param>
    /// <param name="changedAt">When the file last changed; 0 for an invalidation, which waits for nothing.</param>
    /// <param name="readAt">When the settings were last read.</param>
    /// <param name="intervalMs">The backstop interval.</param>
    /// <param name="settleMs">How long a changed file must stay quiet.</param>
    public static bool IsDue(bool wanted, bool listMoved, bool stale, long now, long changedAt, long readAt, long intervalMs, long settleMs)
    {
        if (!wanted)
        {
            return false;
        }

        if (listMoved)
        {
            return true;
        }

        if (stale)
        {
            return changedAt == 0 || now - changedAt >= settleMs;
        }

        return now - readAt >= intervalMs;
    }
}
