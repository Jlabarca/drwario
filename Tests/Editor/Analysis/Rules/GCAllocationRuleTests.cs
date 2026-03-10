using System.Linq;
using DrWario.Editor.Analysis.Rules;
using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class GCAllocationRuleTests
    {
        private GCAllocationRule _rule;

        [SetUp]
        public void SetUp() => _rule = new GCAllocationRule();

        [Test]
        public void Analyze_NoSpikes_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, gcAllocBytes: 500) // under 1024 threshold
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
        public void Analyze_FewSpikes_InfoSeverity()
        {
            // 5 spikes out of 100 frames = 5% < 20%, and count <= 10
            var session = new TestSessionBuilder()
                .AddFrames(95, gcAllocBytes: 500)
                .AddFrames(5, gcAllocBytes: 2000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Info, findings[0].Severity);
            Assert.AreEqual("GC_SPIKE", findings[0].RuleId);
            Assert.AreEqual("Memory", findings[0].Category);
        }

        [Test]
        public void Analyze_MoreThan10Spikes_WarningSeverity()
        {
            // 15 spikes out of 100 = 15% < 20%, but count > 10
            var session = new TestSessionBuilder()
                .AddFrames(85, gcAllocBytes: 500)
                .AddFrames(15, gcAllocBytes: 2000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
        }

        [Test]
        public void Analyze_HighSpikeRatio_CriticalSeverity()
        {
            // 25 spikes out of 100 = 25% > 20%
            var session = new TestSessionBuilder()
                .AddFrames(75, gcAllocBytes: 500)
                .AddFrames(25, gcAllocBytes: 2000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_EditorBaseline_RaisesThreshold()
        {
            // All frames at 1500 bytes — above raw 1024 threshold but below 1024+600=1624 editor threshold
            var baseline = new EditorBaseline { IsValid = true, AvgGcAllocBytes = 600 };
            var session = new TestSessionBuilder()
                .AsEditorSession(baseline)
                .AddFrames(100, gcAllocBytes: 1500)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_EditorSession_HasEnvironmentNote()
        {
            var baseline = new EditorBaseline { IsValid = true, AvgGcAllocBytes = 200 };
            var session = new TestSessionBuilder()
                .AsEditorSession(baseline)
                .AddFrames(75, gcAllocBytes: 500)
                .AddFrames(25, gcAllocBytes: 5000) // well above adjusted threshold
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.IsNotNull(findings[0].EnvironmentNote);
            Assert.That(findings[0].EnvironmentNote, Does.Contain("Editor"));
        }

        [Test]
        public void Analyze_CaptureFramesExcluded()
        {
            // 5 spike frames, but all are marked as capture frames → excluded → no findings
            var builder = new TestSessionBuilder()
                .AddFrames(95, gcAllocBytes: 500)
                .AddFrames(5, gcAllocBytes: 5000);

            // Mark the spike frames (indices 95-99, FrameNumber = 95-99)
            for (int i = 95; i < 100; i++)
                builder.MarkCaptureFrame(i);

            var session = builder.Build();
            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_AffectedFramesPopulated()
        {
            var session = new TestSessionBuilder()
                .AddFrames(90, gcAllocBytes: 500)
                .AddFrames(10, gcAllocBytes: 3000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.IsNotNull(findings[0].AffectedFrames);
            Assert.AreEqual(10, findings[0].AffectedFrames.Length);
        }

        [Test]
        public void Analyze_WorstFrameTracked()
        {
            var session = new TestSessionBuilder()
                .AddFrames(90, gcAllocBytes: 500)
                .AddFrames(5, gcAllocBytes: 2000)
                .AddFrames(1, gcAllocBytes: 10000) // worst at index 95
                .AddFrames(4, gcAllocBytes: 2000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(95, findings[0].FrameIndex);
        }

        [Test]
        public void Analyze_TitleIncludesAvgBytesPerFrame()
        {
            // 100 frames: 90 at 0 bytes, 10 at 10240 bytes = avg 1024 bytes/frame
            var session = new TestSessionBuilder()
                .AddFrames(90, gcAllocBytes: 0)
                .AddFrames(10, gcAllocBytes: 10240)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            // Title should mention average KB/frame
            Assert.That(findings[0].Title, Does.Contain("avg").IgnoreCase
                .Or.Contain("KB/frame").IgnoreCase);
        }

        [Test]
        public void Analyze_DescriptionIncludesAvgBytesLine()
        {
            // All 100 frames at 5120 bytes = avg 5 KB/frame
            var session = new TestSessionBuilder()
                .AddFrames(100, gcAllocBytes: 5120)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            // Description should start with the avg alloc note
            Assert.That(findings[0].Description, Does.Contain("Avg").IgnoreCase);
        }

        // ── AllocCount fallback: fires when GcAllocBytes=0 but GcAllocCount is high ──

        [Test]
        public void Analyze_NoBytesButHighAllocCount_ProducesWarningFinding()
        {
            // GcAllocBytes=0 but GcAllocCount=100 (above 50 warning, below 200 critical)
            var session = new TestSessionBuilder().WithCapacity(200).Build();
            for (int i = 0; i < 100; i++)
                session.RecordFrame(new FrameSample { Timestamp = i * 0.0167f, DeltaTime = 0.0167f,
                    CpuFrameTimeMs = 5f, GcAllocBytes = 0, GcAllocCount = 100, FrameNumber = i });

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("GC_SPIKE", findings[0].RuleId);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
            Assert.That(findings[0].Title, Does.Contain("alloc").IgnoreCase);
        }

        [Test]
        public void Analyze_NoBytesButCriticalAllocCount_ProducesCriticalFinding()
        {
            // avg 920 allocs/frame (as seen in min_client OSX run) — Critical
            var session = new TestSessionBuilder().WithCapacity(200).Build();
            for (int i = 0; i < 100; i++)
                session.RecordFrame(new FrameSample { Timestamp = i * 0.0167f, DeltaTime = 0.0167f,
                    CpuFrameTimeMs = 2f, GcAllocBytes = 0, GcAllocCount = 920, FrameNumber = i });

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_NoBytesLowAllocCount_NoFinding()
        {
            // avg 30 allocs/frame — below 50 warning threshold
            var session = new TestSessionBuilder().WithCapacity(200).Build();
            for (int i = 0; i < 100; i++)
                session.RecordFrame(new FrameSample { Timestamp = i * 0.0167f, DeltaTime = 0.0167f,
                    GcAllocBytes = 0, GcAllocCount = 30, FrameNumber = i });

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_NoBytesNoCountData_NoFinding()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, gcAllocBytes: 0)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_NoBytesCountDataOnMinorityOfFrames_NoFinding()
        {
            // Count data on only 30% of frames — below 50% trust threshold
            var session = new TestSessionBuilder().WithCapacity(200).Build();
            for (int i = 0; i < 100; i++)
                session.RecordFrame(new FrameSample { Timestamp = i * 0.0167f, DeltaTime = 0.0167f,
                    GcAllocBytes = 0, GcAllocCount = i < 30 ? 500 : 0, FrameNumber = i });

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }
    }
}
