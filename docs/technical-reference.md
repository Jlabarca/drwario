# DrWario Technical Reference

> Deep architecture and implementation details for contributors and integrators.

## Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                    Unity Editor                          │
│                                                          │
│  DrWarioWindow ─── DrWarioView (6 tabs)                  │
│                        │                                 │
│                   AnalysisEngine                         │
│                   ┌────┴────┐                            │
│                   │         │                            │
│            Phase 1: Rules   Phase 2: AI                  │
│            ┌──────┴──────┐  ┌──────┴──────┐              │
│            │GCAllocation │  │AIAnalysisRule│              │
│            │FrameDrop    │  │  ├─ LLMPromptBuilder       │
│            │BootStage    │  │  ├─ LLMClient              │
│            │MemoryLeak   │  │  └─ LLMResponseParser      │
│            │AssetLoad    │  └─────────────┘              │
│            │NetworkLat.  │                               │
│            └─────────────┘                               │
│                   │                                      │
│            Phase 3: Deduplicate + Grade                  │
│                   │                                      │
│            DiagnosticReport ──── ReportHistory            │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                    Runtime                               │
│                                                          │
│  RuntimeCollector (MonoBehaviour singleton)               │
│       │                                                  │
│       └── ProfilingSession (ring buffer 3600)            │
│            ├── FrameSample[3600]                          │
│            ├── List<BootStageTiming>                      │
│            ├── List<AssetLoadTiming>                      │
│            └── List<NetworkEvent>                         │
│                                                          │
│  BootTimingHook (static callback facade)                 │
│                                                          │
│  #if UNITY_EDITOR || DEVELOPMENT_BUILD                   │
└──────────────────────────────────────────────────────────┘
```

---

## Data Structures

### FrameSample (struct, 48 bytes)

| Field | Type | Bytes | Source |
|-------|------|-------|--------|
| `Timestamp` | float | 4 | `Time.realtimeSinceStartup` |
| `DeltaTime` | float | 4 | `Time.unscaledDeltaTime` |
| `CpuFrameTimeMs` | float | 4 | `FrameTimingManager` or `deltaTime * 1000` |
| `GpuFrameTimeMs` | float | 4 | `FrameTimingManager` (0 on some platforms) |
| `GcAllocBytes` | long | 8 | Delta of `Profiler.GetTotalAllocatedMemoryLong()` |
| `TotalHeapBytes` | long | 8 | `Profiler.GetTotalAllocatedMemoryLong()` |
| `TextureMemoryBytes` | long | 8 | `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| `MeshMemoryBytes` | long | 8 | `TotalReservedMemory - TotalAllocated` (approx) |

### BootStageTiming (struct)

| Field | Type | Description |
|-------|------|-------------|
| `StageName` | string | Name of the boot stage |
| `DurationMs` | long | Stage duration in milliseconds |
| `Success` | bool | Whether the stage completed successfully |

### AssetLoadTiming (struct)

| Field | Type | Description |
|-------|------|-------------|
| `AssetKey` | string | Asset identifier / path |
| `DurationMs` | long | Load duration in milliseconds |
| `SizeBytes` | long | Asset size (0 if unknown) |

### NetworkEvent (struct)

| Field | Type | Description |
|-------|------|-------------|
| `Timestamp` | float | `Time.realtimeSinceStartup` |
| `Type` | NetworkEventType | `Send`, `Receive`, or `Error` |
| `Bytes` | int | Packet size |
| `LatencyMs` | float | Round-trip for sends, 0 for receives |

### SessionMetadata (struct)

| Field | Type | Description |
|-------|------|-------------|
| `StartTime` | DateTime | UTC session start |
| `EndTime` | DateTime | UTC session end |
| `UnityVersion` | string | `Application.unityVersion` |
| `Platform` | string | `Application.platform.ToString()` |
| `TargetFrameRate` | int | `Application.targetFrameRate` |
| `ScreenWidth` | int | `Screen.width` |
| `ScreenHeight` | int | `Screen.height` |

### DiagnosticFinding (struct)

| Field | Type | Description |
|-------|------|-------------|
| `RuleId` | string | Unique rule identifier (e.g., `GC_SPIKE`, `AI_CORR_01`) |
| `Category` | string | `CPU`, `Memory`, `Boot`, `Assets`, `Network`, `General` |
| `Severity` | Severity | `Info`, `Warning`, `Critical` |
| `Title` | string | Short summary |
| `Description` | string | Detailed explanation with data references |
| `Recommendation` | string | Actionable fix |
| `Metric` | float | The measured value |
| `Threshold` | float | The reference threshold |
| `FrameIndex` | int | Frame index (-1 if not frame-specific) |

---

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `DrWario.Runtime` | FrameSample, ProfilingSession, RuntimeCollector, BootTimingHook, NetworkEventType, SessionMetadata |
| `DrWario.Editor` | DrWarioView, DrWarioWindow, DrWarioPlayModeHook |
| `DrWario.Editor.Analysis` | AnalysisEngine, DiagnosticReport, DiagnosticFinding, IAnalysisRule, ReportHistory, Severity |
| `DrWario.Editor.Analysis.Rules` | GCAllocationRule, FrameDropRule, BootStageRule, MemoryLeakRule, AssetLoadRule, NetworkLatencyRule, AIAnalysisRule |
| `DrWario.Editor.Analysis.LLM` | LLMConfig, LLMClient, LLMPromptBuilder, LLMResponseParser, LLMProvider, LLMResponse |

---

## Runtime Pipeline

### Ring Buffer (ProfilingSession)

- **Capacity:** 3600 frames (~60s at 60fps)
- **Memory:** ~168KB fixed allocation (3600 × 48 bytes)
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

Event lists (`_bootStages`, `_assetLoads`, `_networkEvents`) are unbounded `List<T>`.

### RuntimeCollector — Per-Frame Hot Path

```csharp
// Zero-allocation hot path:
FrameTimingManager.CaptureFrameTimings();             // native call
uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings); // reuses FrameTiming[1]

long totalHeap = Profiler.GetTotalAllocatedMemoryLong();
long gcDelta = totalHeap - _prevTotalGcAlloc;         // heap growth since last frame
if (gcDelta < 0) gcDelta = 0;                         // handle GC collection resets

var sample = new FrameSample { ... };                  // stack-allocated struct
ActiveSession.RecordFrame(sample);                     // struct copy to buffer
```

### Singleton Lifecycle

```
Ensure() → creates hidden DontDestroyOnLoad("[DrWario] RuntimeCollector")
StartSession() → new ProfilingSession → Start() → fires OnSessionStarted
Update() → samples frame data into ring buffer
StopSession() → session.Stop() → disables Update()
DestroyCollector() → Destroy(gameObject)
```

---

## Analysis Engine — 3-Phase Pipeline

### Phase 1: Deterministic Rules

All 6 rules run sequentially. Each receives the full `ProfilingSession` and returns `List<DiagnosticFinding>`.

### Phase 2: AI Analysis (Optional)

`AIAnalysisRule` receives Phase 1 findings as context, then:
1. `LLMPromptBuilder.BuildSystemPrompt()` — expert Unity analyst instructions + `AdditionalContext`
2. `LLMPromptBuilder.BuildUserPrompt()` — structured JSON with session stats, memory trajectory (12 downsampled points + linear regression), boot pipeline, asset loads, pre-analysis findings
3. `LLMClient.SendAsync()` — HTTP POST to provider (blocks via `Task.Wait()`)
4. `LLMResponseParser.Parse()` — JSON array → `DiagnosticFinding` list

### Phase 3: Deduplication + Grading

AI findings (prefixed `AI_`) get priority. Dedup key = `category + normalized title` (stripped of parenthetical data, lowercased). Then `ComputeGrades()` applies the scoring formula.

---

## Rule Algorithms Detail

### GCAllocationRule

```
For each frame: if GcAllocBytes > 1024 → spike
spikeRatio = spikeCount / frameCount
Severity: Critical if ratio > 20%, Warning if count > 10, else Info
Metric: spikeCount | Threshold: 1024
```

### FrameDropRule

```
targetMs = 1000 / TargetFrameRate (default 16.67ms)
Sort all CPU times ascending
dropCount = frames > targetMs
severeCount = frames > 50ms
P95 = sorted[(int)(length * 0.95)]
P99 = sorted[(int)(length * 0.99)]
Severity: Critical if severeCount > 5, Warning if dropRatio > 10%, else Info
Metric: P95 | Threshold: targetMs
```

### MemoryLeakRule

```
Linear regression: slope = (n·ΣXY - ΣX·ΣY) / (n·ΣX² - (ΣX)²) bytes/sec
Requires ≥60 frames
Warning: slope > 1 MB/s | Critical: slope > 5 MB/s
Metric: slope (bytes/sec) | Threshold: 1MB/s
```

### BootStageRule

```
Per-stage: Warning if durationMs > 2000, Critical if > 4000
Failed stages: Critical (RuleId: BOOT_FAILURE)
Total boot: Warning if > 8000ms, Critical if > 16000ms (RuleId: TOTAL_BOOT_TIME)
```

### AssetLoadRule

```
slowLoads = loads where DurationMs > 500
Critical: any load > 2000ms | Warning: >3 slow loads | Info: any slow
Metric: max duration | Threshold: 500ms
```

### NetworkLatencyRule (3 independent checks)

```
1. Errors: Warning if any, Critical if errorRate > 5%
2. Latency: filters receives with LatencyMs > 0
   Critical if max > 250ms, Warning if >25% exceed 100ms
3. Throughput: Info if > 100 KB/s, Warning if > 500 KB/s
```

---

## LLM Integration

### Provider Configuration

| Provider | Default Model | Endpoint | Auth |
|----------|--------------|----------|------|
| Claude | `claude-sonnet-4-6` | `https://api.anthropic.com/v1/messages` | `x-api-key` + `anthropic-version: 2023-06-01` |
| OpenAI | `gpt-4o` | `https://api.openai.com/v1/chat/completions` | `Authorization: Bearer <key>` |
| Ollama | `llama3:70b` | `http://localhost:11434/api/chat` | None |
| Custom | `gpt-4o` | User-defined | `Authorization: Bearer <key>` |

### Request Body Differences

| Provider | System prompt location | Extra fields |
|----------|----------------------|--------------|
| Claude | Top-level `"system"` | No `temperature` |
| OpenAI/Custom | `messages[0].role = "system"` | `"temperature": 0.3` |
| Ollama | Same as OpenAI | `"stream": false` |

### Prompt Structure

**System prompt:**
- Expert Unity performance analyst role
- JSON array output format with exact field names
- Focus: cross-metric correlations, platform-specific issues, Unity patterns
- Optional `AdditionalContext` from framework integration

**User prompt (JSON object):**
- `session` — metadata
- `frameSummary` — CPU/GPU stats (avg/P50/P95/P99/max/min), GC stats, drop counts
- `memoryTrajectory` — 12 downsampled points + OLS regression (`heapSlopeBytePerSec`, `heapSlopeMBPerMin`)
- `bootPipeline` — stage list with durations and success flags
- `assetLoads` — count/avg/max/total + top 10 slowest >500ms
- `preAnalysis` — Phase 1 findings as compact JSON

### Response Parsing

1. Strip markdown code fences if present
2. Extract content between outermost `[` and `]`
3. Wrap as `{"items": <array>}` for `JsonUtility` compatibility
4. Parse into `FindingJson[]` → map to `DiagnosticFinding` list
5. On failure: emit `AI_PARSE_ERROR` Info finding

### API Key Storage

XOR obfuscation with `SystemInfo.deviceUniqueIdentifier` as key, then Base64 encoded. Stored per-provider in EditorPrefs (`DrWario_ApiKey_Claude`, `DrWario_ApiKey_OpenAI`, etc.). **Not encryption** — prevents plaintext in registry.

---

## EditorPrefs Keys

All prefixed with `DrWario_`:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DrWario_Provider` | int | 0 (Claude) | `LLMProvider` enum value |
| `DrWario_ApiKey_Claude` | string | `""` | XOR-obfuscated + Base64 |
| `DrWario_ApiKey_OpenAI` | string | `""` | XOR-obfuscated + Base64 |
| `DrWario_ApiKey_Custom` | string | `""` | XOR-obfuscated + Base64 |
| `DrWario_ModelId` | string | Provider default | Model identifier |
| `DrWario_Endpoint` | string | Provider default | Full API URL |
| `DrWario_Timeout` | int | 30 | HTTP timeout (seconds) |
| `DrWario_Enabled` | bool | false | Master AI toggle |
| `DrWario_AutoStart` | bool | false | Auto-start on Play Mode |

---

## RuleId Reference

| RuleId | Class | Category | Severity Range |
|--------|-------|----------|---------------|
| `GC_SPIKE` | GCAllocationRule | Memory | Info → Critical |
| `FRAME_DROP` | FrameDropRule | CPU | Info → Critical |
| `SLOW_BOOT` | BootStageRule | Boot | Warning → Critical |
| `BOOT_FAILURE` | BootStageRule | Boot | Critical |
| `TOTAL_BOOT_TIME` | BootStageRule | Boot | Warning → Critical |
| `MEMORY_LEAK` | MemoryLeakRule | Memory | Warning → Critical |
| `SLOW_ASSET_LOAD` | AssetLoadRule | Assets | Info → Critical |
| `NETWORK_HEALTH` | NetworkLatencyRule | Network | Info → Critical |
| `AI_*` | LLM output | Any | LLM-determined |
| `AI_UNKNOWN` | LLM (null ruleId) | General | LLM-determined |
| `AI_PARSE_ERROR` | LLMResponseParser | General | Info |

---

## Performance Characteristics

| Aspect | Detail |
|--------|--------|
| Frame buffer memory | ~168KB (3600 × 48B structs, allocated once) |
| Per-frame overhead | Zero-allocation: FrameTimingManager + 3 Profiler API calls + struct copy |
| GetFrames() | Allocates `FrameSample[frameCount]` per call — avoid in hot paths |
| Analysis time | Rules: instant. AI: 2–30s depending on provider/network |
| Report storage | JSON in `Library/DrWarioReports/`, auto-prunes to 50 reports |
| Release builds | Zero footprint — `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` strips all |

---

## Known Issues

### Bugs

1. **GcAllocBytes double-call** — `RuntimeCollector.cs:85-88` calls `Profiler.GetTotalAllocatedMemoryLong()` twice identically. Second call is wasted. `GcAllocBytes` measures heap growth delta, not GC allocation events. Field name is misleading.

2. **Rate limiter is per-instance** — `LLMClient._lastRequestTime` is an instance field. New `LLMClient` per analysis call = rate limiter never triggers between runs. Should be `static`.

3. **GetFrames() called 4x in BuildUserPrompt** — `LLMPromptBuilder.cs` calls `session.GetFrames()` four separate times during prompt construction (~504KB ephemeral allocation). Should cache.

4. **CategoryGrades missing from JSON export** — `Dictionary<string, char>` not serializable by `JsonUtility`. Grades computed but lost on export.

5. **TestConnectionAsync false positive** — Returns `true` on any non-error HTTP, even if body contains a model error (e.g., Ollama model-not-found with HTTP 200).

### Architectural Limitations

1. **`Task.Wait()` blocks editor** — `AIAnalysisRule.Analyze()` blocks main thread up to 30s. No cancel, no progress bar. Fix: make `IAnalysisRule` async.
2. **GPU timing returns 0** on WebGL, some mobile, integrated graphics. No fallback or sentinel.
3. **Hand-rolled JSON extraction** — `LLMClient.ExtractContent()` manually parses provider responses instead of using a JSON library. Fragile against format changes.
4. **AdditionalContext is global mutable static** — Last `[InitializeOnLoad]` class wins. No multi-framework support.
5. **BootStageRule emits 3 RuleIds** — `SLOW_BOOT`, `BOOT_FAILURE`, `TOTAL_BOOT_TIME` from one class. Breaks assumption that `RuleId` property identifies all findings.
