using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class AssetLoadRule : IAnalysisRule
    {
        public string Category => "Assets";
        public string RuleId => "SLOW_ASSET_LOAD";

        private const long SlowLoadMs = 500;
        private const long CriticalLoadMs = 2000;

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var loads = session.AssetLoads;
            if (loads.Count == 0) return findings;

            var slowLoads = loads.Where(l => l.DurationMs > SlowLoadMs).ToList();
            if (slowLoads.Count == 0) return findings;

            float avgMs = (float)loads.Average(l => l.DurationMs);
            long maxMs = loads.Max(l => l.DurationMs);
            var slowest = loads.OrderByDescending(l => l.DurationMs).First();

            var severity = slowLoads.Any(l => l.DurationMs > CriticalLoadMs) ? Severity.Critical
                         : slowLoads.Count > 3 ? Severity.Warning
                         : Severity.Info;

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = $"Slow Asset Loads ({slowLoads.Count} assets over {SlowLoadMs}ms)",
                Description = $"{slowLoads.Count}/{loads.Count} assets exceeded {SlowLoadMs}ms. " +
                              $"Avg: {avgMs:F0}ms | Max: {maxMs}ms. " +
                              $"Slowest: \"{slowest.AssetKey}\" at {slowest.DurationMs}ms.",
                Recommendation = "Pre-warm frequently loaded assets during scene transitions. " +
                                 "Use async preloading (LoadAssetAsync with early dispatch). " +
                                 "Consider reducing asset sizes or using lower-resolution fallbacks.",
                Metric = maxMs,
                Threshold = SlowLoadMs,
                FrameIndex = -1
            });

            return findings;
        }
    }
}
