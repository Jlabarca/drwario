# DrWario - Claude Code Project Instructions

## Project Overview

DrWario is a **standalone Unity Editor tool** for runtime performance diagnostics with optional AI-powered analysis. It profiles frame timing, memory, GC allocations, boot pipeline, asset loads, and network events — then grades your project A-F with actionable recommendations.

**Origin:** Extracted from [HybridFrame](https://github.com/Jlabarca/HybridFrame) (was "AIDoctor"). Now a standalone UPM package installable in any Unity 2022.3+ project.

**Package ID:** `com.jlabarca.drwario`
**Repo:** https://github.com/Jlabarca/drwario
**Unity Version:** 2022.3+
**Version:** 3.0.1
**Dependencies:** None (only UnityEngine/UnityEditor/System)

---

## Repository Structure

```
D:\ware\drwario\
├── package.json                         ← UPM package definition (v3.0.1)
├── README.md                            ← Usage guide
├── CLAUDE.md                            ← This file
├── Runtime/
│   ├── DrWario.Runtime.asmdef           ← All platforms, auto-referenced
│   ├── FrameSample.cs                   ← Data structs (FrameSample, BootStageTiming, AssetLoadTiming, NetworkEvent, SessionMetadata, EditorBaseline)
│   ├── ProfilingSession.cs              ← Ring buffer (3600 frames), boot/asset/network/script/marker/snapshot storage
│   ├── RuntimeCollector.cs              ← MonoBehaviour, samples frame data each Update, OnSessionStarted/OnFrameSampled events
│   ├── BootTimingHook.cs                ← Static callback for boot pipeline instrumentation
│   ├── ProfilerBridge.cs                ← ProfilerRecorder-based subsystem counters (draw calls, batches, triangles, physics, audio, UI)
│   ├── ProfilerMarkerSample.cs          ← Profiler marker timing data struct
│   ├── SceneCensus.cs                   ← SceneCensus, ActiveScriptEntry, ConsoleLogEntry structs
│   └── SceneSnapshot.cs                 ← SceneSnapshotDiff, SceneObjectEntry structs
├── Editor/
│   ├── DrWario.Editor.asmdef            ← Editor-only, refs DrWario.Runtime
│   ├── DrWarioView.cs                   ← Main VisualElement (~2600 lines, 7 tabs)
│   ├── DrWarioWindow.cs                 ← Standalone EditorWindow (Window > DrWario > Diagnostics)
│   ├── DrWarioPlayModeHook.cs           ← [InitializeOnLoad] auto-start/stop profiling on Play Mode
│   ├── EditorBaselineCapture.cs         ← Captures idle editor overhead before Play Mode
│   ├── SceneCensusCapture.cs            ← Captures scene composition (objects, lights, scripts)
│   ├── SceneSnapshotTracker.cs          ← Hierarchy diff tracker (baseline + triggered snapshots)
│   ├── ProfilerBridgeEditor.cs          ← Reflection-based Unity Profiler window navigation
│   ├── UI/
│   │   ├── ChartElement.cs              ← Base chart class (Painter2D, tooltip, empty state)
│   │   ├── LineChart.cs                 ← Line chart with configurable data series
│   │   ├── BarChart.cs                  ← Bar chart for category comparisons
│   │   ├── Histogram.cs                 ← Distribution histogram
│   │   ├── TimelineElement.cs           ← 5-lane interactive timeline (CPU/GC/Boot/Assets/Network) with zoom/pan
│   │   ├── TimelineEvent.cs             ← TimelineEvent struct + TimelineEventBuilder
│   │   └── DataTableBuilder.cs          ← Programmatic data table construction
│   ├── Analysis/
│   │   ├── DiagnosticFinding.cs         ← Severity/Confidence enums + DiagnosticFinding struct
│   │   ├── DiagnosticReport.cs          ← Grade computation, Synthesis field, ExportJson(), ExportText()
│   │   ├── IAnalysisRule.cs             ← Extensible rule interface + IConfigurableRule with threshold slider
│   │   ├── AnalysisEngine.cs            ← Orchestrator: rules → correlation → synthesis → (optional AI enhancement)
│   │   ├── CorrelationEngine.cs         ← Cross-finding correlation detection (8 patterns)
│   │   ├── ReportSynthesizer.cs         ← Executive summary, prioritized actions, bottleneck narrative
│   │   ├── ReportComparison.cs          ← Side-by-side report diff (FindingDiff, MetricDelta, CategoryDelta)
│   │   ├── ReportHistory.cs             ← Persists reports to Library/DrWarioReports/
│   │   ├── RuleConfig.cs                ← EditorPrefs-based per-rule enable/disable and threshold persistence
│   │   ├── EditorAdjustment.cs          ← Baseline subtraction + confidence classification
│   │   ├── HtmlReportBuilder.cs         ← Self-contained HTML export with embedded CSS
│   │   ├── Rules/
│   │   │   ├── GCAllocationRule.cs      ← GC spikes >1KB/frame (RuleId: GC_SPIKE)
│   │   │   ├── FrameDropRule.cs         ← Frame time vs target FPS (RuleId: FRAME_DROP)
│   │   │   ├── BootStageRule.cs         ← Slow stages >2s, failures (RuleId: BOOT_STAGE/BOOT_TOTAL)
│   │   │   ├── MemoryLeakRule.cs        ← Heap growth via linear regression (RuleId: MEMORY_LEAK)
│   │   │   ├── AssetLoadRule.cs         ← Slow loads >500ms (RuleId: ASSET_LOAD)
│   │   │   ├── NetworkLatencyRule.cs    ← Error rates, latency >100ms (RuleId: NETWORK_*)
│   │   │   ├── RenderingEfficiencyRule.cs ← Draw calls, set-pass, triangles (RuleId: RENDER_*)
│   │   │   ├── CPUvsGPUBottleneckRule.cs  ← CPU/GPU bound detection (RuleId: BOTTLENECK_*)
│   │   │   └── AIAnalysisRule.cs        ← LLM-powered analysis (wraps LLMClient)
│   │   └── LLM/
│   │       ├── LLMConfig.cs             ← EditorPrefs-backed config (provider, key, model, endpoint)
│   │       ├── LLMPromptBuilder.cs      ← Builds system + user + MCP + correction prompts
│   │       ├── LLMClient.cs             ← UnityWebRequest to Claude/OpenAI/Ollama/Custom (streaming + non-streaming)
│   │       ├── LLMResponseParser.cs     ← Parses JSON array response into DiagnosticFindings
│   │       └── SseDownloadHandler.cs    ← SSE streaming with progressive JSON array parser
│   └── (no USS/UXML — all UI is C# VisualElement code)
├── Tests/
│   ├── Editor/
│   │   ├── DrWario.Editor.Tests.asmdef  ← EditMode test assembly (178 tests, all passing)
│   │   ├── Helpers/
│   │   │   ├── TestSessionBuilder.cs    ← Fluent builder for synthetic ProfilingSessions
│   │   │   └── TestReportFactory.cs     ← Factory for DiagnosticReport/Finding
│   │   ├── Runtime/                     ← P1: Ring buffer, data struct tests (24 tests)
│   │   ├── Analysis/                    ← P1+P2: Grading, comparison, rules, engine, correlation, synthesis (115 tests)
│   │   ├── LLM/                         ← P1+P2b: Parser, prompt builder tests (45 tests)
│   │   └── Integration/                 ← P3: Full pipeline integration tests (17 tests)
│   └── PlayMode/
│       ├── DrWario.PlayMode.Tests.asmdef ← E2E test assembly (5 tests, EnterPlayMode/ExitPlayMode)
│       ├── Stressors/
│       │   ├── GCSpikeStressor.cs        ← 60KB/frame + 100 small allocs/frame
│       │   ├── MemoryLeakStressor.cs     ← Static list retains 35KB/frame (monotonic leak)
│       │   ├── CyclicChurnStressor.cs    ← Instantiate/Destroy 60 objects every 20 frames
│       │   └── FrameDropStressor.cs      ← 50ms CPU busy-wait in Update
│       └── DrWarioPlayModeTests.cs       ← 5 [UnityTest] E2E tests with real RuntimeCollector sampling
├── docs/
│   └── tests/README.md                  ← Test coverage documentation
└── .gitignore
```

---

## Architecture

### Runtime (all platforms, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)

```
RuntimeCollector (MonoBehaviour, singleton)
  ├── StartSession() → creates ProfilingSession, fires OnSessionStarted event
  ├── Update() → samples FrameSample each frame into ring buffer
  ├── OnFrameSampled event → triggers SceneSnapshotTracker on spikes
  └── StopSession() → marks session complete

ProfilingSession (ring buffer + event storage)
  ├── FrameSample[3600] ring buffer (RecordFrame)
  ├── List<BootStageTiming> (RecordBootStage)
  ├── List<AssetLoadTiming> (RecordAssetLoad)
  ├── List<NetworkEvent> (RecordNetworkEvent)
  ├── List<ActiveScriptEntry> (SetActiveScripts)
  ├── List<ConsoleLogEntry> (RecordConsoleLog)
  ├── List<ProfilerMarkerSample> (SetProfilerMarkers)
  ├── List<SceneSnapshotDiff> (RecordSceneSnapshot)
  └── SceneCensus (scene composition snapshot)

ProfilerBridge (ProfilerRecorder-based)
  └── Captures: DrawCalls, Batches, SetPassCalls, Triangles, Physics, Audio, Animation, UI counters
```

### Editor (analysis pipeline)

```
AnalysisEngine
  ├── Phase 1: Run 8 deterministic rules → List<DiagnosticFinding>
  │   (GC_SPIKE, FRAME_DROP, BOOT_STAGE, MEMORY_LEAK, ASSET_LOAD, NETWORK_*, RENDER_*, BOTTLENECK_*)
  ├── Phase 2: CorrelationEngine.Detect() → cross-finding patterns
  ├── Phase 3: ReportSynthesizer → executive summary, prioritized actions
  ├── ComputeGrades() → letter grades A-F per category + overall
  └── (Optional) AI Enhancement via streaming LLM

DrWarioView (VisualElement, 7 tabs)
  ├── Summary: grade, sparklines (Painter2D), category cards
  ├── Findings: severity-sorted expandable cards with "Show in Profiler" links
  ├── Recommendations: grouped by category, prioritized by correlation
  ├── Timeline: 5-lane interactive timeline (zoom/pan/tooltips)
  ├── History: saved reports with comparison flow + HTML export
  ├── Ask Doctor: free-form LLM Q&A with profiling context
  └── LLM Settings: provider, model, API key, auto-start toggle, rule enable/disable UI
```

---

## Key Patterns

### Grading
- Start at 100, subtract per finding: Critical=-15, Warning=-5, Info=-1
- Confidence multipliers: Low=0.25x, Medium=0.6x, High=1x
- A≥90, B≥80, C≥70, D≥60, F<60
- Same formula per category

### Analysis Pipeline
- **Deterministic first:** 8 rules run without any AI dependency
- **Correlation detection:** 8 cross-finding patterns (GC+drops, leak+GC, GPU+tris, etc.)
- **Report synthesis:** Executive summary, bottleneck narrative, prioritized actions
- **AI enhancement (optional):** "Enhance with AI" button sends report + profiling data to LLM
- The deterministic report is designed to be self-sufficient; AI enhances it

### LLM Integration
- **Providers:** Claude (Anthropic API), OpenAI, Ollama (local), Custom (OpenAI-compatible)
- **Streaming:** SSE with progressive JSON array parser (SseDownloadHandler)
- **System prompt:** Generic Unity expert + optional `AdditionalContext` from frameworks
- **User prompt:** JSON with session metadata, frame summary, memory trajectory, profiler markers, active scripts (with namespace), console logs, scene census (trivial components filtered), scene snapshots, pre-analysis findings, editor baseline
- **MCP prompts:** Suspect check, report correction, two-phase MCP correction
- **Response:** JSON array of findings parsed via `JsonUtility` wrapper
- **Rate limit:** 10s between requests
- **API key storage:** XOR-obfuscated in EditorPrefs (not real encryption, just prevents plaintext)

### Editor Baseline
- `EditorBaselineCapture` measures idle editor overhead before Play Mode
- Baseline values (CPU, GC, draw calls) subtracted from findings to estimate game-only metrics
- Findings get confidence classification: High (survives baseline), Medium (partially), Low (editor-only)

### Framework Integration Points
- `RuntimeCollector.OnSessionStarted` — event for hooking boot/asset/network instrumentation
- `RuntimeCollector.OnFrameSampled` — event for SceneSnapshotTracker spike detection
- `LLMPromptBuilder.AdditionalContext` — static string property for injecting framework-specific LLM context
- `#if DRWARIO_INSTALLED` — via UPM `versionDefines` in consumer's asmdef
- `IAnalysisRule` / `IConfigurableRule` — implement to add custom analysis rules

### Ring Buffer
- Fixed 3600 capacity (~60s at 60fps)
- `_frameWriteIndex` wraps around, `_frameCount` capped at capacity
- `GetFrames()` returns chronological copy (handles wrap-around)

---

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `DrWario.Runtime` | FrameSample, ProfilingSession, RuntimeCollector, BootTimingHook, ProfilerBridge, ProfilerMarkerSample, SceneCensus, ActiveScriptEntry, ConsoleLogEntry, SceneSnapshotDiff, NetworkEventType |
| `DrWario.Editor` | DrWarioView, DrWarioWindow, DrWarioPlayModeHook, EditorBaselineCapture, SceneCensusCapture, SceneSnapshotTracker, ProfilerBridgeEditor |
| `DrWario.Editor.Analysis` | AnalysisEngine, DiagnosticReport, DiagnosticFinding, IAnalysisRule, IConfigurableRule, ReportHistory, ReportComparison, CorrelationEngine, ReportSynthesizer, RuleConfig, EditorAdjustment, HtmlReportBuilder, Severity, Confidence |
| `DrWario.Editor.Analysis.Rules` | GCAllocationRule, FrameDropRule, BootStageRule, MemoryLeakRule, AssetLoadRule, NetworkLatencyRule, RenderingEfficiencyRule, CPUvsGPUBottleneckRule, AIAnalysisRule |
| `DrWario.Editor.Analysis.LLM` | LLMConfig, LLMClient, LLMPromptBuilder, LLMResponseParser, SseDownloadHandler, LLMProvider, LLMResponse |
| `DrWario.Editor.UI` | ChartElement, LineChart, BarChart, Histogram, TimelineElement, TimelineEvent, DataTableBuilder |
| `DrWario.Tests` | All test classes, TestSessionBuilder, TestReportFactory |

---

## EditorPrefs Keys

All prefixed with `DrWario_`:
- `DrWario_Provider` (int)
- `DrWario_ApiKey_Claude`, `DrWario_ApiKey_OpenAI`, etc. (obfuscated string)
- `DrWario_ModelId` (string)
- `DrWario_Endpoint` (string)
- `DrWario_Timeout` (int, default 30)
- `DrWario_Enabled` (bool)
- `DrWario_AutoStart` (bool)
- `DrWario_Rule_{RuleId}_Enabled` (bool, default true)
- `DrWario_Rule_{RuleId}_Threshold` (float)

---

## Testing

**183 tests across 5 tiers, all passing (verified in Unity 6000.3.2f1 via HybridFrame).**

- **P1 (70):** Pure unit tests — ring buffer, grading, comparison, parser, editor adjustment, data structs
- **P2 (91):** Rule tests (all 8 rules), AnalysisEngine, CorrelationEngine, ReportSynthesizer
- **P2b (32):** LLM prompt builder tests — all prompt types, section coverage, filtering
- **P3 (17):** Integration tests — full pipeline flows, report comparison, prompt generation
- **E2E (5):** PlayMode tests — real RuntimeCollector sampling + AnalysisEngine assertion (GC spike, memory leak, cyclic churn, frame drop, healthy baseline)

Test helpers: `TestSessionBuilder` (fluent session builder), `TestReportFactory` (finding/report factory).
See `docs/tests/README.md` for full coverage table.

To run: Open project with DrWario installed, ensure `"testables": ["com.jlabarca.drwario"]` in manifest.json, then:
- EditMode (P1–P3, 178 tests): Window > Test Runner > EditMode > Run All
- E2E PlayMode (5 tests): Window > Test Runner > EditMode > Run All (DrWarioPlayModeTests — uses EnterPlayMode/ExitPlayMode internally, appears in EditMode tab)

---

## HybridFrame Integration

When installed in HybridFrame (`D:\ware\HybridFrame`), integration is automatic:
- `versionDefines` in Core.asmdef and Editor.asmdef define `DRWARIO_INSTALLED`
- `DrWarioIntegration.cs` (in HF) sets `LLMPromptBuilder.AdditionalContext` with HF architecture context
- `HFLauncher.cs` hooks `BootTimingHook.OnStageComplete` via `#if DRWARIO_INSTALLED`
- `YooAssetService.cs` calls `RecordAssetLoad` via `#if DRWARIO_INSTALLED`
- `KcpProvider.cs` calls `RecordNetworkEvent` via `#if DRWARIO_INSTALLED`
- `HFDashboard.cs` conditionally shows DrWario tab

---

## Development Priorities

### Near-term
1. **GPU profiling fallback** — Currently GPU time comes from FrameTimingManager which may be 0 on some platforms. Add fallback estimation
2. **Sparkline improvements** — Add mouse hover tooltips showing exact frame time values

### Longer-term
- **CI integration** — Run headless profiling session, export report, fail build if grade < threshold
- **Plugin auto-discovery** — Scan assemblies for `IAnalysisRule` implementations automatically
- **Real-world validation** — Run DrWario on diverse Unity projects, tune thresholds and correlation patterns based on actual results

### Completed (v3.0)
- ~~Tests~~ — 178 tests (P1+P2+P3), all passing
- ~~Report comparison~~ — Side-by-side with FindingDiff (Fixed/New/Persists)
- ~~Timeline view~~ — 5-lane interactive timeline with zoom/pan
- ~~Streaming LLM~~ — SSE with progressive JSON parser
- ~~Rule enable/disable UI~~ — Per-rule toggles + threshold sliders via RuleConfig
- ~~Expandable finding cards~~ — With "Show in Profiler" frame navigation
- ~~HTML report export~~ — Self-contained HTML with embedded CSS
- ~~Correlation engine~~ — 8 cross-finding pattern detectors
- ~~Report synthesis~~ — Executive summary + prioritized actions (no AI required)
- ~~MCP workflow prompts~~ — Suspect check, report correction, two-phase verification
- ~~Editor baseline~~ — Idle overhead measurement + confidence classification

---

## Code Style

- **Namespaces:** `DrWario.Runtime` for runtime, `DrWario.Editor.*` for editor
- **No external dependencies** — only UnityEngine, UnityEditor, System
- **UI:** All C# VisualElement code (no UXML/USS files)
- **Conditional compilation:** `#if UNITY_EDITOR || DEVELOPMENT_BUILD` for runtime code
- **Log prefix:** `[DrWario]` for all Debug.Log messages
- **Severity levels:** Info (blue), Warning (yellow), Critical (red)
- **Math:** Use `Mathf` not `Math` — Unity doesn't include `System.Math` by default
- **Assembly refs:** Runtime CANNOT reference Editor — use events/delegates for cross-assembly communication

---

## Gotchas

- `RuntimeCollector` requires Play Mode — it's a MonoBehaviour that uses `Update()`
- `FrameTimingManager.GetLatestTimings()` returns 0 on some platforms → fallback to `Time.unscaledDeltaTime * 1000f`
- `AIAnalysisRule.Analyze()` calls `Task.Wait()` — blocking in Editor context, but unavoidable without UniTask dependency
- `LLMClient` uses `UnityWebRequest` which needs main thread — async polling with `Task.Delay(100)`
- `ReportHistory` stores in `Library/DrWarioReports/` — not version controlled, survives reimport
- Ring buffer `GetFrames()` allocates a new array each call — don't call in hot paths
- `TestSessionBuilder.WithMetadata()` — `SessionMetadata` is a struct, so lambda mutations on struct fields are lost (use specific builder methods instead)
- Rule IDs: `GC_SPIKE` (not GC_ALLOC), `FRAME_DROP`, `MEMORY_LEAK`, `BOOT_STAGE`, `ASSET_LOAD`, `NETWORK_*`, `RENDER_*`, `BOTTLENECK_*`
