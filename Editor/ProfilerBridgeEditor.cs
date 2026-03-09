using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DrWario.Editor
{
    /// <summary>
    /// Editor-side bridge for navigating the Unity Profiler window to a specific frame.
    /// Uses reflection to avoid hard compile-time dependencies on ProfilerWindow internals.
    /// </summary>
    public static class ProfilerBridgeEditor
    {
        private static Type _profilerWindowType;
        private static bool _typeResolved;

        /// <summary>
        /// Opens the Unity Profiler window and navigates to the specified frame number.
        /// </summary>
        /// <param name="frameNumber">The Profiler frame index to navigate to.</param>
        public static void NavigateToFrame(int frameNumber)
        {
            try
            {
                var windowType = GetProfilerWindowType();
                if (windowType == null)
                {
                    Debug.LogWarning("[DrWario] Could not find ProfilerWindow type. Unable to navigate to frame.");
                    return;
                }

                // Open or focus the Profiler window
                var window = EditorWindow.GetWindow(windowType);
                if (window == null)
                {
                    Debug.LogWarning("[DrWario] Could not open Profiler window.");
                    return;
                }

                bool navigated = false;

                // Try setting selectedFrameIndex property first (newer Unity versions)
                var selectedFrameProp = windowType.GetProperty(
                    "selectedFrameIndex",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (selectedFrameProp != null && selectedFrameProp.CanWrite)
                {
                    selectedFrameProp.SetValue(window, frameNumber);
                    navigated = true;
                }

                // Fallback: try SetActiveVisibleFrameIndex method
                if (!navigated)
                {
                    var setFrameMethod = windowType.GetMethod(
                        "SetActiveVisibleFrameIndex",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (setFrameMethod != null)
                    {
                        setFrameMethod.Invoke(window, new object[] { frameNumber });
                        navigated = true;
                    }
                }

                if (navigated)
                {
                    Debug.Log($"[DrWario] Navigated Profiler to frame {frameNumber}.");
                }
                else
                {
                    Debug.LogWarning(
                        $"[DrWario] Could not navigate Profiler to frame {frameNumber}. " +
                        "No supported API found on this Unity version.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DrWario] Failed to navigate Profiler to frame {frameNumber}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true if the session has associated Profiler data that can be navigated.
        /// </summary>
        public static bool IsProfilerDataAvailable(Runtime.ProfilingSession session)
        {
            return session != null && session.ProfilerWasRecording;
        }

        private static Type GetProfilerWindowType()
        {
            if (_typeResolved)
                return _profilerWindowType;

            _typeResolved = true;

            // Try the fully-qualified assembly name first
            _profilerWindowType = Type.GetType("UnityEditor.ProfilerWindow, UnityEditor");

            // Fallback: search loaded assemblies
            if (_profilerWindowType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _profilerWindowType = assembly.GetType("UnityEditor.ProfilerWindow");
                    if (_profilerWindowType != null)
                        break;
                }
            }

            if (_profilerWindowType == null)
            {
                Debug.LogWarning("[DrWario] ProfilerWindow type not found in any loaded assembly.");
            }

            return _profilerWindowType;
        }
    }
}
