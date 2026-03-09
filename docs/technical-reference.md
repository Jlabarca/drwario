# DrWario Technical Reference

> Deep architecture and implementation details for contributors and integrators.

## Architecture Evolution

DrWario's architecture has gone through two major iterations. Understanding the evolution explains why the current design exists.

### v1.0 — AI-Dependent Pipeline (Legacy)

The original architecture treated AI as a core part of the analysis pipeline:

```
AnalysisEngine (v1.0 — LEGACY)
  ├── Phase 1: 6 deterministic rules → findings
  ├── Phase 2: AIAnalysisRule → more findings (blocked editor 10-30s)
  └── Phase 3: Deduplicate (AI priority) → grade → report
```

**Limitations that drove the redesign:**
- Reports felt incomplete without AI — rules found symptoms but couldn't explain root causes or prioritize
- AI ran synchronously via `Task.Wait()`, freezing the editor
- No correlation detection — if GC spikes and frame drops coincided, only AI could spot it
- Basic sampling — raw `Profiler.GetTotalAllocatedMemoryLong()` deltas, no rendering/physics/audio counters
- Editor overhead (Scene view, Inspector) inflated all measurements with no adjustment
- No scene awareness — zero knowledge of what GameObjects existed during profiling
- DrWario's own overhead was measured as part of the game's performance

### v2.0 — Self-Sufficient Pipeline (Current)

The redesign makes the deterministic report complete and standalone. AI is an optional enhancement layer triggered on demand.

```
AnalysisEngine (v2.0 — CURRENT)
  ├── Phase 1: 8 deterministic rules (skip disabled rules)
  ├── Phase 2: CorrelationEngine — 8 cross-cutting pattern detectors
  ├── Phase 3: ComputeGrades — A-F per category + overall
  ├── Phase 4: ReportSynthesizer — executive summary, bottleneck ID, prioritized actions
  └── (User clicks "Enhance with AI")
       └── EnhanceWithAI[Streaming]Async — SSE streaming, adds findings, re-grades
```

**Key design decisions:**
1. **Correlation replaces ~60-70% of AI's role** — GC↔frame drops, asset loads↔GC, memory leaks↔allocation patterns, GPU bottlenecks are detected deterministically
2. **Synthesis provides the "so what"** — Executive summary, bottleneck identification, and prioritized action list without needing AI
3. **AI adds unique value** — Platform-specific advice, Unity version quirks, natural language explanation, non-obvious patterns the correlation engine can't detect
4. **Editor baseline** — Pre-Play Mode measurement of idle editor overhead enables threshold adjustment and confidence scoring
5. **Self-overhead subtraction** — DrWario's own ProfilerMarker time is subtracted from CPU measurements; capture frames are excluded from GC analysis

---

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────────┐
│                         Unity Editor                              │
│                                                                   │
│  DrWarioWindow ─── DrWarioView (6 tabs)                           │
│                        │                                          │
│                   AnalysisEngine                                  │
│                   ┌────┴────────────────────────┐                 │
│                   │                              │                │
│            Phase 1: 8 Rules              Phase 2: Correlations    │
│            ┌──────┴──────────┐           ┌──────┴──────┐          │
│            │ GCAllocation    │           │CorrelationEngine       │
│            │ FrameDrop       │           │ (8 patterns)  │        │
│            │ BootStage       │           └──────┬──────┘          │
│            │ MemoryLeak      │                  │                 │
│            │ AssetLoad       │           Phase 3: Grades          │
│            │ NetworkLatency  │                  │                 │
│            │ RenderingEff.   │           Phase 4: Synthesis       │
│            │ CPUvsGPU        │           ┌──────┴──────┐          │
│            └─────────────────┘           │ReportSynthesizer      │
│                                          │ (standalone)  │        │
│                   On-demand:             └──────────────┘          │
│            ┌──────┴──────┐                                        │
│            │AIAnalysisRule│ ← "Enhance with AI" button            │
│            │ (SSE stream) │                                       │
│            └──────────────┘                                       │
│                                                                   │
│  EditorBaselineCapture ─── SceneCensusCapture                     │
│  SceneSnapshotTracker ──── DrWarioPlayModeHook                    │
│                                                                   │
├───────────────────────────────────────────────────────────────────┤
│                         Runtime                                   │
│                                                                   │
│  RuntimeCollector (MonoBehaviour singleton)                        │
│       ├── ProfilerBridge (ProfilerRecorder counters)               │
│       ├── ProfilerMarker sampling (subsystem timing)              │
│       ├── Self-overhead recorder (DrWario.Sample)                 │
│       └── ProfilingSession (ring buffer 3600)                     │
│            ├── FrameSample[3600] (28 fields per frame)            │
│            ├── List<BootStageTiming>                               │
│            ├── List<AssetLoadTiming>                               │
│            ├── List<NetworkEvent>                                  │
│            ├── List<ProfilerMarkerSample>                          │
│            ├── List<SceneSnapshotDiff> (max 100)                  │
│            └── HashSet<int> DrWarioCaptureFrames                  │
│                                                                   │
│  BootTimingHook (static callback facade)                          │
│                                                                   │
│  #if UNITY_EDITOR || DEVELOPMENT_BUILD                            │
└───────────────────────────────────────────────────────────────────┘
```

---

## Data Structures

### FrameSample (struct, 28 fields)

Captured every `Update()` frame by RuntimeCollector.

| Field | Type | Source |
|-------|------|--------|
| `Timestamp` | float | `Time.realtimeSinceStartup` |
| `DeltaTime` | float | `Time.unscaledDeltaTime` |
| `CpuFrameTimeMs` | float | ProfilerBridge or FrameTimingManager, minus DrWario.Sample overhead |
| `GpuFrameTimeMs` | float | `FrameTimingManager` (0 on some platforms) |
| `RenderThreadMs` | float | ProfilerBridge or 0 |
| `GcAllocBytes` | long | ProfilerBridge GC.Alloc counter or heap delta |
| `GcAllocCount` | int | ProfilerBridge GC allocation count or 0 |
| `TotalHeapBytes` | long | ProfilerBridge TotalUsedMemory or `Profiler.GetTotalAllocatedMemoryLong()` |
| `TextureMemoryBytes` | long | ProfilerBridge or `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| `MeshMemoryBytes` | long | ProfilerBridge or `TotalReserved - TotalAllocated` |
| `DrawCalls` | int | ProfilerRecorder |
| `Batches` | int | ProfilerRecorder |
| `SetPassCalls` | int | ProfilerRecorder |
| `Triangles` | int | ProfilerRecorder |
| `Vertices` | int | ProfilerRecorder |
| `PhysicsActiveBodies` | int | ProfilerRecorder |
| `PhysicsKinematicBodies` | int | ProfilerRecorder |
| `PhysicsContacts` | int | ProfilerRecorder |
| `AudioVoiceCount` | int | ProfilerRecorder |
| `AudioDSPLoad` | float | ProfilerRecorder |
| `AnimatorCount` | int | ProfilerRecorder |
| `UICanvasRebuilds` | int | ProfilerRecorder |
| `UILayoutRebuilds` | int | ProfilerRecorder |
| `ObjectCount` | int | Periodic `FindObjectsByType` or SceneSnapshotTracker |
| `NativeMemoryBytes` | long | `Profiler.GetTotalReservedMemoryLong()` |
| `GcCollectionCount` | int | `GC.CollectionCount(0)` — cumulative |
| `FrameNumber` | int | `Time.frameCount` — for Profiler frame navigation |

### SessionMetadata (struct)

| Field | Type | Description |
|-------|------|-------------|
| `StartTime`, `EndTime` | DateTime | UTC session bounds |
| `UnityVersion` | string | `Application.unityVersion` |
| `Platform` | string | `Application.platform.ToString()` |
| `TargetFrameRate` | int | `Application.targetFrameRate` |
| `ScreenWidth`, `ScreenHeight` | int | `Screen.width/height` |
| `IsEditor` | bool | `Application.isEditor` |
| `IsDevelopmentBuild` | bool | `Debug.isDebugBuild` |
| `Baseline` | EditorBaseline | Idle editor overhead (30 frames pre-Play) |
| `SceneViewOpen` | bool | Scene view adds draw calls + rendering |
| `InspectorOpen` | bool | Inspector adds GC from serialization |
| `ProfilerOpen` | bool | Profiler has its own overhead |
| `GameViewCount` | int | Multiple game views = multiple renders |

### EditorBaseline (struct)

Captured during idle editor (30 frames before Play Mode) by `EditorBaselineCapture`.

| Field | Type | Description |
|-------|------|-------------|
| `AvgCpuFrameTimeMs` | float | Editor idle CPU overhead |
| `AvgRenderThreadMs` | float | Editor idle render thread |
| `AvgGcAllocBytes` | long | Editor idle GC per frame |
| `AvgGcAllocCount` | int | Editor idle allocation count |
| `AvgDrawCalls` | int | Editor idle draw calls (Scene view, etc.) |
| `AvgBatches` | int | Editor idle batches |
| `AvgSetPassCalls` | int | Editor idle SetPass calls |
| `AvgUICanvasRebuilds` | int | Editor idle UI canvas rebuilds |
| `AvgUILayoutRebuilds` | int | Editor idle UI layout rebuilds |
| `SampleCount` | int | Frames sampled (target: 30) |
| `IsValid` | bool | True if baseline was captured |

### DiagnosticFinding (struct)

| Field | Type | Description |
|-------|------|-------------|
| `RuleId` | string | Unique rule identifier (e.g., `GC_SPIKE`, `AI_CORR_01`) |
| `Category` | string | `CPU`, `Memory`, `Boot`, `Assets`, `Network`, `Rendering`, `General` |
| `Severity` | Severity | `Info`, `Warning`, `Critical` |
| `Title` | string | Short summary |
| `Description` | string | Detailed explanation with data references |
| `Recommendation` | string | Actionable fix |
| `Metric` | float | The measured value |
| `Threshold` | float | The reference threshold |
| `FrameIndex` | int | Frame index (-1 if not frame-specific) |
| `Confidence` | Confidence | `High`, `Medium`, `Low` — reliability of finding |
| `EnvironmentNote` | string | Optional context about editor impact |
| `AffectedFrames` | int[] | Frame indices where issue was detected (max 100) |

### Scene Tracking Types

| Struct | Fields | Purpose |
|--------|--------|---------|
| `SceneObjectEntry` | InstanceId, Name, ParentInstanceId, ComponentTypes[] | Single GameObject in a snapshot |
| `SceneSnapshotDiff` | FrameIndex, Timestamp, TotalObjectCount, Added[], Removed[], Trigger | Diff between two snapshots |
| `SceneCensus` | TotalGameObjects, TotalComponents, CanvasCount, DirectionalLights, etc. | Static scene analysis |
| `ProfilerMarkerSample` | MarkerName, AvgInclusiveTimeNs, MaxInclusiveTimeNs, AvgCallCount, SampleCount | Subsystem timing |

---

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `DrWario.Runtime` | FrameSample, ProfilingSession, RuntimeCollector, ProfilerBridge, BootTimingHook, NetworkEventType, SessionMetadata, EditorBaseline, SceneObjectEntry, SceneSnapshotDiff, SceneCensus, ProfilerMarkerSample |
| `DrWario.Editor` | DrWarioView, DrWarioWindow, DrWarioPlayModeHook, EditorBaselineCapture, SceneCensusCapture, SceneSnapshotTracker |
| `DrWario.Editor.Analysis` | AnalysisEngine, DiagnosticReport, DiagnosticFinding, IAnalysisRule, IConfigurableRule, ReportHistory, Severity, Confidence, CorrelationEngine, ReportSynthesizer, RuleConfig |
| `DrWario.Editor.Analysis.Rules` | GCAllocationRule, FrameDropRule, BootStageRule, MemoryLeakRule, AssetLoadRule, NetworkLatencyRule, RenderingEfficiencyRule, CPUvsGPUBottleneckRule, AIAnalysisRule |
| `DrWario.Editor.Analysis.LLM` | LLMConfig, LLMClient, LLMPromptBuilder, LLMResponseParser, SseDownloadHandler, LLMProvider, LLMResponse |

---

## Runtime Pipeline

### Ring Buffer (ProfilingSession)

- **Capacity:** 3600 frames (~60s at 60fps)
- **Write (O(1), zero allocation):**

```csharp
_frameBuffer[_frameWriteIndex] = sample;
_frameWriteIndex = (_frameWriteIndex + 1) % _frameBuffer.Length;
if (_frameCount < _frameBuffer.Length) _frameCount++;
```

- **Read (`GetFrames()` — allocates `FrameSample[_frameCount]`):**

```csharp
if (_frameCount < capacity)
    Array.Copy(_frameBuffer, 0, result, 0, _frameCount);  // not yet wrapped
else
    // Two copies: tail (oldest→end) + head (start→writeIndex)
    Array.Copy(buffer, writeIndex, result, 0, capacity - writeIndex);
    Array.Copy(buffer, 0, result, capacity - writeIndex, writeIndex);
```

Additional storage: boot stages, asset loads, network events (unbounded lists), scene snapshots (capped at 100), profiler markers, DrWario capture frame numbers.

### RuntimeCollector — Per-Frame Hot Path

```csharp
using (s_SampleMarker.Auto())  // ProfilerMarker wraps sampling
{
    _profilerBridge?.Sample();                                // ProfilerRecorder counters (zero-alloc)
    // Accumulate marker timing data for subsystem profiling

    // Subtract DrWario.Sample overhead from CPU measurement
    float selfOverheadMs = _selfOverheadRecorder.CurrentValue / 1_000_000f;
    float cpuMs = (profilerBridgeMs > 0 ? profilerBridgeMs : deltaTimeMs) - selfOverheadMs;

    var sample = new FrameSample { ... };                     // stack-allocated struct
    OnFrameSampled?.Invoke(ref sample);                       // editor hooks (scene snapshot tracker)
    ActiveSession.RecordFrame(sample);                        // struct copy to buffer
}
```

### Cross-Assembly Communication

Runtime assembly cannot reference Editor assembly. The bridge uses events:

```
RuntimeCollector.OnFrameSampled (delegate event, ref FrameSample)
    ↓ subscribed by
DrWarioPlayModeHook.OnFrameSampled (Editor)
    ↓ calls
SceneSnapshotTracker.OnFrame(sample) → returns ObjectCount
    ↓ writes back to
sample.ObjectCount
```

---

## Analysis Engine — 4-Phase Pipeline

### Phase 1: Deterministic Rules

8 rules run sequentially (skipping disabled rules via `RuleConfig`). Each receives the full `ProfilingSession` and returns `List<DiagnosticFinding>`.

Rules with `IConfigurableRule` expose adjustable thresholds in the UI.

### Phase 2: Correlation Detection

`CorrelationEngine.Detect()` finds 8 cross-cutting patterns:

| Pattern | Detection | Impact |
|---------|-----------|--------|
| GC↔Frame Drops | Temporal overlap (±2 frame window) | Critical: GC is causing jank |
| Asset Loads↔GC | Both present in same session | Warning: sync loads allocate |
| Boot↔Asset Loads | Slow boot + slow assets | Boot pipeline bottleneck |
| Memory Leak↔GC | Leak + allocation spikes | Leak accelerated by allocations |
| GPU↔Geometry | GPU bottleneck + high tri/draw calls | Geometry overload |
| CPU↔Draw Calls | CPU bottleneck + high draw calls | CPU-side rendering overhead |
| Pervasive GC | >50% of frames have GC spikes | Systemic allocation problem |
| Object Churn/Leak | Scene object growth from snapshots | Instantiate without Destroy |

### Phase 3: Grading

```
Score = 100
For each finding: Critical → -15, Warning → -5, Info → -1
Score = clamp(score, 0, 100)
Grade: A ≥ 90 | B ≥ 80 | C ≥ 70 | D ≥ 60 | F < 60
```

Same formula applied per-category.

### Phase 4: Report Synthesis

`ReportSynthesizer.Synthesize()` produces a standalone summary:
- **Executive summary** — One-paragraph assessment of overall health
- **Bottleneck identification** — Primary bottleneck subsystem
- **Prioritized actions** — Ranked list combining correlation-driven and individual finding actions

### On-Demand: AI Enhancement

`EnhanceWithAIStreamingAsync()` — Takes existing report, adds AI findings via SSE streaming, re-deduplicates, re-grades, re-synthesizes. Does not re-run deterministic rules.

---

## False Positive Prevention

Three mechanisms prevent DrWario from detecting its own work as performance issues:

### 1. Deferred Snapshot Capture

`SceneSnapshotTracker` detects triggers (spike, GC, periodic) on frame N but executes the expensive `FindObjectsByType`/`GetComponents` capture on frame N+1. This prevents DrWario's own allocations from inflating the spike frame's GC measurements.

### 2. Self-Overhead Subtraction

A `ProfilerRecorder` tracks the `DrWario.Sample` marker's time in nanoseconds. This value (which reports the previous frame's overhead, matching when FrameTimingManager also reports the previous frame) is subtracted from `CpuFrameTimeMs`.

### 3. Capture Frame Exclusion

`ProfilingSession.DrWarioCaptureFrames` stores frame numbers where DrWario performed expensive hierarchy captures. `GCAllocationRule` skips these frames entirely when counting GC spikes.

---

## Rule Reference

| RuleId | Class | Category | Configurable | Description |
|--------|-------|----------|-------------|-------------|
| `GC_SPIKE` | GCAllocationRule | Memory | Yes (threshold bytes) | GC allocation spikes per frame |
| `FRAME_DROP` | FrameDropRule | CPU | Yes (target ms) | Frame time vs target FPS, P95/P99 |
| `SLOW_BOOT` | BootStageRule | Boot | No | Individual stages >2s |
| `BOOT_FAILURE` | BootStageRule | Boot | No | Failed boot stages |
| `TOTAL_BOOT_TIME` | BootStageRule | Boot | No | Total boot >8s |
| `MEMORY_LEAK` | MemoryLeakRule | Memory | No | Heap growth via linear regression |
| `SLOW_ASSET_LOAD` | AssetLoadRule | Assets | No | Loads >500ms |
| `NETWORK_HEALTH` | NetworkLatencyRule | Network | No | Errors, latency >100ms, bandwidth |
| `RENDER_EFFICIENCY` | RenderingEfficiencyRule | Rendering | No | Draw calls, batching, SetPass |
| `CPU_GPU_BOTTLENECK` | CPUvsGPUBottleneckRule | CPU/Rendering | No | Bottleneck classification |
| `AI_*` | LLM output | Any | N/A | AI-generated findings |

---

## LLM Integration

### Provider Configuration

| Provider | Default Model | Endpoint | Auth |
|----------|--------------|----------|------|
| Claude | `claude-sonnet-4-6` | `https://api.anthropic.com/v1/messages` | `x-api-key` + `anthropic-version` |
| OpenAI | `gpt-4o` | `https://api.openai.com/v1/chat/completions` | Bearer token |
| Ollama | `llama3:70b` | `http://localhost:11434/api/chat` | None |
| Custom | `gpt-4o` | User-defined | Bearer token |

### Streaming (SSE)

Claude and OpenAI support Server-Sent Events streaming via `SseDownloadHandler` (custom `DownloadHandlerScript`). Findings are parsed progressively as JSON array elements arrive, enabling real-time UI updates. Ollama/Custom fall back to blocking request/response.

### Prompt Structure

**User prompt (JSON):** Session metadata, frame summary (avg/P95/P99), memory trajectory (12 points + regression), boot pipeline, asset loads, pre-analysis findings, editor context (baseline, windows), scene snapshots (baseline/final object counts, top instantiated objects, spike-frame diffs), profiler markers (top-N by inclusive time).

---

## EditorPrefs Keys

All prefixed with `DrWario_`:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DrWario_Provider` | int | 0 (Claude) | `LLMProvider` enum value |
| `DrWario_ApiKey_{Provider}` | string | `""` | XOR-obfuscated + Base64 |
| `DrWario_ModelId` | string | Provider default | Model identifier |
| `DrWario_Endpoint` | string | Provider default | Full API URL |
| `DrWario_Timeout` | int | 30 | HTTP timeout (seconds) |
| `DrWario_Enabled` | bool | false | Master AI toggle |
| `DrWario_AutoStart` | bool | false | Auto-start on Play Mode |
| `DrWario_Rule_{RuleId}_Enabled` | bool | true | Per-rule enable/disable |
| `DrWario_Rule_{RuleId}_Threshold` | float | Rule default | Per-rule threshold override |

---

## Known Issues

### Fixed (v1.1)

1. ~~**GcAllocBytes double-call**~~ — Fixed. Single `Profiler.GetTotalAllocatedMemoryLong()` call.
2. ~~**Rate limiter per-instance**~~ — Fixed. `_lastRequestTime` is now `static`.
3. ~~**CategoryGrades JSON export**~~ — Fixed. Serializable list wrapper.
4. ~~**TestConnectionAsync false positive**~~ — Fixed. Validates response content.
5. ~~**`Task.Wait()` blocks editor**~~ — Fixed. Fully async pipeline.

### Fixed (v2.0)

6. ~~**Basic profiling data**~~ — Fixed. ProfilerBridge with ProfilerRecorder counters.
7. ~~**No editor overhead adjustment**~~ — Fixed. EditorBaseline + confidence scoring.
8. ~~**DrWario inflating its own measurements**~~ — Fixed. Self-overhead subtraction + deferred capture + capture frame exclusion.

### Remaining Limitations

1. **GPU timing returns 0** on WebGL, some mobile, integrated graphics. No fallback.
2. **Hand-rolled JSON extraction** — `LLMClient.ExtractContent()` manually parses provider responses. Fragile.
3. **AdditionalContext is global mutable static** — Last `[InitializeOnLoad]` class wins.
4. **BootStageRule emits 3 RuleIds** — `SLOW_BOOT`, `BOOT_FAILURE`, `TOTAL_BOOT_TIME` from one class.
