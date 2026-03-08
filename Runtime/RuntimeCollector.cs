#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace DrWario.Runtime
{
    /// <summary>
    /// MonoBehaviour that samples frame-level profiling data each Update.
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
            collector.enabled = true;

            OnSessionStarted?.Invoke(ActiveSession);
        }

        public static void StopSession()
        {
            ActiveSession?.Stop();
            if (_instance != null)
                _instance.enabled = false;
        }

        public static void DestroyCollector()
        {
            if (_instance != null)
            {
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

            // Frame timing (CPU/GPU)
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);

            float cpuMs = 0f, gpuMs = 0f;
            if (count > 0)
            {
                cpuMs = (float)_frameTimings[0].cpuFrameTime;
                gpuMs = (float)_frameTimings[0].gpuFrameTime;
            }

            // Memory
            long totalHeap = Profiler.GetTotalAllocatedMemoryLong();
            long gcDelta = totalHeap - _prevTotalGcAlloc;
            if (gcDelta < 0) gcDelta = 0; // Handle GC collection resets
            _prevTotalGcAlloc = totalHeap;

            var sample = new FrameSample
            {
                Timestamp = Time.realtimeSinceStartup,
                DeltaTime = Time.unscaledDeltaTime,
                CpuFrameTimeMs = cpuMs > 0 ? cpuMs : Time.unscaledDeltaTime * 1000f,
                GpuFrameTimeMs = gpuMs,
                GcAllocBytes = gcDelta,
                TotalHeapBytes = totalHeap,
                TextureMemoryBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
                MeshMemoryBytes = Profiler.GetTotalReservedMemoryLong() - totalHeap
            };

            ActiveSession.RecordFrame(sample);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
#endif
