using DrWario.Editor.Analysis;
using NUnit.Framework;
using static DrWario.Tests.TestReportFactory;

namespace DrWario.Tests
{
    [TestFixture]
    public class DiagnosticReportGradingTests
    {
        [Test]
        public void ComputeGrades_NoFindings_GradeA()
        {
            var report = MakeReport();
            report.ComputeGrades();

            Assert.AreEqual(100f, report.HealthScore);
            Assert.AreEqual('A', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_OneCritical_ScoreDeducted15()
        {
            var report = MakeReport(MakeFinding(Severity.Critical));
            report.ComputeGrades();

            Assert.AreEqual(85f, report.HealthScore);
            Assert.AreEqual('B', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_OneWarning_ScoreDeducted5()
        {
            var report = MakeReport(MakeFinding(Severity.Warning));
            report.ComputeGrades();

            Assert.AreEqual(95f, report.HealthScore);
            Assert.AreEqual('A', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_OneInfo_ScoreDeducted1()
        {
            var report = MakeReport(MakeFinding(Severity.Info));
            report.ComputeGrades();

            Assert.AreEqual(99f, report.HealthScore);
            Assert.AreEqual('A', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_LowConfidence_ReducedPenalty()
        {
            // Critical with Low confidence: 15 * 0.25 = 3.75
            var report = MakeReport(MakeFinding(Severity.Critical, Confidence.Low));
            report.ComputeGrades();

            Assert.AreEqual(96.25f, report.HealthScore, 0.01f);
            Assert.AreEqual('A', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_MediumConfidence_ReducedPenalty()
        {
            // Critical with Medium confidence: 15 * 0.6 = 9
            var report = MakeReport(MakeFinding(Severity.Critical, Confidence.Medium));
            report.ComputeGrades();

            Assert.AreEqual(91f, report.HealthScore, 0.01f);
            Assert.AreEqual('A', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_HighConfidence_FullPenalty()
        {
            var report = MakeReport(MakeFinding(Severity.Critical, Confidence.High));
            report.ComputeGrades();

            Assert.AreEqual(85f, report.HealthScore);
            Assert.AreEqual('B', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_ManyFindings_ScoreFlooredAtZero()
        {
            // 10 Critical * 15 = 150, should clamp to 0
            var findings = new DiagnosticFinding[10];
            for (int i = 0; i < 10; i++)
                findings[i] = MakeFinding(Severity.Critical);

            var report = MakeReport(findings);
            report.ComputeGrades();

            Assert.AreEqual(0f, report.HealthScore);
            Assert.AreEqual('F', report.OverallGrade);
        }

        [Test]
        public void ComputeGrades_CategoryGrades_ComputedPerCategory()
        {
            var report = MakeReport(
                MakeFinding(Severity.Critical, category: "CPU"),
                MakeFinding(Severity.Warning, category: "Memory")
            );
            report.ComputeGrades();

            Assert.IsTrue(report.CategoryGrades.ContainsKey("CPU"));
            Assert.IsTrue(report.CategoryGrades.ContainsKey("Memory"));
            // CPU: 100 - 15 = 85 => B
            Assert.AreEqual('B', report.CategoryGrades["CPU"]);
            // Memory: 100 - 5 = 95 => A
            Assert.AreEqual('A', report.CategoryGrades["Memory"]);
        }

        [Test]
        public void ComputeGrades_GradeBoundaries()
        {
            // Exactly 90 => A
            var report90 = MakeReport(
                MakeFinding(Severity.Warning),
                MakeFinding(Severity.Warning)
            );
            report90.ComputeGrades();
            Assert.AreEqual(90f, report90.HealthScore); // 100 - 5 - 5
            Assert.AreEqual('A', report90.OverallGrade);

            // 89 => B (one warning + one info = 100-5-1=94... need 80-89)
            // 100 - 15 - 1 = 84 => B
            var report84 = MakeReport(
                MakeFinding(Severity.Critical),
                MakeFinding(Severity.Info)
            );
            report84.ComputeGrades();
            Assert.AreEqual(84f, report84.HealthScore);
            Assert.AreEqual('B', report84.OverallGrade);

            // Exactly 80 => B: 100 - 15 - 5 = 80
            var report80 = MakeReport(
                MakeFinding(Severity.Critical),
                MakeFinding(Severity.Warning)
            );
            report80.ComputeGrades();
            Assert.AreEqual(80f, report80.HealthScore);
            Assert.AreEqual('B', report80.OverallGrade);

            // 79 => C: need score 70-79
            // 100 - 15 - 5 - 1 = 79
            var report79 = MakeReport(
                MakeFinding(Severity.Critical),
                MakeFinding(Severity.Warning),
                MakeFinding(Severity.Info)
            );
            report79.ComputeGrades();
            Assert.AreEqual(79f, report79.HealthScore);
            Assert.AreEqual('C', report79.OverallGrade);
        }

        [Test]
        public void ComputeGrades_MixedConfidence_CorrectPenalties()
        {
            // 5 Critical Low-confidence findings: 5 * (15 * 0.25) = 5 * 3.75 = 18.75
            var findings = new DiagnosticFinding[5];
            for (int i = 0; i < 5; i++)
                findings[i] = MakeFinding(Severity.Critical, Confidence.Low);

            var report = MakeReport(findings);
            report.ComputeGrades();

            Assert.AreEqual(81.25f, report.HealthScore, 0.01f);
            Assert.AreEqual('B', report.OverallGrade);
        }
    }
}
