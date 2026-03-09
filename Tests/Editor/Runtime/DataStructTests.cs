using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class DataStructTests
    {
        [Test]
        public void DiagnosticFinding_DefaultValues()
        {
            var finding = new DiagnosticFinding();

            Assert.AreEqual(0, finding.FrameIndex); // struct default is 0, not -1
            Assert.AreEqual(Confidence.High, finding.Confidence); // first enum value
            Assert.IsNull(finding.ScriptPath);
            Assert.IsNull(finding.AssetPath);
            Assert.IsNull(finding.RuleId);
            Assert.IsNull(finding.AffectedFrames);
        }

        [Test]
        public void Severity_EnumOrdering()
        {
            Assert.AreEqual(0, (int)Severity.Info);
            Assert.AreEqual(1, (int)Severity.Warning);
            Assert.AreEqual(2, (int)Severity.Critical);
        }

        [Test]
        public void Confidence_EnumOrdering()
        {
            Assert.AreEqual(0, (int)Confidence.High);
            Assert.AreEqual(1, (int)Confidence.Medium);
            Assert.AreEqual(2, (int)Confidence.Low);
        }

        [Test]
        public void SceneCensus_DefaultIsInvalid()
        {
            var census = new SceneCensus();
            Assert.IsFalse(census.IsValid);
        }

        [Test]
        public void NetworkEventType_EnumValues()
        {
            Assert.AreEqual(0, (int)NetworkEventType.Send);
            Assert.AreEqual(1, (int)NetworkEventType.Receive);
            Assert.AreEqual(2, (int)NetworkEventType.Error);
        }

        [Test]
        public void SnapshotTrigger_EnumValues()
        {
            Assert.AreEqual(0, (int)SnapshotTrigger.Baseline);
            Assert.AreEqual(1, (int)SnapshotTrigger.Periodic);
            Assert.AreEqual(2, (int)SnapshotTrigger.FrameSpike);
            Assert.AreEqual(3, (int)SnapshotTrigger.GcSpike);
            Assert.AreEqual(4, (int)SnapshotTrigger.ObjectDelta);
        }

        [Test]
        public void EditorBaseline_DefaultIsInvalid()
        {
            var baseline = new EditorBaseline();
            Assert.IsFalse(baseline.IsValid);
            Assert.AreEqual(0, baseline.SampleCount);
        }
    }
}
