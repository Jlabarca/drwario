using System;

namespace DrWario.Runtime
{
    public struct FrameSample
    {
        // Timing
        public float Timestamp;
        public float DeltaTime;
        public float CpuFrameTimeMs;
        public float GpuFrameTimeMs;
        public float RenderThreadMs;

        // Memory
        public long GcAllocBytes;
        public long TotalHeapBytes;
        public long TextureMemoryBytes;
        public long MeshMemoryBytes;

        // GC detail (from ProfilerRecorder, 0 if unavailable)
        public int GcAllocCount;

        // Rendering (from ProfilerRecorder, 0 if unavailable)
        public int DrawCalls;
        public int Batches;
        public int SetPassCalls;
        public int Triangles;
        public int Vertices;
    }

    public struct BootStageTiming
    {
        public string StageName;
        public long DurationMs;
        public bool Success;
    }

    public struct AssetLoadTiming
    {
        public string AssetKey;
        public long DurationMs;
        public long SizeBytes;
    }

    public struct NetworkEvent
    {
        public float Timestamp;
        public NetworkEventType Type;
        public int Bytes;
        public float LatencyMs; // Round-trip for sends, 0 for receives
    }

    public enum NetworkEventType { Send, Receive, Error }

    public struct SessionMetadata
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public string UnityVersion;
        public string Platform;
        public int TargetFrameRate;
        public int ScreenWidth;
        public int ScreenHeight;
    }
}
