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

            // Detect cyclic instantiation pattern from scene snapshots.
            // If both totalAdded and totalRemoved are large and roughly balanced,
            // the growth is from object lifecycle churn (Instantiate/Destroy cycles),
            // not a traditional leak where objects accumulate without being freed.
            bool isCyclicChurn = false;
            int cyclicAmplitude = 0;
            var snapshots = session.SceneSnapshots;
            if (snapshots != null && snapshots.Count >= 3)
            {
                int totalAdded = 0, totalRemoved = 0;
                int snapsWithLargeAdds = 0, snapsWithLargeRemoves = 0;
                const int CyclicAmplitudeThreshold = 30;
                foreach (var snap in snapshots)
                {
                    int added = snap.Added?.Length ?? 0;
                    int removed = snap.Removed?.Length ?? 0;
                    totalAdded += added;
                    totalRemoved += removed;
                    if (added > CyclicAmplitudeThreshold) snapsWithLargeAdds++;
                    if (removed > CyclicAmplitudeThreshold) snapsWithLargeRemoves++;
                }
                // Cyclic if both adds and removes are substantial and multiple snapshots
                // show large changes in both directions (the hallmark of Instantiate/Destroy cycles)
                if (snapsWithLargeAdds >= 2 && snapsWithLargeRemoves >= 2 &&
                    totalAdded > CyclicAmplitudeThreshold && totalRemoved > CyclicAmplitudeThreshold)
                {
                    int minChurn = totalAdded < totalRemoved ? totalAdded : totalRemoved;
                    int maxChurn = totalAdded > totalRemoved ? totalAdded : totalRemoved;
                    float balance = maxChurn > 0 ? (float)minChurn / maxChurn : 0f;
                    if (balance > 0.25f)
                    {
                        isCyclicChurn = true;
                        cyclicAmplitude = (totalAdded + totalRemoved) / 2;
                    }
                }
            }

            string title, description, recommendation;
            if (isCyclicChurn)
            {
                title = $"Memory Growth from Object Lifecycle Churn ({slopeMBs:F2} MB/s)";
                description = $"Heap grew from {startHeap:F1}MB to {endHeap:F1}MB over {frames.Length} frames " +
                              $"(slope {slopeMBs:F2} MB/s). Scene snapshots show cyclic Instantiate/Destroy bursts " +
                              $"averaging ~{cyclicAmplitude} objects per cycle. " +
                              $"Unity's managed heap expands to absorb each burst but does not shrink between GC cycles, " +
                              $"causing gradual heap inflation rather than a traditional object leak.";
                recommendation = "Pool the objects being instantiated and destroyed in cycles instead of " +
                                 "Instantiate/Destroy. This eliminates per-cycle heap expansion, GC pressure from " +
                                 "constructor allocations, and component setup costs. " +
                                 "Unity's UnityEngine.Pool.ObjectPool<T> or a custom pool suffices.";
            }
            else
            {
                title = $"Potential Memory Leak ({slopeMBs:F2} MB/s growth)";
                description = $"Heap grew from {startHeap:F1}MB to {endHeap:F1}MB " +
                              $"over {frames.Length} frames. Linear regression slope: {slopeMBs:F2} MB/s.";
                recommendation = "Check for undisposed asset handles (missing Dispose/BindTo), " +
                                 "growing collections, retained event handlers, or texture/mesh leaks.";
            }

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = title,
                Description = description,
                Recommendation = recommendation,
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
