using DrWario.Editor.Analysis;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class EditorAdjustmentTests
    {
        [Test]
        public void SubtractBaseline_Float_FlooredAtZero()
        {
            Assert.AreEqual(0f, EditorAdjustment.SubtractBaseline(5f, 10f));
        }

        [Test]
        public void SubtractBaseline_Float_NormalCase()
        {
            Assert.AreEqual(15f, EditorAdjustment.SubtractBaseline(20f, 5f));
        }

        [Test]
        public void SubtractBaseline_Long_FlooredAtZero()
        {
            Assert.AreEqual(0L, EditorAdjustment.SubtractBaseline(100L, 200L));
        }

        [Test]
        public void SubtractBaseline_Int_FlooredAtZero()
        {
            Assert.AreEqual(0, EditorAdjustment.SubtractBaseline(3, 10));
        }

        [Test]
        public void ClassifyConfidence_NotEditor_AlwaysHigh()
        {
            Assert.AreEqual(Confidence.High,
                EditorAdjustment.ClassifyConfidence(100f, 50f, 80f, isEditor: false));
        }

        [Test]
        public void ClassifyConfidence_AdjustedWellAbove_High()
        {
            // adjustedMetric(200) > threshold(100) * 1.5 = 150
            Assert.AreEqual(Confidence.High,
                EditorAdjustment.ClassifyConfidence(300f, 200f, 100f, isEditor: true));
        }

        [Test]
        public void ClassifyConfidence_AdjustedAbove_Medium()
        {
            // adjustedMetric(120) > threshold(100) but < threshold*1.5(150)
            Assert.AreEqual(Confidence.Medium,
                EditorAdjustment.ClassifyConfidence(220f, 120f, 100f, isEditor: true));
        }

        [Test]
        public void ClassifyConfidence_AdjustedBelow_Low()
        {
            // adjustedMetric(80) < threshold(100)
            Assert.AreEqual(Confidence.Low,
                EditorAdjustment.ClassifyConfidence(180f, 80f, 100f, isEditor: true));
        }

        [Test]
        public void BuildEnvironmentNote_NonEditor_ReturnsNull()
        {
            var metadata = new SessionMetadata { IsEditor = false };
            Assert.IsNull(EditorAdjustment.BuildEnvironmentNote(metadata, "CPU time"));
        }

        [Test]
        public void BuildEnvironmentNote_EditorNoBaseline_ReturnsNull()
        {
            var metadata = new SessionMetadata
            {
                IsEditor = true,
                Baseline = new EditorBaseline { IsValid = false }
            };
            Assert.IsNull(EditorAdjustment.BuildEnvironmentNote(metadata, "CPU time"));
        }

        [Test]
        public void BuildEnvironmentNote_EditorWithBaseline_ContainsMetricName()
        {
            var metadata = new SessionMetadata
            {
                IsEditor = true,
                Baseline = new EditorBaseline { IsValid = true }
            };

            var note = EditorAdjustment.BuildEnvironmentNote(metadata, "CPU time");

            Assert.IsNotNull(note);
            Assert.IsTrue(note.Contains("CPU time"));
            Assert.IsTrue(note.Contains("editor overhead"));
        }

        [Test]
        public void BuildEnvironmentNote_SceneViewOpen_MentionsDrawCalls()
        {
            var metadata = new SessionMetadata
            {
                IsEditor = true,
                Baseline = new EditorBaseline { IsValid = true },
                SceneViewOpen = true
            };

            var note = EditorAdjustment.BuildEnvironmentNote(metadata, "test");

            Assert.IsTrue(note.Contains("Scene view"));
        }
    }
}
