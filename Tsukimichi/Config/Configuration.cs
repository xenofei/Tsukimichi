using System;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Config;

/// <summary>
/// Plugin settings persisted by Dalamud (Newtonsoft under the hood). Window size and position are left to ImGui.
/// Load with <see cref="Load"/>, persist with <see cref="Save"/>.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 1;
    public const double MinPollIntervalSeconds = 0.5;
    public const double MaxPollIntervalSeconds = 5.0;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>How often the poller reads game state, clamped to 0.5–5 s when read.</summary>
    public double PollIntervalSeconds { get; set; } = 1.0;

    /// <summary>Print a chat line when a quest becomes available. Off by default; not wired in V1.</summary>
    public bool ChatNoticeNewlyAvailable { get; set; }

    /// <summary>Whether main scenario quests are included in those notices.</summary>
    public bool IncludeMsqInNotices { get; set; }

    /// <summary>Show quests with no journal genre outside the Unlisted node.</summary>
    public bool ShowUnlisted { get; set; }

    /// <summary>Last table filters, restored on load.</summary>
    public FilterSet Filters { get; set; } = new();

    /// <summary>Character the user chose to view explicitly; null follows the live character.</summary>
    public ulong? ViewedContentId { get; set; }

    /// <summary>Poll interval as a <see cref="TimeSpan"/> within the allowed bounds.</summary>
    public TimeSpan PollInterval
    {
        get
        {
            var seconds = double.IsFinite(PollIntervalSeconds)
                ? Math.Clamp(PollIntervalSeconds, MinPollIntervalSeconds, MaxPollIntervalSeconds)
                : 1.0;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    /// <summary>Reads the saved configuration or returns defaults when there is none or it is of another type.</summary>
    public static Configuration Load(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.Filters ??= new FilterSet();
        return config;
    }

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        Version = CurrentVersion;
        pluginInterface.SavePluginConfig(this);
    }
}
