namespace Tsukimichi.Core.Ui;

/// <summary>The unit an age is told in.</summary>
public enum AgeUnit
{
    /// <summary>Under a minute: "just now".</summary>
    JustNow,
    Minutes,
    Hours,
    Days,
}

/// <summary>
/// The one rule for "how long ago" (1.8.0, R7 F6): under a minute "just now", under an hour in minutes, under a day in
/// hours, then in whole days. Every surface that says how old a save is (the character switcher, the Characters list
/// and dashboard, the stale banner) tells it the same way, so one character never reads "47 h ago" in one place and
/// "1 d ago" in another. A time in the future (another client's clock a little ahead) reads "just now".
/// </summary>
public static class AgeText
{
    /// <summary>The unit and the whole count of it for an age (<paramref name="nowUtc"/> minus <paramref name="thenUtc"/>).</summary>
    public static (AgeUnit Unit, int Value) Of(DateTime thenUtc, DateTime nowUtc)
    {
        var age = nowUtc - thenUtc;
        if (age < TimeSpan.FromMinutes(1))
        {
            return (AgeUnit.JustNow, 0);
        }

        if (age < TimeSpan.FromHours(1))
        {
            return (AgeUnit.Minutes, (int)age.TotalMinutes);
        }

        if (age < TimeSpan.FromDays(1))
        {
            return (AgeUnit.Hours, (int)age.TotalHours);
        }

        return (AgeUnit.Days, (int)Math.Min(age.TotalDays, int.MaxValue));
    }
}
