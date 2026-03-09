using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.Rules;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class MemoryLeakRuleTests
    {
        private MemoryLeakRule _rule;

        [SetUp]
        public void SetUp() => _rule = new MemoryLeakRule();

        [Test]
        public void Analyze_FlatHeap_NoFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, heapBytes: 100_000_000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_TooFewFrames_NoFindings()
        {
            // Need >= 60 frames
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(50, startHeap: 100_000_000, growthPerFrame: 500_000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_SlowGrowth_BelowThreshold_NoFindings()
        {
            // Growth per frame = 1000 bytes. At 60fps, ~60KB/s — well under 1MB/s
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(100, startHeap: 100_000_000, growthPerFrame: 1000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(0, findings.Count);
        }

        [Test]
        public void Analyze_ModerateGrowth_WarningSeverity()
        {
            // Growth ~2MB/s at 60fps → growthPerFrame = 2*1024*1024/60 ≈ 34953
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(100, startHeap: 100_000_000, growthPerFrame: 35000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("MEMORY_LEAK", findings[0].RuleId);
            Assert.AreEqual(Severity.Warning, findings[0].Severity);
            Assert.AreEqual("Memory", findings[0].Category);
        }

        [Test]
        public void Analyze_RapidGrowth_CriticalSeverity()
        {
            // Growth ~10MB/s at 60fps → growthPerFrame = 10*1024*1024/60 ≈ 174763
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(100, startHeap: 100_000_000, growthPerFrame: 175000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Critical, findings[0].Severity);
        }

        [Test]
        public void Analyze_EditorSession_MediumConfidenceWithNote()
        {
            var session = new TestSessionBuilder()
                .AsEditorSession()
                .AddFramesWithGrowingHeap(100, startHeap: 100_000_000, growthPerFrame: 35000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Confidence.Medium, findings[0].Confidence);
            Assert.IsNotNull(findings[0].EnvironmentNote);
            Assert.That(findings[0].EnvironmentNote, Does.Contain("Editor"));
        }

        [Test]
        public void Analyze_StandaloneSession_HighConfidence()
        {
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(100, startHeap: 100_000_000, growthPerFrame: 35000)
                .Build();

            var findings = _rule.Analyze(session);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Confidence.High, findings[0].Confidence);
            Assert.IsNull(findings[0].EnvironmentNote);
        }
    }
}
