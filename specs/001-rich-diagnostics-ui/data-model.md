# Data Model: Rich Diagnostics UI

**Feature**: 001-rich-diagnostics-ui | **Date**: 2026-03-08

## Modified Entities

### FrameSample (Runtime/FrameSample.cs)

**Existing fields** (15): Timestamp, DeltaTime, CpuFrameTimeMs, GpuFrameTimeMs, RenderThreadMs, GcAllocBytes, TotalHeapBytes, TextureMemoryBytes, MeshMemoryBytes, GcAllocCount, DrawCalls, Batches, SetPassCalls, Triangles, Vertices

**New fields** (8):

| Field | Type | Source | Default |
|-------|------|--------|---------|
| PhysicsActiveBodies | int | ProfilerRecorder | 0 |
| PhysicsContacts | int | ProfilerRecorder | 0 |
| AudioVoiceCount | int | ProfilerRecorder | 0 |
| AudioDSPLoad | float | ProfilerRecorder | 0f |
| AnimatorCount | int | ProfilerRecorder | 0 |
| UICanvasRebuilds | int | ProfilerRecorder | 0 |
| UILayoutRebuilds | int | ProfilerRecorder | 0 |
| PhysicsKinematicBodies | int | ProfilerRecorder | 0 |

### DiagnosticFinding (Editor/Analysis/DiagnosticFinding.cs)

**Existing fields** (11): RuleId, Category, Severity, Title, Description, Recommendation, Metric, Threshold, FrameIndex, Confidence, EnvironmentNote

**New fields** (3):

| Field | Type | Purpose | Default |
|-------|------|---------|---------|
| ScriptPath | string | Relative path to source file (e.g., "Assets/Scripts/Player.cs") | null |
| ScriptLine | int | Line number in the source file | 0 |
| AssetPath | string | Relative path to Unity asset (e.g., "Assets/Textures/Big.png") | null |

### ProfilingSession (Runtime/ProfilingSession.cs)

**New field**:

| Field | Type | Purpose |
|-------|------|---------|
| SceneCensus | SceneCensus | Snapshot of scene composition at session start |

---

## New Entities

### SceneCensus (Runtime/SceneCensus.cs)

Captures a snapshot of the active scene's composition at the start of a profiling session.

| Field | Type | Description |
|-------|------|-------------|
| TotalGameObjects | int | Active GameObjects in scene |
| TotalComponents | int | Total component instances |
| ComponentDistribution | ComponentCount[] | Top 20 component types by count |
| DirectionalLights | int | Directional light count |
| PointLights | int | Point light count |
| SpotLights | int | Spot light count |
| AreaLights | int | Area light count |
| CanvasCount | int | UI Canvas count |
| CameraCount | int | Camera count |
| ParticleSystemCount | int | ParticleSystem count |
| LODGroupCount | int | LODGroup count |
| RigidbodyCount | int | Rigidbody (3D) count |
| Rigidbody2DCount | int | Rigidbody2D count |
| IsValid | bool | Whether census was successfully captured |

### ComponentCount (Runtime/SceneCensus.cs)

| Field | Type | Description |
|-------|------|-------------|
| TypeName | string | Component type name (e.g., "MeshRenderer") |
| Count | int | Number of instances in scene |

---

## Extended ProfilerBridge Markers

**New markers added to ProfilerBridge** (8 new, 12 existing = 20 total):

| Category | Marker | Field | Type |
|----------|--------|-------|------|
| Physics | `Physics.ActiveDynamicBodies` | PhysicsActiveBodies | int |
| Physics | `Physics.ActiveKinematicBodies` | PhysicsKinematicBodies | int |
| Physics | `Physics.Contacts` | PhysicsContacts | int |
| Audio | `AudioManager.ActiveVoiceCount` | AudioVoiceCount | int |
| Audio | `Audio.DSPLoad` | AudioDSPLoad | float |
| Animation | `Animators.Count` | AnimatorCount | int |
| UI | `UI.CanvasRebuildCount` | UICanvasRebuilds | int |
| UI | `UI.LayoutRebuildCount` | UILayoutRebuilds | int |

---

## LLM Response Schema Extension

**FindingJson struct** (LLMResponseParser.cs):

| Field | Type | Required | New |
|-------|------|----------|-----|
| ruleId | string | yes | no |
| category | string | yes | no |
| severity | string | yes | no |
| title | string | yes | no |
| description | string | yes | no |
| recommendation | string | yes | no |
| metric | float | yes | no |
| threshold | float | yes | no |
| scriptPath | string | optional | **yes** |
| scriptLine | int | optional | **yes** |
| assetPath | string | optional | **yes** |

---

## Chart Data Models (Editor-only, in-memory)

These are internal to the chart components, not persisted:

### LineChartData
- `float[] Values` — Y-axis values
- `float[] TimeOffsets` — X-axis time points
- `string Label` — series name
- `Color Color` — line color

### BarChartData
- `float[] Values` — bar heights
- `string[] Labels` — bar labels (optional)
- `Color Color` — bar color
- `float HighlightThreshold` — values above this are highlighted red

### HistogramData
- `float[] RawValues` — source data to bucket
- `int BucketCount` — number of histogram bins
- `float MinRange` / `float MaxRange` — axis range
