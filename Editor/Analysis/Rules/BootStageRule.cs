using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class BootStageRule : IAnalysisRule, IConfigurableRule
    {
        public string Category => "Boot";
        public string RuleId => "SLOW_BOOT";

        public string ThresholdLabel => "Slow Stage Threshold (ms)";
        public float DefaultThreshold => 2000f;
        public float MinThreshold => 500f;
        public float MaxThreshold => 10000f;

        private const long SlowStageMs = 2000;
        private const long TotalBootWarningMs = 8000;

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var stages = session.BootStages;
            if (stages.Count == 0) return findings;

            long totalMs = stages.Sum(s => s.DurationMs);

            // Flag individual slow stages
            foreach (var stage in stages)
            {
                if (stage.DurationMs > SlowStageMs)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleId = RuleId,
                        Category = Category,
                        Severity = stage.DurationMs > SlowStageMs * 2 ? Severity.Critical : Severity.Warning,
                        Title = $"Slow Boot Stage: {stage.StageName}",
                        Description = $"{stage.StageName} took {stage.DurationMs}ms (threshold: {SlowStageMs}ms). " +
                                      $"Success: {stage.Success}.",
                        Recommendation = "Consider lazy-loading resources in this stage, " +
                                         "parallelizing independent operations, or deferring non-critical work.",
                        Metric = stage.DurationMs,
                        Threshold = SlowStageMs,
                        FrameIndex = -1,
                        ScriptPath = "Runtime/BootTimingHook.cs"
                    });
                }

                if (!stage.Success)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleId = "BOOT_FAILURE",
                        Category = Category,
                        Severity = Severity.Critical,
                        Title = $"Boot Stage Failed: {stage.StageName}",
                        Description = $"{stage.StageName} failed after {stage.DurationMs}ms.",
                        Recommendation = "Check console logs for errors during this boot stage. " +
                                         "Verify network connectivity for patching stages.",
                        Metric = stage.DurationMs,
                        Threshold = 0,
                        FrameIndex = -1
                    });
                }
            }

            // Flag total boot time
            if (totalMs > TotalBootWarningMs)
            {
                findings.Add(new DiagnosticFinding
                {
                    RuleId = "TOTAL_BOOT_TIME",
                    Category = Category,
                    Severity = totalMs > TotalBootWarningMs * 2 ? Severity.Critical : Severity.Warning,
                    Title = $"Total Boot Time: {totalMs}ms",
                    Description = $"Full boot pipeline took {totalMs}ms across {stages.Count} stages. " +
                                  $"Breakdown: {string.Join(", ", stages.Select(s => $"{s.StageName}={s.DurationMs}ms"))}.",
                    Recommendation = "Review the boot pipeline for stages that can be parallelized or deferred. " +
                                     "Consider showing a loading screen with progress feedback.",
                    Metric = totalMs,
                    Threshold = TotalBootWarningMs,
                    FrameIndex = -1
                });
            }

            return findings;
        }
    }
}
