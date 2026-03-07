using System;

namespace DrWario.Runtime
{
    public struct FrameSample
    {
        public float Timestamp;
        public float DeltaTime;
        public float CpuFrameTimeMs;
        public float GpuFrameTimeMs;
        public long GcAllocBytes;
        public long TotalHeapBytes;
        public long TextureMemoryBytes;
        public long MeshMemoryBytes;
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
