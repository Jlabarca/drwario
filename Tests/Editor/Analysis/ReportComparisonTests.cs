using DrWario.Editor.Analysis;
using NUnit.Framework;
using static DrWario.Tests.TestReportFactory;

namespace DrWario.Tests
{
    [TestFixture]
    public class ReportComparisonTests
    {
        [Test]
        public void Constructor_ComputesOverallGradeDelta()
        {
            var a = MakeReport();
            a.HealthScore = 80f;
            var b = MakeReport();
            b.HealthScore = 90f;

            var cmp = new ReportComparison(a, b);

            Assert.AreEqual(10f, cmp.OverallGradeDelta, 0.01f);
        }

        [Test]
        public void Constructor_ComputesMetricDeltas()
        {
            var a = MakeReportWithMetrics(avgCpu: 10f, p95Cpu: 20f, avgGc: 500f, memorySlope: 100f, avgDrawCalls: 200f);
            var b = MakeReportWithMetrics(avgCpu: 15f, p95Cpu: 25f, avgGc: 800f, memorySlope: 50f, avgDrawCalls: 150f);

            a.HealthScore = 80f;
            b.HealthScore = 70f;

            var cmp = new ReportComparison(a, b);

            Assert.AreEqual(5f, cmp.MetricDeltas.AvgCpuTimeDelta, 0.01f);
            Assert.AreEqual(5f, cmp.MetricDeltas.P95CpuTimeDelta, 0.01f);
            Assert.AreEqual(300f, cmp.MetricDeltas.GcRateDelta, 0.01f);
            Assert.AreEqual(-50f, cmp.MetricDeltas.MemorySlopeDelta, 0.01f);
            Assert.AreEqual(-50f, cmp.MetricDeltas.DrawCallsDelta, 0.01f);
            Assert.AreEqual(-10f, cmp.MetricDeltas.HealthScoreDelta, 0.01f);
        }

        [Test]
        public void FindingDiffs_FixedStatus_InANotB()
        {
            var a = MakeReport(MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory"));
            a.ComputeGrades();
            var b = MakeReport();
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            Assert.AreEqual(1, cmp.FindingDiffs.Count);
            Assert.AreEqual(FindingDiffStatus.Fixed, cmp.FindingDiffs[0].Status);
        }

        [Test]
        public void FindingDiffs_NewStatus_InBNotA()
        {
            var a = MakeReport();
            a.ComputeGrades();
            var b = MakeReport(MakeFinding(Severity.Critical, ruleId: "FRAME_DROP", category: "CPU"));
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            Assert.AreEqual(1, cmp.FindingDiffs.Count);
            Assert.AreEqual(FindingDiffStatus.New, cmp.FindingDiffs[0].Status);
        }

        [Test]
        public void FindingDiffs_PersistsStatus_InBoth()
        {
            var finding = MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory");
            var a = MakeReport(finding);
            a.ComputeGrades();
            var b = MakeReport(finding);
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            Assert.AreEqual(1, cmp.FindingDiffs.Count);
            Assert.AreEqual(FindingDiffStatus.Persists, cmp.FindingDiffs[0].Status);
        }

        [Test]
        public void FindingDiffs_Persists_SeverityChange()
        {
            var findingA = MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory");
            var findingB = MakeFinding(Severity.Critical, ruleId: "GC_SPIKE", category: "Memory");

            var a = MakeReport(findingA);
            a.ComputeGrades();
            var b = MakeReport(findingB);
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            var persists = cmp.FindingDiffs.Find(d => d.Status == FindingDiffStatus.Persists);
            // Critical(2) - Warning(1) = 1
            Assert.AreEqual(1, persists.SeverityChange);
        }

        [Test]
        public void FindingDiffs_Persists_MetricDelta()
        {
            var findingA = MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory", metric: 10f);
            var findingB = MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory", metric: 20f);

            var a = MakeReport(findingA);
            a.ComputeGrades();
            var b = MakeReport(findingB);
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            var persists = cmp.FindingDiffs.Find(d => d.Status == FindingDiffStatus.Persists);
            Assert.AreEqual(10f, persists.MetricDelta, 0.01f);
        }

        [Test]
        public void CategoryDeltas_NewCategoryInB()
        {
            var a = MakeReport();
            a.ComputeGrades();
            var b = MakeReport(MakeFinding(Severity.Warning, category: "Memory"));
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            Assert.IsTrue(cmp.CategoryDeltas.ContainsKey("Memory"));
            Assert.AreEqual('-', cmp.CategoryDeltas["Memory"].GradeA);
            Assert.AreNotEqual('-', cmp.CategoryDeltas["Memory"].GradeB);
        }

        [Test]
        public void CategoryDeltas_RemovedCategoryInB()
        {
            var a = MakeReport(MakeFinding(Severity.Warning, category: "Memory"));
            a.ComputeGrades();
            var b = MakeReport();
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            Assert.IsTrue(cmp.CategoryDeltas.ContainsKey("Memory"));
            Assert.AreNotEqual('-', cmp.CategoryDeltas["Memory"].GradeA);
            Assert.AreEqual('-', cmp.CategoryDeltas["Memory"].GradeB);
        }

        [Test]
        public void FindingDiffs_DuplicateRuleIdCategory_FirstOneWins()
        {
            var f1 = MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory", metric: 10f);
            var f2 = MakeFinding(Severity.Critical, ruleId: "GC_SPIKE", category: "Memory", metric: 50f);

            // Two findings with same key in report A
            var a = MakeReport(f1, f2);
            a.ComputeGrades();
            var b = MakeReport(MakeFinding(Severity.Warning, ruleId: "GC_SPIKE", category: "Memory", metric: 20f));
            b.ComputeGrades();

            var cmp = new ReportComparison(a, b);

            // Should only match once (first one wins)
            int persistsCount = 0;
            foreach (var d in cmp.FindingDiffs)
                if (d.Status == FindingDiffStatus.Persists) persistsCount++;

            Assert.AreEqual(1, persistsCount);
        }
    }
}
