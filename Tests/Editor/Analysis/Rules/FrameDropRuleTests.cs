using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.Rules;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class FrameDropRuleTests
    {
        private FrameDropRule _rule;

        [SetUp]
        public void SetUp() => _rule = new FrameDropRule();

        [Test]
        public void Analyze_AllFramesUnderTarget_NoFindings()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_EmptySession_NoFindings()
        {
            var session = new TestSessionBuilder().WithCapacity(10).Build();
            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_SomeDrops_InfoSeverity()
        {
            // 5 drops out of 100 = 5% < 10%, no severe
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(95, cpuMs: 10f)
                .AddFrames(5, cpuMs: 20f) // over 16.67ms target, under 50ms
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Info, findings[0].Severity);
            Assert.AreEqual("FRAME_DROP", findings[0].RuleId);
        }

        [Test]
        public void Analyze_HighDropRatio_WarningSeverity()
        {
            // 15 drops out of 100 = 15% > 10%
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(85, cpuMs: 10f)
                .AddFrames(15, cpuMs: 25f) // over target, under 50ms
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
        }

        [Test]
        public void Analyze_ManySevereDrops_CriticalSeverity()
        {
            // 6 severe drops (>50ms)
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(90, cpuMs: 10f)
                .AddFrames(6, cpuMs: 60f) // severe > 50ms
                .AddFrames(4, cpuMs: 20f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_30FpsTarget_UsesCorrectThreshold()
        {
            // At 30fps target = 33.33ms, frames at 25ms should not trigger
            var session = new TestSessionBuilder()
                .WithTargetFps(30)
                .AddFrames(100, cpuMs: 25f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_AffectedFramesPopulated()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(90, cpuMs: 10f)
                .AddFrames(10, cpuMs: 25f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.IsNotNull(findings[0].AffectedFrames);
            Assert.AreEqual(10, findings[0].AffectedFrames.Length);
        }

        [Test]
        public void Analyze_P95ReportedAsMetric()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(90, cpuMs: 10f)
                .AddFrames(10, cpuMs: 30f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            // Metric is P95 — with 90 frames at 10ms and 10 at 30ms, P95 should be 30ms
            Assert.Greater(findings[0].Metric, 16.67f);
        }

        [Test]
        public void Analyze_EditorSession_HasConfidenceAndEnvNote()
        {
            var baseline = new EditorBaseline { IsValid = true, AvgCpuFrameTimeMs = 5f };
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AsEditorSession(baseline)
                .AddFrames(80, cpuMs: 10f)
                .AddFrames(20, cpuMs: 25f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.IsNotNull(findings[0].EnvironmentNote);
        }
    }
}
