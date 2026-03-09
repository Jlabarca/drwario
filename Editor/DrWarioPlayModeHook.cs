using UnityEditor;
using UnityEngine;
using DrWario.Runtime;

namespace DrWario.Editor
{
    /// <summary>
    /// Auto-starts RuntimeCollector when entering Play Mode if the toggle is enabled.
    /// Also attaches editor context (baseline, open windows) to profiling sessions.
    /// </summary>
    [InitializeOnLoad]
    public static class DrWarioPlayModeHook
    {
        private const string PrefKey = "DrWario_AutoStart";
        private static SceneSnapshotTracker _snapshotTracker;
        private static ProfilingSession _logTargetSession;

        public static bool AutoStartEnabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static DrWarioPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Re-capture baseline when about to enter play mode
            RuntimeCollector.OnSessionStarted += AttachEditorContext;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (!AutoStartEnabled) break;
                    EditorApplication.delayCall += () =>
                    {
                        RuntimeCollector.StartSession();
                        Debug.Log("[DrWario] Auto-started profiling session.");
                    };
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    // Always clean up snapshot tracker and console log capture
                    RuntimeCollector.OnFrameSampled -= OnFrameSampled;
                    StopConsoleLogCapture();
                    _snapshotTracker?.StopSession();
                    _snapshotTracker = null;

                    if (AutoStartEnabled && RuntimeCollector.ActiveSession?.IsRecording == true)
                    {
                        RuntimeCollector.StopSession();
                        Debug.Log($"[DrWario] Auto-stopped. {RuntimeCollector.ActiveSession?.FrameCount ?? 0} frames captured.");
                    }
                    break;
            }
        }

        private static void OnFrameSampled(ref FrameSample sample)
        {
            if (_snapshotTracker != null)
                sample.ObjectCount = _snapshotTracker.OnFrame(sample);
        }

        /// <summary>
        /// Attaches editor baseline and window state to every new profiling session.
        /// This runs for both auto-started and manually started sessions.
        /// </summary>
        internal static void AttachEditorContext(ProfilingSession session)
        {
            var baseline = EditorBaselineCapture.LastBaseline;

            bool sceneViewOpen = SceneView.sceneViews.Count > 0;

            // Detect open editor windows by type name (InspectorWindow, ProfilerWindow, GameView are internal types)
            var allWindows = Resources.FindObjectsOfTypeAll(typeof(EditorWindow));
            bool inspectorOpen = false;
            bool profilerOpen = false;
            int gameViewCount = 0;
            foreach (var w in allWindows)
            {
                string typeName = w.GetType().Name;
                if (typeName == "InspectorWindow") inspectorOpen = true;
                else if (typeName == "ProfilerWindow") profilerOpen = true;
                else if (typeName == "GameView") gameViewCount++;
            }

            session.SetEditorContext(baseline, sceneViewOpen, inspectorOpen, profilerOpen, gameViewCount);

            // Capture scene census
            session.SceneCensus = SceneCensusCapture.Capture();
            if (session.SceneCensus.IsValid)
            {
                Debug.Log($"[DrWario] Scene census: {session.SceneCensus.TotalGameObjects} objects, " +
                          $"{session.SceneCensus.TotalComponents} components, " +
                          $"{session.SceneCensus.CanvasCount} canvases, " +
                          $"{session.SceneCensus.DirectionalLights + session.SceneCensus.PointLights + session.SceneCensus.SpotLights + session.SceneCensus.AreaLights} lights");
            }

            // Capture active MonoBehaviour scripts for suspect identification
            var activeScripts = SceneCensusCapture.CaptureActiveScripts();
            session.SetActiveScripts(activeScripts);
            if (activeScripts.Count > 0)
                Debug.Log($"[DrWario] Active scripts captured: {activeScripts.Count} types");

            // Start console log capture for error/warning context
            StartConsoleLogCapture(session);

            // Start scene snapshot tracker for hierarchy diff tracking
            _snapshotTracker?.StopSession();
            _snapshotTracker = new SceneSnapshotTracker();
            _snapshotTracker.StartSession(session);
            RuntimeCollector.OnFrameSampled += OnFrameSampled;

            if (baseline.IsValid)
            {
                Debug.Log($"[DrWario] Editor context attached — baseline: CPU {baseline.AvgCpuFrameTimeMs:F1}ms, " +
                          $"GC {baseline.AvgGcAllocBytes}B/frame, Draw calls {baseline.AvgDrawCalls}. " +
                          $"Windows: Scene={sceneViewOpen}, Inspector={inspectorOpen}, Profiler={profilerOpen}, GameViews={gameViewCount}");
            }
            else
            {
                Debug.LogWarning("[DrWario] No editor baseline available. Analysis will use unadjusted thresholds. " +
                                 "Baseline is captured on editor startup — try restarting the editor if this persists.");
            }
        }

        private static void StartConsoleLogCapture(ProfilingSession session)
        {
            StopConsoleLogCapture();
            _logTargetSession = session;
            Application.logMessageReceived += OnConsoleLog;
        }

        private static void StopConsoleLogCapture()
        {
            Application.logMessageReceived -= OnConsoleLog;
            _logTargetSession = null;
        }

        private static void OnConsoleLog(string message, string stackTrace, LogType type)
        {
            if (_logTargetSession == null || !_logTargetSession.IsRecording) return;

            // Only capture errors and warnings — skip DrWario's own logs and verbose Log messages
            if (type != LogType.Error && type != LogType.Warning && type != LogType.Exception)
                return;
            if (message.StartsWith("[DrWario]")) return;

            // Extract first meaningful line from stack trace as hint (script name)
            string stackHint = "";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                var firstLine = stackTrace.Split('\n')[0].Trim();
                // Keep just the method signature, trim path info for brevity
                if (firstLine.Length > 80)
                    firstLine = firstLine.Substring(0, 80);
                stackHint = firstLine;
            }

            _logTargetSession.RecordConsoleLog(new ConsoleLogEntry
            {
                Timestamp = Time.realtimeSinceStartup,
                Message = message.Length > 150 ? message.Substring(0, 150) : message,
                LogType = type.ToString(),
                StackTraceHint = stackHint
            });
        }
    }
}
