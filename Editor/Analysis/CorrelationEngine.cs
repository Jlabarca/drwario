#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis
{
    /// <summary>
    /// Detects temporal and causal correlations between findings from different rules.
    /// Runs after individual rules, before report synthesis.
    /// Produces composite findings and correlation insights without AI.
    /// </summary>
    public static class CorrelationEngine
    {
        public struct CorrelationInsight
        {
            public string Id;
            public string[] SourceRuleIds;
            public string Title;
            public string Description;
            public string Recommendation;
            public Severity Severity;
            public Confidence Confidence;
        }

        /// <summary>
        /// Analyze findings and session data for cross-cutting correlations.
        /// Returns correlation insights that connect findings across categories.
        /// </summary>
        public static List<CorrelationInsight> Detect(List<DiagnosticFinding> findings, ProfilingSession session)
        {
            var insights = new List<CorrelationInsight>();
            if (findings == null || findings.Count == 0) return insights;

            var findingsByRule = findings.GroupBy(f => f.RuleId).ToDictionary(g => g.Key, g => g.ToList());

            // 1. GC spikes coinciding with frame drops → GC is causing hitches
            DetectGcFrameDropCorrelation(findingsByRule, session, insights);

            // 2. Asset loads coinciding with GC spikes → asset loading causes GC pressure
            DetectAssetLoadGcCorrelation(findingsByRule, session, insights);

            // 3. Asset loads during boot → boot slowness from synchronous loading
            DetectBootAssetCorrelation(findingsByRule, session, insights);

            // 4. Memory leak + GC spikes → accumulating objects
            DetectLeakGcCorrelation(findingsByRule, insights);

            // 5. GPU-bound + high triangles → geometry complexity
            DetectGpuGeometryCorrelation(findingsByRule, insights);

            // 6. CPU-bound + high draw calls → rendering overhead on CPU
            DetectCpuDrawCallCorrelation(findingsByRule, insights);

            // 7. Pervasive GC every frame → object pooling needed
            DetectPervasiveGcPattern(findingsByRule, session, insights);

            // 8. Object count growth correlating with GC/memory issues
            DetectObjectGrowthCorrelation(findingsByRule, session, insights);

            return insights;
        }

        private static void DetectGcFrameDropCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            ProfilingSession session,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("GC_SPIKE") || !byRule.ContainsKey("FRAME_DROP")) return;

            var gcFrames = GetAffectedFrameSet(byRule["GC_SPIKE"]);
            var dropFrames = GetAffectedFrameSet(byRule["FRAME_DROP"]);

            if (gcFrames.Count == 0 || dropFrames.Count == 0) return;

            // Check overlap: GC spike within ±2 frames of a frame drop
            int overlapCount = 0;
            foreach (int gcFrame in gcFrames)
            {
                for (int offset = -2; offset <= 2; offset++)
                {
                    if (dropFrames.Contains(gcFrame + offset))
                    {
                        overlapCount++;
                        break;
                    }
                }
            }

            float overlapRatio = (float)overlapCount / gcFrames.Count;
            if (overlapRatio < 0.3f) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_GC_DROPS",
                SourceRuleIds = new[] { "GC_SPIKE", "FRAME_DROP" },
                Title = $"GC allocations causing frame hitches ({overlapRatio:P0} overlap)",
                Description = $"{overlapCount} of {gcFrames.Count} GC spike frames coincide with frame drops. " +
                    "Garbage collection pauses are likely the primary cause of your frame time spikes.",
                Recommendation = "Focus on eliminating per-frame allocations first — this will likely fix both " +
                    "the GC warnings and most frame drops simultaneously. Use Unity Profiler's GC.Alloc column " +
                    "to find the hottest allocation sites.",
                Severity = overlapRatio > 0.6f ? Severity.Critical : Severity.Warning,
                Confidence = Confidence.High
            });
        }

        private static void DetectAssetLoadGcCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            ProfilingSession session,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("SLOW_ASSET_LOAD") || !byRule.ContainsKey("GC_SPIKE")) return;

            // Asset loads naturally cause GC pressure from deserialization
            var gcFinding = byRule["GC_SPIKE"].FirstOrDefault();
            var assetFinding = byRule["SLOW_ASSET_LOAD"].FirstOrDefault();

            if (gcFinding.Metric > 0 && assetFinding.Metric > 0)
            {
                insights.Add(new CorrelationInsight
                {
                    Id = "CORR_ASSET_GC",
                    SourceRuleIds = new[] { "SLOW_ASSET_LOAD", "GC_SPIKE" },
                    Title = "Asset loading contributing to GC pressure",
                    Description = "Slow asset loads and GC spikes are both present. Asset deserialization " +
                        "typically allocates temporary buffers that pressure the GC. This is especially impactful " +
                        "when assets load synchronously on the main thread.",
                    Recommendation = "Switch to async asset loading (LoadAssetAsync) and pre-warm assets during " +
                        "loading screens. Consider using Addressables for on-demand loading with automatic " +
                        "reference counting.",
                    Severity = Severity.Warning,
                    Confidence = Confidence.Medium
                });
            }
        }

        private static void DetectBootAssetCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            ProfilingSession session,
            List<CorrelationInsight> insights)
        {
            bool hasSlow = byRule.ContainsKey("SLOW_BOOT") || byRule.ContainsKey("TOTAL_BOOT_TIME");
            if (!hasSlow || !byRule.ContainsKey("SLOW_ASSET_LOAD")) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_BOOT_ASSETS",
                SourceRuleIds = new[] { "SLOW_BOOT", "SLOW_ASSET_LOAD" },
                Title = "Boot time inflated by synchronous asset loading",
                Description = "Slow boot stages and slow asset loads co-occur. Assets loaded during " +
                    "initialization block the boot pipeline. Each synchronous load adds its full " +
                    "duration to total boot time.",
                Recommendation = "Defer non-essential asset loads to after boot completes. Use a minimal " +
                    "loading screen with background async loading. Load only what's needed for the first " +
                    "frame, then stream in the rest.",
                Severity = Severity.Warning,
                Confidence = Confidence.High
            });
        }

        private static void DetectLeakGcCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("MEMORY_LEAK") || !byRule.ContainsKey("GC_SPIKE")) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_LEAK_GC",
                SourceRuleIds = new[] { "MEMORY_LEAK", "GC_SPIKE" },
                Title = "Memory leak amplifying GC pressure",
                Description = "Heap is growing over time while GC spikes are occurring. As leaked objects " +
                    "accumulate, the GC has more memory to scan on each collection, making GC pauses " +
                    "progressively worse over the session lifetime.",
                Recommendation = "Fixing the memory leak is the highest priority — it will reduce both heap " +
                    "growth and GC pause severity. Look for growing collections, undisposed assets, " +
                    "retained event handlers, and delegates that capture references.",
                Severity = Severity.Critical,
                Confidence = Confidence.High
            });
        }

        private static void DetectGpuGeometryCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("BOTTLENECK") || !byRule.ContainsKey("TRIANGLE_COUNT")) return;

            var bottleneck = byRule["BOTTLENECK"].FirstOrDefault();
            if (!bottleneck.Title.Contains("GPU")) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_GPU_TRIS",
                SourceRuleIds = new[] { "BOTTLENECK", "TRIANGLE_COUNT" },
                Title = "GPU bound due to geometry complexity",
                Description = "The GPU is the bottleneck and triangle count is high. The vertex processing " +
                    "stage is likely saturated. Reducing geometric complexity will directly improve " +
                    "GPU frame time.",
                Recommendation = "Add LOD groups to high-poly meshes, enable occlusion culling, and use " +
                    "mesh decimation tools. For mobile, target <500K triangles/frame. " +
                    "Consider using imposters for distant objects.",
                Severity = Severity.Critical,
                Confidence = Confidence.High
            });
        }

        private static void DetectCpuDrawCallCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("BOTTLENECK") || !byRule.ContainsKey("DRAW_CALLS")) return;

            var bottleneck = byRule["BOTTLENECK"].FirstOrDefault();
            if (!bottleneck.Title.Contains("CPU")) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_CPU_DC",
                SourceRuleIds = new[] { "BOTTLENECK", "DRAW_CALLS" },
                Title = "CPU bound due to rendering command overhead",
                Description = "The CPU is the bottleneck and draw call count is high. Each draw call " +
                    "requires CPU work to set up GPU state. The rendering thread is spending too much " +
                    "time issuing commands rather than doing game logic.",
                Recommendation = "Enable SRP Batcher (if using URP/HDRP), use GPU Instancing for repeated " +
                    "meshes, merge static geometry with Static Batching, and reduce unique material count. " +
                    "Target <500 draw calls for mobile, <2000 for desktop.",
                Severity = Severity.Critical,
                Confidence = Confidence.High
            });
        }

        private static void DetectPervasiveGcPattern(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            ProfilingSession session,
            List<CorrelationInsight> insights)
        {
            if (!byRule.ContainsKey("GC_SPIKE")) return;

            var frames = session.GetFrames();
            if (frames == null || frames.Length < 30) return;

            // Count frames with any GC allocation
            int gcFrameCount = 0;
            foreach (var f in frames)
            {
                if (f.GcAllocBytes > 0) gcFrameCount++;
            }

            float gcRatio = (float)gcFrameCount / frames.Length;
            if (gcRatio < 0.8f) return;

            insights.Add(new CorrelationInsight
            {
                Id = "CORR_PERVASIVE_GC",
                SourceRuleIds = new[] { "GC_SPIKE" },
                Title = $"GC allocations on {gcRatio:P0} of frames — object pooling recommended",
                Description = $"{gcFrameCount} of {frames.Length} frames have GC allocations. This pattern " +
                    "indicates objects are being created and discarded every frame rather than reused. " +
                    "This is the single most common cause of GC-related stutters in Unity.",
                Recommendation = "Implement object pooling for frequently instantiated objects. " +
                    "Replace per-frame 'new' calls with pool.Get()/Release(). " +
                    "Common culprits: particle systems, projectiles, UI elements, string formatting, " +
                    "LINQ queries, and lambda closures in hot paths.",
                Severity = Severity.Critical,
                Confidence = Confidence.High
            });
        }

        private static void DetectObjectGrowthCorrelation(
            Dictionary<string, List<DiagnosticFinding>> byRule,
            ProfilingSession session,
            List<CorrelationInsight> insights)
        {
            var snapshots = session.SceneSnapshots;
            if (snapshots == null || snapshots.Count < 2) return;

            var baseline = snapshots[0];
            var last = snapshots[snapshots.Count - 1];
            int growth = last.TotalObjectCount - baseline.TotalObjectCount;

            if (growth < 10) return; // Insignificant

            // Count total objects added across all snapshots
            int totalAdded = 0;
            int totalRemoved = 0;
            foreach (var snap in snapshots)
            {
                if (snap.Added != null) totalAdded += snap.Added.Length;
                if (snap.Removed != null) totalRemoved += snap.Removed.Length;
            }

            // Net growth with churn = likely instantiate/destroy pattern
            bool hasGc = byRule.ContainsKey("GC_SPIKE");
            bool hasLeak = byRule.ContainsKey("MEMORY_LEAK");

            if (hasGc && totalAdded > 20)
            {
                insights.Add(new CorrelationInsight
                {
                    Id = "CORR_OBJECT_CHURN",
                    SourceRuleIds = new[] { "GC_SPIKE" },
                    Title = $"Scene object churn: {totalAdded} created, {totalRemoved} destroyed during session",
                    Description = $"The scene had a net growth of {growth} objects ({baseline.TotalObjectCount} → {last.TotalObjectCount}). " +
                        $"High object creation/destruction rates generate GC pressure from constructor allocations, " +
                        $"component setup, and the managed wrappers around native objects.",
                    Recommendation = "Use object pooling for frequently instantiated objects (projectiles, particles, " +
                        "UI elements, VFX). Reuse existing GameObjects instead of Instantiate/Destroy cycles.",
                    Severity = totalAdded > 100 ? Severity.Critical : Severity.Warning,
                    Confidence = Confidence.High
                });
            }
            else if (hasLeak && growth > 20)
            {
                insights.Add(new CorrelationInsight
                {
                    Id = "CORR_OBJECT_LEAK",
                    SourceRuleIds = new[] { "MEMORY_LEAK" },
                    Title = $"Possible object leak: scene grew by {growth} objects",
                    Description = $"Scene object count grew from {baseline.TotalObjectCount} to {last.TotalObjectCount} " +
                        $"while memory leak was detected. Objects may be instantiated but never destroyed.",
                    Recommendation = "Check for objects that are Instantiated but never Destroyed. " +
                        "Look for missing cleanup in OnDisable/OnDestroy, or objects parented to DontDestroyOnLoad.",
                    Severity = Severity.Warning,
                    Confidence = Confidence.Medium
                });
            }
        }

        private static HashSet<int> GetAffectedFrameSet(List<DiagnosticFinding> findings)
        {
            var set = new HashSet<int>();
            foreach (var f in findings)
            {
                if (f.AffectedFrames != null)
                {
                    foreach (int frame in f.AffectedFrames)
                        set.Add(frame);
                }
                if (f.FrameIndex >= 0)
                    set.Add(f.FrameIndex);
            }
            return set;
        }
    }
}
#endif
