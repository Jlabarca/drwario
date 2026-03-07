using System;
using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class FrameDropRule : IAnalysisRule
    {
        public string Category => "CPU";
        public string RuleId => "FRAME_DROP";

        private const float Target60FpsMs = 16.67f;
        private const float Target30FpsMs = 33.33f;
        private const float SevereDropMs = 50f;

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length == 0) return findings;

            float targetMs = session.Metadata.TargetFrameRate switch
            {
                30 => Target30FpsMs,
                > 0 => 1000f / session.Metadata.TargetFrameRate,
                _ => Target60FpsMs
            };

            var frameTimes = frames.Select(f => f.CpuFrameTimeMs).OrderBy(t => t).ToArray();
            int dropCount = frameTimes.Count(t => t > targetMs);
            int severeCount = frameTimes.Count(t => t > SevereDropMs);

            float p95 = frameTimes[(int)(frameTimes.Length * 0.95f)];
            float p99 = frameTimes[(int)(frameTimes.Length * 0.99f)];
            float avg = frameTimes.Average();
            float max = frameTimes.Max();

            if (dropCount == 0) return findings;

            float dropRatio = (float)dropCount / frames.Length;
            var severity = severeCount > 5 ? Severity.Critical
                         : dropRatio > 0.1f ? Severity.Warning
                         : Severity.Info;

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = $"Frame Drops ({dropCount} frames over {targetMs:F1}ms)",
                Description = $"Avg: {avg:F2}ms | P95: {p95:F2}ms | P99: {p99:F2}ms | Max: {max:F2}ms. " +
                              $"{dropCount}/{frames.Length} frames exceeded target. " +
                              $"{severeCount} severe drops (>{SevereDropMs}ms).",
                Recommendation = "Profile flagged frames in Unity Profiler. Check for expensive physics, " +
                                 "rendering bottlenecks, or heavy script execution in Update/LateUpdate.",
                Metric = p95,
                Threshold = targetMs,
                FrameIndex = -1
            });

            return findings;
        }
    }
}
