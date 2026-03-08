using UnityEditor;
using UnityEngine;
using DrWario.Runtime;

namespace DrWario.Editor
{
    /// <summary>
    /// Captures idle editor overhead before Play Mode by sampling ProfilerRecorder
    /// counters for ~30 frames in edit mode. The resulting EditorBaseline is attached
    /// to the profiling session so analysis rules can subtract editor noise.
    ///
    /// This class is entirely editor-only — zero footprint in builds.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBaselineCapture
    {
        private const int TargetSamples = 30;

        public static EditorBaseline LastBaseline { get; private set; }

        private static ProfilerBridge _bridge;
        private static int _sampleCount;
        private static bool _capturing;

        // Accumulators
        private static double _sumCpu;
        private static double _sumRender;
        private static long _sumGcBytes;
        private static long _sumGcCount;
        private static long _sumDrawCalls;
        private static long _sumBatches;
        private static long _sumSetPass;

        static EditorBaselineCapture()
        {
            // Capture baseline once on domain reload (editor startup / script recompile)
            // Only in edit mode — not during play
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                StartCapture();
        }

        /// <summary>
        /// Begins sampling editor idle overhead. Call before entering Play Mode.
        /// Safe to call multiple times — restarts if already capturing.
        /// </summary>
        public static void StartCapture()
        {
            StopCapture();

            _bridge = new ProfilerBridge();
            _bridge.Start();

            if (!_bridge.IsActive)
            {
                _bridge.Dispose();
                _bridge = null;
                return;
            }

            _sampleCount = 0;
            _sumCpu = 0;
            _sumRender = 0;
            _sumGcBytes = 0;
            _sumGcCount = 0;
            _sumDrawCalls = 0;
            _sumBatches = 0;
            _sumSetPass = 0;
            _capturing = true;

            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (!_capturing || _bridge == null || !_bridge.IsActive)
            {
                StopCapture();
                return;
            }

            // Don't capture during play mode
            if (EditorApplication.isPlaying)
            {
                StopCapture();
                return;
            }

            _bridge.Sample();

            // Skip first few frames (ProfilerRecorder may need to warm up)
            if (_sampleCount < 3)
            {
                _sampleCount++;
                return;
            }

            _sumCpu += _bridge.MainThreadMs;
            _sumRender += _bridge.RenderThreadMs;
            _sumGcBytes += _bridge.GcAllocBytes;
            _sumGcCount += _bridge.GcAllocCount;
            _sumDrawCalls += _bridge.DrawCalls;
            _sumBatches += _bridge.Batches;
            _sumSetPass += _bridge.SetPassCalls;

            _sampleCount++;

            if (_sampleCount >= TargetSamples + 3) // 3 warmup + 30 real
                FinishCapture();
        }

        private static void FinishCapture()
        {
            int realSamples = _sampleCount - 3; // Subtract warmup frames
            if (realSamples <= 0)
            {
                StopCapture();
                return;
            }

            LastBaseline = new EditorBaseline
            {
                AvgCpuFrameTimeMs = (float)(_sumCpu / realSamples),
                AvgRenderThreadMs = (float)(_sumRender / realSamples),
                AvgGcAllocBytes = _sumGcBytes / realSamples,
                AvgGcAllocCount = (int)(_sumGcCount / realSamples),
                AvgDrawCalls = (int)(_sumDrawCalls / realSamples),
                AvgBatches = (int)(_sumBatches / realSamples),
                AvgSetPassCalls = (int)(_sumSetPass / realSamples),
                SampleCount = realSamples,
                IsValid = true
            };

            Debug.Log($"[DrWario] Editor baseline captured ({realSamples} frames): " +
                      $"CPU {LastBaseline.AvgCpuFrameTimeMs:F1}ms, " +
                      $"GC {LastBaseline.AvgGcAllocBytes}B/frame, " +
                      $"Draw calls {LastBaseline.AvgDrawCalls}");

            StopCapture();
        }

        private static void StopCapture()
        {
            _capturing = false;
            EditorApplication.update -= OnEditorUpdate;

            if (_bridge != null)
            {
                _bridge.Dispose();
                _bridge = null;
            }
        }
    }
}
