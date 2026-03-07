using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrWario.Editor.Analysis.LLM
{
    public static class LLMResponseParser
    {
        /// <summary>
        /// Parses the LLM's JSON array response into DiagnosticFinding objects.
        /// Handles markdown-wrapped JSON (```json...```) and bare arrays.
        /// </summary>
        public static List<DiagnosticFinding> Parse(string content)
        {
            var findings = new List<DiagnosticFinding>();
            if (string.IsNullOrEmpty(content)) return findings;

            // Strip markdown code fences if present
            string json = content.Trim();
            if (json.StartsWith("```"))
            {
                int firstNewline = json.IndexOf('\n');
                if (firstNewline > 0)
                    json = json.Substring(firstNewline + 1);
                if (json.EndsWith("```"))
                    json = json.Substring(0, json.Length - 3);
                json = json.Trim();
            }

            // Ensure it starts with [
            int arrayStart = json.IndexOf('[');
            int arrayEnd = json.LastIndexOf(']');
            if (arrayStart < 0 || arrayEnd < 0 || arrayEnd <= arrayStart)
            {
                Debug.LogWarning("[DrWario] LLM response is not a JSON array.");
                return findings;
            }

            json = json.Substring(arrayStart, arrayEnd - arrayStart + 1);

            // Parse using Unity's JsonUtility via wrapper
            try
            {
                var wrapper = JsonUtility.FromJson<FindingArrayWrapper>($"{{\"items\":{json}}}");
                if (wrapper?.items == null) return findings;

                foreach (var item in wrapper.items)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleId = item.ruleId ?? "AI_UNKNOWN",
                        Category = item.category ?? "General",
                        Severity = ParseSeverity(item.severity),
                        Title = item.title ?? "AI Finding",
                        Description = item.description ?? "",
                        Recommendation = item.recommendation ?? "",
                        Metric = item.metric,
                        Threshold = item.threshold,
                        FrameIndex = -1
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DrWario] Failed to parse LLM findings: {e.Message}");

                // Fallback: try to extract at least a summary
                findings.Add(new DiagnosticFinding
                {
                    RuleId = "AI_PARSE_ERROR",
                    Category = "General",
                    Severity = Severity.Info,
                    Title = "AI analysis returned unparseable response",
                    Description = $"The LLM returned a response that could not be parsed into structured findings. Raw length: {content.Length} chars.",
                    Recommendation = "Try re-running the analysis. If this persists, check the LLM model and prompt configuration.",
                    Metric = 0,
                    Threshold = 0,
                    FrameIndex = -1
                });
            }

            return findings;
        }

        private static Severity ParseSeverity(string s)
        {
            if (string.IsNullOrEmpty(s)) return Severity.Info;
            return s.ToLower() switch
            {
                "critical" => Severity.Critical,
                "warning" => Severity.Warning,
                _ => Severity.Info
            };
        }

        [Serializable]
        private class FindingArrayWrapper
        {
            public FindingJson[] items;
        }

        [Serializable]
        private class FindingJson
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
