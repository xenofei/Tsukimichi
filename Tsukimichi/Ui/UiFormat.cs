using System;
using System.Globalization;

namespace Tsukimichi.Ui;

/// <summary>Small formatting helpers shared by the panes. Everything returns a new string; call on change, not per frame.</summary>
public static class UiFormat
{
    /// <summary>Local clock time when the instant is today, else date and time.</summary>
    public static string Time(DateTime utc, DateTime? nowUtc = null)
    {
        var local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var today = (nowUtc ?? DateTime.UtcNow).ToLocalTime().Date;
        return local.ToString(local.Date == today ? Strings.TimeFormat : Strings.DateTimeFormat, CultureInfo.CurrentCulture);
    }

    /// <summary>"just now", "5 min ago", "3 h ago", "2 d ago".</summary>
    public static string Age(DateTime utc, DateTime? nowUtc = null)
    {
        var age = (nowUtc ?? DateTime.UtcNow) - utc;
        if (age < TimeSpan.FromMinutes(1))
        {
            return Strings.JustNow;
        }

        if (age < TimeSpan.FromHours(1))
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.MinutesAgoFormat, (int)age.TotalMinutes);
        }

        if (age < TimeSpan.FromDays(1))
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.HoursAgoFormat, (int)age.TotalHours);
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.DaysAgoFormat, (int)age.TotalDays);
    }

    public static string Count(int done, int total) =>
        string.Format(CultureInfo.CurrentCulture, Strings.CountFormat, done, total);
}
