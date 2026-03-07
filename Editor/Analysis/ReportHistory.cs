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
    }
}
