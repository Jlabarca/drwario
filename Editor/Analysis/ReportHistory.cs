using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DrWario.Editor.Analysis
{
    /// <summary>
    /// Persists DiagnosticReports to Library/DrWarioReports/ for cross-session comparison.
    /// </summary>
    public static class ReportHistory
    {
        private static readonly string ReportsDir = Path.Combine(Application.dataPath, "..", "Library", "DrWarioReports");
        private const int MaxReports = 50;

        public static void Save(DiagnosticReport report)
        {
            Directory.CreateDirectory(ReportsDir);

            string filename = $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(ReportsDir, filename);
            File.WriteAllText(path, report.ExportJson());

            // Prune old reports
            var files = Directory.GetFiles(ReportsDir, "report_*.json")
                .OrderByDescending(f => f)
                .Skip(MaxReports);
            foreach (var f in files)
                File.Delete(f);
        }

        public static List<ReportSummary> ListReports()
        {
            var results = new List<ReportSummary>();
            if (!Directory.Exists(ReportsDir)) return results;

            var files = Directory.GetFiles(ReportsDir, "report_*.json")
                .OrderByDescending(f => f);

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<ReportJson>(json);
                    results.Add(new ReportSummary
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        GeneratedAt = data.generatedAt,
                        Grade = data.overallGrade,
                        HealthScore = data.healthScore,
                        FindingsCount = data.findings?.Length ?? 0,
                        Platform = data.platform
                    });
                }
                catch
                {
                    // Skip corrupt files
                }
            }

            return results;
        }

        public static string LoadReportText(string filePath)
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        }

        /// <summary>
        /// Loads a full DiagnosticReport from a saved JSON file for use in comparisons.
        /// Returns null if the file cannot be loaded or parsed.
        /// </summary>
        public static DiagnosticReport LoadReport(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                var json = File.ReadAllText(filePath);
                var data = UnityEngine.JsonUtility.FromJson<FullReportJson>(json);
                if (data == null) return null;

                var report = new DiagnosticReport();
                report.GeneratedAt = DateTime.TryParse(data.generatedAt, out var dt) ? dt : DateTime.MinValue;
                report.Session = new Runtime.SessionMetadata
                {
                    Platform = data.platform ?? "",
                    UnityVersion = data.unityVersion ?? "",
                    IsEditor = data.isEditor
                };
                report.OverallGrade = data.overallGrade;
                report.HealthScore = data.healthScore;
                report.AvgCpuTimeMs = data.avgCpuTimeMs;
                report.P95CpuTimeMs = data.p95CpuTimeMs;
                report.P99CpuTimeMs = data.p99CpuTimeMs;
                report.AvgGcAllocBytes = data.avgGcAllocBytes;
                report.AvgDrawCalls = data.avgDrawCalls;
                report.MemorySlope = data.memorySlope;

                if (data.categoryGrades != null)
                {
                    foreach (var cg in data.categoryGrades)
                        report.CategoryGrades[cg.category] = cg.grade;
                }

                if (data.findings != null)
                {
                    foreach (var f in data.findings)
                    {
                        Enum.TryParse<Severity>(f.severity, out var sev);
                        Enum.TryParse<Confidence>(f.confidence, out var conf);
                        report.Findings.Add(new DiagnosticFinding
                        {
                            RuleId = f.ruleId ?? "",
                            Category = f.category ?? "",
                            Severity = sev,
                            Confidence = conf,
                            Title = f.title ?? "",
                            Description = f.description ?? "",
                            Recommendation = f.recommendation ?? "",
                            Metric = f.metric,
                            Threshold = f.threshold,
                            EnvironmentNote = f.environmentNote,
                            ScriptPath = f.scriptPath,
                            ScriptLine = f.scriptLine,
                            AssetPath = f.assetPath,
                            AffectedFrames = f.affectedFrames
                        });
                    }
                }

                return report;
            }
            catch
            {
                return null;
            }
        }

        public static void DeleteReport(string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        public static void ClearAll()
        {
            if (Directory.Exists(ReportsDir))
                Directory.Delete(ReportsDir, true);
        }

        public struct ReportSummary
        {
            public string FilePath;
            public string FileName;
            public string GeneratedAt;
            public char Grade;
            public float HealthScore;
            public int FindingsCount;
            public string Platform;
        }

        [Serializable]
        private class ReportJson
        {
            public string generatedAt;
            public string platform;
            public char overallGrade;
            public float healthScore;
            public FindingJson[] findings;
        }

        [Serializable]
        private class FindingJson
        {
            public string ruleId;
        }

        [Serializable]
        private class FullReportJson
        {
            public string generatedAt;
            public string platform;
            public string unityVersion;
            public bool isEditor;
            public char overallGrade;
            public float healthScore;
            public float avgCpuTimeMs;
            public float p95CpuTimeMs;
            public float p99CpuTimeMs;
            public float avgGcAllocBytes;
            public float avgDrawCalls;
            public float memorySlope;
            public FullCategoryGradeJson[] categoryGrades;
            public FullFindingJson[] findings;
        }

        [Serializable]
        private class FullCategoryGradeJson
        {
            public string category;
            public char grade;
        }

        [Serializable]
        private class FullFindingJson
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
