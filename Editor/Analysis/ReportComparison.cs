using System.Collections.Generic;
using System.Linq;

namespace DrWario.Editor.Analysis
{
    public enum FindingDiffStatus { Fixed, New, Persists }

    public struct FindingDiff
    {
        public DiagnosticFinding Finding;
        public FindingDiffStatus Status;
        public int SeverityChange;
        public float MetricDelta;
    }

    public struct GradeDelta
    {
        public string Category;
        public char GradeA;
        public char GradeB;
        public float ScoreDelta;
    }

    public struct MetricDelta
    {
        public float AvgCpuTimeDelta;
        public float P95CpuTimeDelta;
        public float GcRateDelta;
        public float MemorySlopeDelta;
        public float DrawCallsDelta;
        public float HealthScoreDelta;
    }

    public class ReportComparison
    {
        public DiagnosticReport ReportA { get; }
        public DiagnosticReport ReportB { get; }
        public float OverallGradeDelta { get; }
        public Dictionary<string, GradeDelta> CategoryDeltas { get; } = new();
        public List<FindingDiff> FindingDiffs { get; } = new();
        public MetricDelta MetricDeltas { get; }

        public ReportComparison(DiagnosticReport reportA, DiagnosticReport reportB)
        {
            ReportA = reportA;
            ReportB = reportB;

            OverallGradeDelta = reportB.HealthScore - reportA.HealthScore;

            MetricDeltas = new MetricDelta
            {
                AvgCpuTimeDelta = reportB.AvgCpuTimeMs - reportA.AvgCpuTimeMs,
                P95CpuTimeDelta = reportB.P95CpuTimeMs - reportA.P95CpuTimeMs,
                GcRateDelta = reportB.AvgGcAllocBytes - reportA.AvgGcAllocBytes,
                MemorySlopeDelta = reportB.MemorySlope - reportA.MemorySlope,
                DrawCallsDelta = reportB.AvgDrawCalls - reportA.AvgDrawCalls,
                HealthScoreDelta = reportB.HealthScore - reportA.HealthScore
            };

            ComputeCategoryDeltas();
            ComputeFindingDiffs();
        }

        private void ComputeCategoryDeltas()
        {
            var allCategories = ReportA.CategoryGrades.Keys
                .Union(ReportB.CategoryGrades.Keys)
                .Distinct();

            foreach (var cat in allCategories)
            {
                char gradeA = ReportA.CategoryGrades.TryGetValue(cat, out var gA) ? gA : '-';
                char gradeB = ReportB.CategoryGrades.TryGetValue(cat, out var gB) ? gB : '-';

                float scoreA = GradeToScore(gradeA);
                float scoreB = GradeToScore(gradeB);

                CategoryDeltas[cat] = new GradeDelta
                {
                    Category = cat,
                    GradeA = gradeA,
                    GradeB = gradeB,
                    ScoreDelta = scoreB - scoreA
                };
            }
        }

        private void ComputeFindingDiffs()
        {
            var findingsA = ReportA.Findings;
            var findingsB = ReportB.Findings;

            // Build lookup by matching key: RuleId + Category
            var keyedA = new Dictionary<string, DiagnosticFinding>();
            foreach (var f in findingsA)
            {
                string key = $"{f.RuleId}|{f.Category}";
                if (!keyedA.ContainsKey(key))
                    keyedA[key] = f;
            }

            var keyedB = new Dictionary<string, DiagnosticFinding>();
            foreach (var f in findingsB)
            {
                string key = $"{f.RuleId}|{f.Category}";
                if (!keyedB.ContainsKey(key))
                    keyedB[key] = f;
            }

            // Fixed: in A but not B
            foreach (var kvp in keyedA)
            {
                if (!keyedB.ContainsKey(kvp.Key))
                {
                    FindingDiffs.Add(new FindingDiff
                    {
                        Finding = kvp.Value,
                        Status = FindingDiffStatus.Fixed,
                        SeverityChange = 0,
                        MetricDelta = 0
                    });
                }
            }

            // New: in B but not A
            foreach (var kvp in keyedB)
            {
                if (!keyedA.ContainsKey(kvp.Key))
                {
                    FindingDiffs.Add(new FindingDiff
                    {
                        Finding = kvp.Value,
                        Status = FindingDiffStatus.New,
                        SeverityChange = 0,
                        MetricDelta = 0
                    });
                }
            }

            // Persists: in both
            foreach (var kvp in keyedA)
            {
                if (keyedB.TryGetValue(kvp.Key, out var findingB))
                {
                    FindingDiffs.Add(new FindingDiff
                    {
                        Finding = findingB,
                        Status = FindingDiffStatus.Persists,
                        SeverityChange = (int)findingB.Severity - (int)kvp.Value.Severity,
                        MetricDelta = findingB.Metric - kvp.Value.Metric
                    });
                }
            }
        }

        private static float GradeToScore(char grade) => grade switch
        {
            'A' => 95f,
            'B' => 85f,
            'C' => 75f,
            'D' => 65f,
            'F' => 50f,
            _ => 0f
        };
    }
}
