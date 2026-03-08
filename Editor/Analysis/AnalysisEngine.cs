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

        public void RegisterRule(IAnalysisRule rule) => _rules.Add(rule);

        /// <summary>
        /// Runs deterministic rules only (fast, synchronous). Does not call LLM.
        /// </summary>
        public DiagnosticReport Analyze(ProfilingSession session)
        {
            var report = new DiagnosticReport
            {
                GeneratedAt = DateTime.UtcNow,
                Session = session.Metadata
            };

            // Run deterministic rules (fast)
            foreach (var rule in _rules)
            {
                var findings = rule.Analyze(session);
                if (findings != null)
                    report.Findings.AddRange(findings);
            }

            report.ComputeGrades();
            return report;
        }

        /// <summary>
        /// Runs deterministic rules synchronously, then AI analysis asynchronously.
        /// Never blocks the main thread.
        /// </summary>
        public async Task<DiagnosticReport> AnalyzeAsync(ProfilingSession session)
        {
            // Phase 1: Run deterministic rules (fast, synchronous)
            var report = Analyze(session);

            // Phase 2: Run AI rule asynchronously
            if (_aiRule != null)
            {
                _aiRule.RuleFindings = new List<DiagnosticFinding>(report.Findings);
                var aiFindings = await _aiRule.AnalyzeAsync(session);
                if (aiFindings != null)
                    report.Findings.AddRange(aiFindings);

                // Phase 3: Deduplicate — AI findings get priority
                report.Findings = DeduplicateFindings(report.Findings);
                report.ComputeGrades();
            }

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
