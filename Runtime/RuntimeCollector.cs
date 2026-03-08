#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace DrWario.Runtime
{
    /// <summary>
    /// MonoBehaviour that samples frame-level profiling data each Update.
    /// Uses ProfilerRecorder for accurate data when available, falls back to legacy APIs.
    /// Only compiles in Editor and Development builds — zero footprint in release.
    /// </summary>
    public class RuntimeCollector : MonoBehaviour
    {
        public static ProfilingSession ActiveSession { get; private set; }

        /// <summary>
        /// Fired when a new profiling session starts. External frameworks can subscribe
        /// to hook their own instrumentation (e.g., boot timing, asset loads).
        /// </summary>
        public static event Action<ProfilingSession> OnSessionStarted;

        private long _prevTotalGcAlloc;
        private static RuntimeCollector _instance;
        private FrameTiming[] _frameTimings = new FrameTiming[1];
        private ProfilerBridge _profilerBridge;

        private static readonly ProfilerMarker s_SampleMarker = new ProfilerMarker("DrWario.Sample");

        public static RuntimeCollector Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[DrWario] RuntimeCollector");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _instance = go.AddComponent<RuntimeCollector>();
            return _instance;
        }

        public static void StartSession(int capacity = ProfilingSession.DefaultCapacity)
        {
            var collector = Ensure();
            ActiveSession = new ProfilingSession(capacity);
            ActiveSession.Start();
            collector._prevTotalGcAlloc = Profiler.GetTotalAllocatedMemoryLong();

            // Start ProfilerBridge for enhanced data
            collector._profilerBridge?.Dispose();
            collector._profilerBridge = new ProfilerBridge();
            collector._profilerBridge.Start();

            collector.enabled = true;

            OnSessionStarted?.Invoke(ActiveSession);
        }

        public static void StopSession()
        {
            ActiveSession?.Stop();
            if (_instance != null)
            {
                _instance.enabled = false;
                _instance._profilerBridge?.Dispose();
                _instance._profilerBridge = null;
            }
        }

        public static void DestroyCollector()
        {
            if (_instance != null)
            {
                _instance._profilerBridge?.Dispose();
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        private void Awake()
        {
            enabled = false; // Disabled until StartSession is called
        }

        private void Update()
        {
            if (ActiveSession == null || !ActiveSession.IsRecording) return;

            using (s_SampleMarker.Auto())
            {
                // Sample ProfilerRecorder counters (zero-alloc)
                _profilerBridge?.Sample();
                bool hasBridge = _profilerBridge != null && _profilerBridge.IsActive;

                // Frame timing — prefer ProfilerRecorder, fallback to FrameTimingManager, then deltaTime
                float cpuMs = 0f, gpuMs = 0f, renderMs = 0f;

                if (hasBridge && _profilerBridge.MainThreadMs > 0)
                {
                    cpuMs = _profilerBridge.MainThreadMs;
                    renderMs = _profilerBridge.RenderThreadMs;
                }
                else
                {
                    FrameTimingManager.CaptureFrameTimings();
                    uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
                    if (count > 0)
                    {
                        cpuMs = (float)_frameTimings[0].cpuFrameTime;
                        gpuMs = (float)_frameTimings[0].gpuFrameTime;
                    }
                }

                // Memory — prefer ProfilerRecorder for GC, fallback to legacy
                long gcAllocBytes;
                int gcAllocCount = 0;
                long totalHeap;
                long textureMem;
                long meshMem;

                if (hasBridge && _profilerBridge.TotalUsedMemory > 0)
                {
                    gcAllocBytes = _profilerBridge.GcAllocBytes;
                    gcAllocCount = _profilerBridge.GcAllocCount;
                    totalHeap = _profilerBridge.TotalUsedMemory;
                    textureMem = _profilerBridge.TextureMemory;
                    meshMem = _profilerBridge.MeshMemory;
                }
                else
                {
                    // Legacy fallback
                    totalHeap = Profiler.GetTotalAllocatedMemoryLong();
                    gcAllocBytes = totalHeap - _prevTotalGcAlloc;
                    if (gcAllocBytes < 0) gcAllocBytes = 0;
                    _prevTotalGcAlloc = totalHeap;
                    textureMem = Profiler.GetAllocatedMemoryForGraphicsDriver();
                    meshMem = Profiler.GetTotalReservedMemoryLong() - totalHeap;
                }

                var sample = new FrameSample
                {
                    Timestamp = Time.realtimeSinceStartup,
                    DeltaTime = Time.unscaledDeltaTime,
                    CpuFrameTimeMs = cpuMs > 0 ? cpuMs : Time.unscaledDeltaTime * 1000f,
                    GpuFrameTimeMs = gpuMs,
                    RenderThreadMs = renderMs,
                    GcAllocBytes = gcAllocBytes,
                    GcAllocCount = gcAllocCount,
                    TotalHeapBytes = totalHeap,
                    TextureMemoryBytes = textureMem,
                    MeshMemoryBytes = meshMem,

                    // Rendering counters (0 if ProfilerBridge unavailable)
                    DrawCalls = hasBridge ? _profilerBridge.DrawCalls : 0,
                    Batches = hasBridge ? _profilerBridge.Batches : 0,
                    SetPassCalls = hasBridge ? _profilerBridge.SetPassCalls : 0,
                    Triangles = hasBridge ? _profilerBridge.Triangles : 0,
                    Vertices = hasBridge ? _profilerBridge.Vertices : 0,
                };

                ActiveSession.RecordFrame(sample);
            }
        }

        private void OnDestroy()
        {
            _profilerBridge?.Dispose();
            if (_instance == this)
                _instance = null;
        }
    }
}
#endif
