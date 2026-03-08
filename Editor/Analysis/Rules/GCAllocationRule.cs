using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class GCAllocationRule : IAnalysisRule
    {
        public string Category => "Memory";
        public string RuleId => "GC_SPIKE";

        private const long SpikeThresholdBytes = 1024; // 1KB per frame
        private const float CriticalSpikeRatio = 0.2f; // >20% of frames

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length == 0) return findings;

            int spikeCount = 0;
            long worstAlloc = 0;
            int worstFrame = -1;

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].GcAllocBytes > SpikeThresholdBytes)
                {
                    spikeCount++;
                    if (frames[i].GcAllocBytes > worstAlloc)
                    {
                        worstAlloc = frames[i].GcAllocBytes;
                        worstFrame = i;
                    }
                }
            }

            if (spikeCount == 0) return findings;

            float ratio = (float)spikeCount / frames.Length;
            var severity = ratio > CriticalSpikeRatio ? Severity.Critical
                         : spikeCount > 10 ? Severity.Warning
                         : Severity.Info;

            // Include GC alloc count if available (from ProfilerRecorder)
            string allocCountNote = "";
            bool hasAllocCount = frames.Any(f => f.GcAllocCount > 0);
            if (hasAllocCount)
            {
                float avgCount = (float)frames.Average(f => f.GcAllocCount);
                int maxCount = frames.Max(f => f.GcAllocCount);
                allocCountNote = $" Avg {avgCount:F0} allocations/frame (peak {maxCount}).";
            }

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = $"GC Allocation Spikes ({spikeCount} frames)",
                Description = $"{spikeCount}/{frames.Length} frames exceeded {SpikeThresholdBytes}B threshold. " +
                              $"Worst: {worstAlloc / 1024f:F1}KB at frame {worstFrame}. " +
                              $"Spike ratio: {ratio:P1}.{allocCountNote}",
                Recommendation = "Reduce per-frame allocations. Check for string concatenation in Update(), " +
                                 "LINQ in hot paths, boxing of value types, and new[] in loops. " +
                                 "Use the Unity Profiler's GC.Alloc column to find exact allocation sources.",
                Metric = spikeCount,
                Threshold = SpikeThresholdBytes,
                FrameIndex = worstFrame
            });

            return findings;
        }
    }
}
