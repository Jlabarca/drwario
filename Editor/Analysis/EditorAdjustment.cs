using System;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis
{
    /// <summary>
    /// Helpers for adjusting profiling metrics and confidence when running in the Unity Editor.
    /// Rules use these to subtract editor baseline overhead and classify finding reliability.
    /// </summary>
    public static class EditorAdjustment
    {
        /// <summary>
        /// Subtract a baseline value from a raw metric, floored at zero.
        /// </summary>
        public static float SubtractBaseline(float rawMetric, float baselineValue)
        {
            return Math.Max(0f, rawMetric - baselineValue);
        }

        /// <summary>
        /// Subtract a baseline value from a raw metric, floored at zero.
        /// </summary>
        public static long SubtractBaseline(long rawMetric, long baselineValue)
        {
            return Math.Max(0L, rawMetric - baselineValue);
        }

        /// <summary>
        /// Subtract a baseline value from a raw metric, floored at zero.
        /// </summary>
        public static int SubtractBaseline(int rawMetric, int baselineValue)
        {
            return Math.Max(0, rawMetric - baselineValue);
        }

        /// <summary>
        /// Classify confidence for a finding based on whether it survives baseline subtraction.
        /// </summary>
        /// <param name="rawMetric">The measured value</param>
        /// <param name="adjustedMetric">The value after subtracting editor baseline</param>
        /// <param name="threshold">The rule's threshold</param>
        /// <param name="isEditor">Whether this is an editor session</param>
        /// <returns>Confidence level</returns>
        public static Confidence ClassifyConfidence(float rawMetric, float adjustedMetric, float threshold, bool isEditor)
        {
            if (!isEditor)
                return Confidence.High;

            // Adjusted metric is well above threshold — real issue even without editor overhead
            if (adjustedMetric > threshold * 1.5f)
                return Confidence.High;

            // Adjusted metric still above threshold — likely real
            if (adjustedMetric > threshold)
                return Confidence.Medium;

            // Only over threshold because of editor overhead
            return Confidence.Low;
        }

        /// <summary>
        /// Generate a short environment note for editor sessions.
        /// </summary>
        public static string BuildEnvironmentNote(SessionMetadata metadata, string metricName)
        {
            if (!metadata.IsEditor || !metadata.Baseline.IsValid)
                return null;

            string windowNote = "";
            if (metadata.SceneViewOpen)
                windowNote += "Scene view open (adds draw calls + rendering). ";
            if (metadata.InspectorOpen)
                windowNote += "Inspector open (adds GC allocations). ";
            if (metadata.ProfilerOpen)
                windowNote += "Profiler open (adds CPU overhead). ";
            if (metadata.GameViewCount > 1)
                windowNote += $"{metadata.GameViewCount} Game views open (multiplies rendering). ";

            if (string.IsNullOrEmpty(windowNote))
                windowNote = "Editor session. ";

            return $"{windowNote}{metricName} may include editor overhead. Verify in a standalone build.";
        }
    }
}
