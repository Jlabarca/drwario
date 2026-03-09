using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class AnalysisEngineTests
    {
        [Test]
        public void Constructor_RegistersDefaultRules()
        {
            var engine = new AnalysisEngine();
            Assert.AreEqual(8, engine.RegisteredRules.Count);
        }

        [Test]
        public void RegisterRule_CustomRuleIncluded()
        {
            var engine = new AnalysisEngine();
            engine.RegisterRule(new MockRule("MOCK_RULE", "Test"));
            Assert.AreEqual(9, engine.RegisteredRules.Count);
        }

        [Test]
        public void Analyze_EmptySession_ProducesReport()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder().WithCapacity(10).Build();

            var report = engine.Analyze(session);
            Assert.IsNotNull(report);
            Assert.IsNotNull(report.Findings);
        }

        [Test]
        public void Analyze_WithFrameDrops_ProducesFrameDropFinding()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(80, cpuMs: 10f)
                .AddFrames(20, cpuMs: 25f) // drops
                .Build();

            var report = engine.Analyze(session);
            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "FRAME_DROP"));
        }

        [Test]
        public void Analyze_ComputesSummaryMetrics()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 12f, gcAllocBytes: 500, drawCalls: 200)
                .Build();

            var report = engine.Analyze(session);
            Assert.Greater(report.AvgCpuTimeMs, 0f);
            Assert.Greater(report.AvgGcAllocBytes, 0f);
            Assert.Greater(report.AvgDrawCalls, 0f);
            Assert.Greater(report.P95CpuTimeMs, 0f);
        }

        [Test]
        public void Analyze_ProducesSynthesis()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var report = engine.Analyze(session);
            Assert.IsTrue(report.Synthesis.HasValue);
            Assert.IsNotNull(report.Synthesis.Value.ExecutiveSummary);
            Assert.IsNotNull(report.Synthesis.Value.PrioritizedActions);
        }

        [Test]
        public void Analyze_CustomRuleFindings_IncludedInReport()
        {
            var engine = new AnalysisEngine();
            engine.RegisterRule(new MockRule("CUSTOM_CHECK", "Custom"));

            var session = new TestSessionBuilder()
                .AddFrames(100)
                .Build();

            var report = engine.Analyze(session);
            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "CUSTOM_CHECK"));
        }

        [Test]
        public void Analyze_GradesComputed()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var report = engine.Analyze(session);
            Assert.AreNotEqual('\0', report.OverallGrade);
        }

        private class MockRule : IAnalysisRule
        {
            public string RuleId { get; }
            public string Category { get; }

            public MockRule(string ruleId, string category)
            {
                RuleId = ruleId;
                Category = category;
            }

            public List<DiagnosticFinding> Analyze(ProfilingSession session)
            {
                return new List<DiagnosticFinding>
                {
                    new DiagnosticFinding
                    {
                        RuleId = RuleId,
                        Category = Category,
                        Severity = Severity.Info,
                        Title = "Mock finding",
                        Description = "Test finding from mock rule",
                        Metric = 42f,
                        Threshold = 100f,
                        FrameIndex = -1
                    }
                };
            }
        }
    }
}
