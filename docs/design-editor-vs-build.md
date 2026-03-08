# Design: Editor vs Build Context Awareness

## Problem

When profiling in Unity Editor Play Mode, measurements include editor overhead:
- **CPU time**: EditorLoop runs alongside PlayerLoop (Inspector repaints, Scene view, editor scripts)
- **GC allocations**: Editor UI, serialization, Undo system, Inspector reflection all allocate every frame
- **Draw calls / batches**: Scene view renders the scene separately from Game view, doubling rendering stats
- **Memory**: Editor loads metadata, thumbnails, import caches — inflates heap numbers

Current DrWario treats all measurements equally. A game running at solid 60fps in a build gets flagged with "GC spikes" and "high draw calls" that are entirely caused by the editor. The LLM has no way to know what's editor noise vs real game problems.

## Design Goals

1. **Measure editor overhead** so we can quantify it, not just guess
2. **Separate or annotate** editor contributions in analysis findings
3. **Adjust thresholds** — editor sessions get more lenient rules
4. **Inform the LLM** with full environment context so AI analysis is accurate
5. **Zero runtime cost in builds** — all editor awareness is `#if UNITY_EDITOR`

## Architecture

### Layer 1: EditorBaseline (Editor-only, pre-Play)

New static class `EditorBaselineCapture` captures idle editor overhead before Play Mode.

```
EditorApplication.update (30 frames, edit mode)
  └─> ProfilerBridge.Sample() each frame
  └─> Average into EditorBaseline struct
  └─> Store in static field, attached to session on Start()
```

**EditorBaseline struct** (in Runtime/FrameSample.cs):
```csharp
public struct EditorBaseline
{
    public float AvgCpuFrameTimeMs;     // Editor idle CPU overhead
    public float AvgRenderThreadMs;     // Editor idle render thread
    public long AvgGcAllocBytes;        // Editor idle GC per frame
    public int AvgGcAllocCount;         // Editor idle allocation count
    public int AvgDrawCalls;            // Editor idle draw calls (Scene view, etc.)
    public int AvgBatches;              // Editor idle batches
    public int AvgSetPassCalls;         // Editor idle SetPass calls
    public int SampleCount;             // Frames sampled (target: 30)
    public bool IsValid;                // True if baseline was captured
}
```

**Why pre-Play?** During Play Mode, editor overhead mixes with game workload — impossible to separate. Idle editor (before Play) gives us the editor's own floor.

**Limitations:** Editor overhead during Play Mode can differ from idle (e.g., Inspector showing component values updates more). But idle baseline is a conservative estimate. We document this in findings.

### Layer 2: EnvironmentContext in SessionMetadata

Extend `SessionMetadata`:
```csharp
public struct SessionMetadata
{
    // ... existing fields ...

    // Environment context (new)
    public bool IsEditor;               // Application.isEditor
    public bool IsDevelopmentBuild;      // Debug.isDebugBuild
    public EditorBaseline Baseline;     // Editor overhead baseline (#if UNITY_EDITOR only populated)

    // Editor window state (new, editor-only)
    public bool SceneViewOpen;          // Scene view adds draw calls + rendering
    public bool InspectorOpen;          // Inspector adds GC from serialization
    public bool ProfilerOpen;           // Profiler itself has overhead
    public int GameViewCount;           // Multiple game views = multiple renders
}
```

**Populated in** `ProfilingSession.Start()` via `#if UNITY_EDITOR` block:
- `EditorWindow.HasOpenInstances<T>()` detects open windows
- `SceneView.sceneViews.Count` for scene view detection
- Baseline attached from `EditorBaselineCapture.LastBaseline`

### Layer 3: Adjusted Analysis Rules

Rules use session environment context to adjust behavior.

#### 3a. Threshold Multipliers

Each rule applies a multiplier when `session.Metadata.IsEditor`:

| Rule | Build Threshold | Editor Multiplier | Rationale |
|------|----------------|-------------------|-----------|
| FrameDropRule | target FPS ms | ×1.5 | Editor adds ~3-5ms CPU overhead |
| GCAllocationRule | 1KB/frame | +baseline GC bytes | Editor GC is constant overhead |
| RenderingEfficiencyRule | 1000 draw calls | +baseline draw calls | Scene view draw calls |
| CPUvsGPUBottleneckRule | target FPS ms | ×1.3 | Both CPU and render thread inflated |
| MemoryLeakRule | 1 MB/s slope | (no change) | Slope is relative, editor overhead is constant |

Strategy per metric:
- **Additive subtraction** for GC and draw calls (editor adds a fixed floor)
- **Multiplicative relaxation** for timing (editor overhead scales with scene complexity)
- **No change** for relative metrics (slopes, ratios)

#### 3b. Confidence Field on DiagnosticFinding

```csharp
public struct DiagnosticFinding
{
    // ... existing fields ...
    public Confidence Confidence;       // New: how reliable is this finding
    public string EnvironmentNote;      // New: optional context about environment impact
}

public enum Confidence
{
    High,       // Would appear in build too (metric >> threshold even after baseline subtraction)
    Medium,     // Likely real but editor overhead may contribute
    Low         // May be entirely caused by editor overhead
}
```

**Classification logic:**
```
adjustedMetric = rawMetric - baselineContribution

if adjustedMetric > threshold * 2.0:   Confidence.High
if adjustedMetric > threshold:         Confidence.Medium
if rawMetric > threshold:              Confidence.Low (only over threshold because of editor)
else:                                  No finding
```

Findings with `Confidence.Low` include an `EnvironmentNote`:
> "This may be caused by editor overhead. Scene view was open, adding ~{N} draw calls. Verify in a build."

#### 3c. Rule Implementation Pattern

```csharp
// Inside any rule's Analyze() method:
bool isEditor = session.Metadata.IsEditor;
var baseline = session.Metadata.Baseline;

// Adjust raw measurements
float effectiveGcPerFrame = avgGcPerFrame;
if (isEditor && baseline.IsValid)
    effectiveGcPerFrame = Math.Max(0, avgGcPerFrame - baseline.AvgGcAllocBytes);

// Classify confidence
var confidence = Confidence.High;
if (isEditor)
{
    if (effectiveGcPerFrame <= SpikeThresholdBytes && avgGcPerFrame > SpikeThresholdBytes)
        confidence = Confidence.Low;
    else if (isEditor)
        confidence = Confidence.Medium;
}
```

### Layer 4: LLM Prompt Enhancement

Add environment context block to `LLMPromptBuilder.BuildUserPrompt()`:

```json
{
  "environment": {
    "isEditor": true,
    "isDevelopmentBuild": false,
    "editorWindows": {
      "sceneViewOpen": true,
      "inspectorOpen": true,
      "profilerOpen": false,
      "gameViewCount": 1
    },
    "editorBaseline": {
      "avgCpuFrameTimeMs": 4.2,
      "avgGcAllocBytes": 3200,
      "avgGcAllocCount": 12,
      "avgDrawCalls": 45,
      "avgBatches": 38,
      "isValid": true
    },
    "note": "Data captured in Unity Editor Play Mode. Editor overhead (Scene view, Inspector, etc.) is included in measurements. The baseline above was measured during editor idle before Play Mode. Subtract baseline values for approximate game-only metrics."
  }
}
```

Also update the system prompt with guidance:

```
IMPORTANT: When session data is from the Unity Editor (environment.isEditor=true), editor overhead
inflates all metrics. The editorBaseline provides idle editor overhead measured before Play Mode.
Use these to estimate actual game performance:
- CPU time: subtract ~baseline.avgCpuFrameTimeMs from measured values
- GC allocations: subtract ~baseline.avgGcAllocBytes per frame
- Draw calls: subtract ~baseline.avgDrawCalls (especially if Scene view is open)
- Memory totals: editor uses significant memory for metadata/caches — don't alarm on absolute values
When findings have Confidence=Low, flag them as "may be editor overhead" rather than definitive issues.
Recommend the user verify critical findings in a development build.
```

## Implementation Plan

### Phase 1: Data Model (structs + metadata)
1. Add `EditorBaseline` struct to `FrameSample.cs`
2. Add `Confidence` enum + new fields to `DiagnosticFinding`
3. Extend `SessionMetadata` with environment fields
4. Update `ProfilingSession.Start()` to populate environment context

### Phase 2: Baseline Capture
1. Create `Editor/EditorBaselineCapture.cs` — static class, captures 30 frames via `EditorApplication.update`
2. Hook into `DrWarioPlayModeHook` — trigger baseline capture before play, attach to session on start
3. Use existing `ProfilerBridge` for sampling (start briefly in edit mode, dispose after capture)

### Phase 3: Rule Adjustments
1. Add helper: `AnalysisHelpers.AdjustForEditor(rawMetric, baselineMetric, session)` → returns adjusted metric + confidence
2. Update each rule to use adjusted metrics and set Confidence/EnvironmentNote
3. Rules that can't meaningfully adjust (MemoryLeakRule slope) just set `Confidence.Medium` in editor

### Phase 4: LLM Integration
1. Add `environment` block to `LLMPromptBuilder.BuildUserPrompt()`
2. Add editor-awareness guidance to system prompts
3. Include `Confidence` in pre-analysis findings sent to LLM

### Phase 5: UI
1. Show confidence badges on finding cards (High=solid, Medium=dashed border, Low=dimmed)
2. Add "Editor Session" banner in Summary tab with baseline stats
3. Add tooltip: "Metrics adjusted for editor overhead. Verify critical findings in a build."

## Edge Cases

**Baseline not captured** (`IsValid=false`): Rules behave exactly as today (no adjustment). LLM prompt notes "no baseline available."

**Scene view closed during play but was open during baseline**: Baseline overestimates. Acceptable — we'd rather under-report than over-report editor issues.

**Multiple Game views**: Each additional Game view roughly doubles rendering stats. `GameViewCount > 1` triggers a specific note.

**IL2CPP vs Mono**: Build-only consideration. We tag `Metadata.Platform` but don't need special handling — the point is that editor sessions are inherently Mono.

**Profiler window open during session**: The Profiler itself adds CPU overhead and GC. We detect it and note it, but don't try to subtract it (too variable).

## What This Does NOT Do

- **Strip editor overhead from raw FrameSample data**: The ring buffer stores raw measurements. Adjustment happens only at analysis time. This preserves data integrity.
- **Profile actual builds**: DrWario runs in-editor. For build profiling, users should use Unity's Profiler connected to a build. We can add "connect to player" support later.
- **Perfectly isolate game performance**: Editor overhead varies with scene complexity, Inspector selection, etc. The baseline is an estimate, not exact. We're transparent about this.

## File Changes Summary

| File | Change |
|------|--------|
| `Runtime/FrameSample.cs` | Add `EditorBaseline` struct, extend `SessionMetadata` |
| `Runtime/ProfilingSession.cs` | Populate environment fields in `Start()` |
| `Editor/Analysis/DiagnosticFinding.cs` | Add `Confidence` enum, `Confidence` + `EnvironmentNote` fields |
| `Editor/EditorBaselineCapture.cs` | **New** — captures idle editor overhead |
| `Editor/DrWarioPlayModeHook.cs` | Trigger baseline capture before play |
| `Editor/Analysis/Rules/*.cs` | Adjust thresholds, set confidence, add environment notes |
| `Editor/Analysis/LLM/LLMPromptBuilder.cs` | Add environment block + system prompt guidance |
| `Editor/DrWarioView.cs` | Confidence badges, editor session banner |
