#if UNITY_EDITOR
using System.Collections.Generic;
using DrWario.Runtime;

namespace DrWario.Editor.UI
{
    public enum TimelineEventType { FrameSpike, GCAlloc, BootStage, AssetLoad, NetworkEvent }

    public struct TimelineEvent
    {
        public int FrameIndex;
        public float Timestamp;
        public float Duration;
        public TimelineEventType EventType;
        public string Label;
        public float Metric;
    }

    public static class TimelineEventBuilder
    {
        public static List<TimelineEvent> Build(ProfilingSession session)
        {
            var events = new List<TimelineEvent>();
            if (session == null || session.FrameCount == 0)
                return events;

            // Determine target frame time
            int targetFps = session.Metadata.TargetFrameRate;
            float targetMs = targetFps > 0 ? 1000f / targetFps : 16.67f;
            float spikeThreshold = targetMs * 1.5f;

            var frames = session.GetFrames();

            // Frame spikes
            for (int i = 0; i < frames.Length; i++)
            {
                var f = frames[i];

                if (f.CpuFrameTimeMs > spikeThreshold)
                {
                    events.Add(new TimelineEvent
                    {
                        FrameIndex = i,
                        Timestamp = f.Timestamp,
                        Duration = f.CpuFrameTimeMs / 1000f,
                        EventType = TimelineEventType.FrameSpike,
                        Label = $"Frame spike: {f.CpuFrameTimeMs:F1}ms",
                        Metric = f.CpuFrameTimeMs
                    });
                }

                // GC allocations > 1KB
                if (f.GcAllocBytes > 1024)
                {
                    float kb = f.GcAllocBytes / 1024f;
                    events.Add(new TimelineEvent
                    {
                        FrameIndex = i,
                        Timestamp = f.Timestamp,
                        Duration = 0f,
                        EventType = TimelineEventType.GCAlloc,
                        Label = $"GC alloc: {kb:F1} KB",
                        Metric = kb
                    });
                }
            }

            // Boot stages
            float bootTimestamp = frames.Length > 0 ? frames[0].Timestamp : 0f;
            foreach (var stage in session.BootStages)
            {
                events.Add(new TimelineEvent
                {
                    FrameIndex = 0,
                    Timestamp = bootTimestamp,
                    Duration = stage.DurationMs / 1000f,
                    EventType = TimelineEventType.BootStage,
                    Label = $"Boot: {stage.StageName} ({stage.DurationMs}ms){(stage.Success ? "" : " FAILED")}",
                    Metric = stage.DurationMs
                });
                bootTimestamp += stage.DurationMs / 1000f;
            }

            // Asset loads
            foreach (var load in session.AssetLoads)
            {
                events.Add(new TimelineEvent
                {
                    FrameIndex = 0,
                    Timestamp = 0f,
                    Duration = load.DurationMs / 1000f,
                    EventType = TimelineEventType.AssetLoad,
                    Label = $"Asset: {load.AssetKey} ({load.DurationMs}ms)",
                    Metric = load.DurationMs
                });
            }

            // Network events
            foreach (var net in session.NetworkEvents)
            {
                events.Add(new TimelineEvent
                {
                    FrameIndex = 0,
                    Timestamp = net.Timestamp,
                    Duration = net.LatencyMs / 1000f,
                    EventType = TimelineEventType.NetworkEvent,
                    Label = $"Net {net.Type}: {net.Bytes}B{(net.LatencyMs > 0 ? $" ({net.LatencyMs:F0}ms)" : "")}",
                    Metric = net.LatencyMs
                });
            }

            // Sort by timestamp
            events.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return events;
        }
    }
}
#endif
