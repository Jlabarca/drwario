# DrWario Test Suite

**Status:** 178 tests (P1 + P2 + P3 complete)
**Runner:** Unity Test Framework, Edit Mode
**Assembly:** `DrWario.Editor.Tests`

## Quick Start

1. Open HybridFrame project in Unity
2. Ensure `Packages/manifest.json` has `"testables": ["com.jlabarca.drwario"]`
3. Window > General > Test Runner > EditMode > Run All

## Test Structure

```
Tests/Editor/
├── DrWario.Editor.Tests.asmdef
├── Helpers/
│   ├── TestSessionBuilder.cs      ← Fluent builder for synthetic ProfilingSessions
│   └── TestReportFactory.cs       ← Factory methods for DiagnosticReport/Finding
├── Runtime/
│   ├── ProfilingSessionTests.cs   ← Ring buffer, recording, storage (17 tests)
│   └── DataStructTests.cs         ← Enum ordering, struct defaults (7 tests)
├── Analysis/
│   ├── DiagnosticReportGradingTests.cs  ← Health score, grades, confidence (11 tests)
│   ├── ReportComparisonTests.cs         ← Finding diffs, metric deltas (10 tests)
│   ├── EditorAdjustmentTests.cs         ← Baseline subtraction, confidence (12 tests)
│   ├── AnalysisEngineTests.cs           ← Pipeline orchestration, rule registration (8 tests)
│   ├── CorrelationEngineTests.cs        ← Cross-finding correlation detection (10 tests)
│   ├── ReportSynthesizerTests.cs        ← Executive summary, prioritized actions (6 tests)
│   └── Rules/
│       ├── GCAllocationRuleTests.cs         ← GC spikes, severity tiers (10 tests)
│       ├── FrameDropRuleTests.cs            ← Frame drops, P95, 30fps (9 tests)
│       ├── BootStageRuleTests.cs            ← Slow/failed stages, total boot (8 tests)
│       ├── MemoryLeakRuleTests.cs           ← Heap growth regression (7 tests)
│       ├── AssetLoadRuleTests.cs            ← Slow asset loads (6 tests)
│       ├── NetworkLatencyRuleTests.cs       ← Errors, latency, throughput (8 tests)
│       ├── RenderingEfficiencyRuleTests.cs  ← Draw calls, set-pass, triangles (10 tests)
│       └── CPUvsGPUBottleneckRuleTests.cs   ← Bottleneck detection (9 tests)
├── LLM/
│   ├── LLMResponseParserTests.cs        ← JSON parsing, markdown stripping (13 tests)
│   ├── LLMPromptBuilderTests.cs         ← System/user prompt, all sections (22 tests)
│   └── LLMPromptBuilderMcpTests.cs      ← MCP suspect check, correction prompts (10 tests)
└── Integration/
    └── PipelineIntegrationTests.cs      ← Full pipeline, comparison, prompt gen (17 tests)
```

## Test Categories

### P1: Pure Unit Tests (70 tests — DONE)

No Play Mode, no scene, no MonoBehaviours. Tests pure C# logic.

| Test Class | Count | What It Covers |
|-----------|-------|---------------|
| `ProfilingSessionTests` | 17 | Ring buffer wrap-around, frame recording, boot stages, asset loads, console logs, profiler markers, scene snapshots, capture frames |
| `DiagnosticReportGradingTests` | 11 | Score formula (100 - penalties), confidence multipliers (Low=0.25x, Medium=0.6x, High=1x), grade boundaries (A≥90, B≥80, C≥70, D≥60, F<60), per-category grades |
| `ReportComparisonTests` | 10 | Finding diff status (Fixed/New/Persists), severity change, metric delta, category deltas, duplicate key handling |
| `LLMResponseParserTests` | 13 | Valid/empty/malformed JSON, markdown fence stripping, leading text extraction, severity/confidence case mapping, single object parse, environment note |
| `EditorAdjustmentTests` | 12 | Float/long/int baseline subtraction floored at 0, confidence classification (High/Medium/Low), environment note generation with window state |
| `DataStructTests` | 7 | DiagnosticFinding defaults, Severity/Confidence/NetworkEventType/SnapshotTrigger enum ordering, EditorBaseline/SceneCensus default validity |

### P2: Rule & Component Tests (91 tests — DONE)

Individual analysis rules, AnalysisEngine pipeline, CorrelationEngine, ReportSynthesizer.

| Test Class | Count | What It Covers |
|-----------|-------|---------------|
| `GCAllocationRuleTests` | 10 | No spikes, empty session, info/warning/critical severity, editor baseline, capture frame exclusion, affected frames, worst frame |
| `FrameDropRuleTests` | 9 | All under target, empty, info/warning/critical severity, 30fps target, affected frames, P95 metric, editor confidence |
| `BootStageRuleTests` | 8 | No stages, fast stages, slow (warning/critical), failed stage, total boot warning/critical, combined slow+failed |
| `MemoryLeakRuleTests` | 7 | Flat heap, too few frames, slow growth below threshold, moderate/rapid growth, editor medium confidence, standalone high confidence |
| `AssetLoadRuleTests` | 6 | No loads, fast loads, one slow (info), many slow (warning), critical load, slowest asset tracking |
| `NetworkLatencyRuleTests` | 8 | No events, healthy network, with errors, high/low error rate, high/critical latency, throughput |
| `RenderingEfficiencyRuleTests` | 10 | No profiler data, low/high/critical draw calls, high/critical set-pass, high/critical triangles, editor baseline subtraction, editor triangle confidence |
| `CPUvsGPUBottleneckRuleTests` | 9 | Too few frames, both under, no GPU data, GPU/CPU bound, both overloaded, severe GPU critical, moderate warning, editor env note |
| `AnalysisEngineTests` | 8 | Default rules count (8), custom rule registration, empty session, frame drop finding, summary metrics, synthesis production, custom rule in report, grades computed |
| `CorrelationEngineTests` | 10 | No/null findings, GC+frame drop overlap/no-overlap, leak+GC, GPU+triangles, CPU+draw calls, boot+assets, pervasive GC |
| `ReportSynthesizerTests` | 6 | Healthy summary, critical actions, correlation-driven priority, bottleneck with correlation, warnings-only medium impact, correlations stored |

### P2b: LLM Prompt Builder Tests (32 tests — DONE)

Prompt construction for all API surfaces.

| Test Class | Count | What It Covers |
|-----------|-------|---------------|
| `LLMPromptBuilderTests` | 22 | System prompt with/without additional context, Ask Doctor prompt, user prompt sections (session metadata, frame summary, memory trajectory, boot pipeline, asset loads, profiler markers, active scripts with namespaces, console logs, pre-analysis, environment/editor context, GPU elision, scene census trivial component filtering), profiler markers section, clipboard prompt, null handling |
| `LLMPromptBuilderMcpTests` | 10 | MCP suspect check (active scripts, findings, profiling summary, console logs), report correction (audit instructions, profiling data, numbered findings), MCP correction (two phases, report to verify, null report handling) |

### P3: Integration Tests (17 tests — DONE)

Full pipeline flows exercising multiple components together.

| Test Class | Count | What It Covers |
|-----------|-------|---------------|
| `PipelineIntegrationTests` | 17 | GC+frame drop correlation pipeline, memory leak+GC correlation, healthy session grade A, severely degraded grade F, multi-category grading, report comparison (improved metrics, fixed/new findings, same-session no delta), prompt generation from engine output (user prompt, MCP suspect check, clipboard, report correction), full pipeline with all data sources, editor session context, synthesis structure verification |

## Key Test Helpers

### TestSessionBuilder

Fluent builder that creates `ProfilingSession` instances with controlled data. Calls `Start()` internally and overrides `Metadata` to avoid Unity API dependencies.

```csharp
var session = new TestSessionBuilder()
    .WithCapacity(100)
    .WithTargetFps(60)
    .AddFrames(80, cpuMs: 10f, gcAllocBytes: 500)
    .AddFrames(20, cpuMs: 30f, gcAllocBytes: 5000)  // spike frames
    .AddBootStage("Init", 100)
    .AddAssetLoad("big_texture.png", 600)
    .MarkCaptureFrame(50)
    .Build();
```

Key methods:
- `AddFrames(count, cpuMs, gcAllocBytes, heapBytes, gpuMs, drawCalls, ...)` — identical frames
- `AddFramesWithGrowingHeap(count, startHeap, growthPerFrame)` — memory leak simulation
- `AddFrame(FrameSample)` — single custom frame
- `AsEditorSession(EditorBaseline)` — marks as editor with optional baseline
- `WithActiveScripts(List<ActiveScriptEntry>)` — inject script list
- `WithProfilerMarkers(List<ProfilerMarkerSample>)` — inject markers
- `MarkCaptureFrame(int)` — mark DrWario capture frames for GC rule exclusion

### TestReportFactory

Static factory for `DiagnosticReport` and `DiagnosticFinding`:

```csharp
var report = TestReportFactory.MakeReport(
    TestReportFactory.MakeFinding(Severity.Critical, Confidence.High, category: "CPU"),
    TestReportFactory.MakeFinding(Severity.Warning, Confidence.Low, category: "Memory")
);
report.ComputeGrades();

var reportWithMetrics = TestReportFactory.MakeReportWithMetrics(
    avgCpu: 15f, p95Cpu: 25f, avgGc: 800f
);
```

## Assembly Definition

The test asmdef (`DrWario.Editor.Tests`) references:
- `DrWario.Runtime` (GUID: `4b2d7eb1f83eb17560c643870b4b3702`)
- `DrWario.Editor` (GUID: `1a95d00e573399d3622533f1164955ea`)
- `UnityEngine.TestRunner` (GUID: `27619889b8ba8c24980f86f30d6cb2cf`)
- `UnityEditor.TestRunner` (GUID: `0acc523941302664db1f4e527237feb3`)

Requires `overrideReferences: true` with `nunit.framework.dll` and `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.

## Consuming Project Setup

Since DrWario is a UPM package, the consuming project (e.g., HybridFrame) must register it as testable in `Packages/manifest.json`:

```json
{
  "testables": ["com.jlabarca.drwario"]
}
```

Without this, Unity's Test Runner will not discover package tests.

## Writing New Tests

1. Add test classes under the appropriate subdirectory in `Tests/Editor/`
2. Use `TestSessionBuilder` for any test needing a `ProfilingSession`
3. Use `TestReportFactory` for any test needing a `DiagnosticReport`
4. Follow naming convention: `MethodUnderTest_Scenario_ExpectedResult`
5. Keep tests focused — one assertion per concept (multiple Assert calls are fine if testing one behavior)
6. For rule tests: construct a session with known data, call `rule.Analyze(session)`, verify findings
