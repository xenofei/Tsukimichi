using Tsukimichi.Core.Storage;

namespace Tsukimichi.Ui;

/// <summary>
/// The user's unique-reward verdicts (<c>user/overrides.json</c>) as the detail pane sees them: one optional
/// <see cref="UniqueOverride"/> per quest row id. Owned by the Moonlit pane; the plugin attaches an adapter through
/// <see cref="MainWindow.AttachOverrides"/> once that pane exists.
/// </summary>
public interface IUniqueOverrides
{
    /// <summary>The user's verdict for a quest, or null when the shipped data applies.</summary>
    UniqueOverride? Get(uint rowId);

    /// <summary>Removes the user's verdict so the shipped data applies again.</summary>
    void Clear(uint rowId);

    /// <summary>Marks a quest unique (a note names the reward) or not unique (hidden from Moonlit).</summary>
    void Set(uint rowId, bool unique, string? note);
}
