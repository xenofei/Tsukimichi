using Tsukimichi.Core.Chains;

namespace Tsukimichi.Config;

/// <summary>
/// 1.21.0 storylines (feature plan v7 N8): the Loose ends finale notice and the overlay's Loose ends section. Both
/// are off by default (spec-1.21 decision 11); drawn under Settings › Alerts and Settings › Overlay.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// When a storyline's finale is Ready: one chat line and one Tonight line, once per finale, for a line the character
    /// started and never finished (<see cref="LooseEnds"/>). Off by default.
    /// </summary>
    public bool ChatNoticeStorylineFinale { get; set; } = LooseEnds.FinaleNoticeDefault;

    /// <summary>The todo overlay's Loose ends section: the storylines started and never finished, finales first. Off by default.</summary>
    public bool TodoShowLooseEnds { get; set; } = LooseEnds.OverlaySectionDefault;
}
