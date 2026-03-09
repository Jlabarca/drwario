using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrWario.Runtime;
using DrWario.Editor.Analysis.LLM;
using UnityEngine;

namespace DrWario.Editor.Analysis.Rules
{
    /// <summary>
    /// IAnalysisRule implementation that sends profiling data to an LLM for deep analysis.
    /// Gracefully returns empty if LLM is not configured, unavailable, or fails.
    /// </summary>
    public class AIAnalysisRule : IAnalysisRule
    {
        public string Category => "AI";
        public string RuleId => "AI_LLM";

        private readonly LLMConfig _config;
        private List<DiagnosticFinding> _ruleFindings;

        /// <summary>
        /// Pre-analysis findings from deterministic rules, provided for LLM context.
        /// Must be set before calling Analyze().
        /// </summary>
        public List<DiagnosticFinding> RuleFindings
        {
            get => _ruleFindings;
            set => _ruleFindings = value;
        }

        /// <summary>
        /// Set after Analyze() if the LLM returned an error.
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// True if the last Analyze() call successfully received LLM findings.
        /// </summary>
        public bool LastCallSucceeded { get; private set; }

        public AIAnalysisRule(LLMConfig config)
        {
            _config = config;
        }

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            // Synchronous stub — deterministic rules only.
            // For AI analysis, use AnalyzeAsync() via AnalysisEngine.AnalyzeAsync().
            LastError = "Use AnalyzeAsync() for AI analysis.";
            LastCallSucceeded = false;
            return new List<DiagnosticFinding>();
        }

        public async Task<List<DiagnosticFinding>> AnalyzeAsync(ProfilingSession session)
        {
            var client = new LLMClient(_config);

            string systemPrompt = LLMPromptBuilder.BuildSystemPrompt();
            string userPrompt = LLMPromptBuilder.BuildUserPrompt(session, _ruleFindings ?? new List<DiagnosticFinding>());

            Debug.Log($"[DrWario] LLM system prompt: {systemPrompt.Length} chars");
            Debug.Log($"[DrWario] LLM user prompt: {userPrompt.Length} chars ({EstimateTokens(userPrompt)} est. tokens)");
            Debug.Log($"[DrWario] === USER PROMPT START ===\n{userPrompt}\n=== USER PROMPT END ===");

            var response = await client.SendAsync(systemPrompt, userPrompt);

            if (!response.IsSuccess)
            {
                LastError = response.ErrorMessage;
                Debug.LogWarning($"[DrWario] LLM analysis failed: {response.ErrorMessage}");
                return new List<DiagnosticFinding>();
            }

            Debug.Log($"[DrWario] === LLM RESPONSE START ===\n{response.Content}\n=== LLM RESPONSE END ===");

            var findings = LLMResponseParser.Parse(response.Content);
            LastCallSucceeded = findings.Count > 0;

            Debug.Log($"[DrWario] LLM returned {findings.Count} findings.");
            foreach (var f in findings)
            {
                string refs = "";
                if (!string.IsNullOrEmpty(f.ScriptPath)) refs += $" script={f.ScriptPath}:{f.ScriptLine}";
                if (!string.IsNullOrEmpty(f.AssetPath)) refs += $" asset={f.AssetPath}";
                Debug.Log($"[DrWario]   [{f.Severity}] {f.Title} (confidence={f.Confidence}){refs}");
            }
            return findings;
        }

        /// <summary>
        /// Streaming variant of AnalyzeAsync. For Claude/OpenAI providers, uses SSE streaming
        /// to emit findings progressively via onFindingParsed callback. For Ollama/Custom,
        /// falls back to non-streaming SendAsync.
        /// </summary>
        public async Task AnalyzeStreamingAsync(
            ProfilingSession session,
            Action<DiagnosticFinding> onFindingParsed,
            Action<string> onComplete = null,
            Action<string> onError = null)
        {
            var client = new LLMClient(_config);

            string systemPrompt = LLMPromptBuilder.BuildSystemPrompt();
            string userPrompt = LLMPromptBuilder.BuildUserPrompt(session, _ruleFindings ?? new List<DiagnosticFinding>());

            Debug.Log($"[DrWario] LLM streaming system prompt: {systemPrompt.Length} chars");
            Debug.Log($"[DrWario] LLM streaming user prompt: {userPrompt.Length} chars ({EstimateTokens(userPrompt)} est. tokens)");

            var allFindings = new List<DiagnosticFinding>();

            await client.SendStreamingAsync(
                systemPrompt,
                userPrompt,
                onFindingParsed: finding =>
                {
                    allFindings.Add(finding);
                    Debug.Log($"[DrWario] Streaming finding: [{finding.Severity}] {finding.Title}");
                    onFindingParsed?.Invoke(finding);
                },
                onComplete: content =>
                {
                    LastCallSucceeded = allFindings.Count > 0;
                    Debug.Log($"[DrWario] Streaming complete. {allFindings.Count} findings received.");
                    onComplete?.Invoke(content);
                },
                onError: error =>
                {
                    LastError = error;
                    LastCallSucceeded = false;
                    Debug.LogWarning($"[DrWario] Streaming LLM analysis failed: {error}");
                    onError?.Invoke(error);
                }
            );
        }

        private static int EstimateTokens(string text)
        {
            // Rough estimate: ~4 chars per token for English/JSON
            return text.Length / 4;
        }
    }
}
