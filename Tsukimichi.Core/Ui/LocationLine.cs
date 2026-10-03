namespace Tsukimichi.Core.Ui;

/// <summary>
/// The banner's location line (docs/design/v7/ui/spec-1.16.md §A5): the journal path and the level on one line over
/// the art, fitted by a label ladder rather than cut. It tries, in order:
/// <list type="number">
/// <item>the full path with the level ("Main Scenario (Heavensward) › Heavensward · Lv 54");</item>
/// <item>without the "Main Scenario (…) ›" prefix ("Heavensward · Lv 54");</item>
/// <item>the level alone ("Lv 54").</item>
/// </list>
/// It never ends in an ellipsis and never wraps, so no word is left alone over bright art. Pure, so it is tested
/// without ImGui; the hero builds the rungs once per selection.
/// </summary>
public static class LocationLine
{
    /// <summary>The separator between the path and the level.</summary>
    public const string LevelSeparator = " · ";

    /// <summary>
    /// The ladder's rungs for <paramref name="segments"/> (the journal path, outermost first) joined by
    /// <paramref name="pathSeparator"/>, and <paramref name="level"/> ("Lv 54"; empty for none): longest first, each
    /// distinct and non-empty. With no level the last rung is the path's last segment.
    /// </summary>
    public static string[] Rungs(IReadOnlyList<string> segments, string pathSeparator, string level)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(pathSeparator);
        level ??= string.Empty;
        var parts = new List<string>(segments.Count);
        foreach (var segment in segments)
        {
            if (!string.IsNullOrWhiteSpace(segment))
            {
                parts.Add(segment.Trim());
            }
        }

        var rungs = new List<string>(3);
        void Add(string rung)
        {
            if (rung.Length > 0 && !rungs.Contains(rung))
            {
                rungs.Add(rung);
            }
        }

        string WithLevel(string path) => path.Length == 0 ? level : level.Length == 0 ? path : path + LevelSeparator + level;
        if (parts.Count > 0)
        {
            Add(WithLevel(string.Join(pathSeparator, parts)));
            Add(WithLevel(parts[^1]));
        }

        Add(level.Length > 0 ? level : parts.Count > 0 ? parts[^1] : string.Empty);
        return [.. rungs];
    }

    /// <summary>
    /// The rung to draw: the first of <paramref name="widths"/> (the rungs' widths, longest first) that fits
    /// <paramref name="room"/> (with half a pixel's grace, as <see cref="LineFit.NeedsEllipsis"/>), else the last, the
    /// shortest, which is drawn whole and clipped by the banner rather than cut. -1 when there are none.
    /// </summary>
    public static int Fit(ReadOnlySpan<float> widths, float room)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            if (!LineFit.NeedsEllipsis(widths[i], room))
            {
                return i;
            }
        }

        return widths.Length - 1;
    }
}
