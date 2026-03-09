using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class CorrelationEngineTests
    {
        [Test]
        public void Detect_NoFindings_NoCorrelations()
        {
            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(new List<DiagnosticFinding>(), session);
            Assert.AreEqual(0, correlations.Count);
        }

        [Test]
        public void Detect_NullFindings_NoCorrelations()
        {
            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(null, session);
            Assert.AreEqual(0, correlations.Count);
        }

        [Test]
        public void Detect_GcAndFrameDrops_OverlappingFrames_ProducesCorrelation()
        {
            // Create GC_SPIKE and FRAME_DROP findings with overlapping AffectedFrames
            var overlapping = Enumerable.Range(50, 20).ToArray();
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "GC_SPIKE", Category = "Memory", Severity = Severity.Warning,
                    Title = "GC Spikes", AffectedFrames = overlapping
                },
                new DiagnosticFinding
                {
                    RuleId = "FRAME_DROP", Category = "CPU", Severity = Severity.Warning,
                    Title = "Frame Drops", AffectedFrames = overlapping // 100% overlap
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);

            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_GC_DROPS"));
        }

        [Test]
        public void Detect_GcAndFrameDrops_NoOverlap_NoCorrelation()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "GC_SPIKE", Category = "Memory", Severity = Severity.Warning,
                    Title = "GC Spikes", AffectedFrames = new[] { 10, 11, 12, 13, 14 }
                },
                new DiagnosticFinding
                {
                    RuleId = "FRAME_DROP", Category = "CPU", Severity = Severity.Warning,
                    Title = "Frame Drops", AffectedFrames = new[] { 80, 81, 82, 83, 84 }
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsFalse(correlations.Any(c => c.Id == "CORR_GC_DROPS"));
        }

        [Test]
        public void Detect_MemoryLeakAndGc_ProducesCorrelation()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "MEMORY_LEAK", Category = "Memory", Severity = Severity.Warning,
                    Title = "Memory Leak", Metric = 2_000_000f
                },
                new DiagnosticFinding
                {
                    RuleId = "GC_SPIKE", Category = "Memory", Severity = Severity.Warning,
                    Title = "GC Spikes", Metric = 50f
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_LEAK_GC"));
            var leakGc = correlations.First(c => c.Id == "CORR_LEAK_GC");
            Assert.AreEqual(Severity.Critical, leakGc.Severity);
        }

        [Test]
        public void Detect_GpuBottleneckAndHighTriangles_ProducesCorrelation()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "BOTTLENECK", Category = "CPU", Severity = Severity.Warning,
                    Title = "GPU-Bound"
                },
                new DiagnosticFinding
                {
                    RuleId = "TRIANGLE_COUNT", Category = "Rendering", Severity = Severity.Warning,
                    Title = "High Triangle Count"
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_GPU_TRIS"));
        }

        [Test]
        public void Detect_CpuBottleneckAndHighTriangles_NoGpuTrisCorrelation()
        {
            // CPU-bound (not GPU) + high triangles should NOT produce CORR_GPU_TRIS
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "BOTTLENECK", Category = "CPU", Severity = Severity.Warning,
                    Title = "CPU-Bound"
                },
                new DiagnosticFinding
                {
                    RuleId = "TRIANGLE_COUNT", Category = "Rendering", Severity = Severity.Warning,
                    Title = "High Triangle Count"
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsFalse(correlations.Any(c => c.Id == "CORR_GPU_TRIS"));
        }

        [Test]
        public void Detect_CpuBottleneckAndDrawCalls_ProducesCorrelation()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "BOTTLENECK", Category = "CPU", Severity = Severity.Warning,
                    Title = "CPU-Bound"
                },
                new DiagnosticFinding
                {
                    RuleId = "DRAW_CALLS", Category = "Rendering", Severity = Severity.Warning,
                    Title = "High Draw Calls"
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_CPU_DC"));
        }

        [Test]
        public void Detect_SlowBootAndSlowAssets_ProducesCorrelation()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "SLOW_BOOT", Category = "Boot", Severity = Severity.Warning,
                    Title = "Slow Boot"
                },
                new DiagnosticFinding
                {
                    RuleId = "SLOW_ASSET_LOAD", Category = "Assets", Severity = Severity.Warning,
                    Title = "Slow Assets", Metric = 1000f
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_BOOT_ASSETS"));
        }

        [Test]
        public void Detect_PervasiveGc_ProducesCorrelation()
        {
            // GC_SPIKE finding + >80% of frames with GC > 0
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "GC_SPIKE", Category = "Memory", Severity = Severity.Warning,
                    Title = "GC Spikes", Metric = 50f
                }
            };

            // 90% of frames have GC > 0
            var session = new TestSessionBuilder()
                .AddFrames(90, gcAllocBytes: 500) // >0
                .AddFrames(10, gcAllocBytes: 0)
                .Build();

            var correlations = CorrelationEngine.Detect(findings, session);
            Assert.IsTrue(correlations.Any(c => c.Id == "CORR_PERVASIVE_GC"));
        }
    }
}
