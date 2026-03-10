using System.Collections.Generic;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
using DrWario.Runtime;
using NUnit.Framework;

namespace DrWario.Tests
{
    [TestFixture]
    public class LLMPromptBuilderTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset static state before each test
            LLMPromptBuilder.AdditionalContext = null;
        }

        // -- System Prompt --

        [Test]
        public void BuildSystemPrompt_NoAdditionalContext_ReturnsBasePrompt()
        {
            var prompt = LLMPromptBuilder.BuildSystemPrompt();

            Assert.That(prompt, Does.Contain("expert Unity performance analyst"));
            Assert.That(prompt, Does.Not.Contain("Additional project context"));
        }

        [Test]
        public void BuildSystemPrompt_WithAdditionalContext_AppendsContext()
        {
            LLMPromptBuilder.AdditionalContext = "HybridFrame uses ECS architecture.";

            var prompt = LLMPromptBuilder.BuildSystemPrompt();

            Assert.That(prompt, Does.Contain("Additional project context"));
            Assert.That(prompt, Does.Contain("HybridFrame uses ECS architecture."));
        }

        [Test]
        public void BuildAskDoctorSystemPrompt_NoAdditionalContext_ReturnsBasePrompt()
        {
            var prompt = LLMPromptBuilder.BuildAskDoctorSystemPrompt();

            Assert.That(prompt, Does.Contain("DrWario"));
            Assert.That(prompt, Does.Contain("Unity runtime performance consultant"));
            Assert.That(prompt, Does.Not.Contain("Additional project context"));
        }

        [Test]
        public void BuildAskDoctorSystemPrompt_WithAdditionalContext_AppendsContext()
        {
            LLMPromptBuilder.AdditionalContext = "Custom context";

            var prompt = LLMPromptBuilder.BuildAskDoctorSystemPrompt();

            Assert.That(prompt, Does.Contain("Custom context"));
        }

        // -- User Prompt Sections --

        [Test]
        public void BuildUserPrompt_ContainsSessionMetadata()
        {
            var session = new TestSessionBuilder()
                .WithTargetFps(60)
                .AddFrames(50, cpuMs: 12f)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"session\""));
            Assert.That(prompt, Does.Contain("\"unityVersion\""));
            Assert.That(prompt, Does.Contain("\"platform\""));
            Assert.That(prompt, Does.Contain("\"targetFrameRate\": 60"));
        }

        [Test]
        public void BuildUserPrompt_ContainsFrameSummary()
        {
            var session = new TestSessionBuilder()
                .AddFrames(100, cpuMs: 15f, gcAllocBytes: 2048)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"frameSummary\""));
            Assert.That(prompt, Does.Contain("\"cpuFrameTime\""));
            Assert.That(prompt, Does.Contain("\"gcAllocation\""));
            Assert.That(prompt, Does.Contain("\"totalFrames\": 100"));
        }

        [Test]
        public void BuildUserPrompt_ContainsMemoryTrajectory()
        {
            var session = new TestSessionBuilder()
                .AddFramesWithGrowingHeap(100, 50_000_000, 10_000)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"memoryTrajectory\""));
            Assert.That(prompt, Does.Contain("\"samples\""));
            Assert.That(prompt, Does.Contain("\"linearRegression\""));
            Assert.That(prompt, Does.Contain("\"heapSlopeBytePerSec\""));
        }

        [Test]
        public void BuildUserPrompt_TooFewFrames_MemoryTrajectoryNull()
        {
            var session = new TestSessionBuilder()
                .AddFrames(5, cpuMs: 10f)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"memoryTrajectory\": null"));
        }

        [Test]
        public void BuildUserPrompt_WithBootStages_ContainsBootPipeline()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .AddBootStage("Init", 500)
                .AddBootStage("LoadScene", 2000)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"bootPipeline\""));
            Assert.That(prompt, Does.Contain("\"Init\""));
            Assert.That(prompt, Does.Contain("\"LoadScene\""));
            Assert.That(prompt, Does.Contain("\"totalMs\": 2500"));
        }

        [Test]
        public void BuildUserPrompt_NoBootStages_BootPipelineNull()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"bootPipeline\": null"));
        }

        [Test]
        public void BuildUserPrompt_WithAssetLoads_ContainsAssetLoads()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .AddAssetLoad("Textures/Large.png", 800, 5_000_000)
                .AddAssetLoad("Prefabs/Player.prefab", 200, 1_000_000)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"assetLoads\""));
            Assert.That(prompt, Does.Contain("\"count\": 2"));
            Assert.That(prompt, Does.Contain("Textures/Large.png"));
        }

        [Test]
        public void BuildUserPrompt_WithProfilerMarkers_ContainsMarkers()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithProfilerMarkers(new List<ProfilerMarkerSample>
                {
                    new ProfilerMarkerSample
                    {
                        MarkerName = "PlayerLoop",
                        AvgInclusiveTimeNs = 10_000_000, // 10ms
                        AvgExclusiveTimeNs = 2_000_000,  // 2ms
                        MaxInclusiveTimeNs = 20_000_000,
                        AvgCallCount = 1f
                    }
                })
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"profilerMarkers\""));
            Assert.That(prompt, Does.Contain("PlayerLoop"));
        }

        [Test]
        public void BuildUserPrompt_NoMarkers_ProfilerMarkersNull()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"profilerMarkers\": null"));
        }

        [Test]
        public void BuildUserPrompt_WithActiveScripts_ContainsScripts()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithActiveScripts(new List<ActiveScriptEntry>
                {
                    new ActiveScriptEntry
                    {
                        TypeName = "PlayerController",
                        Namespace = "Game.Player",
                        InstanceCount = 1,
                        SampleGameObjectNames = new[] { "Player" }
                    },
                    new ActiveScriptEntry
                    {
                        TypeName = "BulletSpawner",
                        Namespace = null,
                        InstanceCount = 5,
                        SampleGameObjectNames = new[] { "EnemyShip", "PlayerShip" }
                    }
                })
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"activeScripts\""));
            Assert.That(prompt, Does.Contain("PlayerController"));
            Assert.That(prompt, Does.Contain("\"ns\": \"Game.Player\""));
            // BulletSpawner has no namespace, so no ns field
            Assert.That(prompt, Does.Contain("\"type\": \"BulletSpawner\""));
        }

        [Test]
        public void BuildUserPrompt_WithConsoleLogs_ContainsLogs()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithConsoleLogs(new List<ConsoleLogEntry>
                {
                    new ConsoleLogEntry
                    {
                        Timestamp = 1.5f,
                        Message = "NullReferenceException",
                        LogType = "Error",
                        StackTraceHint = "PlayerController.Update()"
                    }
                })
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"consoleLogs\""));
            Assert.That(prompt, Does.Contain("NullReferenceException"));
            Assert.That(prompt, Does.Contain("PlayerController.Update()"));
        }

        [Test]
        public void BuildUserPrompt_WithPreAnalysis_ContainsFindings()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .Build();

            var findings = new List<DiagnosticFinding>
            {
                new DiagnosticFinding
                {
                    RuleId = "FRAME_DROP",
                    Category = "CPU",
                    Severity = Severity.Warning,
                    Confidence = Confidence.High,
                    Title = "Frame drops detected",
                    Metric = 25f,
                    Threshold = 16.67f
                }
            };

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, findings);

            Assert.That(prompt, Does.Contain("\"preAnalysis\""));
            Assert.That(prompt, Does.Contain("\"findingsCount\": 1"));
            Assert.That(prompt, Does.Contain("FRAME_DROP"));
        }

        [Test]
        public void BuildUserPrompt_EditorSession_ContainsEnvironment()
        {
            var session = new TestSessionBuilder()
                .AsEditorSession(new EditorBaseline
                {
                    AvgCpuFrameTimeMs = 3.5f,
                    AvgGcAllocBytes = 200,
                    AvgDrawCalls = 15,
                    SampleCount = 60,
                    IsValid = true
                })
                .AddFrames(50, cpuMs: 12f)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"isEditor\": true"));
            Assert.That(prompt, Does.Contain("\"editorBaseline\""));
            Assert.That(prompt, Does.Contain("\"editorWindows\""));
            Assert.That(prompt, Does.Contain("Editor Play Mode"));
        }

        [Test]
        public void BuildUserPrompt_GpuDataAllZero_GpuSectionElided()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50, cpuMs: 10f, gpuMs: 0f)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Not.Contain("\"gpuFrameTime\""));
        }

        [Test]
        public void BuildUserPrompt_WithGpuData_GpuSectionPresent()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50, cpuMs: 10f, gpuMs: 8f)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"gpuFrameTime\""));
        }

        [Test]
        public void BuildUserPrompt_WithSceneCensus_FiltersTrivialComponents()
        {
            var census = new SceneCensus
            {
                TotalGameObjects = 100,
                TotalComponents = 300,
                ComponentDistribution = new[]
                {
                    new ComponentCount { TypeName = "Transform", Count = 100 },
                    new ComponentCount { TypeName = "MeshRenderer", Count = 50 },
                    new ComponentCount { TypeName = "RectTransform", Count = 30 },
                    new ComponentCount { TypeName = "BoxCollider", Count = 20 }
                },
                IsValid = true,
                CameraCount = 1,
                DirectionalLights = 1
            };

            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithSceneCensus(census)
                .Build();

            var prompt = LLMPromptBuilder.BuildUserPrompt(session, new List<DiagnosticFinding>());

            Assert.That(prompt, Does.Contain("\"sceneCensus\""));
            Assert.That(prompt, Does.Contain("MeshRenderer"));
            Assert.That(prompt, Does.Contain("BoxCollider"));
            // Transform and RectTransform should be filtered
            Assert.That(prompt, Does.Not.Contain("\"type\": \"Transform\""));
            Assert.That(prompt, Does.Not.Contain("\"type\": \"RectTransform\""));
        }

        // -- BuildProfilerMarkersSection --

        [Test]
        public void BuildProfilerMarkersSection_NoMarkers_ReturnsEmpty()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();

            var result = LLMPromptBuilder.BuildProfilerMarkersSection(session);

            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void BuildProfilerMarkersSection_WithMarkers_ReturnsFormattedSection()
        {
            var session = new TestSessionBuilder()
                .AddFrames(50)
                .WithProfilerMarkers(new List<ProfilerMarkerSample>
                {
                    new ProfilerMarkerSample
                    {
                        MarkerName = "Physics.Simulate",
                        AvgInclusiveTimeNs = 5_000_000,
                        AvgExclusiveTimeNs = 4_000_000,
                        MaxInclusiveTimeNs = 8_000_000,
                        AvgCallCount = 2f
                    }
                })
                .Build();

            var result = LLMPromptBuilder.BuildProfilerMarkersSection(session);

            Assert.That(result, Does.Contain("Profiler Markers"));
            Assert.That(result, Does.Contain("Physics.Simulate"));
            Assert.That(result, Does.Contain("5.0")); // avgInclusiveMs
        }

        // -- BuildFullPromptForClipboard --

        [Test]
        public void BuildFullPromptForClipboard_ContainsAllSections()
        {
            var session = new TestSessionBuilder().AddFrames(100, cpuMs: 15f).Build();
            var report = TestReportFactory.MakeReport(
                TestReportFactory.MakeFinding(Severity.Warning, category: "CPU", ruleId: "FRAME_DROP")
            );
            report.ComputeGrades();

            var result = LLMPromptBuilder.BuildFullPromptForClipboard(session, report, "Why is my game slow?");

            Assert.That(result, Does.Contain("=== SYSTEM CONTEXT ==="));
            Assert.That(result, Does.Contain("=== PROFILING DATA ==="));
            Assert.That(result, Does.Contain("=== ANALYSIS REPORT ==="));
            Assert.That(result, Does.Contain("=== QUESTION ==="));
            Assert.That(result, Does.Contain("Why is my game slow?"));
        }

        [Test]
        public void BuildFullPromptForClipboard_NullSession_SkipsProfilingData()
        {
            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var result = LLMPromptBuilder.BuildFullPromptForClipboard(null, report, "question");

            Assert.That(result, Does.Contain("=== SYSTEM CONTEXT ==="));
            Assert.That(result, Does.Not.Contain("=== PROFILING DATA ==="));
            Assert.That(result, Does.Contain("=== ANALYSIS REPORT ==="));
        }

        [Test]
        public void BuildFullPromptForClipboard_NoQuestion_SkipsQuestionSection()
        {
            var session = new TestSessionBuilder().AddFrames(50).Build();
            var report = TestReportFactory.MakeReport();
            report.ComputeGrades();

            var result = LLMPromptBuilder.BuildFullPromptForClipboard(session, report, null);

            Assert.That(result, Does.Not.Contain("=== QUESTION ==="));
        }
    }
}
