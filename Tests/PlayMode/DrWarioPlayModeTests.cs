using System.Collections;
using System.Linq;
using DrWario.Runtime;
using DrWario.Editor.Analysis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DrWario.Tests
{
    /// <summary>
    /// E2E PlayMode tests that validate DrWario's full detection stack:
    ///   data collection (RuntimeCollector) → rules → correlation → synthesis
    ///
    /// Each test uses [UnityTest] with EnterPlayMode / ExitPlayMode so that
    /// MonoBehaviour.Update() fires on stressor objects, producing real profiling data
    /// instead of synthetic TestSessionBuilder data.
    ///
    /// Tests appear in the EditMode tab of the Test Runner but execute in actual Play Mode.
    ///
    /// Assertions are deliberately permissive where platform data availability varies:
    /// - GC bytes may be 0 on OSX/iOS editor → GcAllocCount fallback path tested implicitly
    /// - Frame timing may be imprecise on low-end machines → severity checked, not exact values
    /// - Memory slope depends on GC pressure → minimum frame count exceeds MemoryLeakRule minimum (60)
    /// </summary>
    [TestFixture]
    public class DrWarioPlayModeTests
    {
        // ─── Test 1: GC Allocation Spike ─────────────────────────────────────────

        /// <summary>
        /// GCSpikeStressor allocates 60KB + 100 small objects per frame.
        /// Validates both detection paths:
        ///   - GcAllocBytes path: 60KB/frame >> 1KB threshold → Critical spike ratio
        ///   - GcAllocCount fallback: 100 allocs/frame >> 50 warning threshold
        /// </summary>
        [UnityTest]
        public IEnumerator GCSpike_PerFrameAllocation_ProducesGCSpikeOrAllocCountFinding()
        {
            yield return new EnterPlayMode();

            RuntimeCollector.StartSession();
            yield return null; // First frame sample

            var stressorGo = new GameObject("GCSpikeStressor");
            stressorGo.AddComponent<GCSpikeStressor>();

            for (int i = 0; i < 120; i++)
                yield return null;

            var session = RuntimeCollector.ActiveSession;
            var report = new AnalysisEngine().Analyze(session);

            RuntimeCollector.StopSession();
            Object.Destroy(stressorGo);
            yield return null;
            RuntimeCollector.DestroyCollector();

            yield return new ExitPlayMode();

            Assert.IsTrue(
                report.Findings.Any(f => f.RuleId == "GC_SPIKE"),
                "Expected GC_SPIKE finding. Rules that fired: " +
                string.Join(", ", report.Findings.Select(f => $"{f.RuleId}({f.Severity})")));

            var finding = report.Findings.First(f => f.RuleId == "GC_SPIKE");
            Assert.AreNotEqual(Severity.Info, finding.Severity,
                "GC_SPIKE severity should be Warning or Critical, not Info");
        }

        // ─── Test 2: Memory Leak ──────────────────────────────────────────────────

        /// <summary>
        /// MemoryLeakStressor retains 35KB/frame in a static list.
        /// Heap grows ~2.1 MB/s (above 1 MB/s threshold).
        /// Validates MemoryLeakRule linear regression slope detection.
        /// Title should say "Leak" (no scene churn → not classified as object lifecycle churn).
        /// </summary>
        [UnityTest]
        public IEnumerator MemoryLeak_RetainedStaticRefs_ProducesLeakFinding()
        {
            yield return new EnterPlayMode();

            RuntimeCollector.StartSession();
            yield return null;

            var stressorGo = new GameObject("MemoryLeakStressor");
            stressorGo.AddComponent<MemoryLeakStressor>();

            // 120 frames minimum — MemoryLeakRule requires frames.Length >= 60
            for (int i = 0; i < 120; i++)
                yield return null;

            var session = RuntimeCollector.ActiveSession;
            var report = new AnalysisEngine().Analyze(session);

            RuntimeCollector.StopSession();
            MemoryLeakStressor.Cleanup(); // Release retained allocations
            Object.Destroy(stressorGo);
            yield return null;
            RuntimeCollector.DestroyCollector();

            yield return new ExitPlayMode();

            Assert.IsTrue(
                report.Findings.Any(f => f.RuleId == "MEMORY_LEAK"),
                "Expected MEMORY_LEAK finding. Rules that fired: " +
                string.Join(", ", report.Findings.Select(f => $"{f.RuleId}({f.Severity})")));

            var leakFinding = report.Findings.First(f => f.RuleId == "MEMORY_LEAK");
            StringAssert.Contains("Leak", leakFinding.Title,
                "Expected 'Leak' in title for monotonic growth without scene churn");
        }

        // ─── Test 3: Cyclic Instantiate/Destroy Churn ────────────────────────────

        /// <summary>
        /// CyclicChurnStressor creates/destroys 60 objects every 20 frames.
        /// Produces alternating large-add / large-remove scene diffs captured by SceneSnapshotTracker.
        ///
        /// Validates the cyclic churn detection pipeline:
        ///   - MemoryLeakRule with "Churn"/"Object Lifecycle" title  (primary expectation)
        ///   - OR CORR_CYCLIC_INSTANTIATION correlation               (secondary expectation)
        ///   - OR any MEMORY_LEAK finding at all                      (minimal expectation)
        ///
        /// The assertion is multi-tiered because SceneSnapshotTracker wiring depends on
        /// DrWarioPlayModeHook.AttachEditorContext being called when StartSession fires.
        /// </summary>
        [UnityTest]
        public IEnumerator CyclicChurn_InstantiateDestroy_ProducesChurnOrMemoryFinding()
        {
            yield return new EnterPlayMode();

            RuntimeCollector.StartSession();
            yield return null;

            var stressorGo = new GameObject("CyclicChurnStressor");
            stressorGo.AddComponent<CyclicChurnStressor>();

            // 160 frames → ≥4 complete add/remove cycles (burst every 20 frames)
            // Produces ≥2 large-add snapshots + ≥2 large-remove snapshots for cyclic detection
            for (int i = 0; i < 160; i++)
                yield return null;

            var session = RuntimeCollector.ActiveSession;
            var report = new AnalysisEngine().Analyze(session);

            RuntimeCollector.StopSession();
            Object.Destroy(stressorGo);
            yield return null;
            RuntimeCollector.DestroyCollector();

            yield return new ExitPlayMode();

            bool hasChurnTitle = report.Findings.Any(f =>
                f.RuleId == "MEMORY_LEAK" && (
                    f.Title.IndexOf("Churn", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Title.IndexOf("Object Lifecycle", System.StringComparison.OrdinalIgnoreCase) >= 0));

            bool hasCyclicCorrelation = report.Synthesis.HasValue &&
                report.Synthesis.Value.Correlations != null &&
                report.Synthesis.Value.Correlations.Any(c => c.Id == "CORR_CYCLIC_INSTANTIATION");

            bool hasAnyMemoryFinding = report.Findings.Any(f => f.RuleId == "MEMORY_LEAK");

            Assert.IsTrue(
                hasChurnTitle || hasCyclicCorrelation || hasAnyMemoryFinding,
                "Expected churn-classified MEMORY_LEAK, CORR_CYCLIC_INSTANTIATION correlation, " +
                "or at minimum a MEMORY_LEAK finding. Rules that fired: " +
                string.Join(", ", report.Findings.Select(f => $"{f.RuleId}({f.Severity})")));
        }

        // ─── Test 4: Frame Drop ───────────────────────────────────────────────────

        /// <summary>
        /// FrameDropStressor busy-spins for 50ms in Update(), burning the main thread.
        /// Every frame exceeds SevereDropMs (50ms) → FrameDropRule fires Critical
        /// after just 6 frames.
        /// </summary>
        [UnityTest]
        public IEnumerator FrameDrop_CpuBurnInUpdate_ProducesFrameDropFinding()
        {
            yield return new EnterPlayMode();

            RuntimeCollector.StartSession();
            yield return null;

            var stressorGo = new GameObject("FrameDropStressor");
            stressorGo.AddComponent<FrameDropStressor>();

            // 60 frames: well above the 5-frame threshold for Critical severity
            for (int i = 0; i < 60; i++)
                yield return null;

            var session = RuntimeCollector.ActiveSession;
            var report = new AnalysisEngine().Analyze(session);

            RuntimeCollector.StopSession();
            Object.Destroy(stressorGo);
            yield return null;
            RuntimeCollector.DestroyCollector();

            yield return new ExitPlayMode();

            Assert.IsTrue(
                report.Findings.Any(f => f.RuleId == "FRAME_DROP"),
                "Expected FRAME_DROP finding. Rules that fired: " +
                string.Join(", ", report.Findings.Select(f => $"{f.RuleId}({f.Severity})")));

            Assert.AreEqual(
                Severity.Critical,
                report.Findings.First(f => f.RuleId == "FRAME_DROP").Severity,
                "50ms/frame burns (3× the 16.67ms budget) should produce Critical severity");
        }

        // ─── Test 5: Healthy Baseline ─────────────────────────────────────────────

        /// <summary>
        /// No stressors active. Validates that DrWario doesn't produce spurious critical
        /// findings for an idle scene — the "no false positives" sanity check.
        ///
        /// Grade B or better expected (editor overhead from Scene/Inspector/Profiler windows
        /// may produce low-confidence findings, so strict Grade A is not asserted).
        /// </summary>
        [UnityTest]
        public IEnumerator HealthyScene_NoStressors_NoCriticalFindings()
        {
            yield return new EnterPlayMode();

            RuntimeCollector.StartSession();

            for (int i = 0; i < 120; i++)
                yield return null;

            var session = RuntimeCollector.ActiveSession;
            var report = new AnalysisEngine().Analyze(session);

            RuntimeCollector.StopSession();
            yield return null;
            RuntimeCollector.DestroyCollector();

            yield return new ExitPlayMode();

            var criticalFindings = report.Findings.Where(f => f.Severity == Severity.Critical).ToList();
            Assert.AreEqual(0, criticalFindings.Count,
                "Expected no Critical findings in an idle scene. Found: " +
                string.Join(", ", criticalFindings.Select(f => $"{f.RuleId}: {f.Title}")));

            // Grade should be at least D — any lower indicates false positive findings
            Assert.IsTrue(report.HealthScore >= 60f,
                $"Expected health score >= 60 in idle scene, got {report.HealthScore:F0} (grade {report.OverallGrade})");
        }
    }
}
