# Research: Rich Diagnostics UI

**Feature**: 001-rich-diagnostics-ui | **Date**: 2026-03-08

## R1: ProfilerRecorder Marker Names for Extended Counters

**Decision**: Use the following stable ProfilerRecorder marker names (available in Unity 2022.3+):

| Subsystem | Marker Name | Category | Returns |
|-----------|-------------|----------|---------|
| Physics | `Physics.ActiveDynamicBodies` | `ProfilerCategory.Physics` | int |
| Physics | `Physics.ActiveKinematicBodies` | `ProfilerCategory.Physics` | int |
| Physics | `Physics.Contacts` | `ProfilerCategory.Physics` | int |
| Audio | `AudioManager.ActiveVoiceCount` | `ProfilerCategory.Audio` | int |
| Audio | `Audio.DSPLoad` | `ProfilerCategory.Audio` | float (%) |
| Animation | `Animators.Count` | `ProfilerCategory.Animation` | int |
| UI | `UI.CanvasRebuildCount` | `ProfilerCategory.UI` | int |
| UI | `UI.LayoutRebuildCount` | `ProfilerCategory.UI` | int |

**Rationale**: These are public, stable markers documented in Unity's ProfilerRecorder API. They don't require internal access. Physics markers exist in both PhysX (3D) and Box2D (2D) variants — we use the 3D names with graceful fallback.

**Alternatives considered**:
- Deep Profiler markers (e.g., `Physics.Simulate`) — too fine-grained, adds overhead
- Reflection into internal profiler categories — fragile, version-dependent
- Manual component counting per frame — allocates, slow for large scenes

**Risk**: Some markers may return 0 on platforms without the subsystem active. Mitigation: gracefully skip counters that return 0 consistently (same pattern as existing GPU time fallback).

---

## R2: Chart Rendering Approach in Unity UIToolkit

**Decision**: Use `Painter2D` via `VisualElement.generateVisualContent` (same approach as existing sparkline).

**Rationale**: Already proven in DrWario's sparkline implementation. Painter2D provides `BeginPath()`, `MoveTo()`, `LineTo()`, `Arc()`, `Fill()`, `Stroke()` — sufficient for line charts, bar charts, and histograms. No external dependencies.

**Design patterns**:
- Base `ChartElement` class extends `VisualElement`, handles coordinate mapping and mouse hover
- Each chart type (LineChart, BarChart, Histogram) extends ChartElement
- Tooltip implemented as a positioned `Label` element shown/hidden on `MouseMoveEvent`
- Data passed as simple arrays (float[], not complex models) to minimize allocations

**Alternatives considered**:
- IMGUI (`OnGUI`) — inconsistent styling with UIToolkit, legacy API
- External charting library (e.g., XCharts) — adds dependency, violates "no external deps" constraint
- USS + UXML layouts — project convention is pure C# VisualElement, no USS/UXML
- `GL.Begin`/`GL.End` immediate mode — harder to integrate with UIToolkit event system

---

## R3: MultiColumnListView for Data Tables

**Decision**: Use `MultiColumnListView` (available since Unity 2021.2, stable in 2022.3+).

**Rationale**: Built-in UIToolkit control that supports:
- Column definitions with title, width, sortable flag
- `bindItem` callback for custom cell rendering
- `itemsSource` binding to `IList`
- Column header click sorting via `columnSortingChanged` event

**Implementation pattern**:
```
DataTableBuilder.Create<T>(
    columns: Column[],
    data: IList<T>,
    bindCell: Action<VisualElement, int, Column>
) → MultiColumnListView
```

Sort handling: maintain a `Comparison<T>` delegate per column, re-sort `itemsSource` on header click, call `RefreshItems()`.

**Alternatives considered**:
- `ListView` with custom row layout — no built-in column sorting
- `ScrollView` with manual grid — too much boilerplate
- IMGUI `EditorGUILayout` tables — inconsistent with UIToolkit UI

---

## R4: Opening Scripts and Pinging Assets

**Decision**: Use Unity's built-in APIs:
- **Open script at line**: `UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, line)` or `AssetDatabase.OpenAsset(obj, line)`
- **Ping asset**: `EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path))`

**Rationale**: These are stable public APIs available since Unity 5.x. `OpenFileAtLineExternal` respects the user's configured external editor (VS Code, Rider, Visual Studio). `PingObject` highlights the asset in the Project window with the standard yellow flash.

**For clickable links in UI**: Use `Button` or `Label` with `Clickable` manipulator. Style with underline + blue color to indicate interactivity.

**Alternatives considered**:
- `Process.Start` with IDE-specific arguments — fragile, IDE-dependent
- `EditorUtility.RevealInFinder` — shows in file explorer, not IDE
- Custom protocol handler — overkill, non-standard

---

## R5: Scene Census Performance

**Decision**: Use `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` (Unity 2022.3+ API, faster than deprecated `FindObjectsOfType`).

**Rationale**: `FindObjectsByType` with `None` sort mode is optimized for enumeration without sorting overhead. For a 5,000-object scene, expected completion time is <100ms.

**Census scope**:
- `GameObject` count (total active)
- `Component` type distribution (top 20 by count)
- `Light` count by type (Directional, Point, Spot, Area)
- `Canvas` count (world vs overlay)
- `Camera` count
- `ParticleSystem` count
- `LODGroup` count
- `Rigidbody` + `Rigidbody2D` count

**Timing**: Captured once on session start (not per frame). Stored in `ProfilingSession.SceneCensus`.

**Alternatives considered**:
- Scene hierarchy traversal via `Transform` — slower for deep hierarchies
- `SceneManager.GetActiveScene().GetRootGameObjects()` + recursive — more code, same performance
- Per-frame census — unnecessary overhead, scene composition rarely changes during profiling

---

## R6: LLM Response Schema Extension

**Decision**: Add optional `scriptPath`, `scriptLine`, and `assetPath` fields to the expected JSON response format.

**Current schema** (8 fields):
```json
{ "ruleId", "category", "severity", "title", "description", "recommendation", "metric", "threshold" }
```

**Extended schema** (11 fields):
```json
{
  "ruleId": "AI_GC_SPIKE",
  "category": "Memory",
  "severity": "Warning",
  "title": "...",
  "description": "...",
  "recommendation": "...",
  "metric": 5.2,
  "threshold": 1.0,
  "scriptPath": "Assets/Scripts/PlayerController.cs",
  "scriptLine": 142,
  "assetPath": "Assets/Textures/LargeTexture.png"
}
```

**Backward compatibility**: Fields are optional — `JsonUtility.FromJson` ignores missing fields (defaults to null/0). Existing LLM responses without these fields will parse correctly.

**System prompt update**: Add instruction asking the LLM to include file references when it can identify specific scripts or assets causing issues.
