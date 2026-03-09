# Data Model: Deep Profiler & Iterative Workflow

**Feature**: 002-deep-profiler-workflow
**Date**: 2026-03-08

---

## New Entities

### ProfilerMarkerSample

Aggregated profiler marker data captured per session via `ProfilerRecorder`.

| Field | Type | Description |
|-------|------|-------------|
| MarkerName | string | Unity profiler marker name (e.g., "Physics.Simulate", "Rendering") |
| AvgInclusiveTimeNs | long | Average inclusive time in nanoseconds across all frames |
| AvgExclusiveTimeNs | long | Average exclusive time in nanoseconds (inclusive minus child markers) |
| MaxInclusiveTimeNs | long | Peak inclusive time observed |
| AvgCallCount | float | Average calls per frame |
| SampleCount | int | Number of frames this marker was observed in |

**Storage**: In-memory on `ProfilingSession`. Not persisted.
**Relationships**: Belongs to `ProfilingSession` (one session has 0..N markers).
**Validation**: MarkerName must be non-null. Time values must be >= 0.

---

### TimelineEvent

A time-correlated event rendered on the timeline visualization.

| Field | Type | Description |
|-------|------|-------------|
| FrameIndex | int | Frame number (from ring buffer index, -1 for non-frame events) |
| Timestamp | float | Absolute time in seconds (from session start) |
| Duration | float | Duration in seconds (0 for point events) |
| EventType | TimelineEventType | Enum: FrameSpike, GCAlloc, BootStage, AssetLoad, NetworkEvent |
| Label | string | Display text (e.g., "67ms", "Physics.Init", "scene.bundle") |
| Metric | float | Primary metric value (CPU ms, GC bytes, load ms, etc.) |

**Enum TimelineEventType**: FrameSpike, GCAlloc, BootStage, AssetLoad, NetworkEvent

**Storage**: Computed on-demand from `ProfilingSession` data. Not persisted.
**Relationships**: Derived from `FrameSample[]`, `BootStageTiming[]`, `AssetLoadTiming[]`, `NetworkEvent[]`.
**Validation**: Timestamp must be >= 0. EventType must be valid enum value.

---

### ReportComparison

View model pairing two `DiagnosticReport` instances with computed deltas.

| Field | Type | Description |
|-------|------|-------------|
| ReportA | DiagnosticReport | The "before" report (older) |
| ReportB | DiagnosticReport | The "after" report (newer) |
| OverallGradeDelta | float | HealthScore B - HealthScore A (positive = improvement) |
| CategoryDeltas | Dictionary<string, GradeDelta> | Per-category grade change |
| FindingDiffs | List<FindingDiff> | Classified finding differences |
| MetricDeltas | MetricDelta | Key metric changes |

**Storage**: In-memory only. Computed on construction, never persisted.
**Relationships**: References two `DiagnosticReport` instances.

---

### GradeDelta

| Field | Type | Description |
|-------|------|-------------|
| Category | string | Category name |
| GradeA | char | Grade in Report A |
| GradeB | char | Grade in Report B |
| ScoreDelta | float | Score B - Score A |

---

### FindingDiff

Classification of a finding across two reports.

| Field | Type | Description |
|-------|------|-------------|
| Finding | DiagnosticFinding | The finding itself |
| Status | FindingDiffStatus | Fixed, New, or Persists |
| SeverityChange | int | Severity delta if Persists (0 = same, negative = improved) |
| MetricDelta | float | Metric change if Persists |

**Enum FindingDiffStatus**: Fixed, New, Persists

**Matching Rule**: Two findings match if `RuleId == RuleId && Category == Category`.
- **Fixed**: In Report A but not Report B
- **New**: In Report B but not Report A
- **Persists**: In both (compare severity and metric)

---

### MetricDelta

| Field | Type | Description |
|-------|------|-------------|
| AvgCpuTimeDelta | float | Change in average CPU frame time (ms) |
| P95CpuTimeDelta | float | Change in P95 CPU frame time (ms) |
| GcRateDelta | float | Change in GC allocation rate (bytes/frame) |
| MemorySlopeDelta | float | Change in memory growth slope |
| DrawCallsDelta | float | Change in average draw calls |
| HealthScoreDelta | float | Change in overall health score |

---

### RuleConfig

Per-rule configuration stored in EditorPrefs.

| Field | Type | Description |
|-------|------|-------------|
| RuleId | string | Matches `IAnalysisRule.RuleId` |
| Enabled | bool | Whether this rule runs during analysis (default: true) |
| Threshold | float | Custom threshold override (NaN = use rule default) |

**Storage**: EditorPrefs with keys `DrWario_Rule_{RuleId}_Enabled` (bool) and `DrWario_Rule_{RuleId}_Threshold` (float).
**Validation**: RuleId must match a registered rule. Threshold must be > 0 when set.

---

### SseDownloadHandler (internal)

Custom `DownloadHandlerScript` for streaming LLM responses.

| Field | Type | Description |
|-------|------|-------------|
| _buffer | StringBuilder | Accumulates raw SSE text |
| _provider | LLMProvider | Determines SSE parsing format |
| OnChunkReceived | Action<string> | Callback fired with accumulated content on each chunk |
| OnFindingParsed | Action<DiagnosticFinding> | Callback fired when a complete finding is parsed from stream |
| OnComplete | Action<string> | Callback with full content when stream ends |
| OnError | Action<string> | Callback on stream error |

**State Machine**: Tracks brace depth for progressive JSON array parsing. Emits findings as complete JSON objects are detected within the `[...]` array.

---

## Modified Entities

### FrameSample (existing)

**Add field**:

| Field | Type | Description |
|-------|------|-------------|
| FrameNumber | int | Absolute frame number from `Time.frameCount`. Used for Profiler frame navigation. |

### ProfilingSession (existing)

**Add fields/members**:

| Field | Type | Description |
|-------|------|-------------|
| _profilerMarkers | List<ProfilerMarkerSample> | Top N marker samples aggregated over session |
| ProfilerMarkers | IReadOnlyList<ProfilerMarkerSample> | Public read access |
| ProfilerWasRecording | bool | Whether Unity Profiler was recording during this session |

### DiagnosticFinding (existing)

Already has `FrameIndex`, `ScriptPath`, `ScriptLine`, `AssetPath` fields needed for jump-to-profiler and expandable cards.

**Add field**:

| Field | Type | Description |
|-------|------|-------------|
| AffectedFrames | int[] | Array of frame indices where this finding's condition was detected. Used by expandable cards for frame list and mini-chart. Null if not frame-specific. Capped at 100 entries. |

### DiagnosticReport (existing)

**Add fields** for summary metrics (needed by ReportComparison to compute MetricDelta without access to raw ProfilingSession):

| Field | Type | Description |
|-------|------|-------------|
| AvgCpuTimeMs | float | Average CPU frame time across session |
| P95CpuTimeMs | float | 95th percentile CPU frame time |
| P99CpuTimeMs | float | 99th percentile CPU frame time |
| AvgGcAllocBytes | float | Average GC allocation bytes per frame |
| AvgDrawCalls | float | Average draw calls per frame |
| MemorySlope | float | Linear regression slope of heap memory over time (bytes/frame) |

### LLMClient (existing)

**Add method**: `SendStreamingAsync()` — returns void, fires callbacks via `SseDownloadHandler`.

### LLMPromptBuilder (existing)

**Add section**: `BuildProfilerMarkersSection()` — generates "profilerMarkers" JSON section from `ProfilingSession.ProfilerMarkers`.

### AnalysisEngine (existing)

**Add method**: `AnalyzeStreamingAsync()` — yields findings progressively via callback.
**Modify**: `Analyze()` checks `RuleConfig.IsEnabled(rule.RuleId)` before running each rule.

### DrWarioView (existing)

**Add tabs**: Timeline tab.
**Add elements**: "Compare with..." button in History, "Export HTML" button, rule config UI in Settings, expandable finding cards.

---

## State Transitions

### Streaming Analysis

```
Idle → Analyzing (deterministic rules) → Streaming (LLM) → Complete
                                        ↓ (on error)
                                        Partial (keep findings so far)
```

### Report Comparison

```
History (list) → Select Report A → "Compare with..." → Select Report B → Comparison View
                                                                          ↓ (back)
                                                                          History (list)
```

### Timeline Interaction

```
No Data → Session Complete → Timeline Rendered
                              ↓ (zoom)
                              Zoomed (centered on cursor)
                              ↓ (pan)
                              Panned
                              ↓ (click event)
                              Event Detail Shown
```
