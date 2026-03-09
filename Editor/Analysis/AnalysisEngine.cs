using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrWario.Runtime;
using DrWario.Editor.Analysis.LLM;
using DrWario.Editor.Analysis.Rules;

namespace DrWario.Editor.Analysis
{
    public class AnalysisEngine
    {
        private readonly List<IAnalysisRule> _rules = new();
        private AIAnalysisRule _aiRule;

        public string AIError => _aiRule?.LastError;
        public bool AICallSucceeded => _aiRule?.LastCallSucceeded ?? false;

        /// <summary>
        /// Fired during streaming analysis when a single AI finding is parsed from the SSE stream.
        /// Subscribers can use this to update UI progressively as findings arrive.
        /// </summary>
        public event Action<DiagnosticFinding> OnStreamingFindingReceived;

        public AnalysisEngine(LLMConfig llmConfig = null)
        {
            _rules.Add(new GCAllocationRule());
            _rules.Add(new FrameDropRule());
            _rules.Add(new BootStageRule());
            _rules.Add(new MemoryLeakRule());
            _rules.Add(new AssetLoadRule());
            _rules.Add(new NetworkLatencyRule());
            _rules.Add(new RenderingEfficiencyRule());
            _rules.Add(new CPUvsGPUBottleneckRule());

            if (llmConfig != null && llmConfig.IsConfigured)
            {
                _aiRule = new AIAnalysisRule(llmConfig);
            }
        }

        public IReadOnlyList<IAnalysisRule> RegisteredRules => _rules;

        public void RegisterRule(IAnalysisRule rule) => _rules.Add(rule);

        /// <summary>
        /// Runs deterministic rules only (fast, synchronous). Does not call LLM.
        /// </summary>
        public DiagnosticReport Analyze(ProfilingSession session)
        {
            var report = new DiagnosticReport
            {
                GeneratedAt = DateTime.UtcNow,
                Session = session.Metadata,
                SceneCensus = session.SceneCensus
            };

            // Compute summary metrics from frame data
            var frames = session.GetFrames();
            if (frames != null && frames.Length > 0)
            {
                report.AvgCpuTimeMs = frames.Average(f => f.CpuFrameTimeMs);
                report.AvgGcAllocBytes = (float)frames.Average(f => f.GcAllocBytes);
                report.AvgDrawCalls = (float)frames.Average(f => f.DrawCalls);

                var sortedCpu = frames.Select(f => f.CpuFrameTimeMs).OrderBy(v => v).ToArray();
                int count = sortedCpu.Length;
                report.P95CpuTimeMs = sortedCpu[(int)(0.95f * (count - 1))];
                report.P99CpuTimeMs = sortedCpu[(int)(0.99f * (count - 1))];

                report.MemorySlope = count > 1
                    ? (float)(frames[count - 1].TotalHeapBytes - frames[0].TotalHeapBytes) / count
                    : 0f;
            }

            // Run deterministic rules (fast) — skip disabled rules
            bool anyEnabled = false;
            foreach (var rule in _rules)
            {
                if (!RuleConfig.IsEnabled(rule.RuleId))
                    continue;
                anyEnabled = true;
                var findings = rule.Analyze(session);
                if (findings != null)
                    report.Findings.AddRange(findings);
            }

            if (!anyEnabled)
            {
                UnityEngine.Debug.LogWarning("[DrWario] Warning: all rules disabled");
                report.Findings.Add(new DiagnosticFinding
                {
                    RuleId = "SYS_NO_RULES",
                    Category = "System",
                    Severity = Severity.Info,
                    Title = "All analysis rules are disabled",
                    Description = "No deterministic rules ran because all are disabled in settings.",
                    Recommendation = "Enable rules in the Rules section of LLM Settings to get diagnostic findings.",
                    FrameIndex = -1
                });
            }

            // Phase 2: Correlation detection — find cross-cutting patterns
            var correlations = CorrelationEngine.Detect(report.Findings, session);
            if (correlations.Count > 0)
                UnityEngine.Debug.Log($"[DrWario] Found {correlations.Count} correlation(s) across findings.");

            report.ComputeGrades();

            // Phase 3: Report synthesis — executive summary, prioritized actions
            report.Synthesis = ReportSynthesizer.Synthesize(report, correlations, session);
            UnityEngine.Debug.Log($"[DrWario] Report synthesis: {report.Synthesis.Value.PrioritizedActions.Count} prioritized actions.");

            return report;
        }

        /// <summary>
        /// Enhance an existing report with AI analysis (non-streaming).
        /// Does not re-run deterministic rules.
        /// </summary>
        public async Task<DiagnosticReport> EnhanceWithAIAsync(DiagnosticReport report, ProfilingSession session)
        {
            if (_aiRule == null) return report;

            _aiRule.RuleFindings = new List<DiagnosticFinding>(report.Findings);
            var aiFindings = await _aiRule.AnalyzeAsync(session);
            if (aiFindings != null)
                report.Findings.AddRange(aiFindings);

            report.Findings = DeduplicateFindings(report.Findings);
            report.ComputeGrades();
            var correlations = CorrelationEngine.Detect(report.Findings, session);
            report.Synthesis = ReportSynthesizer.Synthesize(report, correlations, session);

            return report;
        }

        /// <summary>
        /// Enhance an existing report with AI analysis via SSE streaming.
        /// Fires OnStreamingFindingReceived for each AI finding as it arrives.
        /// Does not re-run deterministic rules.
        /// </summary>
        public async Task<DiagnosticReport> EnhanceWithAIStreamingAsync(DiagnosticReport report, ProfilingSession session)
        {
            if (_aiRule == null) return report;

            _aiRule.RuleFindings = new List<DiagnosticFinding>(report.Findings);

            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

            await _aiRule.AnalyzeStreamingAsync(
                session,
                onFindingParsed: finding =>
                {
                    report.Findings.Add(finding);
                    OnStreamingFindingReceived?.Invoke(finding);
                },
                onComplete: _ =>
                {
                    report.Findings = DeduplicateFindings(report.Findings);
                    report.ComputeGrades();
                    var postCorrelations = CorrelationEngine.Detect(report.Findings, session);
                    report.Synthesis = ReportSynthesizer.Synthesize(report, postCorrelations, session);
                    tcs.TrySetResult(true);
                },
                onError: error =>
                {
                    UnityEngine.Debug.LogWarning($"[DrWario] Streaming analysis error: {error}");
                    tcs.TrySetResult(false);
                }
            );

            await tcs.Task;
            return report;
        }

        /// <summary>
        /// When AI findings cover the same category+severity as a rule finding,
        /// keep the AI version (richer context) and drop the rule duplicate.
        /// </summary>
        private static List<DiagnosticFinding> DeduplicateFindings(List<DiagnosticFinding> findings)
        {
            var result = new List<DiagnosticFinding>();
            var seen = new HashSet<string>();

            // AI findings (prefixed "AI_") get priority — process them first
            var ordered = findings
                .OrderByDescending(f => f.RuleId.StartsWith("AI_") ? 1 : 0)
                .ThenByDescending(f => f.Severity);

            foreach (var f in ordered)
            {
                // Dedup key: category + normalized title keywords
                string key = $"{f.Category}|{NormalizeTitle(f.Title)}";
                if (seen.Add(key))
                    result.Add(f);
            }

            return result;
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            // Extract the core concept: e.g., "GC Allocation Spikes (47 frames)" → "gc allocation spikes"
            int parenIdx = title.IndexOf('(');
            string core = parenIdx > 0 ? title.Substring(0, parenIdx) : title;
            return core.Trim().ToLowerInvariant();
        }
    }
}
