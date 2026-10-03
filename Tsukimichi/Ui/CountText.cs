using System;
using System.Globalization;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// A label built from one count and a localized format ("3 on", "2 set", "17 more kinds ›"), rebuilt only when the
/// count or the language changes, so drawing it every frame allocates nothing. A format written for ImGui ("%d days")
/// is filled the same way. Framework thread only.
/// </summary>
public sealed class CountText
{
    private readonly Func<string> format;
    private int count = int.MinValue;
    private int language = -1;
    private string text = string.Empty;

    /// <param name="format">Reads the format each time the language changes: a .NET format with {0}, or a printf one with %d.</param>
    public CountText(Func<string> format) => this.format = format ?? throw new ArgumentNullException(nameof(format));

    /// <summary>The label for <paramref name="value"/>.</summary>
    public string For(int value)
    {
        if (value == count && language == Loc.Version)
        {
            return text;
        }

        count = value;
        language = Loc.Version;
        var f = format();
        text = f.Contains("%d", StringComparison.Ordinal)
            ? f.Replace("%d", value.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
            : string.Format(CultureInfo.CurrentCulture, f, value);
        return text;
    }
}
