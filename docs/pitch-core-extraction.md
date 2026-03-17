# DrWario Core Extraction Pitch

## The Idea

Extract DrWario's analysis engine into a **standalone C# DLL** (`DrWario.Core`) with zero Unity dependencies. Unity becomes one of many possible frontends — a thin editor UI that feeds profiling data into the core and displays results.

```
┌─────────────────────────────────────────────────────────┐
│                    DrWario.Core.dll                      │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │AnalysisEngine│  │   8 Rules    │  │  LLM Client   │  │
│  │ Correlation  │  │ GC, Memory,  │  │ Prompt Builder│  │
│  │ Synthesis    │  │ Frame, Boot  │  │ Response Parse│  │
│  └──────────────┘  └──────────────┘  └───────────────┘  │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  Data Structs │  │  Grading    │  │  Report Export │  │
│  │ FrameSample   │  │  A-F scale  │  │  JSON / Text  │  │
│  │ ProfilingSession│ │  per-cat   │  │  HTML         │  │
│  └──────────────┘  └──────────────┘  └───────────────┘  │
│                                                         │
│  Abstractions: IHttpClient, IConfigStorage, ILogger,    │
│                IFileSystem, IJsonParser                  │
└───────────────────────────┬─────────────────────────────┘
                            │
              ┌─────────────┼─────────────────┐
              │             │                 │
      ┌───────▼──────┐ ┌───▼──────┐  ┌───────▼───────┐
      │ Unity Plugin │ │ CLI Tool │  │  CI Pipeline  │
      │ Editor UI    │ │ Analyze  │  │  Grade gate   │
      │ ProfilerRec. │ │ .NET app │  │  JSON report  │
      │ VisualElement│ │ any perf │  │  fail if < B  │
      └──────────────┘ │ data     │  └───────────────┘
                       └──────────┘
```

## What Goes Into the DLL

The analysis engine is already **~75-80% pure C#** with no Unity dependency. The Unity coupling is shallow — a handful of `Mathf.Clamp`, `Debug.Log`, `EditorPrefs`, and `JsonUtility` calls scattered across otherwise clean code.

### Ready to extract today (zero changes needed)

| Component | What it does |
|-----------|-------------|
| `DiagnosticFinding` | Severity enum, finding struct |
| `IAnalysisRule` | Rule interface |
| `CorrelationEngine` | 8 cross-finding correlation patterns |
| `ReportSynthesizer` | Executive summaries, bottleneck narratives |
| `EditorAdjustment` | Baseline subtraction, confidence classification |
| All data structs | FrameSample, BootStageTiming, AssetLoadTiming, NetworkEvent, SceneCensus |
| 6 deterministic rules | GC, FrameDrop, MemoryLeak, Boot, AssetLoad, Network |
| 2 rendering rules | RenderingEfficiency, CPUvsGPUBottleneck |

### Needs minor abstraction (~1-2 line changes each)

| Component | Unity dependency | Fix |
|-----------|-----------------|-----|
| `AnalysisEngine` | 1x `Debug.LogWarning` | `ILogger` |
| `DiagnosticReport` | 3x `Mathf.Clamp`, 1x `JsonUtility` | `Math.Clamp`, `System.Text.Json` |
| `LLMPromptBuilder` | 1x `using UnityEngine` | Remove unused import |
| `LLMResponseParser` | 2x `JsonUtility.FromJson` | `System.Text.Json` |

### Needs interface layer

| Component | Unity dependency | Abstraction |
|-----------|-----------------|-------------|
| `LLMClient` | `UnityWebRequest` | `IHttpClient` (swap in `HttpClient` for .NET) |
| `SseDownloadHandler` | `UnityWebRequest` streaming | `IStreamingHttpClient` |
| `LLMConfig` | `EditorPrefs` | `IConfigStorage` |
| `ReportHistory` | `Application.dataPath` | `IFileSystem` |
| `ProfilingSession` | `Application.*`, `Time.*` | `IPlatformInfo`, `ITimeProvider` |

### Stays in Unity (non-extractable)

| Component | Why |
|-----------|-----|
| `RuntimeCollector` | MonoBehaviour, Unity Profiler APIs |
| `ProfilerBridge` | ProfilerRecorder, ProfilerCategory |
| `DrWarioView` | VisualElement UI (~2600 LOC) |
| `DrWarioWindow` | EditorWindow |
| `SceneSnapshotTracker` | Unity hierarchy access |
| `SceneCensusCapture` | Unity scene queries |
| All UI components | Charts, Timeline — VisualElement |

## What This Unlocks

### 1. CLI Profiling Tool
A `drwario` command-line tool that analyzes any .NET application's profiling data:

```bash
# Profile a .NET app, export data, analyze
drwario analyze session.json --grade-threshold B
# Exit code 1 if grade < B → CI gate
```

### 2. CI/CD Quality Gate
Run profiling in headless Unity (or any runtime), pipe session data to DrWario Core, fail the build if performance regresses.

### 3. Other Game Engines
Godot, Stride, MonoGame, or any C# runtime can use the analysis engine. Each engine provides its own collector that feeds `ProfilingSession` data — the rules, grading, and LLM analysis work identically.

### 4. Cloud Analysis Service
Host DrWario Core as an API endpoint. Users upload profiling sessions, get back graded reports with AI-enhanced findings. No Unity installation required.

### 5. Better Testing
Pure C# DLL = standard xUnit/NUnit tests with no Unity Test Runner overhead. Mock `ProfilingSession` data, assert rule outputs, test grading math — all in milliseconds.

### 6. Generic Performance Analysis
The rule patterns (spike detection, leak detection via linear regression, percentile analysis, correlation) apply to **any** performance data — web servers, mobile apps, microservices. The data structs generalize naturally:
- `FrameSample` → any timed sample (request, tick, frame)
- `BootStageTiming` → any startup pipeline
- `NetworkEvent` → any RPC/HTTP call

## Pros

**Architecture**
- Clean separation of concerns — analysis logic has no business knowing about `EditorPrefs` or `VisualElement`
- Makes the existing implicit boundary explicit and enforceable
- Forces good abstractions (`IHttpClient`, `IConfigStorage`) that improve testability everywhere
- The analysis pipeline (Rules → Correlation → Synthesis → AI) is already a clean, engine-agnostic design

**Development velocity**
- Test the core with xUnit in <1 second instead of Unity Test Runner's 10-30 second startup
- Develop analysis improvements without opening Unity
- Iterate on LLM prompts and parsing in a console app
- CI runs tests on the DLL directly — no Unity license needed for test infrastructure

**Market reach**
- Addresses any C# runtime, not just Unity
- CLI tool for DevOps/CI teams who don't use Unity Editor
- NuGet distribution reaches the entire .NET ecosystem
- Cloud-hosted analysis as a service (SaaS potential)

**Unity plugin gets simpler**
- The Unity package becomes a thin wrapper: collect data → call DLL → display results
- UI code stays in Unity where it belongs
- Easier to maintain — Unity API changes only affect the thin adapter layer
- Plugin size decreases (the DLL is a single file reference)

## Cons

**Complexity cost**
- 5-8 new interfaces to maintain (`IHttpClient`, `IConfigStorage`, `ILogger`, `IFileSystem`, `IJsonParser`, `IPlatformInfo`, `ITimeProvider`)
- Two build artifacts to version and distribute (NuGet package + UPM package)
- Unity adapter layer adds indirection — debugging crosses assembly boundaries
- Must keep the DLL compatible with Unity's .NET Standard 2.1 (no .NET 8+ features)

**Distribution friction**
- UPM doesn't natively consume NuGet packages — need to bundle the DLL or use NuGetForUnity
- Users who only care about Unity get a more complex package structure
- DLL versioning must stay in sync with Unity package version
- Potential confusion: "do I install the NuGet or the UPM package?"

**Feature constraints**
- Some analysis benefits from Unity-specific knowledge (e.g., knowing that `Resources.Load` is synchronous) — generic analysis loses this context
- `System.Text.Json` behaves differently from `JsonUtility` — migration needs testing
- `HttpClient` vs `UnityWebRequest` have different threading models — the abstraction must handle this
- Can't use Unity's `Mathf`, `Color`, `Vector2` in the DLL — need pure C# alternatives for any math/visualization helpers

**Effort vs. payoff (short-term)**
- ~11-14 hours of refactoring for a project that currently only targets Unity
- No immediate users for the CLI/cloud/other-engine scenarios
- Risk of over-engineering if DrWario's primary value proposition stays Unity-specific
- The current codebase works — this is a "build it and they will come" bet

**Testing burden**
- Must test both the DLL independently AND the Unity integration
- Unity adapter implementations need their own integration tests
- Platform-specific behavior (IL2CPP, WebGL, mobile) can't be tested in the DLL

## Recommended Approach

### Phase 1: Prove the concept (low risk)
Create `DrWario.Core` as a separate **assembly definition** within the existing UPM package — no separate repo, no NuGet, no distribution change. Just enforce the boundary:

```
Runtime/
├── DrWario.Runtime.asmdef          ← Unity collectors (unchanged)
├── DrWario.Core.asmdef             ← NEW: zero Unity refs
│   ├── Core/
│   │   ├── DiagnosticFinding.cs
│   │   ├── AnalysisEngine.cs
│   │   ├── CorrelationEngine.cs
│   │   ├── ReportSynthesizer.cs
│   │   └── ...
│   ├── Rules/
│   │   └── ...
│   └── Abstractions/
│       ├── ILogger.cs
│       └── ...
```

This gives us the clean boundary **without** the distribution complexity. The Unity plugin still ships as a single UPM package, but the analysis assembly has zero Unity references — enforced by the asmdef.

### Phase 2: Extract when there's demand
When a real use case appears (CI pipeline, Godot port, cloud service), extract `DrWario.Core.asmdef` into a standalone `.csproj` / NuGet package. The abstraction boundary is already clean — this becomes a mechanical move.

### Phase 3: Build wrappers
CLI tool, cloud API, other engine plugins — each is a thin wrapper around the core DLL, just like the Unity plugin.

## Summary

| Aspect | Current | After extraction |
|--------|---------|-----------------|
| Target | Unity only | Any C# runtime |
| Testing | Unity Test Runner | xUnit (fast) + Unity integration |
| Distribution | UPM only | UPM + NuGet + CLI |
| Analysis engine | Mixed with Unity APIs | Pure C# DLL |
| Unity plugin | Everything | Thin wrapper (collect + display) |
| LOC in DLL | — | ~4000 (75-80% of analysis code) |
| New abstractions | — | 5-8 interfaces |
| Effort (Phase 1) | — | ~6-8 hours |
| Effort (full) | — | ~14-20 hours |

The core insight: **DrWario's analysis engine already is a generic performance diagnostics tool that happens to be compiled inside Unity.** Extracting it makes that truth explicit and opens the door to a much larger addressable market — while making the Unity plugin itself simpler and more maintainable.
