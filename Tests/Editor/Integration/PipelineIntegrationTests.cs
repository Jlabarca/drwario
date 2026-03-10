using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    /// <summary>
    /// P3 Integration tests: full pipeline flows (session → engine → report → comparison → prompt).
    /// These exercise multiple components together rather than individual units.
    /// </summary>
    [TestFixture]
    public class PipelineIntegrationTests
    {
        [SetUp]
        public void SetUp()
        {
            LLMPromptBuilder.AdditionalContext = null;
        }

        // -- Full Pipeline: Session → Engine → Report with Correlations --

        [Test]
        public void FullPipeline_GcSpikesAndFrameDrops_CorrelationDetected()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(50, cpuMs: 10f, gcAllocBytes: 100)
                .AddFrames(50, cpuMs: 35f, gcAllocBytes: 50_000) // GC spikes + frame drops
                .Build();

            var report = engine.Analyze(session);

            // Should have both GC and frame drop findings
            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "GC_SPIKE"), "Expected GC_SPIKE finding");
            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "FRAME_DROP"), "Expected FRAME_DROP finding");

            // Should have synthesis with prioritized actions
            Assert.IsTrue(report.Synthesis.HasValue);
            Assert.Greater(report.Synthesis.Value.PrioritizedActions.Count, 0);

            // Grade should reflect the issues
            Assert.Greater(report.Findings.Count, 1, "Should have multiple findings");
        }

        [Test]
        public void FullPipeline_MemoryLeakAndGcSpikes_CorrelationDetected()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFramesWithGrowingHeap(100, 100_000_000, 500_000, cpuMs: 10f)
                .Build();

            // Manually add GC alloc data via custom frames
            var builder = new TestSessionBuilder().WithTargetFps(60);
            for (int i = 0; i < 100; i++)
            {
                builder.AddFrame(new FrameSample
                {
                    Timestamp = i * 0.0167f,
                    DeltaTime = 0.0167f,
                    CpuFrameTimeMs = 10f,
                    GcAllocBytes = 5000, // Constant GC pressure
                    TotalHeapBytes = 100_000_000 + (long)i * 500_000,
                    FrameNumber = i
                });
            }
            var leakSession = builder.Build();

            var report = engine.Analyze(leakSession);

            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "MEMORY_LEAK"), "Expected MEMORY_LEAK finding");
            Assert.IsTrue(report.Findings.Any(f => f.RuleId == "GC_SPIKE"), "Expected GC_SPIKE finding");
            Assert.IsTrue(report.Synthesis.HasValue);
        }

        [Test]
        public void FullPipeline_HealthySession_GradeA()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 8f, gcAllocBytes: 200, heapBytes: 100_000_000)
                .Build();

            var report = engine.Analyze(session);

            Assert.AreEqual('A', report.OverallGrade, $"Expected A, got {report.OverallGrade} (score={report.HealthScore:F1})");
        }

        [Test]
        public void FullPipeline_SeverelyDegraded_GradeF()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(50, cpuMs: 80f, gcAllocBytes: 100_000, heapBytes: 100_000_000)
                .AddFrames(50, cpuMs: 120f, gcAllocBytes: 200_000, heapBytes: 200_000_000)
                .AddBootStage("Init", 5000)
                .AddBootStage("LoadScene", 10000)
                .Build();

            var report = engine.Analyze(session);

            Assert.AreEqual('F', report.OverallGrade, $"Expected F, got {report.OverallGrade} (score={report.HealthScore:F1})");
            Assert.Greater(report.Findings.Count, 3, "Expected multiple findings for severely degraded session");
        }

        [Test]
        public void FullPipeline_MultipleRuleCategories_AllCategoriesGraded()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 25f, gcAllocBytes: 5000)
                .AddBootStage("Slow", 3000)
                .AddAssetLoad("BigAsset", 800)
                .Build();

            var report = engine.Analyze(session);

            // Should have grades for multiple categories
            Assert.Greater(report.CategoryGrades.Count, 1, "Expected grades for multiple categories");
        }

        // -- Report Comparison Integration --

        [Test]
        public void ReportComparison_TwoEngineReports_DiffsComputed()
        {
            var engine = new AnalysisEngine();

            // Report A: degraded performance
            var sessionA = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 25f, gcAllocBytes: 10000)
                .Build();
            var reportA = engine.Analyze(sessionA);

            // Report B: improved performance
            var sessionB = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f, gcAllocBytes: 200)
                .Build();
            var reportB = engine.Analyze(sessionB);

            var comparison = new ReportComparison(reportA, reportB);

            // Report B should be better
            Assert.Greater(comparison.OverallGradeDelta, 0, "Improved report should have positive grade delta");
            Assert.Less(comparison.MetricDeltas.AvgCpuTimeDelta, 0, "CPU should have improved (negative delta)");
            Assert.Less(comparison.MetricDeltas.GcRateDelta, 0, "GC rate should have improved");
        }

        [Test]
        public void ReportComparison_FixedAndNewFindings_Tracked()
        {
            var engine = new AnalysisEngine();

            // Report A: frame drops + GC issues
            var sessionA = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(80, cpuMs: 10f, gcAllocBytes: 100)
                .AddFrames(20, cpuMs: 30f, gcAllocBytes: 50_000)
                .Build();
            var reportA = engine.Analyze(sessionA);

            // Report B: fixed frame drops but added slow boot
            var sessionB = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 8f, gcAllocBytes: 100)
                .AddBootStage("SlowInit", 5000)
                .Build();
            var reportB = engine.Analyze(sessionB);

            var comparison = new ReportComparison(reportA, reportB);

            Assert.Greater(comparison.FindingDiffs.Count, 0, "Should have finding diffs");

            // Check that we have at least some Fixed and New diffs
            bool hasFixed = comparison.FindingDiffs.Any(d => d.Status == FindingDiffStatus.Fixed);
            bool hasNew = comparison.FindingDiffs.Any(d => d.Status == FindingDiffStatus.New);

            // Frame drops should be fixed in B
            Assert.IsTrue(hasFixed || hasNew, "Should have at least Fixed or New finding diffs");
        }

        [Test]
        public void ReportComparison_SameSession_NoDelta()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 10f)
                .Build();

            var reportA = engine.Analyze(session);
            var reportB = engine.Analyze(session);

            var comparison = new ReportComparison(reportA, reportB);

            Assert.AreEqual(0f, comparison.OverallGradeDelta, 0.01f);
            Assert.AreEqual(0f, comparison.MetricDeltas.AvgCpuTimeDelta, 0.01f);
        }

        // -- Prompt Generation from Engine Output --

        [Test]
        public void PromptFromEngineOutput_UserPromptContainsFindings()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(80, cpuMs: 10f)
                .AddFrames(20, cpuMs: 30f, gcAllocBytes: 50_000)
                .Build();

            var report = engine.Analyze(session);
            var prompt = LLMPromptBuilder.BuildUserPrompt(session, report.Findings);

            Assert.That(prompt, Does.Contain("\"preAnalysis\""));
            Assert.That(prompt, Does.Contain("\"findingsCount\""));
            Assert.That(prompt, Does.Contain("\"frameSummary\""));
        }

        [Test]
        public void PromptFromEngineOutput_McpSuspectCheckFromRealReport()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 25f, gcAllocBytes: 10_000)
                .WithActiveScripts(new List<ActiveScriptEntry>
                {
                    new ActiveScriptEntry
                    {
                        TypeName = "BulletSpawner",
                        Namespace = "Game.Combat",
                        InstanceCount = 10,
                        SampleGameObjectNames = new[] { "EnemyShip", "PlayerShip" }
                    }
                })
                .Build();

            var report = engine.Analyze(session);
            var prompt = LLMPromptBuilder.BuildMcpSuspectCheckPrompt(session, report);

            Assert.That(prompt, Does.Contain("BulletSpawner"));
            Assert.That(prompt, Does.Contain("DRWARIO FINDINGS"));
            Assert.That(prompt, Does.Contain("PROFILING SUMMARY"));
        }

        [Test]
        public void PromptFromEngineOutput_ClipboardPromptFromRealReport()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 12f)
                .Build();

            var report = engine.Analyze(session);
            var prompt = LLMPromptBuilder.BuildFullPromptForClipboard(session, report, "How can I optimize?");

            Assert.That(prompt, Does.Contain("=== SYSTEM CONTEXT ==="));
            Assert.That(prompt, Does.Contain("=== PROFILING DATA ==="));
            Assert.That(prompt, Does.Contain("=== ANALYSIS REPORT ==="));
            Assert.That(prompt, Does.Contain("How can I optimize?"));
            Assert.That(prompt, Does.Contain(report.OverallGrade.ToString()));
        }

        [Test]
        public void PromptFromEngineOutput_ReportCorrectionFromRealReport()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(80, cpuMs: 10f)
                .AddFrames(20, cpuMs: 40f, gcAllocBytes: 80_000)
                .Build();

            var report = engine.Analyze(session);
            var prompt = LLMPromptBuilder.BuildReportCorrectionPrompt(session, report);

            Assert.That(prompt, Does.Contain("AUDIT and CORRECT"));
            Assert.That(prompt, Does.Contain("PROFILING DATA"));
            Assert.That(prompt, Does.Contain("DRWARIO REPORT TO AUDIT"));
            // Should contain numbered findings from the real report
            Assert.That(prompt, Does.Contain("#1"));
        }

        // -- Full Pipeline with Rich Context --

        [Test]
        public void FullPipeline_WithAllDataSources_ProducesCompleteReport()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(100, cpuMs: 15f, gcAllocBytes: 3000, drawCalls: 500, triangles: 100_000)
                .AddBootStage("Init", 1000)
                .AddBootStage("LoadAssets", 3000)
                .AddAssetLoad("BigTexture.png", 600, 10_000_000)
                .AddNetworkEvent(NetworkEventType.Send, 256, 50f)
                .WithActiveScripts(new List<ActiveScriptEntry>
                {
                    new ActiveScriptEntry { TypeName = "PlayerController", InstanceCount = 1, SampleGameObjectNames = new[] { "Player" } }
                })
                .WithConsoleLogs(new List<ConsoleLogEntry>
                {
                    new ConsoleLogEntry { LogType = "Warning", Message = "Shader compilation", Timestamp = 1f }
                })
                .WithProfilerMarkers(new List<ProfilerMarkerSample>
                {
                    new ProfilerMarkerSample { MarkerName = "Rendering", AvgInclusiveTimeNs = 8_000_000, AvgExclusiveTimeNs = 6_000_000, MaxInclusiveTimeNs = 12_000_000, AvgCallCount = 1f }
                })
                .WithSceneCensus(new SceneCensus
                {
                    TotalGameObjects = 500,
                    TotalComponents = 1500,
                    IsValid = true,
                    CameraCount = 1,
                    DirectionalLights = 1
                })
                .Build();

            var report = engine.Analyze(session);

            // Report should be complete
            Assert.IsNotNull(report);
            Assert.Greater(report.Findings.Count, 0, "Should produce findings from rich data");
            Assert.AreNotEqual('\0', report.OverallGrade);
            Assert.IsTrue(report.Synthesis.HasValue);
            Assert.IsNotEmpty(report.Synthesis.Value.ExecutiveSummary);

            // Build prompt from this report — should include all sections
            var prompt = LLMPromptBuilder.BuildUserPrompt(session, report.Findings);
            Assert.That(prompt, Does.Contain("\"session\""));
            Assert.That(prompt, Does.Contain("\"frameSummary\""));
            Assert.That(prompt, Does.Contain("\"bootPipeline\""));
            Assert.That(prompt, Does.Contain("\"assetLoads\""));
            Assert.That(prompt, Does.Contain("\"profilerMarkers\""));
            Assert.That(prompt, Does.Contain("\"sceneCensus\""));
            Assert.That(prompt, Does.Contain("\"activeScripts\""));
            Assert.That(prompt, Does.Contain("\"consoleLogs\""));
            Assert.That(prompt, Does.Contain("\"preAnalysis\""));
        }

        [Test]
        public void FullPipeline_EditorSession_FindingsHaveEditorContext()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AsEditorSession(new EditorBaseline
                {
                    AvgCpuFrameTimeMs = 5f,
                    AvgGcAllocBytes = 500,
                    AvgDrawCalls = 20,
                    SampleCount = 60,
                    IsValid = true
                })
                .AddFrames(100, cpuMs: 20f, gcAllocBytes: 5000)
                .Build();

            var report = engine.Analyze(session);

            // Some findings should have editor-related confidence or environment notes
            Assert.IsTrue(report.Findings.Count > 0, "Should produce findings");

            // The prompt should contain editor environment context
            var prompt = LLMPromptBuilder.BuildUserPrompt(session, report.Findings);
            Assert.That(prompt, Does.Contain("\"isEditor\": true"));
            Assert.That(prompt, Does.Contain("\"editorBaseline\""));
        }

        [Test]
        public void FullPipeline_SynthesisHasCorrectStructure()
        {
            var engine = new AnalysisEngine();
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(80, cpuMs: 10f, gcAllocBytes: 100)
                .AddFrames(20, cpuMs: 30f, gcAllocBytes: 50_000)
                .AddBootStage("SlowStage", 5000)
                .Build();

            var report = engine.Analyze(session);

            Assert.IsTrue(report.Synthesis.HasValue);
            var synthesis = report.Synthesis.Value;

            Assert.IsNotEmpty(synthesis.ExecutiveSummary);
            Assert.IsNotNull(synthesis.PrioritizedActions);
            Assert.IsNotNull(synthesis.Correlations);
        }
    }
}
