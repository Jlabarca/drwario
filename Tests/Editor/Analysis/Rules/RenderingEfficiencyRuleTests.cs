using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.Rules;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class RenderingEfficiencyRuleTests
    {
        private RenderingEfficiencyRule _rule;

        [SetUp]
        public void SetUp() => _rule = new RenderingEfficiencyRule();

        [Test]
        public void Analyze_NoProfilerData_NoFindings()
        {
            // DrawCalls=0 for all frames → no profiler data
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 10f, drawCalls: 0)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_LowDrawCalls_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 500) // under 1000 threshold
                .Build();

            var findings = _rule.Analyze(session);
            Assert.IsFalse(findings.Any(f => f.RuleId == "DRAW_CALLS"));
        }

        [Test]
        public void Analyze_HighDrawCalls_ProducesFinding()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 1500) // > 1000 threshold
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "DRAW_CALLS");
            Assert.IsNotNull(finding.Title);
            Assert.AreEqual("Rendering", finding.Category);
        }

        [Test]
        public void Analyze_CriticalDrawCalls_CriticalSeverity()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 3500) // > 3000 critical threshold
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "DRAW_CALLS");
            Assert.AreEqual(Severity.Critical, finding.Severity);
        }

        [Test]
        public void Analyze_HighSetPassCalls_ProducesFinding()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 500, setPassCalls: 150) // > 100 threshold
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "SET_PASS_CALLS");
            Assert.IsNotNull(finding.Title);
        }

        [Test]
        public void Analyze_CriticalSetPass_CriticalSeverity()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 500, setPassCalls: 250) // > 200 critical
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "SET_PASS_CALLS");
            Assert.AreEqual(Severity.Critical, finding.Severity);
        }

        [Test]
        public void Analyze_HighTriangles_ProducesFinding()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 500, triangles: 3_000_000) // > 2M threshold
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "TRIANGLE_COUNT");
            Assert.IsNotNull(finding.Title);
            Assert.AreEqual(Severity.Warning, finding.Severity);
        }

        [Test]
        public void Analyze_CriticalTriangles_CriticalSeverity()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, drawCalls: 500, triangles: 6_000_000) // > 5M critical
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "TRIANGLE_COUNT");
            Assert.AreEqual(Severity.Critical, finding.Severity);
        }

        [Test]
        public void Analyze_EditorBaseline_SubtractsDrawCalls()
        {
            // 1200 draw calls minus 300 editor baseline = 900, under 1000 threshold
            var baseline = new EditorBaseline { IsValid = true, AvgDrawCalls = 300 };
            var session = new TestSessionBuilder()
                .AsEditorSession(baseline)
                .AddFrames(100, drawCalls: 1200)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.IsFalse(findings.Any(f => f.RuleId == "DRAW_CALLS"));
        }

        [Test]
        public void Analyze_EditorSession_TrianglesHaveMediumConfidence()
        {
            var session = new TestSessionBuilder()
                .AsEditorSession()
                .AddFrames(100, drawCalls: 500, triangles: 3_000_000)
                .Build();

            var finding = _rule.Analyze(session).FirstOrDefault(f => f.RuleId == "TRIANGLE_COUNT");
            Assert.AreEqual(Confidence.Medium, finding.Confidence);
        }
    }
}
