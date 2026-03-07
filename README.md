# DrWario

Runtime performance diagnostics for Unity with AI-powered analysis.

DrWario profiles your game at runtime, applies 6 deterministic analysis rules, and optionally sends statistical summaries to an LLM for deeper insights. No source code is ever sent — only profiling metrics.

## Installation

**Unity Package Manager (git URL):**

```
Window > Package Manager > + > Add package from git URL
```

```
https://github.com/Jlabarca/drwario.git
```

**Or add to `manifest.json`:**

```json
{
  "dependencies": {
    "com.jlabarca.drwario": "https://github.com/Jlabarca/drwario.git"
  }
}
```

**Requirements:** Unity 2022.3+. No external dependencies.

## Quick Start

1. Open **Window > DrWario > Diagnostics**
2. Enter **Play Mode**
3. Click **Start Profiling**
4. Play your game for 10-60 seconds
5. Click **Stop Profiling**
6. Click **Analyze**

You'll get a letter grade (A-F), per-category breakdowns, a frame time sparkline, and actionable recommendations.

## Features

### Profiling
- **Frame timing** — CPU/GPU frame times via `FrameTimingManager`
- **Memory tracking** — Heap size, GC allocations, texture/mesh memory
- **Boot pipeline** — Per-stage timing and success/failure (when instrumented)
- **Asset loads** — Duration tracking per asset key
- **Network events** — Send/receive/error counts with bandwidth metrics
- **Ring buffer** — 3600-frame circular buffer (~60s at 60fps), zero-alloc during recording

### Analysis Rules

| Rule | Category | What it detects |
|------|----------|----------------|
| GC Allocation | Memory | Per-frame GC spikes above 1KB threshold |
| Frame Drop | CPU | Frames exceeding target framerate, P95/P99 stats |
| Boot Stage | Boot | Slow stages (>2s), failed stages, total boot time |
| Memory Leak | Memory | Heap growth via linear regression (>1MB/s) |
| Asset Load | Assets | Slow loads (>500ms), critical loads (>2s) |
| Network Latency | Network | Error rates, high latency (>100ms), bandwidth |

### AI Analysis (Optional)
- Connect Claude, OpenAI, GPT-4o, Ollama, or any OpenAI-compatible endpoint
- LLM receives statistical summaries only (~1000 tokens) — no source code
- AI findings are deduplicated against rule findings (AI version kept when overlapping)
- Works without LLM — rule-based analysis is always available

### UI Tabs
- **Summary** — Grade, health score, category cards, frame time sparkline
- **Findings** — All issues sorted by severity with descriptions
- **Recommendations** — Grouped by category with actionable fixes
- **History** — Saved reports with grade trends, delete/clear
- **Ask Doctor** — Free-form questions answered by LLM using your profiling data
- **LLM Settings** — Provider, model, API key, auto-start toggle

### Export
- **JSON** — Machine-readable report with all findings and metrics
- **Text** — Human-readable report with severity icons and recommendations

## Auto-Start Profiling

In **LLM Settings**, enable "Auto-start profiling on Play Mode". DrWario will automatically start recording when you enter Play Mode and stop when you exit.

## Framework Integration

DrWario works standalone in any Unity project. Frameworks can optionally integrate deeper:

### Hook into session start

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DrWario.Runtime;

RuntimeCollector.OnSessionStarted += session =>
{
    // Wire up your boot pipeline timing
    myProcedureManager.OnStageComplete = BootTimingHook.OnStageComplete;
};
#endif
```

### Record asset loads

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
DrWario.Runtime.RuntimeCollector.ActiveSession?.RecordAssetLoad("my-asset", durationMs);
#endif
```

### Record network events

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
DrWario.Runtime.RuntimeCollector.ActiveSession?.RecordNetworkEvent(
    DrWario.Runtime.NetworkEventType.Send, byteCount);
#endif
```

### Inject LLM context

Frameworks can inject domain-specific context into LLM prompts:

```csharp
#if DRWARIO_INSTALLED
using DrWario.Editor.Analysis.LLM;
using UnityEditor;

[InitializeOnLoad]
static class MyFrameworkDrWarioIntegration
{
    static MyFrameworkDrWarioIntegration()
    {
        LLMPromptBuilder.AdditionalContext = @"
Framework: MyFramework
- Uses async boot pipeline with 5 stages
- Asset bundles loaded via custom CDN
- Networking: WebSocket for reliable, UDP for real-time";
    }
}
#endif
```

### Conditional compilation

When DrWario is installed via UPM, add `versionDefines` to your `.asmdef`:

```json
{
    "versionDefines": [
        {
            "name": "com.jlabarca.drwario",
            "expression": "",
            "define": "DRWARIO_INSTALLED"
        }
    ]
}
```

Then guard your integration code with `#if DRWARIO_INSTALLED`.

## Assembly Structure

```
DrWario.Runtime    — FrameSample, ProfilingSession, RuntimeCollector, BootTimingHook
                     All platforms. #if UNITY_EDITOR || DEVELOPMENT_BUILD guarded.
                     Zero footprint in release builds.

DrWario.Editor     — Analysis engine, rules, LLM integration, UI
                     Editor only. References DrWario.Runtime.
```

## API Reference

### DrWario.Runtime

| Type | Description |
|------|-------------|
| `RuntimeCollector` | MonoBehaviour that samples frame data each Update |
| `RuntimeCollector.StartSession(capacity)` | Begin recording (creates GameObject) |
| `RuntimeCollector.StopSession()` | Stop recording, preserve data |
| `RuntimeCollector.ActiveSession` | Current ProfilingSession (null if none) |
| `RuntimeCollector.OnSessionStarted` | Event fired when a new session begins |
| `ProfilingSession` | Ring buffer + boot/asset/network event storage |
| `BootTimingHook.OnStageComplete` | Callback for boot pipeline instrumentation |
| `FrameSample` | Per-frame CPU, GPU, GC, heap, texture, mesh data |

### DrWario.Editor

| Type | Description |
|------|-------------|
| `AnalysisEngine` | Runs all rules + optional AI analysis |
| `DiagnosticReport` | Grade, score, findings, export (JSON/text) |
| `IAnalysisRule` | Interface for custom analysis rules |
| `LLMConfig` | Provider, API key, model, endpoint settings |
| `LLMPromptBuilder.AdditionalContext` | Static property for framework context injection |
| `DrWarioView` | VisualElement — embeddable in any EditorWindow |
| `DrWarioWindow` | Standalone EditorWindow at Window > DrWario > Diagnostics |

## License

MIT
