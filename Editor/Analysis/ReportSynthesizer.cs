#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis
{
    /// <summary>
    /// Produces a standalone human-readable report from deterministic findings and correlations.
    /// No AI required — this is the "good enough" report that works without LLM.
    /// </summary>
    public static class ReportSynthesizer
    {
        public struct ActionItem
        {
            public int Priority;
            public string Action;
            public string Rationale;
            public string[] RelatedRuleIds;
            public string ExpectedImpact; // "High", "Medium", "Low"
        }

        public struct ReportSynthesis
        {
            public string ExecutiveSummary;
            public string BottleneckSummary;
            public List<ActionItem> PrioritizedActions;
            public List<CorrelationEngine.CorrelationInsight> Correlations;
        }

        /// <summary>
        /// Build a complete synthesis from findings, correlations, and session data.
        /// </summary>
        public static ReportSynthesis Synthesize(
            DiagnosticReport report,
            List<CorrelationEngine.CorrelationInsight> correlations,
            ProfilingSession session)
        {
            var synthesis = new ReportSynthesis
            {
                Correlations = correlations ?? new List<CorrelationEngine.CorrelationInsight>(),
                BottleneckSummary = BuildBottleneckSummary(report, correlations),
                PrioritizedActions = BuildPrioritizedActions(report, correlations),
                ExecutiveSummary = "" // set after bottleneck + actions are computed
            };

            synthesis.ExecutiveSummary = BuildExecutiveSummary(report, synthesis, session);
            return synthesis;
        }

        private static string BuildExecutiveSummary(
            DiagnosticReport report,
            ReportSynthesis synthesis,
            ProfilingSession session)
        {
            var sb = new StringBuilder();
            var findings = report.Findings;
            int criticalCount = findings.Count(f => f.Severity == Severity.Critical);
            int warningCount = findings.Count(f => f.Severity == Severity.Warning);

            // Opening sentence: overall health
            if (report.OverallGrade <= 'B')
            {
                sb.Append($"Project is in {GradeDescription(report.OverallGrade)} shape (grade {report.OverallGrade}, {report.HealthScore:F0}/100)");
            }
            else
            {
                sb.Append($"Project needs attention (grade {report.OverallGrade}, {report.HealthScore:F0}/100)");
            }

            // Session context
            float durationS = (float)(session.Metadata.EndTime - session.Metadata.StartTime).TotalSeconds;
            if (durationS > 0)
            {
                sb.Append($" based on {durationS:F0}s of profiling data ({session.FrameCount} frames)");
            }
            sb.Append(". ");

            // Finding counts
            if (criticalCount > 0)
            {
                sb.Append($"{criticalCount} critical issue{Plural(criticalCount)} found");
                if (warningCount > 0)
                    sb.Append($" along with {warningCount} warning{Plural(warningCount)}");
                sb.Append(". ");
            }
            else if (warningCount > 0)
            {
                sb.Append($"No critical issues, but {warningCount} warning{Plural(warningCount)} to address. ");
            }
            else
            {
                sb.Append("No significant issues detected. ");
            }

            // Bottleneck sentence
            if (!string.IsNullOrEmpty(synthesis.BottleneckSummary))
            {
                sb.Append(synthesis.BottleneckSummary);
                sb.Append(" ");
            }

            // Top priority action
            if (synthesis.PrioritizedActions.Count > 0)
            {
                var top = synthesis.PrioritizedActions[0];
                sb.Append($"Top priority: {top.Action.ToLower()}.");
            }

            return sb.ToString();
        }

        private static string BuildBottleneckSummary(
            DiagnosticReport report,
            List<CorrelationEngine.CorrelationInsight> correlations)
        {
            // Check for explicit bottleneck finding
            var bottleneck = report.Findings.FirstOrDefault(f => f.RuleId == "BOTTLENECK");
            if (!string.IsNullOrEmpty(bottleneck.Title))
            {
                string bottleneckType = bottleneck.Title;

                // Enrich with correlation context
                var gpuTris = correlations?.FirstOrDefault(c => c.Id == "CORR_GPU_TRIS");
                if (gpuTris.HasValue && gpuTris.Value.Id != null)
                    return $"Primary bottleneck: {bottleneckType}, driven by high geometry complexity.";

                var cpuDc = correlations?.FirstOrDefault(c => c.Id == "CORR_CPU_DC");
                if (cpuDc.HasValue && cpuDc.Value.Id != null)
                    return $"Primary bottleneck: {bottleneckType}, driven by excessive draw calls.";

                return $"Primary bottleneck: {bottleneckType}.";
            }

            // Infer bottleneck from findings
            bool hasGc = report.Findings.Any(f => f.RuleId == "GC_SPIKE");
            bool hasDrops = report.Findings.Any(f => f.RuleId == "FRAME_DROP");
            bool hasLeak = report.Findings.Any(f => f.RuleId == "MEMORY_LEAK");

            var gcDropCorr = correlations?.FirstOrDefault(c => c.Id == "CORR_GC_DROPS");
            if (gcDropCorr.HasValue && gcDropCorr.Value.Id != null)
                return "Primary bottleneck: GC allocations are causing frame hitches.";

            if (hasLeak && hasGc)
                return "Primary bottleneck: memory leak amplifying GC pressure over time.";

            if (hasDrops)
                return $"Frame drops detected — average CPU time {report.AvgCpuTimeMs:F1}ms, P95 {report.P95CpuTimeMs:F1}ms.";

            return "";
        }

        private static List<ActionItem> BuildPrioritizedActions(
            DiagnosticReport report,
            List<CorrelationEngine.CorrelationInsight> correlations)
        {
            var actions = new List<ActionItem>();
            var findings = report.Findings;
            var correlationIds = new HashSet<string>(
                (correlations ?? new List<CorrelationEngine.CorrelationInsight>())
                .Select(c => c.Id)
                .Where(id => id != null));

            // Correlation-driven actions first (they synthesize multiple findings)
            if (correlationIds.Contains("CORR_LEAK_GC"))
            {
                actions.Add(new ActionItem
                {
                    Priority = 1,
                    Action = "Fix the memory leak — this will also reduce GC pressure",
                    Rationale = "Memory leak and GC spikes are compounding. Fixing the leak addresses both issues.",
                    RelatedRuleIds = new[] { "MEMORY_LEAK", "GC_SPIKE" },
                    ExpectedImpact = "High"
                });
            }

            if (correlationIds.Contains("CORR_GC_DROPS"))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = "Eliminate per-frame GC allocations to fix frame hitches",
                    Rationale = "GC spikes are directly causing your frame drops. Fixing allocations fixes both.",
                    RelatedRuleIds = new[] { "GC_SPIKE", "FRAME_DROP" },
                    ExpectedImpact = "High"
                });
            }

            if (correlationIds.Contains("CORR_PERVASIVE_GC"))
            {
                // Only add if not already covered by leak+gc correlation
                if (!correlationIds.Contains("CORR_LEAK_GC"))
                {
                    actions.Add(new ActionItem
                    {
                        Priority = actions.Count + 1,
                        Action = "Implement object pooling for per-frame allocations",
                        Rationale = "GC allocations on nearly every frame indicate objects created and discarded each Update.",
                        RelatedRuleIds = new[] { "GC_SPIKE" },
                        ExpectedImpact = "High"
                    });
                }
            }

            if (correlationIds.Contains("CORR_GPU_TRIS"))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = "Reduce geometry complexity with LODs and occlusion culling",
                    Rationale = "GPU is saturated by triangle count. Reducing geometry directly improves GPU frame time.",
                    RelatedRuleIds = new[] { "BOTTLENECK", "TRIANGLE_COUNT" },
                    ExpectedImpact = "High"
                });
            }

            if (correlationIds.Contains("CORR_CPU_DC"))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = "Reduce draw calls via batching, instancing, or SRP Batcher",
                    Rationale = "CPU is spending too much time issuing rendering commands.",
                    RelatedRuleIds = new[] { "BOTTLENECK", "DRAW_CALLS" },
                    ExpectedImpact = "High"
                });
            }

            if (correlationIds.Contains("CORR_BOOT_ASSETS"))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = "Defer non-essential asset loading to after boot completes",
                    Rationale = "Synchronous asset loads are inflating boot time.",
                    RelatedRuleIds = new[] { "SLOW_BOOT", "SLOW_ASSET_LOAD" },
                    ExpectedImpact = "Medium"
                });
            }

            // Individual finding-driven actions (only for findings not already covered by correlations)
            var coveredRuleIds = new HashSet<string>(actions.SelectMany(a => a.RelatedRuleIds));

            // Critical findings not yet covered
            foreach (var f in findings
                .Where(f => f.Severity == Severity.Critical && !coveredRuleIds.Contains(f.RuleId))
                .OrderByDescending(f => f.Metric))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = f.Recommendation,
                    Rationale = f.Description,
                    RelatedRuleIds = new[] { f.RuleId },
                    ExpectedImpact = "High"
                });
                coveredRuleIds.Add(f.RuleId);
            }

            // Warning findings not yet covered
            foreach (var f in findings
                .Where(f => f.Severity == Severity.Warning && !coveredRuleIds.Contains(f.RuleId))
                .OrderByDescending(f => f.Metric))
            {
                actions.Add(new ActionItem
                {
                    Priority = actions.Count + 1,
                    Action = f.Recommendation,
                    Rationale = f.Description,
                    RelatedRuleIds = new[] { f.RuleId },
                    ExpectedImpact = "Medium"
                });
                coveredRuleIds.Add(f.RuleId);
            }

            return actions;
        }

        private static string GradeDescription(char grade) => grade switch
        {
            'A' => "excellent",
            'B' => "good",
            'C' => "fair",
            'D' => "poor",
            _ => "critical"
        };

        private static string Plural(int count) => count == 1 ? "" : "s";
    }
}
#endif
