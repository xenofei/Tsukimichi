using Tsukimichi.Core.Unique;

namespace Tsukimichi.Config;

/// <summary>
/// The Moonlit table's column sort (owner request after 1.22.0): the column and direction its headers last asked for,
/// restored on load as the quest table's <see cref="SortColumn"/> and <see cref="SortDescending"/> are.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>Last Moonlit table sort column; <see cref="MoonlitSortColumn.Default"/> is the usual order.</summary>
    public MoonlitSortColumn MoonlitSortColumn { get; set; } = MoonlitSortColumn.Default;

    /// <summary>Whether the Moonlit table's sort column sorts descending.</summary>
    public bool MoonlitSortDescending { get; set; }

    /// <summary>The persisted Moonlit sort as one value.</summary>
    public MoonlitSortSpec MoonlitSort() => new(MoonlitSortColumn, MoonlitSortDescending);
}
