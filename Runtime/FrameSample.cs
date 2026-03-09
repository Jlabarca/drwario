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

        // Extended subsystem counters (from ProfilerRecorder, 0 if unavailable)
        public int PhysicsActiveBodies;
        public int PhysicsKinematicBodies;
        public int PhysicsContacts;
        public int AudioVoiceCount;
        public float AudioDSPLoad;
        public int AnimatorCount;
        public int UICanvasRebuilds;
        public int UILayoutRebuilds;

        // Scene object count (sampled periodically, held between samples)
        public int ObjectCount;

        // Native/reserved memory (from Profiler API)
        public long NativeMemoryBytes;

        // GC generation 0 collection count (cumulative, use delta for per-frame)
        public int GcCollectionCount;

        // Absolute frame number for Profiler frame navigation
        public int FrameNumber;
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

        // Environment context — identifies editor vs build sessions
        public bool IsEditor;
        public bool IsDevelopmentBuild;
        public EditorBaseline Baseline;

        // Editor window state (only meaningful when IsEditor=true)
        public bool SceneViewOpen;
        public bool InspectorOpen;
        public bool ProfilerOpen;
        public int GameViewCount;
    }

    /// <summary>
    /// Captures idle editor overhead measured before Play Mode.
    /// Used to estimate how much of the profiling data is editor noise vs actual game performance.
    /// Only populated in editor sessions.
    /// </summary>
    public struct EditorBaseline
    {
        public float AvgCpuFrameTimeMs;
        public float AvgRenderThreadMs;
        public long AvgGcAllocBytes;
        public int AvgGcAllocCount;
        public int AvgDrawCalls;
        public int AvgBatches;
        public int AvgSetPassCalls;
        public int AvgUICanvasRebuilds;
        public int AvgUILayoutRebuilds;
        public int SampleCount;
        public bool IsValid;
    }
}
