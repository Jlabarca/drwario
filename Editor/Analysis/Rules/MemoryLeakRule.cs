using System.Collections.Generic;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class MemoryLeakRule : IAnalysisRule
    {
        public string Category => "Memory";
        public string RuleId => "MEMORY_LEAK";

        // Bytes per second growth rate that triggers a warning
        private const double GrowthWarningBytesPerSec = 1024 * 1024; // 1MB/s
        private const double GrowthCriticalBytesPerSec = 5 * 1024 * 1024; // 5MB/s

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length < 60) return findings; // Need at least ~1s of data

            // Simple linear regression on heap size over time
            // y = totalHeapBytes, x = timestamp
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = frames.Length;

            for (int i = 0; i < n; i++)
            {
                double x = frames[i].Timestamp;
                double y = frames[i].TotalHeapBytes;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            double denom = n * sumX2 - sumX * sumX;
            if (denom == 0) return findings;

            double slope = (n * sumXY - sumX * sumY) / denom; // bytes per second

            if (slope <= GrowthWarningBytesPerSec) return findings;

            double startHeap = frames[0].TotalHeapBytes / (1024.0 * 1024.0);
            double endHeap = frames[n - 1].TotalHeapBytes / (1024.0 * 1024.0);
            double slopeMBs = slope / (1024.0 * 1024.0);

            var severity = slope > GrowthCriticalBytesPerSec ? Severity.Critical : Severity.Warning;

            // Editor memory includes Undo history, Inspector caches, import metadata.
            // Slope is still meaningful but may be inflated — tag with Medium confidence.
            bool isEditor = session.Metadata.IsEditor;

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = $"Potential Memory Leak ({slopeMBs:F2} MB/s growth)",
                Description = $"Heap grew from {startHeap:F1}MB to {endHeap:F1}MB " +
                              $"over {frames.Length} frames. Linear regression slope: {slopeMBs:F2} MB/s.",
                Recommendation = "Check for undisposed asset handles (missing Dispose/BindTo), " +
                                 "growing collections, retained event handlers, or texture/mesh leaks.",
                Metric = (float)slope,
                Threshold = (float)GrowthWarningBytesPerSec,
                FrameIndex = -1,
                Confidence = isEditor ? Confidence.Medium : Confidence.High,
                EnvironmentNote = isEditor
                    ? "Editor memory includes Undo history, Inspector caches, and import metadata. " +
                      "Verify memory growth in a standalone build for accurate leak detection."
                    : null
            });

            return findings;
        }
    }
}
