using System.Collections.Generic;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Ui;

/// <summary>
/// The user's unique-reward verdicts (<c>user/overrides.json</c>) as the detail pane and the settings window see
/// them: one optional <see cref="UniqueOverride"/> per quest row id. Owned by the Moonlit pane, which implements this
/// and is attached through <see cref="MainWindow.AttachOverrides"/> and <see cref="ConfigWindow.Overrides"/>.
/// </summary>
public interface IUniqueOverrides
{
    /// <summary>Every stored verdict by quest row id. Read only; do not keep across frames without checking <see cref="Version"/>.</summary>
    IReadOnlyDictionary<uint, UniqueOverride> All { get; }

    /// <summary>Increments on every change (set, clear, clear all, reload) so caches keyed on it can refresh.</summary>
    int Version { get; }

    /// <summary>The user's verdict for a quest, or null when the shipped data applies.</summary>
    UniqueOverride? Get(uint rowId);

    /// <summary>Removes the user's verdict so the shipped data applies again.</summary>
    void Clear(uint rowId);

    /// <summary>Removes every verdict.</summary>
    void ClearAll();

    /// <summary>Marks a quest unique (a note names the reward) or not unique (hidden from Moonlit).</summary>
    void Set(uint rowId, bool unique, string? note);

    /// <summary>
    /// Puts verdicts back exactly as they were, dates included, in one save: the Undo of a verdict, Restore, Restore
    /// all and a note edit (feature plan v6 S2).
    /// </summary>
    void PutBack(IReadOnlyCollection<KeyValuePair<uint, UniqueOverride>> verdicts);
}
