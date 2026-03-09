# DrWario - Claude Code Project Instructions

## Project Overview

DrWario is a **standalone Unity Editor tool** for runtime performance diagnostics with optional AI-powered analysis. It profiles frame timing, memory, GC allocations, boot pipeline, asset loads, and network events — then grades your project A-F with actionable recommendations.

**Origin:** Extracted from [HybridFrame](https://github.com/Jlabarca/HybridFrame) (was "AIDoctor"). Now a standalone UPM package installable in any Unity 2022.3+ project.

**Package ID:** `com.jlabarca.drwario`
**Repo:** https://github.com/Jlabarca/drwario
**Unity Version:** 2022.3+
**Dependencies:** None (only UnityEngine/UnityEditor/System)

---

## Repository Structure

```
D:\ware\drwario\
├── package.json                         ← UPM package definition
├── README.md                            ← Usage guide
├── CLAUDE.md                            ← This file
├── Runtime/
│   ├── DrWario.Runtime.asmdef           ← All platforms, auto-referenced
│   ├── FrameSample.cs                   ← Data structs (FrameSample, BootStageTiming, AssetLoadTiming, NetworkEvent, SessionMetadata)
│   ├── ProfilingSession.cs              ← Ring buffer (3600 frames), boot/asset/network event storage
│   ├── RuntimeCollector.cs              ← MonoBehaviour, samples frame data each Update, OnSessionStarted event
│   └── BootTimingHook.cs                ← Static callback for boot pipeline instrumentation
├── Editor/
│   ├── DrWario.Editor.asmdef            ← Editor-only, refs DrWario.Runtime
│   ├── DrWarioView.cs                   ← Main VisualElement (6 tabs: Summary, Findings, Recommendations, History, Ask Doctor, LLM Settings)
│   ├── DrWarioWindow.cs                 ← Standalone EditorWindow (Window > DrWario > Diagnostics)
│   ├── DrWarioPlayModeHook.cs           ← [InitializeOnLoad] auto-start/stop profiling on Play Mode
│   ├── Analysis/
│   │   ├── DiagnosticFinding.cs         ← Severity enum + DiagnosticFinding struct (8 fields)
│   │   ├── DiagnosticReport.cs          ← Grade computation, ExportJson(), ExportText()
│   │   ├── IAnalysisRule.cs             ← Extensible rule interface
│   │   ├── AnalysisEngine.cs            ← Runs all rules + optional AI, deduplicates findings
│   │   ├── ReportHistory.cs             ← Persists reports to Library/DrWarioReports/
│   │   ├── Rules/
│   │   │   ├── GCAllocationRule.cs      ← GC spikes >1KB/frame
│   │   │   ├── FrameDropRule.cs         ← Frame time vs target FPS, P95/P99
│   │   │   ├── BootStageRule.cs         ← Slow stages >2s, failures, total boot
│   │   │   ├── MemoryLeakRule.cs        ← Heap growth via linear regression
│   │   │   ├── AssetLoadRule.cs         ← Slow loads >500ms
│   │   │   ├── NetworkLatencyRule.cs    ← Error rates, latency >100ms, bandwidth
│   │   │   └── AIAnalysisRule.cs        ← LLM-powered analysis (wraps LLMClient)
│   │   └── LLM/
│   │       ├── LLMConfig.cs             ← EditorPrefs-backed config (provider, key, model, endpoint)
│   │       ├── LLMPromptBuilder.cs      ← Builds system + user prompts, AdditionalContext property
│   │       ├── LLMClient.cs             ← UnityWebRequest to Claude/OpenAI/Ollama/Custom
│   │       └── LLMResponseParser.cs     ← Parses JSON array response into DiagnosticFindings
│   └── (no USS/UXML — all UI is C# VisualElement code)
└── .gitignore
```

---

## Architecture

### Runtime (all platforms, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`)

```
RuntimeCollector (MonoBehaviour, singleton)
  ├── StartSession() → creates ProfilingSession, fires OnSessionStarted event
  ├── Update() → samples FrameSample each frame into ring buffer
  └── StopSession() → marks session complete

ProfilingSession (ring buffer)
  ├── FrameSample[3600] ring buffer (RecordFrame)
  ├── List<BootStageTiming> (RecordBootStage)
  ├── List<AssetLoadTiming> (RecordAssetLoad)
  └── List<NetworkEvent> (RecordNetworkEvent)
```

### Editor (analysis pipeline)

```
AnalysisEngine
  ├── Phase 1: Run 6 deterministic rules → List<DiagnosticFinding>
  ├── Phase 2: Run AIAnalysisRule (optional) → additional findings
  ├── Phase 3: Deduplicate (AI findings get priority over rule findings)
  └── ComputeGrades() → letter grades A-F per category + overall

DrWarioView (VisualElement, 6 tabs)
  ├── Summary: grade, sparkline (Painter2D), category cards
  ├── Findings: severity-sorted cards
  ├── Recommendations: grouped by category
  ├── History: saved reports with delete/clear
  ├── Ask Doctor: free-form LLM Q&A with profiling context
  └── LLM Settings: provider, model, API key, auto-start toggle
```

---

## Key Patterns

### Grading
- Start at 100, subtract per finding: Critical=-15, Warning=-5, Info=-1
- A≥90, B≥80, C≥70, D≥60, F<60
- Same formula per category

### LLM Integration
- **Providers:** Claude (Anthropic API), OpenAI, Ollama (local), Custom (OpenAI-compatible)
- **System prompt:** Generic Unity expert + optional `AdditionalContext` from frameworks
- **User prompt:** JSON with session metadata, frame summary (avg/P95/P99), memory trajectory (12 downsampled points + linear regression), boot pipeline, asset loads, pre-analysis findings
- **Response:** JSON array of findings parsed via `JsonUtility` wrapper
- **Rate limit:** 10s between requests
- **API key storage:** XOR-obfuscated in EditorPrefs (not real encryption, just prevents plaintext)

### Framework Integration Points
- `RuntimeCollector.OnSessionStarted` — event for hooking boot/asset/network instrumentation
- `LLMPromptBuilder.AdditionalContext` — static string property for injecting framework-specific LLM context
- `#if DRWARIO_INSTALLED` — via UPM `versionDefines` in consumer's asmdef
- `IAnalysisRule` — implement to add custom analysis rules via `AnalysisEngine.RegisterRule()`

### Ring Buffer
- Fixed 3600 capacity (~60s at 60fps)
- `_frameWriteIndex` wraps around, `_frameCount` capped at capacity
- `GetFrames()` returns chronological copy (handles wrap-around)

---

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `DrWario.Runtime` | FrameSample, ProfilingSession, RuntimeCollector, BootTimingHook, NetworkEventType |
| `DrWario.Editor` | DrWarioView, DrWarioWindow, DrWarioPlayModeHook |
| `DrWario.Editor.Analysis` | AnalysisEngine, DiagnosticReport, DiagnosticFinding, IAnalysisRule, ReportHistory, Severity |
| `DrWario.Editor.Analysis.Rules` | GCAllocationRule, FrameDropRule, BootStageRule, MemoryLeakRule, AssetLoadRule, NetworkLatencyRule, AIAnalysisRule |
| `DrWario.Editor.Analysis.LLM` | LLMConfig, LLMClient, LLMPromptBuilder, LLMResponseParser, LLMProvider, LLMResponse |

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

### Near-term improvements
1. **Tests** — 91 automated tests designed (see `D:\ware\HybridFrame\docs\aidoctor\automated-e2e\test-plan.md`), 0 implemented. Priority: ProfilingSession ring buffer, report grading, rule tests
2. **GPU profiling** — Currently GPU time comes from FrameTimingManager which may be 0 on some platforms. Add fallback estimation
3. **Custom rule registration** — `AnalysisEngine.RegisterRule()` exists but there's no UI for enabling/disabling individual rules
4. **Sparkline improvements** — Add mouse hover tooltips showing exact frame time values
5. **Report comparison** — History tab shows reports but doesn't compare them side-by-side

### Longer-term
- **CI integration** — Run headless profiling session, export report, fail build if grade < threshold
- **Plugin system** — Let users drop in custom `IAnalysisRule` implementations that auto-discover
- **Timeline view** — Visual timeline of frame spikes, GC events, and boot stages
- **Streaming LLM** — Show LLM response as it arrives instead of waiting for full response

---

## Code Style

- **Namespaces:** `DrWario.Runtime` for runtime, `DrWario.Editor.*` for editor
- **No external dependencies** — only UnityEngine, UnityEditor, System
- **UI:** All C# VisualElement code (no UXML/USS files)
- **Conditional compilation:** `#if UNITY_EDITOR || DEVELOPMENT_BUILD` for runtime code
- **Log prefix:** `[DrWario]` for all Debug.Log messages
- **Severity levels:** Info (blue), Warning (yellow), Critical (red)

---

## Gotchas

- `RuntimeCollector` requires Play Mode — it's a MonoBehaviour that uses `Update()`
- `FrameTimingManager.GetLatestTimings()` returns 0 on some platforms → fallback to `Time.unscaledDeltaTime * 1000f`
- `AIAnalysisRule.Analyze()` calls `Task.Wait()` — blocking in Editor context, but unavoidable without UniTask dependency
- `LLMClient` uses `UnityWebRequest` which needs main thread — async polling with `Task.Delay(100)`
- `ReportHistory` stores in `Library/DrWarioReports/` — not version controlled, survives reimport
- Ring buffer `GetFrames()` allocates a new array each call — don't call in hot paths
