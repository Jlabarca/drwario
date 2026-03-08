using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrWario.Runtime;
using UnityEngine;

namespace DrWario.Editor.Analysis.LLM
{
    public static class LLMPromptBuilder
    {
        /// <summary>
        /// External frameworks can inject additional context into the system prompt.
        /// For example, a game framework can describe its architecture, boot pipeline,
        /// asset management patterns, etc. Set this before running analysis.
        /// </summary>
        public static string AdditionalContext { get; set; }

        private const string BaseSystemPrompt = @"You are an expert Unity performance analyst.

Your task: Analyze the profiling session data and return findings as a JSON array.
Each finding object must have these exact fields:
- ruleId (string): unique identifier prefixed with ""AI_""
- category (string): one of ""CPU"", ""Memory"", ""Boot"", ""Assets"", ""Network""
- severity (string): one of ""Critical"", ""Warning"", ""Info""
- title (string): short summary
- description (string): detailed explanation with data references
- recommendation (string): actionable fix with specific code patterns
- metric (number): the measured value
- threshold (number): the reference threshold

Focus on:
1. Correlations between metrics (e.g. GC spikes causing frame drops)
2. Platform-specific issues (WebGL memory limits, mobile GPU constraints)
3. Unity-specific patterns (undisposed handles, shader compilation stalls, texture streaming issues)
4. Prioritized, actionable recommendations

Return ONLY a JSON array of findings. No markdown, no preamble, no explanation outside the JSON.";

        private const string AskDoctorSystemPrompt = @"You are DrWario, an expert Unity runtime performance consultant.

You have deep knowledge of:
- Unity's rendering pipeline (URP, HDRP, Built-in), draw call batching, GPU instancing, occlusion culling
- Unity's memory model: managed heap (Mono/IL2CPP), native allocations, texture/mesh VRAM, AssetBundle memory
- Garbage collection: Boehm GC behavior, incremental GC, allocation patterns that trigger collections
- Frame timing: CPU-bound vs GPU-bound detection, VSync impact, FrameTimingManager accuracy
- Boot/loading: scene loading, addressables, async operations, shader warmup, preloading strategies
- Networking: packet batching, interpolation, client-side prediction, bandwidth optimization
- Platform constraints: mobile thermal throttling, WebGL memory limits, console fixed memory budgets

Analyze the profiling data provided. Be specific — reference actual numbers from the data.
Give actionable recommendations with concrete Unity API calls, settings, or code patterns.
Prioritize by impact: fix the biggest bottleneck first.
If data is insufficient for a definitive answer, say so and suggest what additional profiling to do.";

        public static string BuildSystemPrompt()
        {
            if (string.IsNullOrEmpty(AdditionalContext))
                return BaseSystemPrompt;

            return BaseSystemPrompt + "\n\nAdditional project context:\n" + AdditionalContext;
        }

        public static string BuildAskDoctorSystemPrompt()
        {
            if (string.IsNullOrEmpty(AdditionalContext))
                return AskDoctorSystemPrompt;

            return AskDoctorSystemPrompt + "\n\nAdditional project context:\n" + AdditionalContext;
        }

        /// <summary>
        /// Builds a complete standalone prompt (system + data + question) suitable for
        /// copying to clipboard and pasting into any LLM chat interface.
        /// </summary>
        public static string BuildFullPromptForClipboard(
            ProfilingSession session,
            DiagnosticReport report,
            string userQuestion)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== SYSTEM CONTEXT ===");
            sb.AppendLine(BuildAskDoctorSystemPrompt());
            sb.AppendLine();

            if (session != null && session.FrameCount > 0)
            {
                var findings = report?.Findings ?? new List<DiagnosticFinding>();
                sb.AppendLine("=== PROFILING DATA ===");
                sb.AppendLine(BuildUserPrompt(session, findings));
                sb.AppendLine();
            }

            if (report != null)
            {
                sb.AppendLine("=== ANALYSIS REPORT ===");
                sb.AppendLine($"Overall Grade: {report.OverallGrade} | Health Score: {report.HealthScore:F0}/100");
                sb.AppendLine($"Total Findings: {report.Findings.Count}");
                sb.AppendLine();

                foreach (var kv in report.CategoryGrades)
                    sb.AppendLine($"  [{kv.Value}] {kv.Key}");
                sb.AppendLine();

                sb.AppendLine("Findings (sorted by severity):");
                foreach (var f in report.Findings.OrderByDescending(f => f.Severity))
                {
                    sb.AppendLine($"  [{f.Severity}] {f.Title}");
                    sb.AppendLine($"    {f.Description}");
                    sb.AppendLine($"    Recommendation: {f.Recommendation}");
                    if (f.Metric != 0 || f.Threshold != 0)
                        sb.AppendLine($"    Metric: {f.Metric:F1} (threshold: {f.Threshold:F1})");
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(userQuestion))
            {
                sb.AppendLine("=== QUESTION ===");
                sb.AppendLine(userQuestion);
            }

            return sb.ToString();
        }

        public static string BuildUserPrompt(ProfilingSession session, List<DiagnosticFinding> ruleFindings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Session metadata
            sb.AppendLine("  \"session\": {");
            sb.AppendLine($"    \"startTime\": \"{session.Metadata.StartTime:o}\",");
            sb.AppendLine($"    \"endTime\": \"{session.Metadata.EndTime:o}\",");
            sb.AppendLine($"    \"durationSeconds\": {(session.Metadata.EndTime - session.Metadata.StartTime).TotalSeconds:F1},");
            sb.AppendLine($"    \"unityVersion\": \"{session.Metadata.UnityVersion}\",");
            sb.AppendLine($"    \"platform\": \"{session.Metadata.Platform}\",");
            sb.AppendLine($"    \"targetFrameRate\": {session.Metadata.TargetFrameRate},");
            sb.AppendLine($"    \"screenWidth\": {session.Metadata.ScreenWidth},");
            sb.AppendLine($"    \"screenHeight\": {session.Metadata.ScreenHeight}");
            sb.AppendLine("  },");

            // Frame summary
            AppendFrameSummary(sb, session);

            // Memory trajectory
            AppendMemoryTrajectory(sb, session);

            // Boot pipeline
            AppendBootPipeline(sb, session);

            // Asset loads
            AppendAssetLoads(sb, session);

            // Pre-analysis (rule-based findings)
            AppendPreAnalysis(sb, ruleFindings);

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendFrameSummary(StringBuilder sb, ProfilingSession session)
        {
            var frames = session.GetFrames();
            if (frames.Length == 0)
            {
                sb.AppendLine("  \"frameSummary\": null,");
                return;
            }

            var cpuTimes = frames.Select(f => f.CpuFrameTimeMs).OrderBy(t => t).ToArray();
            var gpuTimes = frames.Select(f => f.GpuFrameTimeMs).OrderBy(t => t).ToArray();
            var gcAllocs = frames.Select(f => f.GcAllocBytes).ToArray();

            float targetMs = session.Metadata.TargetFrameRate > 0
                ? 1000f / session.Metadata.TargetFrameRate
                : 16.67f;

            int dropCount = cpuTimes.Count(t => t > targetMs);
            int severeCount = cpuTimes.Count(t => t > 50f);
            int gcSpikeCount = gcAllocs.Count(a => a > 1024);

            sb.AppendLine("  \"frameSummary\": {");
            sb.AppendLine($"    \"totalFrames\": {frames.Length},");

            sb.AppendLine("    \"cpuFrameTime\": {");
            sb.AppendLine($"      \"avg\": {cpuTimes.Average():F2},");
            sb.AppendLine($"      \"p50\": {Percentile(cpuTimes, 0.50f):F2},");
            sb.AppendLine($"      \"p95\": {Percentile(cpuTimes, 0.95f):F2},");
            sb.AppendLine($"      \"p99\": {Percentile(cpuTimes, 0.99f):F2},");
            sb.AppendLine($"      \"max\": {cpuTimes.Max():F2},");
            sb.AppendLine($"      \"min\": {cpuTimes.Min():F2}");
            sb.AppendLine("    },");

            sb.AppendLine("    \"gpuFrameTime\": {");
            sb.AppendLine($"      \"avg\": {gpuTimes.Average():F2},");
            sb.AppendLine($"      \"p95\": {Percentile(gpuTimes, 0.95f):F2},");
            sb.AppendLine($"      \"max\": {gpuTimes.Max():F2}");
            sb.AppendLine("    },");

            // Render thread timing
            var renderTimes = frames.Select(f => f.RenderThreadMs).Where(t => t > 0).ToArray();
            if (renderTimes.Length > 0)
            {
                var sortedRender = renderTimes.OrderBy(t => t).ToArray();
                sb.AppendLine("    \"renderThreadTime\": {");
                sb.AppendLine($"      \"avg\": {sortedRender.Average():F2},");
                sb.AppendLine($"      \"p95\": {Percentile(sortedRender, 0.95f):F2},");
                sb.AppendLine($"      \"max\": {sortedRender.Max():F2}");
                sb.AppendLine("    },");
            }

            // Rendering metrics (from ProfilerRecorder — 0 if unavailable)
            bool hasRenderData = frames.Any(f => f.DrawCalls > 0);
            if (hasRenderData)
            {
                var drawCalls = frames.Select(f => f.DrawCalls).Where(d => d > 0).ToArray();
                var batches = frames.Select(f => f.Batches).Where(b => b > 0).ToArray();
                var setPasses = frames.Select(f => f.SetPassCalls).Where(s => s > 0).ToArray();
                var tris = frames.Select(f => (long)f.Triangles).Where(t => t > 0).ToArray();

                sb.AppendLine("    \"rendering\": {");
                if (drawCalls.Length > 0)
                {
                    sb.AppendLine($"      \"drawCalls\": {{ \"avg\": {drawCalls.Average():F0}, \"max\": {drawCalls.Max()} }},");
                }
                if (batches.Length > 0)
                {
                    sb.AppendLine($"      \"batches\": {{ \"avg\": {batches.Average():F0}, \"max\": {batches.Max()} }},");
                    if (drawCalls.Length > 0)
                    {
                        float batchingEfficiency = 1f - (float)batches.Average() / drawCalls.Average();
                        sb.AppendLine($"      \"batchingEfficiency\": {batchingEfficiency * 100:F1},");
                    }
                }
                if (setPasses.Length > 0)
                {
                    sb.AppendLine($"      \"setPassCalls\": {{ \"avg\": {setPasses.Average():F0}, \"max\": {setPasses.Max()} }},");
                }
                if (tris.Length > 0)
                {
                    sb.AppendLine($"      \"triangles\": {{ \"avg\": {tris.Average():F0}, \"max\": {tris.Max()} }}");
                }
                sb.AppendLine("    },");
            }

            // Bottleneck classification
            float avgCpu = cpuTimes.Average();
            float avgGpu = gpuTimes.Average();
            string bottleneck = "unknown";
            if (avgGpu > 0 && avgCpu > 0)
            {
                if (avgGpu > avgCpu * 1.3f) bottleneck = "GPU-bound";
                else if (avgCpu > avgGpu * 1.3f) bottleneck = "CPU-bound";
                else bottleneck = "balanced";
            }
            else if (avgGpu == 0)
            {
                bottleneck = "GPU data unavailable";
            }

            sb.AppendLine("    \"gcAllocation\": {");
            sb.AppendLine($"      \"avgPerFrame\": {(long)gcAllocs.Average()},");
            sb.AppendLine($"      \"maxPerFrame\": {gcAllocs.Max()},");
            sb.AppendLine($"      \"totalBytes\": {gcAllocs.Sum()},");
            sb.AppendLine($"      \"spikeCount\": {gcSpikeCount},");
            sb.AppendLine($"      \"spikeThreshold\": 1024");
            sb.AppendLine("    },");

            // GC allocation count (from ProfilerRecorder)
            bool hasGcCount = frames.Any(f => f.GcAllocCount > 0);
            if (hasGcCount)
            {
                var gcCounts = frames.Select(f => f.GcAllocCount).ToArray();
                sb.AppendLine("    \"gcAllocCount\": {");
                sb.AppendLine($"      \"avgPerFrame\": {gcCounts.Average():F1},");
                sb.AppendLine($"      \"maxPerFrame\": {gcCounts.Max()},");
                sb.AppendLine($"      \"totalAllocations\": {gcCounts.Sum()}");
                sb.AppendLine("    },");
            }

            sb.AppendLine("    \"frameDrops\": {");
            sb.AppendLine($"      \"count\": {dropCount},");
            sb.AppendLine($"      \"severeCount\": {severeCount},");
            sb.AppendLine($"      \"dropRatio\": {(float)dropCount / frames.Length:F4},");
            sb.AppendLine($"      \"targetMs\": {targetMs:F2}");
            sb.AppendLine("    },");

            sb.AppendLine($"    \"bottleneck\": \"{bottleneck}\"");
            sb.AppendLine("  },");
        }

        private static void AppendMemoryTrajectory(StringBuilder sb, ProfilingSession session)
        {
            var frames = session.GetFrames();
            if (frames.Length < 10)
            {
                sb.AppendLine("  \"memoryTrajectory\": null,");
                return;
            }

            // Downsample to ~12 points
            int step = Math.Max(1, frames.Length / 12);
            float startTime = frames[0].Timestamp;

            sb.AppendLine("  \"memoryTrajectory\": {");
            sb.AppendLine("    \"samples\": [");

            bool first = true;
            for (int i = 0; i < frames.Length; i += step)
            {
                if (!first) sb.AppendLine(",");
                first = false;
                var f = frames[i];
                double heapMB = f.TotalHeapBytes / (1024.0 * 1024.0);
                double texMB = f.TextureMemoryBytes / (1024.0 * 1024.0);
                double meshMB = f.MeshMemoryBytes / (1024.0 * 1024.0);
                sb.Append($"      {{ \"timeOffset\": {f.Timestamp - startTime:F1}, \"heapMB\": {heapMB:F1}, \"textureMB\": {texMB:F1}, \"meshMB\": {meshMB:F1} }}");
            }
            sb.AppendLine();
            sb.AppendLine("    ],");

            // Linear regression on heap
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = frames.Length;
            for (int i = 0; i < n; i++)
            {
                double x = frames[i].Timestamp - startTime;
                double y = frames[i].TotalHeapBytes;
                sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x;
            }
            double denom = n * sumX2 - sumX * sumX;
            double slope = denom != 0 ? (n * sumXY - sumX * sumY) / denom : 0;
            double slopeMBMin = slope / (1024.0 * 1024.0) * 60.0;

            sb.AppendLine("    \"linearRegression\": {");
            sb.AppendLine($"      \"heapSlopeBytePerSec\": {slope:F0},");
            sb.AppendLine($"      \"heapSlopeMBPerMin\": {slopeMBMin:F2}");
            sb.AppendLine("    },");

            // Memory breakdown at end of session
            var lastFrame = frames[frames.Length - 1];
            sb.AppendLine("    \"currentBreakdown\": {");
            sb.AppendLine($"      \"totalHeapMB\": {lastFrame.TotalHeapBytes / (1024.0 * 1024.0):F1},");
            sb.AppendLine($"      \"textureMemoryMB\": {lastFrame.TextureMemoryBytes / (1024.0 * 1024.0):F1},");
            sb.AppendLine($"      \"meshMemoryMB\": {lastFrame.MeshMemoryBytes / (1024.0 * 1024.0):F1}");
            sb.AppendLine("    }");
            sb.AppendLine("  },");
        }

        private static void AppendBootPipeline(StringBuilder sb, ProfilingSession session)
        {
            var stages = session.BootStages;
            if (stages.Count == 0)
            {
                sb.AppendLine("  \"bootPipeline\": null,");
                return;
            }

            long totalMs = stages.Sum(s => s.DurationMs);
            sb.AppendLine("  \"bootPipeline\": {");
            sb.AppendLine($"    \"totalMs\": {totalMs},");
            sb.AppendLine("    \"stages\": [");
            for (int i = 0; i < stages.Count; i++)
            {
                var s = stages[i];
                string comma = i < stages.Count - 1 ? "," : "";
                sb.AppendLine($"      {{ \"name\": \"{s.StageName}\", \"durationMs\": {s.DurationMs}, \"success\": {s.Success.ToString().ToLower()} }}{comma}");
            }
            sb.AppendLine("    ]");
            sb.AppendLine("  },");
        }

        private static void AppendAssetLoads(StringBuilder sb, ProfilingSession session)
        {
            var loads = session.AssetLoads;
            if (loads.Count == 0)
            {
                sb.AppendLine("  \"assetLoads\": null,");
                return;
            }

            var slowLoads = loads.Where(l => l.DurationMs > 500).OrderByDescending(l => l.DurationMs).Take(10).ToList();

            sb.AppendLine("  \"assetLoads\": {");
            sb.AppendLine($"    \"count\": {loads.Count},");
            sb.AppendLine($"    \"avgMs\": {(long)loads.Average(l => l.DurationMs)},");
            sb.AppendLine($"    \"maxMs\": {loads.Max(l => l.DurationMs)},");
            sb.AppendLine($"    \"totalMs\": {loads.Sum(l => l.DurationMs)},");
            sb.AppendLine("    \"slowLoads\": [");
            for (int i = 0; i < slowLoads.Count; i++)
            {
                var l = slowLoads[i];
                string comma = i < slowLoads.Count - 1 ? "," : "";
                sb.AppendLine($"      {{ \"assetKey\": \"{EscapeJson(l.AssetKey)}\", \"durationMs\": {l.DurationMs} }}{comma}");
            }
            sb.AppendLine("    ]");
            sb.AppendLine("  },");
        }

        private static void AppendPreAnalysis(StringBuilder sb, List<DiagnosticFinding> findings)
        {
            if (findings == null || findings.Count == 0)
            {
                sb.AppendLine("  \"preAnalysis\": null");
                return;
            }

            sb.AppendLine("  \"preAnalysis\": {");
            sb.AppendLine($"    \"findingsCount\": {findings.Count},");
            sb.AppendLine("    \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                string comma = i < findings.Count - 1 ? "," : "";
                sb.AppendLine($"      {{ \"ruleId\": \"{f.RuleId}\", \"category\": \"{f.Category}\", \"severity\": \"{f.Severity}\", \"title\": \"{EscapeJson(f.Title)}\", \"metric\": {f.Metric:F1}, \"threshold\": {f.Threshold:F1} }}{comma}");
            }
            sb.AppendLine("    ]");
            sb.AppendLine("  }");
        }

        private static float Percentile(float[] sorted, float p)
        {
            int idx = Mathf.Clamp((int)(sorted.Length * p), 0, sorted.Length - 1);
            return sorted[idx];
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
