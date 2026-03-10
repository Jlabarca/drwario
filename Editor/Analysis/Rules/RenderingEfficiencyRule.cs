using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    /// <summary>
    /// Analyzes rendering efficiency using ProfilerRecorder data:
    /// draw calls, batching ratio, set-pass calls, triangle count.
    /// Produces no findings if ProfilerRecorder data is unavailable (all zeros).
    /// </summary>
    public class RenderingEfficiencyRule : IAnalysisRule
    {
        public string Category => "Rendering";
        public string RuleId => "RENDER_EFFICIENCY";

        private const int HighDrawCalls = 1000;
        private const int CriticalDrawCalls = 3000;
        private const int HighSetPassCalls = 100;
        private const int CriticalSetPassCalls = 200;
        private const long HighTriangles = 2_000_000;
        private const long CriticalTriangles = 5_000_000;
        private const float PoorBatchingRatio = 0.3f;  // <30% batching = poor

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length == 0) return findings;

            // Only run if we have ProfilerRecorder data
            var framesWithDrawCalls = frames.Where(f => f.DrawCalls > 0).ToArray();
            if (framesWithDrawCalls.Length == 0) return findings;

            int avgDrawCalls = (int)framesWithDrawCalls.Average(f => f.DrawCalls);
            int maxDrawCalls = framesWithDrawCalls.Max(f => f.DrawCalls);

            var framesWithBatches = frames.Where(f => f.Batches > 0).ToArray();
            int avgBatches = framesWithBatches.Length > 0 ? (int)framesWithBatches.Average(f => f.Batches) : 0;

            var framesWithSetPass = frames.Where(f => f.SetPassCalls > 0).ToArray();
            int avgSetPass = framesWithSetPass.Length > 0 ? (int)framesWithSetPass.Average(f => f.SetPassCalls) : 0;
            int maxSetPass = framesWithSetPass.Length > 0 ? framesWithSetPass.Max(f => f.SetPassCalls) : 0;

            var framesWithTris = frames.Where(f => f.Triangles > 0).ToArray();
            long avgTris = framesWithTris.Length > 0 ? (long)framesWithTris.Average(f => f.Triangles) : 0;
            int maxTris = framesWithTris.Length > 0 ? framesWithTris.Max(f => f.Triangles) : 0;

            // Editor adjustment: subtract baseline draw calls (Scene view, Gizmos, etc.)
            bool isEditor = session.Metadata.IsEditor;
            var baseline = session.Metadata.Baseline;
            int adjustedDrawCalls = avgDrawCalls;
            int adjustedSetPass = avgSetPass;
            if (isEditor && baseline.IsValid)
            {
                adjustedDrawCalls = EditorAdjustment.SubtractBaseline(avgDrawCalls, baseline.AvgDrawCalls);
                adjustedSetPass = EditorAdjustment.SubtractBaseline(avgSetPass, baseline.AvgSetPassCalls);
            }
            string envNote = EditorAdjustment.BuildEnvironmentNote(session.Metadata, "Rendering stats");

            // Draw call analysis (use adjusted value for threshold check)
            if (adjustedDrawCalls > HighDrawCalls)
            {
                float batchingRatio = avgBatches > 0 && avgDrawCalls > 0
                    ? 1f - (float)avgBatches / avgDrawCalls
                    : 0f;

                var severity = avgDrawCalls > CriticalDrawCalls ? Severity.Critical
                    : batchingRatio < PoorBatchingRatio ? Severity.Warning
                    : Severity.Info;

                string batchingNote = avgBatches > 0
                    ? $" Batching efficiency: {batchingRatio * 100:F0}% ({avgBatches} batches from {avgDrawCalls} draw calls)."
                    : "";

                var drawConfidence = EditorAdjustment.ClassifyConfidence(avgDrawCalls, adjustedDrawCalls, HighDrawCalls, isEditor);
                string editorNote = isEditor && baseline.IsValid
                    ? $" (editor baseline: ~{baseline.AvgDrawCalls} draw calls subtracted)"
                    : "";

                findings.Add(new DiagnosticFinding
                {
                    RuleId = "DRAW_CALLS",
                    Category = Category,
                    Severity = severity,
                    Title = $"High Draw Calls (avg {avgDrawCalls}, max {maxDrawCalls})",
                    Description = $"Averaging {avgDrawCalls} draw calls per frame (peak {maxDrawCalls}).{batchingNote}{editorNote}",
                    Recommendation = batchingRatio < PoorBatchingRatio
                        ? "Batching is poor. Enable GPU Instancing on materials, use SRP Batcher (URP/HDRP), " +
                          "merge static geometry with StaticBatchingUtility, and reduce unique material count."
                        : "Consider mesh combining for static objects, texture atlasing to reduce material count, " +
                          "and LOD groups for distant objects.",
                    Metric = avgDrawCalls,
                    Threshold = HighDrawCalls,
                    FrameIndex = -1,
                    Confidence = drawConfidence,
                    EnvironmentNote = envNote
                });
            }

            // Set-pass call analysis (use adjusted value for threshold check)
            if (adjustedSetPass > HighSetPassCalls)
            {
                var severity = adjustedSetPass > CriticalSetPassCalls ? Severity.Critical : Severity.Warning;
                var setPassConfidence = EditorAdjustment.ClassifyConfidence(avgSetPass, adjustedSetPass, HighSetPassCalls, isEditor);

                findings.Add(new DiagnosticFinding
                {
                    RuleId = "SET_PASS_CALLS",
                    Category = Category,
                    Severity = severity,
                    Title = $"High SetPass Calls (avg {avgSetPass}, max {maxSetPass})",
                    Description = $"Averaging {avgSetPass} SetPass calls per frame (peak {maxSetPass}). " +
                                  $"Each SetPass call is a GPU state change (shader/material switch).",
                    Recommendation = "Reduce unique material/shader count. " +
                                     "Avoid 'new Material(mat)' in Awake/Update — each call creates a unique instance " +
                                     "that breaks batching; use MaterialPropertyBlock instead. " +
                                     "Enable SRP Batcher for compatible shaders. " +
                                     "Sort rendering by material to minimize state changes.",
                    Metric = avgSetPass,
                    Threshold = HighSetPassCalls,
                    FrameIndex = -1,
                    Confidence = setPassConfidence,
                    EnvironmentNote = envNote
                });
            }

            // Triangle count analysis (no editor adjustment — triangles aren't significantly affected by editor)
            if (avgTris > HighTriangles)
            {
                var severity = avgTris > CriticalTriangles ? Severity.Critical : Severity.Warning;

                findings.Add(new DiagnosticFinding
                {
                    RuleId = "TRIANGLE_COUNT",
                    Category = Category,
                    Severity = severity,
                    Title = $"High Triangle Count (avg {avgTris:N0}, max {maxTris:N0})",
                    Description = $"Rendering {avgTris:N0} triangles per frame on average (peak {maxTris:N0}).",
                    Recommendation = "Add LOD groups (LODGroup component) to reduce distant geometry. " +
                                     "Use occlusion culling (Window > Rendering > Occlusion Culling). " +
                                     "Consider mesh decimation for high-poly assets. " +
                                     "On mobile, target <500K triangles per frame.",
                    Metric = avgTris,
                    Threshold = HighTriangles,
                    FrameIndex = -1,
                    Confidence = isEditor ? Confidence.Medium : Confidence.High
                });
            }

            return findings;
        }
    }
}
