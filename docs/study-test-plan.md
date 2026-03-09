# DrWario v3.0 Test Plan

## TL;DR

DrWario has **zero tests** today. This plan defines ~135 tests across 3 tiers: **P1 (56 pure unit tests)** covering ring buffer, grading, all 6 rules, LLM response parsing, report comparison, and correlation engine — no Play Mode needed. **P2 (56 editor tests)** for the analysis pipeline, MCP prompt builders, report history, and scene census. **P3 (17 integration tests)** for Play Mode workflows, end-to-end profiling, and LLM client. Start with P1 — it covers the highest-risk logic (grading math, rule thresholds, buffer wrap-around) with the lowest setup cost. A `TestSessionBuilder` fluent helper is included to eliminate boilerplate. Estimated total: 10-15 hours.

This document is an actionable guide for implementing tests for DrWario v3.0. It covers current test coverage, prioritized test categories, specific test cases, and infrastructure recommendations.

**Estimated total test count: ~135 tests**

---

## 1. Current Test Coverage

### Existing Tests

**Zero tests exist.** No `Tests/` directory, no `*Test*.cs` files, and no test assembly definitions are present in the repository.

### Original 91-Test Plan

The CLAUDE.md references a test plan at `D:\ware\HybridFrame\docs\aidoctor\automated-e2e\test-plan.md` designed when DrWario was still part of HybridFrame ("AIDoctor"). That plan covered pre-v3.0 functionality (ring buffer, grading, basic rules). The v3.0 changes add substantial new surface area that requires additional tests beyond that original plan.

---

## 2. Priority 1: Pure Unit Tests (no Unity API dependencies)

These tests exercise pure C# logic that does not call UnityEngine APIs. They can run in Unity Test Framework's **Edit Mode** test runner but require no scene, no Play Mode, and no GameObjects.

**Important caveat:** Several classes reference `UnityEngine.Mathf`, `UnityEngine.Debug`, or `UnityEngine.JsonUtility`. These are lightweight Unity APIs available in Edit Mode tests. The tests below are "pure" in the sense that they need no scene setup, no MonoBehaviours, and no Play Mode -- but they still run inside Unity's test runner since the code references Unity namespaces.

### 2.1 ProfilingSession Ring Buffer (~12 tests)

Test class: `ProfilingSessionTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `RecordFrame_UnderCapacity_FrameCountIncreases` | Record 10 frames into a capacity-100 session, verify `FrameCount == 10` |
| 2 | `RecordFrame_AtCapacity_FrameCountCapped` | Record 200 frames into capacity-100 session, verify `FrameCount == 100` |
| 3 | `GetFrames_UnderCapacity_ReturnsChronologicalOrder` | Record 5 frames with increasing timestamps, verify GetFrames returns them in order |
| 4 | `GetFrames_WrapAround_ReturnsChronologicalOrder` | Record 150 frames into capacity-100, verify the returned 100 frames are frames 50-149 in order |
| 5 | `GetFrames_Empty_ReturnsEmptyArray` | Call GetFrames on a fresh session, verify empty array |
| 6 | `RecordFrame_WhenNotRecording_IsIgnored` | Create session without calling Start(), record a frame, verify FrameCount stays 0 |
| 7 | `Start_ResetsState` | Record frames, call Start() again, verify FrameCount resets to 0 |
| 8 | `SetActiveScripts_StoresAndReturns` | Set a list of ActiveScriptEntry, verify ActiveScripts returns them |
| 9 | `RecordConsoleLog_UnderLimit_StoresAll` | Record 10 console logs, verify all 10 are stored |
| 10 | `RecordConsoleLog_OverLimit_CapsAt50` | Record 60 console logs, verify count is capped at 50 |
| 11 | `SetProfilerMarkers_ClearsAndReplaces` | Set markers, then set new markers, verify only new ones are present |
| 12 | `RecordSceneSnapshot_CapsAt100` | Record 110 snapshots, verify count is capped at 100 |
| 13 | `MarkCaptureFrame_TracksFrameNumbers` | Mark frames 10, 20, 30 -- verify DrWarioCaptureFrames contains all three |

### 2.2 DiagnosticReport Grading (~10 tests)

Test class: `DiagnosticReportGradingTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `ComputeGrades_NoFindings_GradeA` | Empty findings list yields HealthScore=100, OverallGrade='A' |
| 2 | `ComputeGrades_OneCritical_ScoreDeducted15` | Single Critical finding yields HealthScore=85, Grade='B' |
| 3 | `ComputeGrades_OneWarning_ScoreDeducted5` | Single Warning finding yields HealthScore=95, Grade='A' |
| 4 | `ComputeGrades_OneInfo_ScoreDeducted1` | Single Info finding yields HealthScore=99, Grade='A' |
| 5 | `ComputeGrades_LowConfidence_ReducedPenalty` | Critical + Low confidence yields penalty of 15*0.25=3.75, so HealthScore=96.25, Grade='A' |
| 6 | `ComputeGrades_MediumConfidence_ReducedPenalty` | Critical + Medium confidence yields penalty of 15*0.6=9, so HealthScore=91, Grade='A' |
| 7 | `ComputeGrades_HighConfidence_FullPenalty` | Critical + High confidence yields penalty of 15, so HealthScore=85, Grade='B' |
| 8 | `ComputeGrades_ManyFindings_ScoreFlooredAtZero` | 10 Critical findings (10*15=150) yields HealthScore=0, Grade='F' |
| 9 | `ComputeGrades_CategoryGrades_ComputedPerCategory` | Two findings in different categories yields independent category grades |
| 10 | `ComputeGrades_GradeBoundaries` | Score exactly at 90/80/70/60 boundaries yields correct grade letters |

**Helper needed:** A factory method to create a `DiagnosticReport` with a given list of `DiagnosticFinding` structs. The report needs a valid `SessionMetadata` (set `Platform`, `UnityVersion` to test strings).

```csharp
static DiagnosticReport MakeReport(params DiagnosticFinding[] findings)
{
    var report = new DiagnosticReport
    {
        GeneratedAt = System.DateTime.UtcNow,
        Session = new SessionMetadata
        {
            Platform = "Test",
            UnityVersion = "2022.3.0f1",
            StartTime = System.DateTime.UtcNow.AddSeconds(-10),
            EndTime = System.DateTime.UtcNow
        }
    };
    report.Findings.AddRange(findings);
    return report;
}

static DiagnosticFinding MakeFinding(
    Severity severity,
    Confidence confidence = Confidence.High,
    string category = "CPU",
    string ruleId = "TEST")
{
    return new DiagnosticFinding
    {
        RuleId = ruleId,
        Category = category,
        Severity = severity,
        Confidence = confidence,
        Title = $"Test {severity}",
        Description = "Test description",
        Recommendation = "Test recommendation",
        FrameIndex = -1
    };
}
```

### 2.3 ReportComparison (~10 tests)

Test class: `ReportComparisonTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Constructor_ComputesOverallGradeDelta` | Report A (score 80) vs B (score 90) yields OverallGradeDelta=10 |
| 2 | `Constructor_ComputesMetricDeltas` | Different AvgCpuTimeMs, P95, GcRate, MemorySlope, DrawCalls between reports |
| 3 | `FindingDiffs_FixedStatus_InANotB` | Finding in A but not B is classified as Fixed |
| 4 | `FindingDiffs_NewStatus_InBNotA` | Finding in B but not A is classified as New |
| 5 | `FindingDiffs_PersistsStatus_InBoth` | Same RuleId+Category in both reports is classified as Persists |
| 6 | `FindingDiffs_Persists_SeverityChange` | Same finding with Warning in A and Critical in B yields SeverityChange=1 |
| 7 | `FindingDiffs_Persists_MetricDelta` | Same finding with Metric=10 in A and Metric=20 in B yields MetricDelta=10 |
| 8 | `CategoryDeltas_NewCategoryInB` | Category only in B gets GradeA='-', GradeB=actual grade |
| 9 | `CategoryDeltas_RemovedCategoryInB` | Category only in A gets GradeA=actual, GradeB='-' |
| 10 | `FindingDiffs_DuplicateRuleIdCategory_FirstOneWins` | Multiple findings with same RuleId+Category in one report, only first is used for matching |

**Key:** Matching uses `RuleId|Category` as the key. Tests must verify this specific matching logic.

### 2.4 LLMResponseParser (~12 tests)

Test class: `LLMResponseParserTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Parse_EmptyString_ReturnsEmpty` | null or "" input returns empty list |
| 2 | `Parse_ValidJsonArray_ReturnsFindings` | Well-formed `[{"ruleId":"AI_1",...}]` parses correctly |
| 3 | `Parse_MarkdownWrapped_StripsFences` | Input wrapped in ` ```json ... ``` ` parses correctly |
| 4 | `Parse_LeadingText_FindsArrayBrackets` | Input like `"Here are findings: [...]"` extracts the array |
| 5 | `Parse_MissingOptionalFields_UsesDefaults` | JSON without scriptPath/assetPath/confidence yields null/null/Medium defaults |
| 6 | `Parse_SeverityCaseInsensitive` | `"severity":"critical"` maps to Severity.Critical |
| 7 | `Parse_ConfidenceMapping` | "high"->High, "low"->Low, "medium"->Medium, null->Medium |
| 8 | `Parse_MalformedJson_ReturnsFallbackFinding` | Unparseable JSON returns single finding with RuleId="AI_PARSE_ERROR" |
| 9 | `Parse_NoArrayBrackets_ReturnsEmpty` | Input with no `[` or `]` returns empty list |
| 10 | `ParseSingle_ValidObject_ReturnsFinding` | Single `{...}` JSON object returns a DiagnosticFinding |
| 11 | `ParseSingle_InvalidObject_ReturnsNull` | Malformed single object returns null |
| 12 | `Parse_MultipleFindings_AllParsed` | Array with 3 finding objects returns list of length 3 |

**Note:** These tests use `UnityEngine.JsonUtility` internally, so they must run in Unity's test runner, but need no scene or Play Mode.

### 2.5 EditorAdjustment (~8 tests)

Test class: `EditorAdjustmentTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `SubtractBaseline_Float_FlooredAtZero` | SubtractBaseline(5f, 10f) returns 0f |
| 2 | `SubtractBaseline_Float_NormalCase` | SubtractBaseline(20f, 5f) returns 15f |
| 3 | `SubtractBaseline_Int_FlooredAtZero` | SubtractBaseline(3, 10) returns 0 |
| 4 | `ClassifyConfidence_NotEditor_AlwaysHigh` | isEditor=false always returns Confidence.High |
| 5 | `ClassifyConfidence_AdjustedWellAbove_High` | adjustedMetric > threshold*1.5 returns High |
| 6 | `ClassifyConfidence_AdjustedAbove_Medium` | threshold < adjustedMetric < threshold*1.5 returns Medium |
| 7 | `ClassifyConfidence_AdjustedBelow_Low` | adjustedMetric < threshold returns Low |
| 8 | `BuildEnvironmentNote_NonEditor_ReturnsNull` | Non-editor metadata returns null |

### 2.6 DiagnosticFinding & Structs (~4 tests)

Test class: `DataStructTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `DiagnosticFinding_DefaultValues` | Default struct has FrameIndex=0 (not -1), Confidence=High (first enum value), null strings |
| 2 | `Severity_EnumOrdering` | Info=0, Warning=1, Critical=2 |
| 3 | `Confidence_EnumOrdering` | High=0, Medium=1, Low=2 |
| 4 | `SceneCensus_DefaultIsInvalid` | Default SceneCensus has IsValid=false |

**Priority 1 subtotal: ~56 tests**

---

## 3. Priority 2: Editor Tests (require Unity Editor context)

These run in Unity Test Framework's **Edit Mode** test runner. They need Unity Editor APIs but not Play Mode.

### 3.1 Individual Analysis Rule Tests (~35 tests)

Each rule can be tested by constructing a synthetic `ProfilingSession`, populating it with crafted `FrameSample` data, and calling `Analyze()`.

**Helper needed:** A `TestSessionBuilder` to create synthetic sessions:

```csharp
class TestSessionBuilder
{
    private ProfilingSession _session;
    private List<FrameSample> _frames = new();

    public TestSessionBuilder(int capacity = 3600)
    {
        _session = new ProfilingSession(capacity);
    }

    public TestSessionBuilder WithMetadata(Action<SessionMetadata> configure)
    {
        // Session.Start() sets metadata from Unity APIs, so for tests
        // we need a way to inject metadata. See note below.
    }

    public TestSessionBuilder AddFrames(int count, Action<int, FrameSample> configure = null)
    {
        for (int i = 0; i < count; i++)
        {
            var sample = new FrameSample { Timestamp = i * 0.0167f, DeltaTime = 0.0167f };
            configure?.Invoke(i, sample);
            _frames.Add(sample);
        }
        return this;
    }

    public TestSessionBuilder AddBootStage(string name, long durationMs, bool success = true)
    {
        _session.RecordBootStage(name, durationMs, success);
        return this;
    }

    public TestSessionBuilder AddAssetLoad(string key, long durationMs, long sizeBytes = 0)
    {
        _session.RecordAssetLoad(key, durationMs, sizeBytes);
        return this;
    }

    public ProfilingSession Build()
    {
        // Must call Start() to enable recording, then record all frames
        _session.Start();
        foreach (var frame in _frames)
            _session.RecordFrame(frame);
        return _session;
    }
}
```

**Problem:** `ProfilingSession.Start()` reads `Application.unityVersion`, `Application.platform`, etc. In Edit Mode tests these return real values, which is fine. But `Metadata.TargetFrameRate` will be -1 unless overridden. Rules read `session.Metadata.TargetFrameRate` to compute target frame time. The `SetEditorContext()` method can set baseline data. For fully controlled tests, consider adding a `ProfilingSession.SetMetadataForTest(SessionMetadata)` internal method, or use reflection to set the Metadata field directly (it is public).

#### GCAllocationRule (~5 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `GCAllocationRule_NoSpikes_NoFindings` | All frames have GcAllocBytes=500 (under 1024), expect no findings |
| 2 | `GCAllocationRule_SpikeAboveThreshold_ReturnsWarning` | 15 of 100 frames have GcAllocBytes=2048, expect Warning finding |
| 3 | `GCAllocationRule_HighSpikeRatio_ReturnsCritical` | 25 of 100 frames (>20%) have spikes, expect Critical |
| 4 | `GCAllocationRule_EditorBaseline_AdjustsThreshold` | Session with IsEditor=true, Baseline.AvgGcAllocBytes=500, spikes at 1200 (under adjusted 1524), expect no findings |
| 5 | `GCAllocationRule_CaptureFramesExcluded` | Frames marked via MarkCaptureFrame() are excluded from spike counting |

#### FrameDropRule (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `FrameDropRule_AllUnderTarget_NoFindings` | All CpuFrameTimeMs=10 at 60fps target, no findings |
| 2 | `FrameDropRule_DropsPresent_ReturnsWithAffectedFrames` | Some frames at 25ms, verify AffectedFrames populated |
| 3 | `FrameDropRule_SevereDrops_CriticalSeverity` | 6+ frames over 50ms yields Critical |
| 4 | `FrameDropRule_EditorConfidence_Adjusted` | Editor session with baseline subtracted affects confidence level |

#### BootStageRule (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `BootStageRule_NoStages_NoFindings` | Empty boot stages returns no findings |
| 2 | `BootStageRule_SlowStage_ReturnsFinding` | Stage with 3000ms yields Warning with ScriptPath set |
| 3 | `BootStageRule_FailedStage_ReturnsCritical` | Stage with Success=false yields BOOT_FAILURE Critical finding |
| 4 | `BootStageRule_TotalBootTimeHigh_ReturnsFinding` | Total boot >8000ms yields TOTAL_BOOT_TIME finding |

#### MemoryLeakRule (~3 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `MemoryLeakRule_FlatHeap_NoFindings` | All frames have same TotalHeapBytes, no findings |
| 2 | `MemoryLeakRule_StrongGrowth_ReturnsCritical` | Heap growing at >5MB/s yields Critical |
| 3 | `MemoryLeakRule_EditorSession_MediumConfidence` | Editor session yields Confidence.Medium and EnvironmentNote set |

#### AssetLoadRule (~3 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `AssetLoadRule_NoLoads_NoFindings` | Empty asset loads returns no findings |
| 2 | `AssetLoadRule_SlowLoad_ReturnsFinding` | Asset at 600ms yields finding with AssetPath populated |
| 3 | `AssetLoadRule_CriticalLoad_CriticalSeverity` | Asset at 2500ms yields Critical severity |

#### NetworkLatencyRule (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `NetworkLatencyRule_NoEvents_NoFindings` | Empty events returns no findings |
| 2 | `NetworkLatencyRule_Errors_ReturnsFinding` | Mix of events with errors yields error rate finding |
| 3 | `NetworkLatencyRule_HighLatency_ReturnsFinding` | Receive events with LatencyMs=150 yields latency finding |
| 4 | `NetworkLatencyRule_HighThroughput_ReturnsFinding` | >100KB/s traffic yields throughput finding |

#### RenderingEfficiencyRule (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `RenderingEfficiency_NoDrawCalls_NoFindings` | All DrawCalls=0, no findings (ProfilerRecorder unavailable) |
| 2 | `RenderingEfficiency_HighDrawCalls_ReturnsFinding` | Avg >1000 draw calls yields DRAW_CALLS finding |
| 3 | `RenderingEfficiency_HighSetPass_ReturnsFinding` | Avg >100 set-pass calls yields SET_PASS_CALLS finding |
| 4 | `RenderingEfficiency_HighTriangles_ReturnsFinding` | Avg >2M triangles yields TRIANGLE_COUNT finding |

#### CPUvsGPUBottleneckRule (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `BottleneckRule_BothUnderTarget_NoFindings` | CPU=10ms, GPU=8ms at 60fps, no findings |
| 2 | `BottleneckRule_GpuBound_ReturnsGPUFinding` | CPU=10ms, GPU=25ms yields "GPU-Bound" |
| 3 | `BottleneckRule_CpuBound_ReturnsCPUFinding` | CPU=25ms, GPU=10ms yields "CPU-Bound" |
| 4 | `BottleneckRule_TooFewFrames_NoFindings` | <30 frames returns no findings |

#### CorrelationEngine (~4 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Detect_GcAndFrameDropOverlap_ReturnsCorrelation` | Findings with overlapping AffectedFrames yields CORR_GC_DROPS |
| 2 | `Detect_LeakAndGc_ReturnsCorrelation` | Both MEMORY_LEAK and GC_SPIKE present yields CORR_LEAK_GC |
| 3 | `Detect_NoFindings_ReturnsEmpty` | Empty findings list returns empty correlations |
| 4 | `Detect_ObjectGrowth_ReturnsChurnCorrelation` | SceneSnapshots showing growth + GC findings yields CORR_OBJECT_CHURN |

### 3.2 AnalysisEngine Tests (~6 tests)

Test class: `AnalysisEngineTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Analyze_RegistersDefaultRules` | New AnalysisEngine has 8 registered rules |
| 2 | `Analyze_RunsAllRules` | Analyze a session with known issues, verify findings from multiple rules |
| 3 | `Analyze_ComputesSummaryMetrics` | Verify AvgCpuTimeMs, P95, P99, MemorySlope computed from frames |
| 4 | `Analyze_ProducesSynthesis` | Report has non-null Synthesis with ExecutiveSummary and PrioritizedActions |
| 5 | `RegisterRule_CustomRuleIncluded` | Register a mock IAnalysisRule, verify its findings appear in report |
| 6 | `Analyze_AllRulesDisabled_InfoFinding` | When RuleConfig disables all rules, yields SYS_NO_RULES info finding |

**Note:** Test #6 requires mocking `RuleConfig.IsEnabled()` or using EditorPrefs to temporarily disable rules and restoring after the test.

### 3.3 ReportSynthesizer Tests (~5 tests)

Test class: `ReportSynthesizerTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Synthesize_NoFindings_HealthySummary` | Grade A report yields "excellent shape" in ExecutiveSummary |
| 2 | `Synthesize_CriticalFindings_PrioritizedActions` | Report with Critical findings yields non-empty PrioritizedActions |
| 3 | `Synthesize_CorrelationDriven_ActionsFirst` | Correlations yield actions with lower Priority numbers than standalone findings |
| 4 | `Synthesize_BottleneckSummary_WithCorrelation` | GPU bottleneck + CORR_GPU_TRIS yields "driven by high geometry complexity" |
| 5 | `Synthesize_DuplicateRuleIds_CoveredOnce` | Same ruleId in correlation and standalone finding doesn't produce duplicate actions |

### 3.4 LLMPromptBuilder Tests (~10 tests)

Test class: `LLMPromptBuilderTests`

These tests construct a `ProfilingSession` with known data and verify the prompt string contains expected sections.

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `BuildSystemPrompt_WithoutAdditionalContext_BaseOnly` | AdditionalContext=null yields base prompt without "Additional project context" |
| 2 | `BuildSystemPrompt_WithAdditionalContext_Appended` | AdditionalContext="HF context" yields prompt containing "HF context" |
| 3 | `BuildUserPrompt_ContainsSessionSection` | Output contains `"session":` with startTime, platform, etc. |
| 4 | `BuildUserPrompt_ContainsFrameSummary` | Output contains `"frameSummary":` with cpuFrameTime, gcAllocation sections |
| 5 | `BuildUserPrompt_ContainsMemoryTrajectory` | Session with >10 frames yields `"memoryTrajectory":` with samples array |
| 6 | `BuildUserPrompt_ContainsProfilerMarkers` | Session with markers yields `"profilerMarkers": [` with marker names |
| 7 | `BuildUserPrompt_ContainsActiveScripts` | Session with ActiveScripts yields `"activeScripts": [` |
| 8 | `BuildUserPrompt_ContainsConsoleLogs` | Session with ConsoleLogs yields `"consoleLogs": [` |
| 9 | `BuildUserPrompt_ContainsSceneCensus` | Session with valid SceneCensus yields `"sceneCensus":` |
| 10 | `BuildUserPrompt_EmptySession_NullSections` | Session with 0 frames yields `"frameSummary": null` |

#### MCP Workflow Prompt Tests (~5 tests)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `BuildMcpSuspectCheckPrompt_ContainsActiveScripts` | Output contains "ACTIVE SCRIPTS" section |
| 2 | `BuildMcpSuspectCheckPrompt_ContainsFindings` | Output contains "DRWARIO FINDINGS" section |
| 3 | `BuildReportCorrectionPrompt_ContainsAuditInstructions` | Output contains "AUDIT and CORRECT" |
| 4 | `BuildMcpReportCorrectionPrompt_ContainsTwoPhases` | Output contains "PHASE 1" and "PHASE 2" |
| 5 | `BuildProfilerMarkersSection_EmptyMarkers_ReturnsEmpty` | No markers yields empty string |

### 3.5 SceneCensusCapture Tests (~3 tests)

Test class: `SceneCensusCaptureTests` (Edit Mode, creates temporary scene objects)

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `Capture_EmptyScene_ReturnsValidCensus` | Empty scene yields IsValid=true, TotalGameObjects reflects only default objects |
| 2 | `Capture_WithObjects_CountsCorrectly` | Create 5 GameObjects with Lights, verify counts |
| 3 | `CaptureActiveScripts_FiltersDrWarioScripts` | MonoBehaviours named "DrWario*" and Unity internals are excluded |

**Cleanup:** Each test must destroy created GameObjects in TearDown to avoid polluting other tests.

### 3.6 EditorBaselineCapture Tests (~2 tests)

Test class: `EditorBaselineCaptureTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `LastBaseline_AfterCapture_IsValid` | After a capture cycle completes, LastBaseline.IsValid is true |
| 2 | `LastBaseline_HasReasonableValues` | SampleCount > 0, AvgCpuFrameTimeMs > 0 |

**Note:** This requires waiting for the 33 update ticks (3 warmup + 30 samples) to complete. Use `EditorApplication.update` or a coroutine-based test approach.

**Priority 2 subtotal: ~56 tests**

---

## 4. Priority 3: Integration Tests

These are longer-running tests that exercise multiple systems end-to-end.

### 4.1 Full Analysis Pipeline (~5 tests)

Test class: `AnalysisPipelineIntegrationTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `FullPipeline_SyntheticBadSession_GradeF` | Session with extreme GC spikes, frame drops, memory leak yields grade F with findings from multiple rules |
| 2 | `FullPipeline_SyntheticGoodSession_GradeA` | Session with all metrics under thresholds yields grade A, minimal findings |
| 3 | `FullPipeline_GenerateReport_ThenCompare` | Generate two reports, create ReportComparison, verify Fixed/New/Persists classification |
| 4 | `FullPipeline_ExportJson_ValidStructure` | Generate report, call ExportJson(), verify parseable JSON with expected fields |
| 5 | `FullPipeline_ExportText_ContainsAllSections` | Generate report, call ExportText(), verify contains grade, findings, synthesis sections |

### 4.2 LLM Prompt Round-Trip (~4 tests)

Test class: `LLMRoundTripTests`

These test the prompt-to-parse cycle without actual LLM calls.

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `BuildPrompt_ParseMockResponse_FindingsPreserved` | Build prompt from session, parse a hardcoded JSON response, verify findings have correct fields |
| 2 | `ParseMockResponse_WithScriptPath_PreservedInFinding` | Mock response includes scriptPath and assetPath, verify they survive parsing |
| 3 | `ParseMockResponse_WithConfidence_PreservedInFinding` | Mock response includes confidence, verify it maps correctly |
| 4 | `BuildPrompt_SpecialCharacters_JsonEscaped` | Session with asset names containing quotes/backslashes, verify prompt has escaped JSON |

### 4.3 Report Comparison Workflow (~4 tests)

Test class: `ReportComparisonIntegrationTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `CompareReports_ImprovementScenario` | Report B has fewer findings than A, verify positive HealthScoreDelta and Fixed diffs |
| 2 | `CompareReports_RegressionScenario` | Report B has more findings, verify negative delta and New diffs |
| 3 | `CompareReports_SameFinding_WorseMetric` | Same finding persists but metric is worse, verify MetricDelta is positive |
| 4 | `CompareReports_EmptyReports` | Two reports with no findings, verify empty FindingDiffs and zero deltas |

### 4.4 CorrelationEngine + Synthesizer End-to-End (~4 tests)

Test class: `CorrelationSynthesisIntegrationTests`

| # | Method Name | Description |
|---|-------------|-------------|
| 1 | `GcAndFrameDrops_ProducesCorrelation_AndSynthesisAction` | Session with GC spikes on frame drop frames yields CORR_GC_DROPS correlation and "Eliminate per-frame GC allocations" action |
| 2 | `LeakAndGc_ProducesHighPriorityAction` | Memory leak + GC spikes yields Priority=1 action about fixing the leak |
| 3 | `GpuBound_HighTriangles_ProducesGeometryAction` | GPU-bound + high triangles yields "Reduce geometry complexity" action |
| 4 | `PervasiveGc_ProducesPoolingAction` | >80% frames with GC allocations yields "object pooling" action |

**Priority 3 subtotal: ~17 tests**

---

## 5. Test Infrastructure Recommendations

### 5.1 Directory Structure

```
d:\ware\drwario\
├── Tests/
│   ├── Editor/
│   │   ├── DrWario.Editor.Tests.asmdef
│   │   ├── Helpers/
│   │   │   ├── TestSessionBuilder.cs
│   │   │   ├── TestReportFactory.cs
│   │   │   └── TestDataFixtures.cs
│   │   ├── Runtime/
│   │   │   ├── ProfilingSessionTests.cs
│   │   │   ├── DataStructTests.cs
│   │   │   └── FrameSampleTests.cs
│   │   ├── Analysis/
│   │   │   ├── DiagnosticReportGradingTests.cs
│   │   │   ├── ReportComparisonTests.cs
│   │   │   ├── EditorAdjustmentTests.cs
│   │   │   ├── AnalysisEngineTests.cs
│   │   │   ├── CorrelationEngineTests.cs
│   │   │   ├── ReportSynthesizerTests.cs
│   │   │   └── Rules/
│   │   │       ├── GCAllocationRuleTests.cs
│   │   │       ├── FrameDropRuleTests.cs
│   │   │       ├── BootStageRuleTests.cs
│   │   │       ├── MemoryLeakRuleTests.cs
│   │   │       ├── AssetLoadRuleTests.cs
│   │   │       ├── NetworkLatencyRuleTests.cs
│   │   │       ├── RenderingEfficiencyRuleTests.cs
│   │   │       └── CPUvsGPUBottleneckRuleTests.cs
│   │   ├── LLM/
│   │   │   ├── LLMResponseParserTests.cs
│   │   │   ├── LLMPromptBuilderTests.cs
│   │   │   └── LLMRoundTripTests.cs
│   │   ├── Integration/
│   │   │   ├── AnalysisPipelineIntegrationTests.cs
│   │   │   ├── ReportComparisonIntegrationTests.cs
│   │   │   └── CorrelationSynthesisIntegrationTests.cs
│   │   └── Editor/
│   │       ├── SceneCensusCaptureTests.cs
│   │       └── EditorBaselineCaptureTests.cs
│   └── Runtime/
│       └── (empty for now — Play Mode tests would go here for RuntimeCollector)
```

### 5.2 Assembly Definition (DrWario.Editor.Tests.asmdef)

```json
{
    "name": "DrWario.Editor.Tests",
    "rootNamespace": "DrWario.Tests",
    "references": [
        "DrWario.Runtime",
        "DrWario.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Important:** The `overrideReferences: true` and `precompiledReferences: ["nunit.framework.dll"]` are required for NUnit attributes to resolve. The `defineConstraints: ["UNITY_INCLUDE_TESTS"]` ensures tests are stripped from non-test builds.

Also register in `package.json`:

```json
{
    "testables": ["com.jlabarca.drwario"]
}
```

Wait -- since this is a UPM package, the `testables` entry goes in the **consuming project's** `manifest.json`, not in the package's `package.json`. However, the package itself should declare test assemblies properly. In `package.json`, add:

```json
{
    "samples": [],
    "relatedPackages": {},
    "testAssemblies": ["DrWario.Editor.Tests"]
}
```

### 5.3 TestSessionBuilder (core helper)

This is the most important helper. Every rule test and integration test needs synthetic `ProfilingSession` data.

```csharp
using System;
using System.Collections.Generic;
using DrWario.Runtime;

namespace DrWario.Tests
{
    /// <summary>
    /// Fluent builder for creating synthetic ProfilingSession instances for testing.
    /// Bypasses Unity API calls in Start() by setting metadata directly.
    /// </summary>
    public class TestSessionBuilder
    {
        private int _capacity = 3600;
        private SessionMetadata _metadata;
        private SceneCensus _census;
        private readonly List<FrameSample> _frames = new();
        private readonly List<(string name, long ms, bool success)> _bootStages = new();
        private readonly List<(string key, long ms, long bytes)> _assetLoads = new();
        private readonly List<(NetworkEventType type, int bytes, float latencyMs)> _networkEvents = new();
        private List<ActiveScriptEntry> _activeScripts;
        private List<ConsoleLogEntry> _consoleLogs;
        private List<ProfilerMarkerSample> _markers;
        private readonly List<SceneSnapshotDiff> _snapshots = new();
        private readonly HashSet<int> _captureFrames = new();

        public TestSessionBuilder()
        {
            _metadata = new SessionMetadata
            {
                StartTime = DateTime.UtcNow.AddSeconds(-10),
                EndTime = DateTime.UtcNow,
                UnityVersion = "2022.3.0f1",
                Platform = "WindowsEditor",
                TargetFrameRate = 60,
                ScreenWidth = 1920,
                ScreenHeight = 1080,
                IsEditor = false,
                IsDevelopmentBuild = false
            };
        }

        public TestSessionBuilder WithCapacity(int c) { _capacity = c; return this; }

        public TestSessionBuilder WithTargetFps(int fps)
        {
            _metadata.TargetFrameRate = fps;
            return this;
        }

        public TestSessionBuilder AsEditorSession(EditorBaseline baseline = default)
        {
            _metadata.IsEditor = true;
            _metadata.Baseline = baseline;
            return this;
        }

        public TestSessionBuilder WithSceneCensus(SceneCensus census)
        {
            _census = census;
            return this;
        }

        /// <summary>
        /// Add N identical frames with given CPU time and GC alloc.
        /// </summary>
        public TestSessionBuilder AddFrames(int count,
            float cpuMs = 10f, long gcAllocBytes = 0, long heapBytes = 100_000_000,
            float gpuMs = 0f, int drawCalls = 0, int triangles = 0,
            float renderThreadMs = 0f, int batches = 0, int setPassCalls = 0,
            int physicsActiveBodies = 0, int physicsContacts = 0,
            int audioVoiceCount = 0, int animatorCount = 0,
            int uiCanvasRebuilds = 0, int uiLayoutRebuilds = 0)
        {
            int startIdx = _frames.Count;
            for (int i = 0; i < count; i++)
            {
                _frames.Add(new FrameSample
                {
                    Timestamp = (startIdx + i) * 0.0167f,
                    DeltaTime = 0.0167f,
                    CpuFrameTimeMs = cpuMs,
                    GpuFrameTimeMs = gpuMs,
                    RenderThreadMs = renderThreadMs,
                    GcAllocBytes = gcAllocBytes,
                    TotalHeapBytes = heapBytes,
                    DrawCalls = drawCalls,
                    Triangles = triangles,
                    Batches = batches,
                    SetPassCalls = setPassCalls,
                    PhysicsActiveBodies = physicsActiveBodies,
                    PhysicsContacts = physicsContacts,
                    AudioVoiceCount = audioVoiceCount,
                    AnimatorCount = animatorCount,
                    UICanvasRebuilds = uiCanvasRebuilds,
                    UILayoutRebuilds = uiLayoutRebuilds,
                    FrameNumber = startIdx + i,
                });
            }
            return this;
        }

        /// <summary>
        /// Add a single frame with custom configuration.
        /// </summary>
        public TestSessionBuilder AddFrame(FrameSample sample)
        {
            _frames.Add(sample);
            return this;
        }

        /// <summary>
        /// Add frames with linearly growing heap (for memory leak tests).
        /// </summary>
        public TestSessionBuilder AddFramesWithGrowingHeap(
            int count, long startHeap, long growthPerFrame, float cpuMs = 10f)
        {
            int startIdx = _frames.Count;
            for (int i = 0; i < count; i++)
            {
                _frames.Add(new FrameSample
                {
                    Timestamp = (startIdx + i) * 0.0167f,
                    DeltaTime = 0.0167f,
                    CpuFrameTimeMs = cpuMs,
                    TotalHeapBytes = startHeap + (long)i * growthPerFrame,
                    FrameNumber = startIdx + i,
                });
            }
            return this;
        }

        public TestSessionBuilder AddBootStage(string name, long durationMs, bool success = true)
        {
            _bootStages.Add((name, durationMs, success));
            return this;
        }

        public TestSessionBuilder AddAssetLoad(string key, long durationMs, long sizeBytes = 0)
        {
            _assetLoads.Add((key, durationMs, sizeBytes));
            return this;
        }

        public TestSessionBuilder AddNetworkEvent(NetworkEventType type, int bytes, float latencyMs = 0)
        {
            _networkEvents.Add((type, bytes, latencyMs));
            return this;
        }

        public TestSessionBuilder WithActiveScripts(List<ActiveScriptEntry> scripts)
        {
            _activeScripts = scripts;
            return this;
        }

        public TestSessionBuilder WithConsoleLogs(List<ConsoleLogEntry> logs)
        {
            _consoleLogs = logs;
            return this;
        }

        public TestSessionBuilder WithProfilerMarkers(List<ProfilerMarkerSample> markers)
        {
            _markers = markers;
            return this;
        }

        public TestSessionBuilder AddSceneSnapshot(SceneSnapshotDiff snapshot)
        {
            _snapshots.Add(snapshot);
            return this;
        }

        public TestSessionBuilder MarkCaptureFrame(int frameNumber)
        {
            _captureFrames.Add(frameNumber);
            return this;
        }

        public ProfilingSession Build()
        {
            var session = new ProfilingSession(_capacity);
            session.Start();

            // Override metadata (Start() sets it from Unity APIs)
            session.Metadata = _metadata;
            session.SceneCensus = _census;

            foreach (var frame in _frames)
                session.RecordFrame(frame);

            foreach (var (name, ms, success) in _bootStages)
                session.RecordBootStage(name, ms, success);

            foreach (var (key, ms, bytes) in _assetLoads)
                session.RecordAssetLoad(key, ms, bytes);

            // NetworkEvent recording requires Time.realtimeSinceStartup which
            // won't work in tests. Use reflection or add a test-friendly overload.
            // For now, we skip network events in pure unit tests or add a
            // RecordNetworkEventRaw method.

            if (_activeScripts != null)
                session.SetActiveScripts(_activeScripts);

            if (_consoleLogs != null)
                foreach (var log in _consoleLogs)
                    session.RecordConsoleLog(log);

            if (_markers != null)
                session.SetProfilerMarkers(_markers);

            foreach (var snap in _snapshots)
                session.RecordSceneSnapshot(snap);

            foreach (var fn in _captureFrames)
                session.MarkCaptureFrame(fn);

            return session;
        }
    }
}
```

### 5.4 Mock LLM Responses

For `LLMResponseParser` tests, use hardcoded JSON strings:

```csharp
static class MockLLMResponses
{
    public const string ValidSingleFinding = @"[
        {
            ""ruleId"": ""AI_GC_PATTERN"",
            ""category"": ""Memory"",
            ""severity"": ""Warning"",
            ""title"": ""String concatenation in Update"",
            ""description"": ""Detected string + operator in hot path"",
            ""recommendation"": ""Use StringBuilder or cached strings"",
            ""metric"": 2048,
            ""threshold"": 1024,
            ""scriptPath"": ""Assets/Scripts/Player.cs"",
            ""scriptLine"": 42,
            ""assetPath"": null,
            ""confidence"": ""High""
        }
    ]";

    public const string MarkdownWrapped = "```json\n" + ValidSingleFinding + "\n```";

    public const string MalformedJson = @"{ this is not valid json [}";

    public const string EmptyArray = "[]";

    public const string WithMissingFields = @"[
        {
            ""ruleId"": ""AI_MINIMAL"",
            ""category"": ""CPU"",
            ""severity"": ""Info"",
            ""title"": ""Minimal finding"",
            ""description"": ""desc"",
            ""recommendation"": ""rec"",
            ""metric"": 5,
            ""threshold"": 10
        }
    ]";
}
```

### 5.5 Testing Without LLM Calls

- `LLMResponseParser` is fully testable without network calls -- it just parses strings.
- `LLMPromptBuilder` is fully testable -- it just builds strings from session data.
- `LLMClient` should NOT be unit tested (it makes HTTP requests). Instead, test it via integration tests with a mock server, or skip it.
- `AIAnalysisRule` calls `LLMClient` internally. To test it, either:
  - Mock `LLMClient` by extracting an interface (future refactor)
  - Test only the `LLMResponseParser` and `LLMPromptBuilder` in isolation
  - Use a `DRWARIO_TEST_LLM_RESPONSE` EditorPrefs key to inject a mock response (feature suggestion)

### 5.6 Network Event Testing Workaround

`ProfilingSession.RecordNetworkEvent()` calls `UnityEngine.Time.realtimeSinceStartup` for the timestamp. In Edit Mode tests, this returns the editor's real time, which is fine for most tests. However, the timestamps won't be controlled. Options:

1. Accept that timestamps are editor-time (sufficient for most tests)
2. Add an internal overload: `RecordNetworkEventRaw(NetworkEvent evt)` that takes a pre-built struct
3. Use reflection to add events directly to `_networkEvents`

Recommendation: Option 1 is simplest and sufficient for testing NetworkLatencyRule, since the rule only reads `LatencyMs` and `Type` fields.

---

## 6. Specific Test Cases for v3.0 Changes

### 6.1 SceneCensus & ActiveScriptEntry (new structs)

| # | Test | Description |
|---|------|-------------|
| 1 | `SceneCensus_ComponentDistribution_LimitedTo20` | Create census with 30 component types, verify ComponentDistribution has 20 entries sorted by count |
| 2 | `ActiveScriptEntry_SampleGameObjectNames_LimitedTo3` | Create entry with 5 sample names, verify builder caps at 3 |
| 3 | `ConsoleLogEntry_AllFieldsPopulated` | Create entry with all fields, verify nothing is lost |
| 4 | `ProfilingSession_SetActiveScripts_NullSafe` | SetActiveScripts(null) does not throw |
| 5 | `ProfilingSession_RecordConsoleLog_NullListInitialized` | First RecordConsoleLog call initializes the internal list |

### 6.2 Confidence-Adjusted Grading (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `Grade_AllLowConfidence_MinimalImpact` | 5 Critical Low-confidence findings: penalty = 5 * 3.75 = 18.75, score = 81.25, grade B |
| 2 | `Grade_MixedConfidence_CorrectPenalties` | Mix of High/Medium/Low confidence findings yields expected score |
| 3 | `Grade_CategoryGrades_UseConfidenceAdjustment` | Per-category grades also apply confidence multipliers |

### 6.3 ReportComparison (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `Comparison_MetricDeltas_AllFields` | Verify AvgCpuTimeDelta, P95CpuTimeDelta, GcRateDelta, MemorySlopeDelta, DrawCallsDelta, HealthScoreDelta |
| 2 | `Comparison_GradeToScore_Mapping` | 'A'->95, 'B'->85, 'C'->75, 'D'->65, 'F'->50, '-'->0 |
| 3 | `Comparison_FindingKey_RuleIdPipeCategory` | Finding with RuleId="GC_SPIKE" Category="Memory" uses key "GC_SPIKE|Memory" |
| 4 | `Comparison_SeverityChange_Calculation` | Finding changes from Warning(1) to Critical(2), SeverityChange = 2-1 = 1 |

### 6.4 Extended FrameSample Fields (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `FrameSample_RenderingCounters_RecordedInSession` | Frames with DrawCalls/Batches/SetPassCalls/Triangles/Vertices stored and retrieved via GetFrames() |
| 2 | `FrameSample_PhysicsCounters_RecordedInSession` | PhysicsActiveBodies/KinematicBodies/Contacts stored correctly |
| 3 | `FrameSample_AudioUIAnimationCounters_Stored` | AudioVoiceCount/AudioDSPLoad/AnimatorCount/UICanvasRebuilds/UILayoutRebuilds stored correctly |
| 4 | `RenderingEfficiencyRule_UsesExtendedFields` | Rule reads DrawCalls, Batches, SetPassCalls, Triangles from FrameSample |

### 6.5 ProfilerMarkers (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `SetProfilerMarkers_StoresMarkerData` | Set 5 markers, verify ProfilerMarkers returns all 5 with correct fields |
| 2 | `SetProfilerMarkers_ClearsPrevious` | Set markers twice, verify only second set persists |
| 3 | `BuildProfilerMarkersSection_FormatsCorrectly` | Verify output contains marker names and timing in expected format |
| 4 | `BuildUserPrompt_IncludesProfilerMarkers` | Session with markers yields "profilerMarkers" section in JSON prompt |

### 6.6 CaptureFrame Exclusion (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `MarkCaptureFrame_ExcludedFromGCRule` | Frame 50 is marked, frame 50 has GcAllocBytes=10000, GCAllocationRule does not count it as a spike |
| 2 | `MarkCaptureFrame_NonMarkedFrames_StillCounted` | Frames not marked are still counted as spikes |
| 3 | `DrWarioCaptureFrames_EmptyByDefault` | New session has empty DrWarioCaptureFrames set |

### 6.7 CorrelationEngine (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `DetectGcFrameDropCorrelation_HighOverlap_Critical` | >60% overlap yields Critical severity |
| 2 | `DetectObjectGrowthCorrelation_ChurnWithGc_Detected` | Growing object count + GC findings yields CORR_OBJECT_CHURN |
| 3 | `DetectPervasiveGcPattern_Over80Percent_Detected` | >80% frames with GC allocations yields CORR_PERVASIVE_GC |
| 4 | `DetectGpuGeometry_OnlyCpuBound_NotDetected` | CPU-bound bottleneck + high triangles does NOT trigger CORR_GPU_TRIS |

### 6.8 MCP Workflow Prompts (new in v3.0)

| # | Test | Description |
|---|------|-------------|
| 1 | `McpSuspectCheck_NoActiveScripts_SkipsSection` | No ActiveScripts yields prompt without "ACTIVE SCRIPTS" header |
| 2 | `McpSuspectCheck_WithConsoleLogs_IncludesErrors` | Console logs present yields "CONSOLE ERRORS/WARNINGS" section |
| 3 | `ReportCorrection_NumberedFindings` | Findings are numbered (#1, #2, etc.) in the prompt |
| 4 | `McpReportCorrection_IncludesProfilingSummary` | Prompt contains profiling data section when session has frames |
| 5 | `FullClipboardPrompt_ContainsAllSections` | BuildFullPromptForClipboard produces SYSTEM CONTEXT, PROFILING DATA, ANALYSIS REPORT, QUESTION sections |

---

## 7. Implementation Order

Start with the infrastructure, then work through priorities:

1. **Create test directories and asmdef** (30 min)
2. **Implement TestSessionBuilder** (1 hour) -- this unblocks everything
3. **Priority 1 pure tests** (estimated 4-6 hours):
   - `ProfilingSessionTests` -- foundational, proves ring buffer works
   - `DiagnosticReportGradingTests` -- validates the confidence-adjusted scoring
   - `ReportComparisonTests` -- validates the new comparison feature
   - `LLMResponseParserTests` -- validates parsing with mock data
   - `EditorAdjustmentTests` -- validates the baseline math
   - `DataStructTests` -- quick sanity checks
4. **Priority 2 rule tests** (estimated 3-5 hours):
   - Start with `GCAllocationRuleTests` (exercises capture frame exclusion + baseline adjustment)
   - Then `FrameDropRuleTests`, `RenderingEfficiencyRuleTests`, `CPUvsGPUBottleneckRuleTests`
   - Then remaining rules
   - Then `CorrelationEngineTests`, `ReportSynthesizerTests`
   - Then `LLMPromptBuilderTests`
5. **Priority 3 integration tests** (estimated 2-3 hours)

**Total estimated implementation time: 10-15 hours**

---

## 8. Key Testability Concerns

### ProfilingSession.Start() calls Unity APIs

`Start()` reads `Application.unityVersion`, `Application.platform`, `Screen.width`, etc. In Edit Mode tests these return real editor values, which is acceptable. The `Metadata` field is public and can be overwritten after `Start()` for controlled tests.

### RuleConfig uses EditorPrefs

`AnalysisEngine.Analyze()` calls `RuleConfig.IsEnabled(ruleId)` which reads EditorPrefs. For tests:
- Either ensure all rules are enabled (the default)
- Or wrap test code with `RuleConfig.SetEnabled(ruleId, true)` and clean up in TearDown
- Or test rules directly by calling `rule.Analyze(session)` which bypasses RuleConfig

**Recommendation:** Test rules directly via `rule.Analyze(session)` for unit tests. Test the full `AnalysisEngine.Analyze()` only in integration tests.

### CorrelationEngine and ReportSynthesizer have #if UNITY_EDITOR

These classes are wrapped in `#if UNITY_EDITOR`. Since all tests run in the Editor test runner, this is not a problem. However, it means these tests can never run outside Unity (e.g., in a standalone NUnit runner).

### RecordNetworkEvent uses Time.realtimeSinceStartup

As discussed in section 5.6, timestamps will be editor-time. This is fine for testing the NetworkLatencyRule since it reads `LatencyMs` and event `Type`, not raw timestamps. The throughput calculation uses event timestamps for duration, which will be close to zero in fast tests. To test throughput findings, either:
- Add a small delay between events (not recommended)
- Add events with manually set timestamps via reflection
- Accept that throughput tests may need a test-specific code path
