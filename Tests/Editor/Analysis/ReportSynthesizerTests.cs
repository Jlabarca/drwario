using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class ReportSynthesizerTests
    {
        [Test]
        public void Synthesize_NoFindings_HealthySummary()
        {
            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var synthesis = ReportSynthesizer.Synthesize(report, new List<CorrelationEngine.CorrelationInsight>(), session);

            Assert.IsNotNull(synthesis.ExecutiveSummary);
            Assert.That(synthesis.ExecutiveSummary, Does.Contain("excellent").IgnoreCase
                .Or.Contain("good").IgnoreCase);
        }

        [Test]
        public void Synthesize_CriticalFindings_HasPrioritizedActions()
        {
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Critical, Confidence.High, category: "CPU", ruleId: "FRAME_DROP")
            );
            report.ComputeGrades();
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 25f)
                .Build();

            var synthesis = ReportSynthesizer.Synthesize(report, new List<CorrelationEngine.CorrelationInsight>(), session);

            Assert.Greater(synthesis.PrioritizedActions.Count, 0);
        }

        [Test]
        public void Synthesize_CorrelationDriven_ActionsFirst()
        {
            var findings = new List<DiagnosticFinding>
            {
                TestReportFactory.MakeFinding(Severity.Critical, Confidence.High, category: "Memory", ruleId: "MEMORY_LEAK"),
                TestReportFactory.MakeFinding(Severity.Warning, Confidence.High, category: "Memory", ruleId: "GC_SPIKE")
            };
            var report = TestReportFactory.MakeReport(findings.ToArray());
            report.ComputeGrades();

            var correlations = new List<CorrelationEngine.CorrelationInsight>
            {
                new CorrelationEngine.CorrelationInsight
                {
                    Id = "CORR_LEAK_GC",
                    SourceRuleIds = new[] { "MEMORY_LEAK", "GC_SPIKE" },
                    Title = "Memory leak amplifying GC pressure",
                    Severity = Severity.Critical,
                    Confidence = Confidence.High
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var synthesis = ReportSynthesizer.Synthesize(report, correlations, session);

            Assert.Greater(synthesis.PrioritizedActions.Count, 0);
            // First action should be correlation-driven (covers MEMORY_LEAK + GC_SPIKE)
            var first = synthesis.PrioritizedActions[0];
            Assert.AreEqual(1, first.Priority);
            Assert.That(first.RelatedRuleIds, Does.Contain("MEMORY_LEAK"));
        }

        [Test]
        public void Synthesize_BottleneckWithCorrelation_EnrichedSummary()
        {
            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "BOTTLENECK", Category = "CPU", Severity = Severity.Warning,
                    Title = "GPU-Bound", Description = "GPU exceeds CPU",
                    Recommendation = "Reduce GPU load"
                },
                new DiagnosticFinding
                {
                    RuleId = "TRIANGLE_COUNT", Category = "Rendering", Severity = Severity.Warning,
                    Title = "High Triangles", Description = "3M tris",
                    Recommendation = "Add LODs"
                }
            };
            var report = TestReportFactory.MakeReport(findings.ToArray());
            report.ComputeGrades();

            var correlations = new List<CorrelationEngine.CorrelationInsight>
            {
                new CorrelationEngine.CorrelationInsight
                {
                    Id = "CORR_GPU_TRIS",
                    SourceRuleIds = new[] { "BOTTLENECK", "TRIANGLE_COUNT" },
                    Title = "GPU bound due to geometry complexity",
                    Severity = Severity.Critical,
                    Confidence = Confidence.High
                }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var synthesis = ReportSynthesizer.Synthesize(report, correlations, session);

            Assert.IsNotNull(synthesis.BottleneckSummary);
            Assert.That(synthesis.BottleneckSummary, Does.Contain("geometry").IgnoreCase);
        }

        [Test]
        public void Synthesize_WarningsOnly_NoCritical_MediumImpactActions()
        {
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning, Confidence.High, category: "CPU", ruleId: "FRAME_DROP"),
                TestReportFactory.MakeFinding(Severity.Warning, Confidence.High, category: "Assets", ruleId: "SLOW_ASSET_LOAD")
            );
            report.ComputeGrades();

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var synthesis = ReportSynthesizer.Synthesize(report, new List<CorrelationEngine.CorrelationInsight>(), session);

            Assert.Greater(synthesis.PrioritizedActions.Count, 0);
            Assert.IsTrue(synthesis.PrioritizedActions.All(a => a.ExpectedImpact == "Medium"));
        }

        [Test]
        public void Synthesize_CorrelationsStoredInSynthesis()
        {
            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var correlations = new List<CorrelationEngine.CorrelationInsight>
            {
                new CorrelationEngine.CorrelationInsight { Id = "CORR_TEST", Title = "Test" }
            };

            var session = new TestSessionBuilder().AddFrames(100).Build();
            var synthesis = ReportSynthesizer.Synthesize(report, correlations, session);

            Assert.AreEqual(1, synthesis.Correlations.Count);
            Assert.AreEqual("CORR_TEST", synthesis.Correlations[0].Id);
        }
    }
}
