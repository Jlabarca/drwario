using System;
using DrWario.Editor.Analysis;
using DrWario.Runtime;

namespace DrWario.Tests
{
    /// <summary>
    /// Factory methods for creating DiagnosticReport and DiagnosticFinding instances in tests.
    /// </summary>
    public static class TestReportFactory
    {
        public static DiagnosticReport MakeReport(params DiagnosticFinding[] findings)
        {
            var report = new DiagnosticReport
            {
                GeneratedAt = DateTime.UtcNow,
                Session = new SessionMetadata
                {
                    Platform = "Test",
                    UnityVersion = "2022.3.0f1",
                    StartTime = DateTime.UtcNow.AddSeconds(-10),
                    EndTime = DateTime.UtcNow
                }
            };
            report.Findings.AddRange(findings);
            return report;
        }

        public static DiagnosticReport MakeReportWithMetrics(
            float avgCpu = 10f, float p95Cpu = 15f, float p99Cpu = 20f,
            float avgGc = 500f, float avgDrawCalls = 100f, float memorySlope = 0f,
            params DiagnosticFinding[] findings)
        {
            var report = MakeReport(findings);
            report.AvgCpuTimeMs = avgCpu;
            report.P95CpuTimeMs = p95Cpu;
            report.P99CpuTimeMs = p99Cpu;
            report.AvgGcAllocBytes = avgGc;
            report.AvgDrawCalls = avgDrawCalls;
            report.MemorySlope = memorySlope;
            return report;
        }

        public static DiagnosticFinding MakeFinding(
            Severity severity,
            Confidence confidence = Confidence.High,
            string category = "CPU",
            string ruleId = "TEST",
            float metric = 0f,
            float threshold = 0f)
        {
            return new DiagnosticFinding
            {
                RuleId = ruleId,
                Category = category,
                Severity = severity,
                Confidence = confidence,
                Title = $"Test {severity}",
                Description = "Test description",
                Recommendation = "Test recommendation",
                FrameIndex = -1,
                Metric = metric,
                Threshold = threshold
            };
        }
    }
}
