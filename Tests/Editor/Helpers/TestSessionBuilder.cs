using System;
using System.Collections.Generic;
using DrWario.Runtime;

namespace DrWario.Tests
{
    /// <summary>
    /// Fluent builder for creating synthetic ProfilingSession instances for testing.
    /// Calls Start() to enable recording, then overrides Metadata for controlled values.
    /// </summary>
    public class TestSessionBuilder
    {
        private int _capacity = 3600;
        private SessionMetadata _metadata;
        private SceneCensus _census;
        private readonly List<FrameSample> _frames = new();
        private readonly List<(string name, long ms, bool success)> _bootStages = new();
        private readonly List<(string key, long ms, long bytes)> _assetLoads = new();
        private readonly List<(NetworkEventType type, int bytes, float latencyMs)> _networkEvents = new();
        private List<ActiveScriptEntry> _activeScripts;
        private List<ConsoleLogEntry> _consoleLogs;
        private List<ProfilerMarkerSample> _markers;
        private readonly List<SceneSnapshotDiff> _snapshots = new();
        private readonly HashSet<int> _captureFrames = new();

        public TestSessionBuilder()
        {
            _metadata = new SessionMetadata
            {
                StartTime = DateTime.UtcNow.AddSeconds(-10),
                EndTime = DateTime.UtcNow,
                UnityVersion = "2022.3.0f1",
                Platform = "WindowsEditor",
                TargetFrameRate = 60,
                ScreenWidth = 1920,
                ScreenHeight = 1080,
                IsEditor = false,
                IsDevelopmentBuild = false
            };
        }

        public TestSessionBuilder WithCapacity(int c) { _capacity = c; return this; }

        public TestSessionBuilder WithTargetFps(int fps)
        {
            _metadata.TargetFrameRate = fps;
            return this;
        }

        public TestSessionBuilder AsEditorSession(EditorBaseline baseline = default)
        {
            _metadata.IsEditor = true;
            _metadata.Baseline = baseline;
            return this;
        }

        public TestSessionBuilder WithMetadata(Action<SessionMetadata> configure)
        {
            configure?.Invoke(_metadata);
            return this;
        }

        public TestSessionBuilder WithSceneCensus(SceneCensus census)
        {
            _census = census;
            return this;
        }

        /// <summary>
        /// Add N identical frames with given parameters.
        /// </summary>
        public TestSessionBuilder AddFrames(int count,
            float cpuMs = 10f, long gcAllocBytes = 0, long heapBytes = 100_000_000,
            float gpuMs = 0f, int drawCalls = 0, int triangles = 0,
            float renderThreadMs = 0f, int batches = 0, int setPassCalls = 0)
        {
            int startIdx = _frames.Count;
            for (int i = 0; i < count; i++)
            {
                _frames.Add(new FrameSample
                {
                    Timestamp = (startIdx + i) * 0.0167f,
                    DeltaTime = 0.0167f,
                    CpuFrameTimeMs = cpuMs,
                    GpuFrameTimeMs = gpuMs,
                    RenderThreadMs = renderThreadMs,
                    GcAllocBytes = gcAllocBytes,
                    TotalHeapBytes = heapBytes,
                    DrawCalls = drawCalls,
                    Triangles = triangles,
                    Batches = batches,
                    SetPassCalls = setPassCalls,
                    FrameNumber = startIdx + i,
                });
            }
            return this;
        }

        public TestSessionBuilder AddFrame(FrameSample sample)
        {
            _frames.Add(sample);
            return this;
        }

        /// <summary>
        /// Add frames with linearly growing heap (for memory leak tests).
        /// </summary>
        public TestSessionBuilder AddFramesWithGrowingHeap(
            int count, long startHeap, long growthPerFrame, float cpuMs = 10f)
        {
            int startIdx = _frames.Count;
            for (int i = 0; i < count; i++)
            {
                _frames.Add(new FrameSample
                {
                    Timestamp = (startIdx + i) * 0.0167f,
                    DeltaTime = 0.0167f,
                    CpuFrameTimeMs = cpuMs,
                    TotalHeapBytes = startHeap + (long)i * growthPerFrame,
                    FrameNumber = startIdx + i,
                });
            }
            return this;
        }

        public TestSessionBuilder AddBootStage(string name, long durationMs, bool success = true)
        {
            _bootStages.Add((name, durationMs, success));
            return this;
        }

        public TestSessionBuilder AddAssetLoad(string key, long durationMs, long sizeBytes = 0)
        {
            _assetLoads.Add((key, durationMs, sizeBytes));
            return this;
        }

        public TestSessionBuilder AddNetworkEvent(NetworkEventType type, int bytes, float latencyMs = 0)
        {
            _networkEvents.Add((type, bytes, latencyMs));
            return this;
        }

        public TestSessionBuilder WithActiveScripts(List<ActiveScriptEntry> scripts)
        {
            _activeScripts = scripts;
            return this;
        }

        public TestSessionBuilder WithConsoleLogs(List<ConsoleLogEntry> logs)
        {
            _consoleLogs = logs;
            return this;
        }

        public TestSessionBuilder WithProfilerMarkers(List<ProfilerMarkerSample> markers)
        {
            _markers = markers;
            return this;
        }

        public TestSessionBuilder AddSceneSnapshot(SceneSnapshotDiff snapshot)
        {
            _snapshots.Add(snapshot);
            return this;
        }

        public TestSessionBuilder MarkCaptureFrame(int frameNumber)
        {
            _captureFrames.Add(frameNumber);
            return this;
        }

        public ProfilingSession Build()
        {
            var session = new ProfilingSession(_capacity);
            session.Start();

            // Override metadata (Start() sets it from Unity APIs)
            session.Metadata = _metadata;
            session.SceneCensus = _census;

            foreach (var frame in _frames)
                session.RecordFrame(frame);

            foreach (var (name, ms, success) in _bootStages)
                session.RecordBootStage(name, ms, success);

            foreach (var (key, ms, bytes) in _assetLoads)
                session.RecordAssetLoad(key, ms, bytes);

            foreach (var (type, bytes, latencyMs) in _networkEvents)
                session.RecordNetworkEvent(type, bytes, latencyMs);

            if (_activeScripts != null)
                session.SetActiveScripts(_activeScripts);

            if (_consoleLogs != null)
                foreach (var log in _consoleLogs)
                    session.RecordConsoleLog(log);

            if (_markers != null)
                session.SetProfilerMarkers(_markers);

            foreach (var snap in _snapshots)
                session.RecordSceneSnapshot(snap);

            foreach (var fn in _captureFrames)
                session.MarkCaptureFrame(fn);

            return session;
        }
    }
}
