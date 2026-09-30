using System;

namespace Tsukimichi.Localization;

/// <summary>
/// A label composed from localized strings (a button text with its ImGui id, a line joined from two strings) that is
/// drawn every frame: composed once, and again only after a language switch (<see cref="Loc.Version"/>), so the draw
/// path does not allocate. Framework thread only.
/// </summary>
public sealed class LocText
{
    private readonly Func<string> build;
    private int version = -1;
    private string text = string.Empty;

    public LocText(Func<string> build) => this.build = build;

    public string Value
    {
        get
        {
            if (version != Loc.Version)
            {
                text = build();
                version = Loc.Version;
            }

            return text;
        }
    }

    public override string ToString() => Value;
}

/// <summary>
/// An array of localized strings (tab labels, column headers, combo items) composed once and again only after a
/// language switch (<see cref="Loc.Version"/>). Framework thread only.
/// </summary>
public sealed class LocArray
{
    private readonly Func<string[]> build;
    private int version = -1;
    private string[] items = [];

    public LocArray(Func<string[]> build) => this.build = build;

    public string[] Value
    {
        get
        {
            if (version != Loc.Version)
            {
                items = build();
                version = Loc.Version;
            }

            return items;
        }
    }
}
