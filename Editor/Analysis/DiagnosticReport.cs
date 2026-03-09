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
        public SceneCensus SceneCensus;
        public char OverallGrade;
        public float HealthScore;
        public Dictionary<string, char> CategoryGrades = new();
        public List<DiagnosticFinding> Findings = new();

        public float AvgCpuTimeMs;
        public float P95CpuTimeMs;
        public float P99CpuTimeMs;
        public float AvgGcAllocBytes;
        public float AvgDrawCalls;
        public float MemorySlope;

        /// <summary>
        /// Deterministic report synthesis — executive summary, prioritized actions, correlations.
        /// Populated by ReportSynthesizer after correlation detection. No AI required.
        /// </summary>
        public ReportSynthesizer.ReportSynthesis? Synthesis;

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

        private static string WordWrap(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxWidth) return $"  {text}";
            var sb = new StringBuilder();
            var words = text.Split(' ');
            int lineLen = 0;
            sb.Append("  ");
            foreach (var word in words)
            {
                if (lineLen + word.Length + 1 > maxWidth && lineLen > 0)
                {
                    sb.AppendLine();
                    sb.Append("  ");
                    lineLen = 0;
                }
                if (lineLen > 0) { sb.Append(' '); lineLen++; }
                sb.Append(word);
                lineLen += word.Length;
            }
            return sb.ToString();
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
            if (SceneCensus.IsValid)
            {
                int totalLights = SceneCensus.DirectionalLights + SceneCensus.PointLights + SceneCensus.SpotLights + SceneCensus.AreaLights;
                sb.AppendLine($"Scene:     {SceneCensus.TotalGameObjects} objects | {totalLights} lights | {SceneCensus.CanvasCount} canvases | {SceneCensus.RigidbodyCount} rigidbodies");
            }
            sb.AppendLine();
            sb.AppendLine($"  Overall Grade: {OverallGrade}  ({HealthScore:F0}/100)");
            sb.AppendLine();

            foreach (var kv in CategoryGrades)
                sb.AppendLine($"  [{kv.Value}] {kv.Key}");

            // Synthesis section (deterministic, no AI)
            if (Synthesis.HasValue)
            {
                var syn = Synthesis.Value;

                sb.AppendLine();
                sb.AppendLine("───────────────────────────────────────");
                sb.AppendLine("  Summary");
                sb.AppendLine("───────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine(WordWrap(syn.ExecutiveSummary, 60));

                if (syn.Correlations != null && syn.Correlations.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("  Insights:");
                    foreach (var c in syn.Correlations)
                    {
                        sb.AppendLine($"  * {c.Title}");
                        sb.AppendLine($"    {c.Description}");
                    }
                }

                if (syn.PrioritizedActions != null && syn.PrioritizedActions.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("  Prioritized Actions:");
                    foreach (var a in syn.PrioritizedActions)
                    {
                        sb.AppendLine($"  {a.Priority}. [{a.ExpectedImpact}] {a.Action}");
                    }
                }
            }

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
                if (!string.IsNullOrEmpty(f.ScriptPath))
                    sb.AppendLine($"     Script: {f.ScriptPath}{(f.ScriptLine > 0 ? $":{f.ScriptLine}" : "")}");
                if (!string.IsNullOrEmpty(f.AssetPath))
                    sb.AppendLine($"     Asset: {f.AssetPath}");
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
            public float avgCpuTimeMs;
            public float p95CpuTimeMs;
            public float p99CpuTimeMs;
            public float avgGcAllocBytes;
            public float avgDrawCalls;
            public float memorySlope;

            public SerializableReport(DiagnosticReport r)
            {
                generatedAt = r.GeneratedAt.ToString("o");
                platform = r.Session.Platform;
                unityVersion = r.Session.UnityVersion;
                isEditor = r.Session.IsEditor;
                overallGrade = r.OverallGrade;
                healthScore = r.HealthScore;
                avgCpuTimeMs = r.AvgCpuTimeMs;
                p95CpuTimeMs = r.P95CpuTimeMs;
                p99CpuTimeMs = r.P99CpuTimeMs;
                avgGcAllocBytes = r.AvgGcAllocBytes;
                avgDrawCalls = r.AvgDrawCalls;
                memorySlope = r.MemorySlope;
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
                        environmentNote = f.EnvironmentNote ?? "",
                        scriptPath = f.ScriptPath ?? "",
                        scriptLine = f.ScriptLine,
                        assetPath = f.AssetPath ?? "",
                        affectedFrames = f.AffectedFrames
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
            public string scriptPath;
            public int scriptLine;
            public string assetPath;
            public int[] affectedFrames;
        }
    }
}
