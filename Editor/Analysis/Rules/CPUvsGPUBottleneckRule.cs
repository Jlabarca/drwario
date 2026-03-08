using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;

namespace DrWario.Editor.Analysis.Rules
{
    /// <summary>
    /// Classifies whether the application is CPU-bound, GPU-bound, or balanced.
    /// Uses CPU frame time, GPU frame time, and render thread time to determine
    /// the bottleneck and provide targeted recommendations.
    /// </summary>
    public class CPUvsGPUBottleneckRule : IAnalysisRule
    {
        public string Category => "CPU";
        public string RuleId => "BOTTLENECK";

        public List<DiagnosticFinding> Analyze(ProfilingSession session)
        {
            var findings = new List<DiagnosticFinding>();
            var frames = session.GetFrames();
            if (frames.Length < 30) return findings;

            float targetMs = session.Metadata.TargetFrameRate > 0
                ? 1000f / session.Metadata.TargetFrameRate
                : 16.67f;

            float rawAvgCpu = frames.Average(f => f.CpuFrameTimeMs);

            // Editor adjustment: subtract baseline CPU overhead
            bool isEditor = session.Metadata.IsEditor;
            var baseline = session.Metadata.Baseline;
            float avgCpu = rawAvgCpu;
            if (isEditor && baseline.IsValid)
                avgCpu = EditorAdjustment.SubtractBaseline(rawAvgCpu, baseline.AvgCpuFrameTimeMs);
            var gpuFrames = frames.Where(f => f.GpuFrameTimeMs > 0).ToArray();
            var renderFrames = frames.Where(f => f.RenderThreadMs > 0).ToArray();

            // Need GPU or render thread data to classify
            if (gpuFrames.Length == 0 && renderFrames.Length == 0) return findings;

            float avgGpu = gpuFrames.Length > 0 ? gpuFrames.Average(f => f.GpuFrameTimeMs) : 0;
            float avgRender = renderFrames.Length > 0 ? renderFrames.Average(f => f.RenderThreadMs) : 0;

            // Count frames where each component exceeds target
            int cpuOverTarget = frames.Count(f => f.CpuFrameTimeMs > targetMs);
            int gpuOverTarget = gpuFrames.Count(f => f.GpuFrameTimeMs > targetMs);
            int renderOverTarget = renderFrames.Count(f => f.RenderThreadMs > targetMs);

            // Classify bottleneck
            string bottleneck;
            string description;
            string recommendation;
            Severity severity;

            bool cpuOverloaded = avgCpu > targetMs;
            bool gpuOverloaded = avgGpu > targetMs;
            bool renderOverloaded = avgRender > targetMs;

            if (gpuOverloaded && avgGpu > avgCpu * 1.3f)
            {
                bottleneck = "GPU-Bound";
                severity = avgGpu > targetMs * 2 ? Severity.Critical : Severity.Warning;

                string renderDetail = "";
                bool hasRenderData = frames.Any(f => f.DrawCalls > 0);
                if (hasRenderData)
                {
                    int avgDraw = (int)frames.Where(f => f.DrawCalls > 0).Average(f => f.DrawCalls);
                    long avgTris = (long)frames.Where(f => f.Triangles > 0).DefaultIfEmpty().Average(f => f.Triangles);
                    renderDetail = $" Draw calls: {avgDraw}, triangles: {avgTris:N0}.";
                }

                description = $"GPU time ({avgGpu:F1}ms) significantly exceeds CPU ({avgCpu:F1}ms). " +
                              $"Target: {targetMs:F1}ms. GPU over target in {gpuOverTarget}/{gpuFrames.Length} frames.{renderDetail}";
                recommendation = "Reduce GPU workload: lower shadow resolution, reduce post-processing " +
                                 "(especially bloom, SSAO, SSR), use LOD groups, reduce overdraw with " +
                                 "occlusion culling, lower render scale on mobile.";
            }
            else if (cpuOverloaded && avgCpu > avgGpu * 1.3f)
            {
                bottleneck = "CPU-Bound";
                severity = avgCpu > targetMs * 2 ? Severity.Critical : Severity.Warning;
                description = $"CPU time ({avgCpu:F1}ms) significantly exceeds GPU ({avgGpu:F1}ms). " +
                              $"Target: {targetMs:F1}ms. CPU over target in {cpuOverTarget}/{frames.Length} frames.";

                if (renderOverloaded)
                {
                    description += $" Render thread also overloaded ({avgRender:F1}ms) — " +
                                   "rendering commands may be the CPU bottleneck.";
                    recommendation = "Reduce CPU rendering cost: enable SRP Batcher, " +
                                     "reduce draw calls via batching/instancing, " +
                                     "use BurstCompile for heavy systems, move work off main thread.";
                }
                else
                {
                    recommendation = "Reduce CPU workload: optimize expensive Update()/LateUpdate() scripts, " +
                                     "reduce physics simulation (fewer rigidbodies, longer fixed timestep), " +
                                     "use Job System + Burst for parallel computation, avoid allocations in hot paths.";
                }
            }
            else if (cpuOverloaded && gpuOverloaded)
            {
                bottleneck = "Both CPU and GPU Overloaded";
                severity = Severity.Critical;
                description = $"Both CPU ({avgCpu:F1}ms) and GPU ({avgGpu:F1}ms) exceed target ({targetMs:F1}ms). " +
                              $"The application needs optimization on both fronts.";
                recommendation = "Start with the slower component. " +
                                 "CPU: profile heavy scripts, reduce physics. " +
                                 "GPU: reduce draw calls, lower quality settings, use LODs. " +
                                 "Consider reducing target frame rate if both are borderline.";
            }
            else
            {
                // Both under target — no finding needed
                return findings;
            }

            // In editor, CPU-bound classification may be inflated by editor overhead
            var confidence = Confidence.High;
            string envNote = null;
            if (isEditor)
            {
                float adjustedMetric = System.Math.Max(avgCpu, avgGpu);
                confidence = EditorAdjustment.ClassifyConfidence(
                    System.Math.Max(rawAvgCpu, avgGpu), adjustedMetric, targetMs, true);
                envNote = EditorAdjustment.BuildEnvironmentNote(session.Metadata, "CPU/GPU timing");
                if (bottleneck == "CPU-Bound" && baseline.IsValid)
                    description += $" (Note: editor baseline ~{baseline.AvgCpuFrameTimeMs:F1}ms subtracted from CPU avg)";
            }

            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = severity,
                Title = bottleneck,
                Description = description,
                Recommendation = recommendation,
                Metric = System.Math.Max(avgCpu, avgGpu),
                Threshold = targetMs,
                FrameIndex = -1,
                Confidence = confidence,
                EnvironmentNote = envNote
            });

            return findings;
        }
    }
}
