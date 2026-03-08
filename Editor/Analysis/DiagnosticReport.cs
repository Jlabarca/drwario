using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrWario.Runtime;
using UnityEngine;

namespace DrWario.Editor.Analysis
{
    public class DiagnosticReport
    {
        public DateTime GeneratedAt;
        public SessionMetadata Session;
        public char OverallGrade;
        public float HealthScore;
        public Dictionary<string, char> CategoryGrades = new();
        public List<DiagnosticFinding> Findings = new();

        public void ComputeGrades()
        {
            // Overall: start at 100, subtract per finding.
            // Low-confidence findings (likely editor overhead) get reduced penalty.
            float score = 100f;
            foreach (var f in Findings)
            {
                float penalty = f.Severity switch
                {
                    Severity.Critical => 15f,
                    Severity.Warning => 5f,
                    _ => 1f
                };
                // Reduce penalty for low-confidence editor findings
                if (f.Confidence == Confidence.Low)
                    penalty *= 0.25f;
                else if (f.Confidence == Confidence.Medium)
                    penalty *= 0.6f;
                score -= penalty;
            }
            HealthScore = Mathf.Clamp(score, 0f, 100f);
            OverallGrade = ScoreToGrade(HealthScore);

            // Per-category grades
            var categories = Findings.Select(f => f.Category).Distinct();
            foreach (var cat in categories)
            {
                float catScore = 100f;
                foreach (var f in Findings.Where(f => f.Category == cat))
                {
                    float catPenalty = f.Severity switch
                    {
                        Severity.Critical => 15f,
                        Severity.Warning => 5f,
                        _ => 1f
                    };
                    if (f.Confidence == Confidence.Low)
                        catPenalty *= 0.25f;
                    else if (f.Confidence == Confidence.Medium)
                        catPenalty *= 0.6f;
                    catScore -= catPenalty;
                }
                CategoryGrades[cat] = ScoreToGrade(Mathf.Clamp(catScore, 0f, 100f));
            }
        }

        private static char ScoreToGrade(float score) => score switch
        {
            >= 90f => 'A',
            >= 80f => 'B',
            >= 70f => 'C',
            >= 60f => 'D',
            _ => 'F'
        };

        public string ExportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("       DrWario Diagnostic Report");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Generated: {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Platform:  {Session.Platform}");
            sb.AppendLine($"Unity:     {Session.UnityVersion}");
            sb.AppendLine($"Duration:  {(Session.EndTime - Session.StartTime).TotalSeconds:F1}s");
            sb.AppendLine($"Environment: {(Session.IsEditor ? "Editor" : "Build")}");
            if (Session.IsEditor && Session.Baseline.IsValid)
                sb.AppendLine($"Baseline:  CPU {Session.Baseline.AvgCpuFrameTimeMs:F1}ms | GC {Session.Baseline.AvgGcAllocBytes}B/frame | Draw calls {Session.Baseline.AvgDrawCalls}");
            sb.AppendLine();
            sb.AppendLine($"  Overall Grade: {OverallGrade}  ({HealthScore:F0}/100)");
            sb.AppendLine();

            foreach (var kv in CategoryGrades)
                sb.AppendLine($"  [{kv.Value}] {kv.Key}");

            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("  Findings");
            sb.AppendLine("───────────────────────────────────────");

            foreach (var f in Findings.OrderByDescending(f => f.Severity))
            {
                var icon = f.Severity switch
                {
                    Severity.Critical => "[!!]",
                    Severity.Warning => "[! ]",
                    _ => "[i ]"
                };
                string confLabel = f.Confidence != Confidence.High ? $" [{f.Confidence} confidence]" : "";
                sb.AppendLine($"{icon}{confLabel} {f.Title}");
                sb.AppendLine($"     {f.Description}");
                sb.AppendLine($"     -> {f.Recommendation}");
                if (!string.IsNullOrEmpty(f.EnvironmentNote))
                    sb.AppendLine($"     Note: {f.EnvironmentNote}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public string ExportJson()
        {
            return JsonUtility.ToJson(new SerializableReport(this), true);
        }

        [Serializable]
        private class SerializableReport
        {
            public string generatedAt;
            public string platform;
            public string unityVersion;
            public bool isEditor;
            public char overallGrade;
            public float healthScore;
            public List<SerializableCategoryGrade> categoryGrades = new();
            public List<SerializableFinding> findings = new();

            public SerializableReport(DiagnosticReport r)
            {
                generatedAt = r.GeneratedAt.ToString("o");
                platform = r.Session.Platform;
                unityVersion = r.Session.UnityVersion;
                isEditor = r.Session.IsEditor;
                overallGrade = r.OverallGrade;
                healthScore = r.HealthScore;
                foreach (var kv in r.CategoryGrades)
                    categoryGrades.Add(new SerializableCategoryGrade { category = kv.Key, grade = kv.Value });
                foreach (var f in r.Findings)
                {
                    findings.Add(new SerializableFinding
                    {
                        ruleId = f.RuleId,
                        category = f.Category,
                        severity = f.Severity.ToString(),
                        confidence = f.Confidence.ToString(),
                        title = f.Title,
                        description = f.Description,
                        recommendation = f.Recommendation,
                        metric = f.Metric,
                        threshold = f.Threshold,
                        environmentNote = f.EnvironmentNote ?? ""
                    });
                }
            }
        }

        [Serializable]
        private class SerializableCategoryGrade
        {
            public string category;
            public char grade;
        }

        [Serializable]
        private class SerializableFinding
        {
            public string ruleId;
            public string category;
            public string severity;
            public string confidence;
            public string title;
            public string description;
            public string recommendation;
            public float metric;
            public float threshold;
            public string environmentNote;
        }
    }
}
