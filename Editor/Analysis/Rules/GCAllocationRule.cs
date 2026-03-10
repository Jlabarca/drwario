using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class GCAllocationRule : IAnalysisRule, IConfigurableRule
    {
        public string Category => "Memory";
        public string RuleId => "GC_SPIKE";

        public string ThresholdLabel => "GC Spike Threshold (bytes)";
        public float DefaultThreshold => 1024f;
        public float MinThreshold => 256f;
        public float MaxThreshold => 16384f;

        private const long SpikeThresholdBytes = 1024; // 1KB per frame
        private const float CriticalSpikeRatio = 0.2f; // >20% of frames

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length == 0) return findings;

            // Editor adjustment: subtract baseline GC per frame before counting spikes
            bool isEditor = session.Metadata.IsEditor;
            var baseline = session.Metadata.Baseline;
            long effectiveThreshold = SpikeThresholdBytes;
            if (isEditor && baseline.IsValid)
                effectiveThreshold = SpikeThresholdBytes + baseline.AvgGcAllocBytes;

            // Exclude frames where DrWario itself triggered expensive captures
            // (scene hierarchy enumeration causes GC allocations that aren't the game's fault)
            var captureFrames = session.DrWarioCaptureFrames;

            int spikeCount = 0;
            int rawSpikeCount = 0;
            long worstAlloc = 0;
            int worstFrame = -1;
            var spikeFrames = new List<int>();

            for (int i = 0; i < frames.Length; i++)
            {
                // Skip frames where DrWario performed expensive captures
                if (captureFrames.Contains(frames[i].FrameNumber))
                    continue;

                if (frames[i].GcAllocBytes > SpikeThresholdBytes)
                    rawSpikeCount++;

                if (frames[i].GcAllocBytes > effectiveThreshold)
                {
                    spikeCount++;
                    spikeFrames.Add(i);
                    if (frames[i].GcAllocBytes > worstAlloc)
                    {
                        worstAlloc = frames[i].GcAllocBytes;
                        worstFrame = i;
                    }
                }
            }

            // Fallback: when GcAllocBytes is unavailable (ProfilerRecorder not present on some
            // platforms/editor configs), detect via GcAllocCount. 920 allocs/frame is as significant
            // as 102 KB/frame even without byte data.
            if (spikeCount == 0)
                return TryDetectByAllocCount(findings, frames, isEditor, session.Metadata);

            float ratio = (float)spikeCount / frames.Length;
            var severity = ratio > CriticalSpikeRatio ? Severity.Critical
                         : spikeCount > 10 ? Severity.Warning
                         : Severity.Info;

            // Average alloc bytes/frame (all frames, not just spikes — headline metric)
            long totalAllocBytes = 0;
            foreach (var f in frames) totalAllocBytes += f.GcAllocBytes;
            long avgAllocBytes = frames.Length > 0 ? totalAllocBytes / frames.Length : 0;

            // Include GC alloc count if available (from ProfilerRecorder)
            string allocCountNote = "";
            bool hasAllocCount = frames.Any(f => f.GcAllocCount > 0);
            if (hasAllocCount)
            {
                float avgCount = (float)frames.Average(f => f.GcAllocCount);
                int maxCount = frames.Max(f => f.GcAllocCount);
                allocCountNote = $" Avg {avgCount:F0} allocations/frame (peak {maxCount}).";
            }

            // Classify confidence based on whether spikes survive baseline subtraction
            var confidence = Confidence.High;
            string envNote = null;
            if (isEditor)
            {
                if (spikeCount < rawSpikeCount && spikeCount == 0)
                    confidence = Confidence.Low;
                else if (spikeCount < rawSpikeCount)
                    confidence = Confidence.Medium;
                envNote = EditorAdjustment.BuildEnvironmentNote(session.Metadata, "GC allocations");
            }

            string thresholdNote = isEditor && baseline.IsValid
                ? $" (adjusted from {SpikeThresholdBytes}B + {baseline.AvgGcAllocBytes}B editor baseline)"
                : "";

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = $"GC Allocation Spikes ({spikeCount} frames, avg {avgAllocBytes / 1024f:F0}KB/frame)",
                Description = $"Avg {avgAllocBytes / 1024f:F1}KB/frame across all frames. " +
                              $"{spikeCount}/{frames.Length} frames exceeded {effectiveThreshold}B threshold{thresholdNote}. " +
                              $"Worst: {worstAlloc / 1024f:F1}KB at frame {worstFrame}. " +
                              $"Spike ratio: {ratio:P1}.{allocCountNote}",
                Recommendation = "Reduce per-frame allocations. Check for string concatenation in Update(), " +
                                 "LINQ in hot paths, boxing of value types, and new[] in loops. " +
                                 "Use the Unity Profiler's GC.Alloc column to find exact allocation sources.",
                Metric = spikeCount,
                Threshold = SpikeThresholdBytes,
                FrameIndex = worstFrame,
                Confidence = confidence,
                EnvironmentNote = envNote,
                AffectedFrames = spikeFrames.Take(100).ToArray()
            });

            return findings;
        }

        /// <summary>
        /// Fallback detection when GcAllocBytes is unavailable (ProfilerRecorder not capturing
        /// byte amounts on this platform/editor config). Uses GcAllocCount instead.
        /// Fired in real sessions where iOS/OSX editor doesn't expose alloc bytes but does
        /// expose allocation counts (e.g., 920 allocs/frame average on min_client home UI).
        /// </summary>
        private static List<DiagnosticFinding> TryDetectByAllocCount(
            List<DiagnosticFinding> findings, FrameSample[] frames,
            bool isEditor, SessionMetadata metadata)
        {
            // Need count data on a meaningful fraction of frames
            int framesWithCountData = 0;
            long totalCount = 0;
            int maxCount = 0;
            foreach (var f in frames)
            {
                if (f.GcAllocCount > 0)
                {
                    framesWithCountData++;
                    totalCount += f.GcAllocCount;
                    if (f.GcAllocCount > maxCount) maxCount = f.GcAllocCount;
                }
            }

            // Need count data on at least half the frames to trust it
            if (framesWithCountData < frames.Length / 2) return findings;

            float avgCount = (float)totalCount / frames.Length;

            // Warning: >50 allocs/frame average (suggests objects created every frame)
            // Critical: >200 allocs/frame average (heavy allocation pressure)
            const float WarningAllocsPerFrame = 50f;
            const float CriticalAllocsPerFrame = 200f;

            if (avgCount < WarningAllocsPerFrame) return findings;

            var severity = avgCount >= CriticalAllocsPerFrame ? Severity.Critical : Severity.Warning;

            // High-alloc frames = frames over 10 allocs (essentially everything with data)
            int highAllocFrames = 0;
            foreach (var f in frames) if (f.GcAllocCount > 10) highAllocFrames++;
            float highAllocRatio = (float)highAllocFrames / frames.Length;

            string envNote = isEditor ? EditorAdjustment.BuildEnvironmentNote(metadata, "GC allocations") : null;

            findings.Add(new DiagnosticFinding
            {
                RuleId = "GC_SPIKE",
                Category = "Memory",
                Severity = severity,
                Title = $"High GC Allocation Rate (avg {avgCount:F0} allocs/frame)",
                Description = $"Avg {avgCount:F0} GC allocations/frame (peak {maxCount}/frame) across {framesWithCountData} frames. " +
                              $"{highAllocFrames}/{frames.Length} frames had >10 allocations ({highAllocRatio:P0}). " +
                              $"Note: allocation byte amounts were not available from the profiler on this platform/config — " +
                              $"count data used instead.",
                Recommendation = "Reduce per-frame object creation. Check for string concatenation in Update(), " +
                                 "LINQ queries in hot paths, boxing of value types, new[] in loops, " +
                                 "and Instantiate/Destroy cycles. Use object pooling for frequently created objects. " +
                                 "Enable the Allocation Profiler in Unity Profiler to identify specific allocation sites.",
                Metric = avgCount,
                Threshold = WarningAllocsPerFrame,
                FrameIndex = -1,
                Confidence = isEditor ? Confidence.Medium : Confidence.High,
                EnvironmentNote = envNote
            });

            return findings;
        }
    }
}
