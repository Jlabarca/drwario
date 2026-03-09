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
- scriptPath (string, optional): relative Unity asset path to the script causing the issue (e.g. ""Assets/Scripts/PlayerController.cs""). Include only when you can identify a specific script.
- scriptLine (int, optional): line number in the script, 0 if unknown
- assetPath (string, optional): relative Unity asset path to a related asset (e.g. ""Assets/Textures/LargeTexture.png""). Include only when you can identify a specific asset.

Focus on:
1. Correlations between metrics (e.g. GC spikes causing frame drops)
2. Platform-specific issues (WebGL memory limits, mobile GPU constraints)
3. Unity-specific patterns (undisposed handles, shader compilation stalls, texture streaming issues)
4. Prioritized, actionable recommendations

IMPORTANT: When session data is from the Unity Editor (environment.isEditor=true), editor overhead
inflates all metrics. The editorBaseline provides idle editor overhead measured before Play Mode.
Use these to estimate actual game performance:
- CPU time: subtract ~baseline.avgCpuFrameTimeMs from measured values
- GC allocations: subtract ~baseline.avgGcAllocBytes per frame
- Draw calls: subtract ~baseline.avgDrawCalls (especially if Scene view is open)
- Memory totals: editor uses significant memory for metadata/caches — don't alarm on absolute values
When findings have confidence=""Low"", flag them as ""may be editor overhead"" rather than definitive issues.
Recommend the user verify critical findings in a development build.

When scene census data is provided (sceneCensus), consider scene composition in your analysis:
- Too many point/spot lights without baking → recommend light baking or reducing dynamic lights
- Missing LOD groups on high-poly meshes → recommend adding LOD groups
- Excessive particle systems → recommend pooling or reducing emission rates
- Many rigidbodies without sleeping → recommend adjusting sleep thresholds
- Canvas count → recommend combining canvases or using world-space canvases

When profilerMarkers data is available, reference specific marker names in your findings rather than guessing which subsystem is expensive. Use the marker timing data to attribute frame budget usage.

When activeScripts data is available, use it to identify specific scripts as suspects. For example, if GC spikes correlate with a script that has many instances, name it as a likely cause. The ""on"" field shows sample GameObjects running that script. Reference specific script names in your scriptPath field when you can identify them.

When consoleLogs data is available (errors/warnings captured during profiling), correlate them with performance findings. Errors during frame spikes are especially relevant — they may indicate the root cause.

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
                    string confTag = f.Confidence != Confidence.High ? $" [Confidence: {f.Confidence}]" : "";
                    sb.AppendLine($"  [{f.Severity}]{confTag} {f.Title}");
                    sb.AppendLine($"    {f.Description}");
                    sb.AppendLine($"    Recommendation: {f.Recommendation}");
                    if (f.Metric != 0 || f.Threshold != 0)
                        sb.AppendLine($"    Metric: {f.Metric:F1} (threshold: {f.Threshold:F1})");
                    if (!string.IsNullOrEmpty(f.EnvironmentNote))
                        sb.AppendLine($"    Note: {f.EnvironmentNote}");
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

        /// <summary>
        /// Builds a prompt for an LLM with UnityMCP access to inspect suspects identified by DrWario.
        /// The LLM should use MCP tools to check GameObjects, components, and scripts.
        /// </summary>
        public static string BuildMcpSuspectCheckPrompt(ProfilingSession session, DiagnosticReport report)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You have access to Unity Editor via MCP tools. DrWario has identified performance suspects that need verification.");
            sb.AppendLine();
            sb.AppendLine("YOUR TASK: Use UnityMCP tools to inspect each suspect below. For each one:");
            sb.AppendLine("1. Use find_gameobjects to locate the suspect GameObjects");
            sb.AppendLine("2. Use manage_gameobject(action=\"get_components\") to inspect their components");
            sb.AppendLine("3. Use manage_script to read the relevant scripts if needed");
            sb.AppendLine("4. Confirm or deny whether each suspect is likely causing the reported issue");
            sb.AppendLine("5. Suggest specific fixes based on what you find in the actual code/components");
            sb.AppendLine();

            // Active scripts as suspects
            if (session?.ActiveScripts != null && session.ActiveScripts.Count > 0)
            {
                sb.AppendLine("=== ACTIVE SCRIPTS (by instance count) ===");
                foreach (var s in session.ActiveScripts)
                {
                    string names = string.Join(", ", s.SampleGameObjectNames ?? Array.Empty<string>());
                    sb.AppendLine($"  {s.TypeName} x{s.InstanceCount} — on: {names}");
                }
                sb.AppendLine();
            }

            // Findings as context for what to investigate
            if (report?.Findings != null && report.Findings.Count > 0)
            {
                sb.AppendLine("=== DRWARIO FINDINGS (what to investigate) ===");
                foreach (var f in report.Findings.OrderByDescending(f => f.Severity))
                {
                    sb.AppendLine($"  [{f.Severity}] {f.Title}");
                    sb.AppendLine($"    {f.Description}");
                    if (f.AffectedFrames != null && f.AffectedFrames.Length > 0)
                        sb.AppendLine($"    Affected frames: {string.Join(", ", f.AffectedFrames.Take(10))}");
                    sb.AppendLine();
                }
            }

            // Console errors as additional clues
            if (session?.ConsoleLogs != null && session.ConsoleLogs.Count > 0)
            {
                sb.AppendLine("=== CONSOLE ERRORS/WARNINGS DURING SESSION ===");
                foreach (var l in session.ConsoleLogs)
                {
                    sb.Append($"  [{l.LogType}] {l.Message}");
                    if (!string.IsNullOrEmpty(l.StackTraceHint))
                        sb.Append($" — {l.StackTraceHint}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // Compact profiling summary so LLM can correlate suspects with perf data
            if (session != null && session.FrameCount > 0)
            {
                var frames = session.GetFrames();
                if (frames.Length > 0)
                {
                    var cpuTimes = frames.Select(f => f.CpuFrameTimeMs).OrderBy(t => t).ToArray();
                    var gcAllocs = frames.Select(f => f.GcAllocBytes).ToArray();
                    float targetMs = session.Metadata.TargetFrameRate > 0
                        ? 1000f / session.Metadata.TargetFrameRate : 16.67f;

                    sb.AppendLine("=== PROFILING SUMMARY ===");
                    sb.AppendLine($"  Frames: {frames.Length} | Target: {targetMs:F1}ms ({session.Metadata.TargetFrameRate}fps)");
                    sb.AppendLine($"  CPU: avg={cpuTimes.Average():F1}ms, p95={Percentile(cpuTimes, 0.95f):F1}ms, p99={Percentile(cpuTimes, 0.99f):F1}ms, max={cpuTimes.Max():F1}ms");
                    sb.AppendLine($"  Frame drops: {cpuTimes.Count(t => t > targetMs)} (>{targetMs:F0}ms) | Severe: {cpuTimes.Count(t => t > 50f)} (>50ms)");
                    sb.AppendLine($"  GC: avg={gcAllocs.Average():F0}B/frame, spikes(>1KB)={gcAllocs.Count(a => a > 1024)}");

                    // Memory trajectory (first and last heap)
                    long firstHeap = frames[0].TotalHeapBytes;
                    long lastHeap = frames[frames.Length - 1].TotalHeapBytes;
                    float heapGrowthMB = (lastHeap - firstHeap) / (1024f * 1024f);
                    sb.AppendLine($"  Heap: {firstHeap / (1024 * 1024)}MB → {lastHeap / (1024 * 1024)}MB (Δ{heapGrowthMB:+0.0;-0.0;0.0}MB)");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("Focus on the top suspects — scripts with many instances and scripts mentioned in console errors. Report what you find and suggest concrete fixes.");

            return sb.ToString();
        }

        /// <summary>
        /// Builds a prompt asking the LLM to verify and correct a DrWario report.
        /// Includes full profiling data and findings for the LLM to audit.
        /// </summary>
        public static string BuildReportCorrectionPrompt(ProfilingSession session, DiagnosticReport report)
        {
            var sb = new StringBuilder();

            sb.AppendLine("DrWario generated the following performance analysis report. Your task is to AUDIT and CORRECT it.");
            sb.AppendLine();
            sb.AppendLine("For each finding, evaluate:");
            sb.AppendLine("1. Is this finding accurate based on the profiling data? (Confirm / Likely False Positive / Needs More Data)");
            sb.AppendLine("2. Is the severity appropriate? (Correct / Should be higher / Should be lower)");
            sb.AppendLine("3. Is the recommendation actionable and correct? (Correct / Improve recommendation)");
            sb.AppendLine("4. Are there missed issues in the data that DrWario didn't flag?");
            sb.AppendLine();

            // Include profiling data
            if (session != null && session.FrameCount > 0)
            {
                var findings = report?.Findings ?? new List<DiagnosticFinding>();
                sb.AppendLine("=== PROFILING DATA ===");
                sb.AppendLine(BuildUserPrompt(session, findings));
                sb.AppendLine();
            }

            // Include the full report
            if (report != null)
            {
                sb.AppendLine("=== DRWARIO REPORT TO AUDIT ===");
                sb.AppendLine($"Overall Grade: {report.OverallGrade} | Health Score: {report.HealthScore:F0}/100");

                foreach (var kv in report.CategoryGrades)
                    sb.AppendLine($"  [{kv.Value}] {kv.Key}");
                sb.AppendLine();

                sb.AppendLine("Findings:");
                int n = 1;
                foreach (var f in report.Findings.OrderByDescending(f => f.Severity))
                {
                    string confTag = f.Confidence != Confidence.High ? $" [Confidence: {f.Confidence}]" : "";
                    sb.AppendLine($"  #{n} [{f.Severity}]{confTag} {f.Title}");
                    sb.AppendLine($"     {f.Description}");
                    sb.AppendLine($"     Recommendation: {f.Recommendation}");
                    if (!string.IsNullOrEmpty(f.EnvironmentNote))
                        sb.AppendLine($"     Note: {f.EnvironmentNote}");
                    sb.AppendLine();
                    n++;
                }
            }

            sb.AppendLine("Output a corrected report: for each finding, state your verdict (Confirmed / False Positive / Adjusted). Add any missed findings. Provide a corrected overall assessment.");

            return sb.ToString();
        }

        /// <summary>
        /// Builds a combined prompt: use UnityMCP to inspect suspects AND correct the report.
        /// </summary>
        public static string BuildMcpReportCorrectionPrompt(ProfilingSession session, DiagnosticReport report)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You have access to Unity Editor via MCP tools. DrWario generated a performance report with suspects.");
            sb.AppendLine();
            sb.AppendLine("YOUR TASK (two phases):");
            sb.AppendLine();
            sb.AppendLine("PHASE 1 — INVESTIGATE SUSPECTS:");
            sb.AppendLine("Use UnityMCP tools to inspect the suspects identified below:");
            sb.AppendLine("- find_gameobjects to locate suspect GameObjects");
            sb.AppendLine("- manage_gameobject(action=\"get_components\") to inspect their components");
            sb.AppendLine("- manage_script to read relevant scripts for code-level analysis");
            sb.AppendLine("- manage_scene(action=\"get_hierarchy\") if you need to understand the scene structure");
            sb.AppendLine();
            sb.AppendLine("PHASE 2 — CORRECT THE REPORT:");
            sb.AppendLine("Based on what you found, for each DrWario finding:");
            sb.AppendLine("1. Confirm or deny the finding with evidence from your inspection");
            sb.AppendLine("2. Adjust severity if needed");
            sb.AppendLine("3. Improve recommendations with specific code/component changes");
            sb.AppendLine("4. Add any new findings you discovered that DrWario missed");
            sb.AppendLine();

            // Active scripts
            if (session?.ActiveScripts != null && session.ActiveScripts.Count > 0)
            {
                sb.AppendLine("=== ACTIVE SCRIPTS (suspects) ===");
                foreach (var s in session.ActiveScripts)
                {
                    string names = string.Join(", ", s.SampleGameObjectNames ?? Array.Empty<string>());
                    sb.AppendLine($"  {s.TypeName} x{s.InstanceCount} — on: {names}");
                }
                sb.AppendLine();
            }

            // Console errors
            if (session?.ConsoleLogs != null && session.ConsoleLogs.Count > 0)
            {
                sb.AppendLine("=== CONSOLE ERRORS/WARNINGS ===");
                foreach (var l in session.ConsoleLogs)
                {
                    sb.Append($"  [{l.LogType}] {l.Message}");
                    if (!string.IsNullOrEmpty(l.StackTraceHint))
                        sb.Append($" — {l.StackTraceHint}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // Profiling data
            if (session != null && session.FrameCount > 0)
            {
                var findings = report?.Findings ?? new List<DiagnosticFinding>();
                sb.AppendLine("=== PROFILING DATA ===");
                sb.AppendLine(BuildUserPrompt(session, findings));
                sb.AppendLine();
            }

            // Report to correct
            if (report?.Findings != null && report.Findings.Count > 0)
            {
                sb.AppendLine("=== DRWARIO REPORT TO VERIFY & CORRECT ===");
                sb.AppendLine($"Overall Grade: {report.OverallGrade} | Health Score: {report.HealthScore:F0}/100");
                sb.AppendLine();

                int n = 1;
                foreach (var f in report.Findings.OrderByDescending(f => f.Severity))
                {
                    string confTag = f.Confidence != Confidence.High ? $" [Confidence: {f.Confidence}]" : "";
                    sb.AppendLine($"  #{n} [{f.Severity}]{confTag} {f.Title}");
                    sb.AppendLine($"     {f.Description}");
                    sb.AppendLine($"     Recommendation: {f.Recommendation}");
                    sb.AppendLine();
                    n++;
                }
            }

            sb.AppendLine("Start by investigating the top suspects with MCP tools, then produce a corrected report.");

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

            // Environment context (editor vs build)
            AppendEnvironment(sb, session);

            // Frame summary
            AppendFrameSummary(sb, session);

            // Memory trajectory
            AppendMemoryTrajectory(sb, session);

            // Boot pipeline
            AppendBootPipeline(sb, session);

            // Asset loads
            AppendAssetLoads(sb, session);

            // Profiler markers (subsystem timing)
            AppendProfilerMarkers(sb, session);

            // Extended subsystem counters
            AppendExtendedCounters(sb, session);

            // Scene census
            AppendSceneCensus(sb, session);

            // Scene snapshot diffs (object churn during session)
            AppendSceneSnapshots(sb, session);

            // Active scripts (suspect identification)
            AppendActiveScripts(sb, session);

            // Console logs (errors/warnings during session)
            AppendConsoleLogs(sb, session);

            // Pre-analysis (rule-based findings)
            AppendPreAnalysis(sb, ruleFindings);

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Builds a standalone profiler markers section string for display or export.
        /// Returns empty string if no marker data is available.
        /// </summary>
        public static string BuildProfilerMarkersSection(ProfilingSession session)
        {
            if (session?.ProfilerMarkers == null || session.ProfilerMarkers.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("--- Profiler Markers (top subsystem timing) ---");
            sb.Append("profilerMarkers: [");

            for (int i = 0; i < session.ProfilerMarkers.Count; i++)
            {
                var m = session.ProfilerMarkers[i];
                double avgIncMs = m.AvgInclusiveTimeNs / 1_000_000.0;
                double avgExcMs = m.AvgExclusiveTimeNs / 1_000_000.0;
                double maxIncMs = m.MaxInclusiveTimeNs / 1_000_000.0;

                if (i > 0) sb.Append(",");
                sb.AppendLine();
                sb.Append($"  {{ \"name\": \"{EscapeJson(m.MarkerName)}\", \"avgInclusiveMs\": {avgIncMs:F1}, \"avgExclusiveMs\": {avgExcMs:F1}, \"maxInclusiveMs\": {maxIncMs:F1}, \"avgCallCount\": {m.AvgCallCount:F1} }}");
            }

            sb.AppendLine();
            sb.AppendLine("]");
            return sb.ToString();
        }

        private static void AppendProfilerMarkers(StringBuilder sb, ProfilingSession session)
        {
            if (session?.ProfilerMarkers == null || session.ProfilerMarkers.Count == 0)
            {
                sb.AppendLine("  \"profilerMarkers\": null,");
                return;
            }

            sb.AppendLine("  \"profilerMarkers\": [");
            for (int i = 0; i < session.ProfilerMarkers.Count; i++)
            {
                var m = session.ProfilerMarkers[i];
                double avgIncMs = m.AvgInclusiveTimeNs / 1_000_000.0;
                double avgExcMs = m.AvgExclusiveTimeNs / 1_000_000.0;
                double maxIncMs = m.MaxInclusiveTimeNs / 1_000_000.0;
                string comma = i < session.ProfilerMarkers.Count - 1 ? "," : "";

                sb.AppendLine($"    {{ \"name\": \"{EscapeJson(m.MarkerName)}\", \"avgInclusiveMs\": {avgIncMs:F1}, \"avgExclusiveMs\": {avgExcMs:F1}, \"maxInclusiveMs\": {maxIncMs:F1}, \"avgCallCount\": {m.AvgCallCount:F1} }}{comma}");
            }
            sb.AppendLine("  ],");
        }

        private static void AppendEnvironment(StringBuilder sb, ProfilingSession session)
        {
            var m = session.Metadata;
            sb.AppendLine("  \"environment\": {");
            sb.AppendLine($"    \"isEditor\": {m.IsEditor.ToString().ToLower()},");
            sb.AppendLine($"    \"isDevelopmentBuild\": {m.IsDevelopmentBuild.ToString().ToLower()},");

            if (m.IsEditor)
            {
                sb.AppendLine("    \"editorWindows\": {");
                sb.AppendLine($"      \"sceneViewOpen\": {m.SceneViewOpen.ToString().ToLower()},");
                sb.AppendLine($"      \"inspectorOpen\": {m.InspectorOpen.ToString().ToLower()},");
                sb.AppendLine($"      \"profilerOpen\": {m.ProfilerOpen.ToString().ToLower()},");
                sb.AppendLine($"      \"gameViewCount\": {m.GameViewCount}");
                sb.AppendLine("    },");

                if (m.Baseline.IsValid)
                {
                    sb.AppendLine("    \"editorBaseline\": {");
                    sb.AppendLine($"      \"avgCpuFrameTimeMs\": {m.Baseline.AvgCpuFrameTimeMs:F1},");
                    sb.AppendLine($"      \"avgRenderThreadMs\": {m.Baseline.AvgRenderThreadMs:F1},");
                    sb.AppendLine($"      \"avgGcAllocBytes\": {m.Baseline.AvgGcAllocBytes},");
                    sb.AppendLine($"      \"avgGcAllocCount\": {m.Baseline.AvgGcAllocCount},");
                    sb.AppendLine($"      \"avgDrawCalls\": {m.Baseline.AvgDrawCalls},");
                    sb.AppendLine($"      \"avgBatches\": {m.Baseline.AvgBatches},");
                    sb.AppendLine($"      \"avgSetPassCalls\": {m.Baseline.AvgSetPassCalls},");
                    sb.AppendLine($"      \"sampleCount\": {m.Baseline.SampleCount},");
                    sb.AppendLine($"      \"isValid\": true");
                    sb.AppendLine("    },");
                }

                sb.AppendLine("    \"note\": \"Data captured in Unity Editor Play Mode. Editor overhead (Scene view, Inspector, etc.) " +
                              "is included in measurements. Subtract baseline values for approximate game-only metrics.\"");
            }

            sb.AppendLine("  },");
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
                        float batchingEfficiency = 1f - (float)(batches.Average() / drawCalls.Average());
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

        private static void AppendSceneSnapshots(StringBuilder sb, ProfilingSession session)
        {
            var snapshots = session.SceneSnapshots;
            if (snapshots == null || snapshots.Count == 0)
            {
                sb.AppendLine("  \"sceneSnapshots\": null,");
                return;
            }

            // Summarize: baseline count, final count, significant diffs only
            var baseline = snapshots[0];
            var last = snapshots[snapshots.Count - 1];
            int netGrowth = last.TotalObjectCount - baseline.TotalObjectCount;

            sb.AppendLine("  \"sceneSnapshots\": {");
            sb.AppendLine($"    \"baselineObjectCount\": {baseline.TotalObjectCount},");
            sb.AppendLine($"    \"finalObjectCount\": {last.TotalObjectCount},");
            sb.AppendLine($"    \"netGrowth\": {netGrowth},");
            sb.AppendLine($"    \"snapshotCount\": {snapshots.Count},");

            // Include top object names added (most frequently instantiated types)
            var addedNames = new Dictionary<string, int>();
            foreach (var snap in snapshots)
            {
                if (snap.Added == null) continue;
                foreach (var obj in snap.Added)
                {
                    // Strip "(Clone)" suffix and instance numbers for grouping
                    string baseName = obj.Name;
                    int cloneIdx = baseName.IndexOf("(Clone)");
                    if (cloneIdx > 0) baseName = baseName.Substring(0, cloneIdx).Trim();

                    if (addedNames.ContainsKey(baseName))
                        addedNames[baseName]++;
                    else
                        addedNames[baseName] = 1;
                }
            }

            if (addedNames.Count > 0)
            {
                sb.AppendLine("    \"topInstantiatedObjects\": [");
                var top = addedNames.OrderByDescending(kv => kv.Value).Take(10);
                bool first = true;
                foreach (var kv in top)
                {
                    if (!first) sb.AppendLine(",");
                    first = false;
                    sb.Append($"      {{ \"name\": \"{EscapeJson(kv.Key)}\", \"count\": {kv.Value} }}");
                }
                sb.AppendLine();
                sb.AppendLine("    ],");
            }

            // Include significant diffs (spikes only, not periodic)
            var significantDiffs = new List<string>();
            foreach (var snap in snapshots)
            {
                if (snap.Trigger != SnapshotTrigger.FrameSpike && snap.Trigger != SnapshotTrigger.GcSpike)
                    continue;
                int added = snap.Added?.Length ?? 0;
                int removed = snap.Removed?.Length ?? 0;
                if (added > 0 || removed > 0)
                {
                    significantDiffs.Add($"      {{ \"frame\": {snap.FrameIndex}, \"trigger\": \"{snap.Trigger}\", \"added\": {added}, \"removed\": {removed}, \"total\": {snap.TotalObjectCount} }}");
                }
            }

            if (significantDiffs.Count > 0)
            {
                sb.AppendLine("    \"spikeFrameDiffs\": [");
                sb.AppendLine(string.Join(",\n", significantDiffs));
                sb.AppendLine("    ]");
            }
            else
            {
                sb.AppendLine("    \"spikeFrameDiffs\": []");
            }

            sb.AppendLine("  },");
        }

        private static void AppendActiveScripts(StringBuilder sb, ProfilingSession session)
        {
            var scripts = session.ActiveScripts;
            if (scripts == null || scripts.Count == 0) return;

            sb.AppendLine("  \"activeScripts\": [");
            for (int i = 0; i < scripts.Count; i++)
            {
                var s = scripts[i];
                string names = string.Join(", ", s.SampleGameObjectNames ?? Array.Empty<string>());
                string comma = i < scripts.Count - 1 ? "," : "";
                sb.AppendLine($"    {{ \"type\": \"{EscapeJson(s.TypeName)}\", \"instances\": {s.InstanceCount}, \"on\": \"{EscapeJson(names)}\" }}{comma}");
            }
            sb.AppendLine("  ],");
        }

        private static void AppendConsoleLogs(StringBuilder sb, ProfilingSession session)
        {
            var logs = session.ConsoleLogs;
            if (logs == null || logs.Count == 0) return;

            sb.AppendLine("  \"consoleLogs\": [");
            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                string comma = i < logs.Count - 1 ? "," : "";
                sb.Append($"    {{ \"type\": \"{l.LogType}\", \"msg\": \"{EscapeJson(l.Message)}\"");
                if (!string.IsNullOrEmpty(l.StackTraceHint))
                    sb.Append($", \"src\": \"{EscapeJson(l.StackTraceHint)}\"");
                sb.AppendLine($" }}{comma}");
            }
            sb.AppendLine("  ],");
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
                sb.AppendLine($"      {{ \"ruleId\": \"{f.RuleId}\", \"category\": \"{f.Category}\", \"severity\": \"{f.Severity}\", \"confidence\": \"{f.Confidence}\", \"title\": \"{EscapeJson(f.Title)}\", \"metric\": {f.Metric:F1}, \"threshold\": {f.Threshold:F1} }}{comma}");
            }
            sb.AppendLine("    ]");
            sb.AppendLine("  }");
        }

        private static void AppendExtendedCounters(StringBuilder sb, ProfilingSession session)
        {
            var frames = session.GetFrames();
            if (frames.Length == 0) return;

            bool hasPhysics = frames.Any(f => f.PhysicsActiveBodies > 0 || f.PhysicsContacts > 0);
            bool hasAudio = frames.Any(f => f.AudioVoiceCount > 0);
            bool hasAnimation = frames.Any(f => f.AnimatorCount > 0);
            bool hasUI = frames.Any(f => f.UICanvasRebuilds > 0 || f.UILayoutRebuilds > 0);

            if (!hasPhysics && !hasAudio && !hasAnimation && !hasUI) return;

            sb.AppendLine("  \"extendedCounters\": {");
            bool needsComma = false;

            if (hasPhysics)
            {
                if (needsComma) sb.AppendLine(",");
                sb.Append("    \"physics\": { ");
                sb.Append($"\"avgActiveBodies\": {frames.Average(f => f.PhysicsActiveBodies):F0}, ");
                sb.Append($"\"avgKinematicBodies\": {frames.Average(f => f.PhysicsKinematicBodies):F0}, ");
                sb.Append($"\"avgContacts\": {frames.Average(f => f.PhysicsContacts):F0}, ");
                sb.Append($"\"maxContacts\": {frames.Max(f => f.PhysicsContacts)}");
                sb.Append(" }");
                needsComma = true;
            }
            if (hasAudio)
            {
                if (needsComma) sb.AppendLine(",");
                sb.Append("    \"audio\": { ");
                sb.Append($"\"avgVoices\": {frames.Average(f => f.AudioVoiceCount):F0}, ");
                sb.Append($"\"maxVoices\": {frames.Max(f => f.AudioVoiceCount)}, ");
                sb.Append($"\"avgDSPLoad\": {frames.Average(f => f.AudioDSPLoad):F1}");
                sb.Append(" }");
                needsComma = true;
            }
            if (hasAnimation)
            {
                if (needsComma) sb.AppendLine(",");
                sb.Append("    \"animation\": { ");
                sb.Append($"\"avgAnimators\": {frames.Average(f => f.AnimatorCount):F0}, ");
                sb.Append($"\"maxAnimators\": {frames.Max(f => f.AnimatorCount)}");
                sb.Append(" }");
                needsComma = true;
            }
            if (hasUI)
            {
                if (needsComma) sb.AppendLine(",");
                sb.Append("    \"ui\": { ");
                sb.Append($"\"avgCanvasRebuilds\": {frames.Average(f => f.UICanvasRebuilds):F1}, ");
                sb.Append($"\"maxCanvasRebuilds\": {frames.Max(f => f.UICanvasRebuilds)}, ");
                sb.Append($"\"avgLayoutRebuilds\": {frames.Average(f => f.UILayoutRebuilds):F1}, ");
                sb.Append($"\"maxLayoutRebuilds\": {frames.Max(f => f.UILayoutRebuilds)}");
                sb.Append(" }");
            }
            sb.AppendLine();
            sb.AppendLine("  },");
        }

        private static void AppendSceneCensus(StringBuilder sb, ProfilingSession session)
        {
            var census = session.SceneCensus;
            if (!census.IsValid) return;

            sb.AppendLine("  \"sceneCensus\": {");
            sb.AppendLine($"    \"totalGameObjects\": {census.TotalGameObjects},");
            sb.AppendLine($"    \"totalComponents\": {census.TotalComponents},");

            if (census.ComponentDistribution != null && census.ComponentDistribution.Length > 0)
            {
                sb.AppendLine("    \"componentDistribution\": [");
                for (int i = 0; i < census.ComponentDistribution.Length; i++)
                {
                    var c = census.ComponentDistribution[i];
                    string comma = i < census.ComponentDistribution.Length - 1 ? "," : "";
                    sb.AppendLine($"      {{ \"type\": \"{EscapeJson(c.TypeName)}\", \"count\": {c.Count} }}{comma}");
                }
                sb.AppendLine("    ],");
            }

            sb.AppendLine("    \"lights\": {");
            sb.AppendLine($"      \"directional\": {census.DirectionalLights},");
            sb.AppendLine($"      \"point\": {census.PointLights},");
            sb.AppendLine($"      \"spot\": {census.SpotLights},");
            sb.AppendLine($"      \"area\": {census.AreaLights}");
            sb.AppendLine("    },");
            sb.AppendLine($"    \"canvasCount\": {census.CanvasCount},");
            sb.AppendLine($"    \"cameraCount\": {census.CameraCount},");
            sb.AppendLine($"    \"particleSystemCount\": {census.ParticleSystemCount},");
            sb.AppendLine($"    \"lodGroupCount\": {census.LODGroupCount},");
            sb.AppendLine($"    \"rigidbodyCount\": {census.RigidbodyCount},");
            sb.AppendLine($"    \"rigidbody2DCount\": {census.Rigidbody2DCount}");
            sb.AppendLine("  },");
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
