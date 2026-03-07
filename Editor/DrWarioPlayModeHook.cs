using UnityEditor;
using UnityEngine;
using DrWario.Runtime;

namespace DrWario.Editor
{
    /// <summary>
    /// Auto-starts RuntimeCollector when entering Play Mode if the toggle is enabled.
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
    }
}
