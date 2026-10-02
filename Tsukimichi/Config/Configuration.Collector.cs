using Tsukimichi.Core.Chains;

namespace Tsukimichi.Config;

/// <summary>
/// 1.9.0 collector extras (feature plan v5): the free-trial view and the story recap's length. Drawn under Settings ›
/// Display › Free trial and story recap (<c>Ui/ConfigWindow.Collector.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// "I'm on the free trial" (R9 F3): the quest counts, My blues and the quest table fold what the trial does not
    /// include (<see cref="Core.Query.FreeTrial"/>) into "Beyond your trial" instead of reading it as Blocked. Off by
    /// default; Tsukimichi never turns it on by itself, since whether an account is a trial is not certain from the
    /// game's state.
    /// </summary>
    public bool FreeTrialView { get; set; }

    /// <summary>How many main scenario quests the story recap reads ("Previously…", R9 F5); 3–40, 10 by default.</summary>
    public int RecapLength { get; set; } = StoryRecap.DefaultLength;

    /// <summary><see cref="RecapLength"/> within the allowed bounds.</summary>
    public int RecapLengthClamped => StoryRecap.ClampLength(RecapLength);
}
