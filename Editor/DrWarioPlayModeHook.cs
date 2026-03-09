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
            if (!AutoStartEnabled) return;

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    // Delay one frame to let Unity settle
                    EditorApplication.delayCall += () =>
                    {
                        RuntimeCollector.StartSession();
                        Debug.Log("[DrWario] Auto-started profiling session.");
                    };
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    if (RuntimeCollector.ActiveSession?.IsRecording == true)
                    {
                        RuntimeCollector.StopSession();
                        Debug.Log($"[DrWario] Auto-stopped. {RuntimeCollector.ActiveSession?.FrameCount ?? 0} frames captured.");
                    }
                    break;
            }
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
    }
}
