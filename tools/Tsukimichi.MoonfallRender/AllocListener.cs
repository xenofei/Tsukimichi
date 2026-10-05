using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace Tsukimichi.MoonfallRender;

/// <summary>The runtime's allocation ticks (one every ~100 KB) by type, for --alloc: where a frame's allocations come from.</summary>
internal sealed class AllocListener : EventListener
{
    public ConcurrentDictionary<string, int> Types { get; } = new(StringComparer.Ordinal);

    public bool On { get; set; }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)0x1);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (On && eventData.EventName is { } name && name.StartsWith("GCAllocationTick", StringComparison.Ordinal) && eventData.Payload is { } p && eventData.PayloadNames is { } names)
        {
            var i = names.IndexOf("TypeName");
            var type = i >= 0 ? p[i]?.ToString() ?? "?" : "?";
            Types.AddOrUpdate(type, 1, static (_, n) => n + 1);
        }
    }
}
