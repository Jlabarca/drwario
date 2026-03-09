using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.Rules;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class CPUvsGPUBottleneckRuleTests
    {
        private CPUvsGPUBottleneckRule _rule;

        [SetUp]
        public void SetUp() => _rule = new CPUvsGPUBottleneckRule();

        [Test]
        public void Analyze_TooFewFrames_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(20, cpuMs: 25f, gpuMs: 10f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_BothUnderTarget_NoFindings()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f, gpuMs: 8f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_NoGpuData_NoFindings()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 25f, gpuMs: 0f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_GpuBound_FindingTitleContainsGPU()
        {
            // GPU=25ms > CPU=10ms * 1.3, GPU > target 16.67ms
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f, gpuMs: 25f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.That(findings[0].Title, Does.Contain("GPU"));
            Assert.AreEqual("BOTTLENECK", findings[0].RuleId);
            Assert.AreEqual("CPU", findings[0].Category);
        }

        [Test]
        public void Analyze_CpuBound_FindingTitleContainsCPU()
        {
            // CPU=25ms > GPU=10ms * 1.3, CPU > target 16.67ms
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 25f, gpuMs: 10f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.That(findings[0].Title, Does.Contain("CPU"));
        }

        [Test]
        public void Analyze_BothOverloaded_CriticalSeverity()
        {
            // Both CPU=20ms and GPU=20ms over target, but neither is 1.3× the other
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 20f, gpuMs: 20f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
            Assert.That(findings[0].Title, Does.Contain("Both"));
        }

        [Test]
        public void Analyze_GpuSeverely_CriticalSeverity()
        {
            // GPU=40ms > 2 * target 16.67ms
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f, gpuMs: 40f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_GpuModerate_WarningSeverity()
        {
            // GPU=25ms > target but < 2*target (33.34ms)
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f, gpuMs: 25f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
        }

        [Test]
        public void Analyze_EditorSession_HasEnvironmentNote()
        {
            var baseline = new EditorBaseline { IsValid = true, AvgCpuFrameTimeMs = 3f };
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AsEditorSession(baseline)
                .AddFrames(100, cpuMs: 25f, gpuMs: 10f)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.IsNotNull(findings[0].EnvironmentNote);
        }
    }
}
