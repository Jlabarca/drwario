# DrWario Test Suite

**Status:** 70 tests passing (P1 complete)
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
│   └── EditorAdjustmentTests.cs         ← Baseline subtraction, confidence (12 tests)
└── LLM/
    └── LLMResponseParserTests.cs  ← JSON parsing, markdown stripping (13 tests)
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

### P2: Editor Tests (planned — ~56 tests)

Individual analysis rules, AnalysisEngine pipeline, LLMPromptBuilder, SceneCensusCapture.
See [study-test-plan.md](../study-test-plan.md) sections 3.1–3.6.

### P3: Integration Tests (planned — ~17 tests)

Full pipeline, LLM round-trip, report comparison workflows, correlation+synthesis.
See [study-test-plan.md](../study-test-plan.md) sections 4.1–4.4.

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
