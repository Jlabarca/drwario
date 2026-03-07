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
            // Overall: start at 100, subtract per finding
            float score = 100f;
            foreach (var f in Findings)
            {
                score -= f.Severity switch
                {
                    Severity.Critical => 15f,
                    Severity.Warning => 5f,
                    _ => 1f
                };
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
                    catScore -= f.Severity switch
                    {
                        Severity.Critical => 15f,
                        Severity.Warning => 5f,
                        _ => 1f
                    };
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
                sb.AppendLine($"{icon} {f.Title}");
                sb.AppendLine($"     {f.Description}");
                sb.AppendLine($"     -> {f.Recommendation}");
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
            public char overallGrade;
            public float healthScore;
            public List<SerializableFinding> findings = new();

            public SerializableReport(DiagnosticReport r)
            {
                generatedAt = r.GeneratedAt.ToString("o");
                platform = r.Session.Platform;
                unityVersion = r.Session.UnityVersion;
                overallGrade = r.OverallGrade;
                healthScore = r.HealthScore;
                foreach (var f in r.Findings)
                {
                    findings.Add(new SerializableFinding
                    {
                        ruleId = f.RuleId,
                        category = f.Category,
                        severity = f.Severity.ToString(),
                        title = f.Title,
                        description = f.Description,
                        recommendation = f.Recommendation,
                        metric = f.Metric,
                        threshold = f.Threshold
                    });
                }
            }
        }

        [Serializable]
        private class SerializableFinding
        {
            public string ruleId;
            public string category;
            public string severity;
            public string title;
            public string description;
            public string recommendation;
            public float metric;
            public float threshold;
        }
    }
}
