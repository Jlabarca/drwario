using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    public class NetworkLatencyRule : IAnalysisRule
    {
        public string Category => "Network";
        public string RuleId => "NETWORK_HEALTH";

        private const float HighLatencyMs = 100f;
        private const float CriticalLatencyMs = 250f;
        private const float ErrorRatioThreshold = 0.05f;

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var events = session.NetworkEvents;
            if (events.Count == 0) return findings;

            var sends = events.Where(e => e.Type == NetworkEventType.Send).ToList();
            var receives = events.Where(e => e.Type == NetworkEventType.Receive).ToList();
            var errors = events.Where(e => e.Type == NetworkEventType.Error).ToList();

            long totalBytesSent = sends.Sum(e => (long)e.Bytes);
            long totalBytesReceived = receives.Sum(e => (long)e.Bytes);

            // Check error rate
            if (errors.Count > 0)
            {
                float errorRatio = (float)errors.Count / events.Count;
                var severity = errorRatio > ErrorRatioThreshold ? Severity.Critical : Severity.Warning;

                findings.Add(new DiagnosticFinding
                {
                    RuleId = RuleId,
                    Category = Category,
                    Severity = severity,
                    Title = $"Network Errors Detected ({errors.Count} errors)",
                    Description = $"{errors.Count} packet processing errors out of {events.Count} total events " +
                                  $"({errorRatio:P1} error rate). " +
                                  $"Sent: {sends.Count} packets ({totalBytesSent / 1024f:F1}KB) | " +
                                  $"Received: {receives.Count} packets ({totalBytesReceived / 1024f:F1}KB).",
                    Recommendation = "Investigate packet deserialization failures. " +
                                     "Check if sender and receiver share the same serialization schema version. " +
                                     "Enable verbose networking logging to identify root cause.",
                    Metric = errorRatio * 100f,
                    Threshold = ErrorRatioThreshold * 100f,
                    FrameIndex = -1
                });
            }

            // Check latency on receive events that have latency data
            var withLatency = receives.Where(e => e.LatencyMs > 0).ToList();
            if (withLatency.Count > 0)
            {
                float avgLatency = withLatency.Average(e => e.LatencyMs);
                float maxLatency = withLatency.Max(e => e.LatencyMs);
                int highCount = withLatency.Count(e => e.LatencyMs > HighLatencyMs);

                if (highCount > 0)
                {
                    var severity = maxLatency > CriticalLatencyMs ? Severity.Critical
                                 : highCount > withLatency.Count / 4 ? Severity.Warning
                                 : Severity.Info;

                    findings.Add(new DiagnosticFinding
                    {
                        RuleId = RuleId,
                        Category = Category,
                        Severity = severity,
                        Title = $"High Network Latency ({highCount} events over {HighLatencyMs}ms)",
                        Description = $"Avg latency: {avgLatency:F1}ms | Max: {maxLatency:F1}ms. " +
                                      $"{highCount}/{withLatency.Count} receive events exceeded {HighLatencyMs}ms.",
                        Recommendation = "Consider client-side prediction for player movement. " +
                                         "Reduce packet frequency or batch small updates. " +
                                         "Check server proximity and connection quality.",
                        Metric = maxLatency,
                        Threshold = HighLatencyMs,
                        FrameIndex = -1
                    });
                }
            }

            // Traffic summary (info-level if traffic seems unusually high)
            float sessionDuration = 0;
            if (events.Count >= 2)
            {
                sessionDuration = events[events.Count - 1].Timestamp - events[0].Timestamp;
            }

            if (sessionDuration > 1f && totalBytesSent + totalBytesReceived > 0)
            {
                float kbPerSecond = (totalBytesSent + totalBytesReceived) / 1024f / sessionDuration;
                if (kbPerSecond > 100f) // >100 KB/s is notable
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleId = RuleId,
                        Category = Category,
                        Severity = kbPerSecond > 500f ? Severity.Warning : Severity.Info,
                        Title = $"High Network Throughput ({kbPerSecond:F1} KB/s)",
                        Description = $"Total: {(totalBytesSent + totalBytesReceived) / 1024f:F1}KB over {sessionDuration:F1}s. " +
                                      $"Sent: {totalBytesSent / 1024f:F1}KB | Received: {totalBytesReceived / 1024f:F1}KB.",
                        Recommendation = "Review packet sizes and send frequency. " +
                                         "Consider delta compression for position updates. " +
                                         "Ensure unused channels are not generating traffic.",
                        Metric = kbPerSecond,
                        Threshold = 100f,
                        FrameIndex = -1
                    });
                }
            }

            return findings;
        }
    }
}
