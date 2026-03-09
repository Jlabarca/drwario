#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Accumulates per-frame profiler marker timing data across the session.
        /// </summary>
        private struct MarkerAccumulator
        {
            public string name;
            public long totalNs;
            public long maxNs;
            public long totalCallCount;
            public int sampleCount;
            public ProfilerRecorder recorder;
        }

        private MarkerAccumulator[] _markerAccumulators;

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

            // Detect whether the Unity Profiler was already recording
            ActiveSession.ProfilerWasRecording = Profiler.enabled;

            // Start profiler marker recorders for subsystem timing
            collector.StartMarkerRecorders();

            collector.enabled = true;

            OnSessionStarted?.Invoke(ActiveSession);
        }

        private void StartMarkerRecorders()
        {
            DisposeMarkerRecorders();

            var markerDefs = new (string name, ProfilerCategory category)[]
            {
                ("PlayerLoop", ProfilerCategory.Internal),
                ("FixedUpdate", ProfilerCategory.Internal),
                ("Update", ProfilerCategory.Internal),
                ("LateUpdate", ProfilerCategory.Internal),
                ("Rendering", ProfilerCategory.Render),
                ("Physics.Processing", ProfilerCategory.Physics),
                ("Animation.Update", ProfilerCategory.Animation),
            };

            var accumulators = new List<MarkerAccumulator>();
            foreach (var (name, category) in markerDefs)
            {
                try
                {
                    var recorder = ProfilerRecorder.StartNew(category, name, 1);
                    accumulators.Add(new MarkerAccumulator
                    {
                        name = name,
                        totalNs = 0,
                        maxNs = 0,
                        totalCallCount = 0,
                        sampleCount = 0,
                        recorder = recorder,
                    });
                }
                catch
                {
                    // Marker not available on this platform — skip gracefully
                }
            }

            _markerAccumulators = accumulators.ToArray();
        }

        private void DisposeMarkerRecorders()
        {
            if (_markerAccumulators == null) return;
            for (int i = 0; i < _markerAccumulators.Length; i++)
            {
                _markerAccumulators[i].recorder.Dispose();
            }
            _markerAccumulators = null;
        }

        public static void StopSession()
        {
            // Aggregate marker data before stopping the session
            if (_instance != null && ActiveSession != null && _instance._markerAccumulators != null)
            {
                var markerList = new List<ProfilerMarkerSample>();
                for (int i = 0; i < _instance._markerAccumulators.Length; i++)
                {
                    ref var acc = ref _instance._markerAccumulators[i];
                    if (acc.sampleCount > 0)
                    {
                        markerList.Add(new ProfilerMarkerSample
                        {
                            MarkerName = acc.name,
                            AvgInclusiveTimeNs = acc.totalNs / acc.sampleCount,
                            AvgExclusiveTimeNs = acc.totalNs / acc.sampleCount, // inclusive == exclusive for these top-level markers
                            MaxInclusiveTimeNs = acc.maxNs,
                            AvgCallCount = (float)acc.totalCallCount / acc.sampleCount,
                            SampleCount = acc.sampleCount,
                        });
                    }
                }

                // Sort by AvgInclusiveTimeNs descending, keep top 20
                markerList.Sort((a, b) => b.AvgInclusiveTimeNs.CompareTo(a.AvgInclusiveTimeNs));
                if (markerList.Count > 20)
                    markerList.RemoveRange(20, markerList.Count - 20);

                ActiveSession.SetProfilerMarkers(markerList);
                _instance.DisposeMarkerRecorders();
            }

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
                _instance.DisposeMarkerRecorders();
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

                // Accumulate marker timing data
                if (_markerAccumulators != null)
                {
                    for (int i = 0; i < _markerAccumulators.Length; i++)
                    {
                        ref var acc = ref _markerAccumulators[i];
                        if (acc.recorder.Valid && acc.recorder.CurrentValue > 0)
                        {
                            long val = acc.recorder.CurrentValue;
                            acc.totalNs += val;
                            if (val > acc.maxNs) acc.maxNs = val;
                            acc.totalCallCount += acc.recorder.Count > 0 ? acc.recorder.Count : 1;
                            acc.sampleCount++;
                        }
                    }
                }

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

                    // Extended subsystem counters (0 if unavailable)
                    PhysicsActiveBodies = hasBridge ? _profilerBridge.PhysicsActiveBodies : 0,
                    PhysicsKinematicBodies = hasBridge ? _profilerBridge.PhysicsKinematicBodies : 0,
                    PhysicsContacts = hasBridge ? _profilerBridge.PhysicsContacts : 0,
                    AudioVoiceCount = hasBridge ? _profilerBridge.AudioVoiceCount : 0,
                    AudioDSPLoad = hasBridge ? _profilerBridge.AudioDSPLoad : 0f,
                    AnimatorCount = hasBridge ? _profilerBridge.AnimatorCount : 0,
                    UICanvasRebuilds = hasBridge ? _profilerBridge.UICanvasRebuilds : 0,
                    UILayoutRebuilds = hasBridge ? _profilerBridge.UILayoutRebuilds : 0,
                    FrameNumber = Time.frameCount,
                };

                ActiveSession.RecordFrame(sample);
            }
        }

        private void OnDestroy()
        {
            DisposeMarkerRecorders();
            _profilerBridge?.Dispose();
            if (_instance == this)
                _instance = null;
        }
    }
}
#endif
