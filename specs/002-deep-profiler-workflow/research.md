# Research: Deep Profiler & Iterative Workflow

**Feature**: 002-deep-profiler-workflow
**Date**: 2026-03-08

## Decision 1: Profiler Marker Capture Strategy

**Decision**: Use flat ProfilerRecorder counters for subsystem timing, NOT hierarchical marker trees.

**Rationale**: ProfilerRecorder is a flat counter API — it cannot capture parent-child marker relationships or inclusive/exclusive times. The hierarchical `FrameDataView` API is internal/undocumented and fragile across Unity versions. Instead, capture timing for known subsystems via ProfilerRecorder markers that return nanoseconds (e.g., `Main Thread`, `Render Thread`) and supplement with aggregate counters (physics bodies, GC allocs, etc.).

**Approach**:
- Add ProfilerRecorder for subsystem timing markers: `PlayerLoop`, `FixedUpdate`, `Update`, `LateUpdate`, `Rendering`, `Physics.Processing`, `Animation.Update`
- These return inclusive nanosecond timing per frame
- Store top-N most expensive markers per session (averaged)
- Include in LLM prompt as `profilerMarkers` section

**Alternatives considered**:
- `FrameDataView` (internal API) — fragile, breaks between Unity versions
- Custom `ProfilerMarker` wrapping of user code — requires user instrumentation, not automatic
- Frame export + offline parsing — not real-time

**Performance overhead**: <0.05ms per frame for 20 ProfilerRecorder reads (zero allocation, native calls).

---

## Decision 2: SSE Streaming Implementation

**Decision**: Create custom `DownloadHandlerScript` subclass that overrides `ReceiveData()` for chunked SSE processing. Accumulate text and parse findings progressively.

**Rationale**: Unity's `DownloadHandlerBuffer` waits for the complete response. `DownloadHandlerScript` (Unity 2022.3+) allows overriding `ReceiveData()` which fires on each received chunk, enabling real-time SSE processing.

**SSE Format**:
- **Claude**: `event: content_block_delta\ndata: {"type":"text_delta","text":"..."}\n\n` — accumulate `text` fields
- **OpenAI**: `data: {"choices":[{"delta":{"content":"..."}}]}\n\n` — accumulate `content` fields, ends with `data: [DONE]`
- **Ollama**: Supports `"stream": true` with similar format
- **Custom**: Falls back to non-streaming

**Progressive parsing**: Implement a brace-depth state machine to detect complete JSON objects within the streaming array. Emit `OnFindingParsed` event for each complete finding object. Fall back to full-parse-at-end if state machine can't parse.

**Alternatives considered**:
- HTTP/2 streaming — not available in UnityWebRequest
- WebSocket — different protocol, not supported by Claude/OpenAI APIs
- Polling with range requests — not supported by LLM APIs

---

## Decision 3: Profiler Frame Navigation

**Decision**: Use `EditorWindow.GetWindow<ProfilerWindow>()` + `ProfilerWindow.selectedFrameIndex` property.

**Rationale**: Both are public APIs stable since Unity 2019+. The `selectedFrameIndex` is a static property that sets the active frame and scrolls the Profiler view. Must store absolute frame numbers (`Time.frameCount`) in `FrameSample`.

**Requirements**:
- Add `FrameNumber` field to `FrameSample` struct (stores `Time.frameCount`)
- Detect if Profiler was recording during session via `Profiler.enabled` check at session start
- Store recording state in `ProfilingSession.Metadata`
- Wrap in try-catch for graceful degradation on platforms where Profiler API behaves differently

**Alternatives considered**:
- Reflection into internal APIs — unnecessary, public API sufficient
- Deep linking via command line — not applicable in editor context

---

## Decision 4: Timeline Rendering Architecture

**Decision**: Extend `ChartElement` base class with zoom/pan state. Use `Painter2D` with viewport culling. No ScrollView — timeline manages its own scrolling.

**Rationale**: DrWario's existing `ChartElement` provides coordinate mapping, tooltip infrastructure, and `generateVisualContent` rendering. Adding `_zoomLevel` and `_panOffsetX` state with modified `MapX()` gives zoom/pan behavior. Viewport culling ensures only visible events are drawn (critical for 3600+ frames).

**Approach**:
- `TimelineElement : ChartElement` with lanes (Dictionary of event type → color)
- `WheelEvent` for zoom (centered on cursor), `MouseDown/Move/Up` for pan
- Lane layout: divide Y-axis evenly among lane count, alternating background colors
- Events drawn as rectangles (duration events) or circles (point events)
- Reference lines at target frame time (16.67ms, 33.33ms)
- Tooltip on hover showing event details

**Performance**: At most 3600 frame events + boot/asset/network events per lane. With culling, typically 50-200 visible events at any zoom level. Painter2D handles this easily at 60fps.

**Alternatives considered**:
- ScrollView container — interferes with custom zoom/pan behavior
- IMGUI timeline — doesn't integrate with VisualElement tab system
- Canvas2D/GraphView — too heavyweight for this use case

---

## Decision 5: Report Comparison Data Model

**Decision**: Create `ReportComparison` struct that pairs two `DiagnosticReport` instances and computes deltas on construction.

**Rationale**: The comparison is a view model — it doesn't need persistence. Computed once when user selects two reports, displayed in a dedicated comparison view within the History tab.

**Finding classification**:
- Match findings by `RuleId + Category` pair
- "Fixed": exists in Report A but not Report B
- "New": exists in Report B but not Report A
- "Persists": exists in both (may have different severity/metric)

**Metric deltas**: Compare avg CPU time, P95, GC rate, memory slope, draw calls, overall grade, per-category grades.

**Alternatives considered**:
- Persisting comparisons to disk — unnecessary complexity, computed on-demand is fast
- Fuzzy matching on finding title — too unreliable, RuleId is deterministic

---

## Decision 6: Rule Configuration Architecture

**Decision**: Add `IRuleConfig` interface with `Enabled` and `GetThreshold()`/`SetThreshold()`. Store in EditorPrefs with `DrWario_Rule_{RuleId}_Enabled` and `DrWario_Rule_{RuleId}_Threshold` keys.

**Rationale**: Each rule already has a `RuleId` string. Using EditorPrefs with rule-specific keys is consistent with existing DrWario settings patterns. The `IAnalysisRule` interface gets optional threshold support — rules that support configurable thresholds implement the interface.

**Approach**:
- `AnalysisEngine` checks `RuleConfig.IsEnabled(rule.RuleId)` before running each rule
- Rules with thresholds read from `RuleConfig.GetThreshold(ruleId, defaultValue)`
- UI displays all registered rules with toggles and optional threshold sliders
- Custom rules registered via `RegisterRule()` also appear

**Alternatives considered**:
- JSON config file — inconsistent with EditorPrefs pattern used everywhere else
- ScriptableObject — would require asset in project, not suitable for UPM package

---

## Decision 7: HTML Report Export

**Decision**: Generate self-contained HTML with inline CSS and SVG charts built from string templates.

**Rationale**: No external dependencies allowed. Inline SVG provides vector charts without JavaScript. CSS is embedded in a `<style>` tag. The HTML is a single file that opens in any browser.

**Approach**:
- `HtmlReportBuilder` static class that takes `DiagnosticReport` + `ProfilingSession`
- Template strings for page structure, finding cards, grade display
- SVG generation for charts (reuse chart data computation from `AddCharts()`)
- Color-coded severity badges, grade letter styling
- Save via `EditorUtility.SaveFilePanel` + `File.WriteAllText`

**Alternatives considered**:
- Markdown export — not visually rich enough
- PDF via external library — violates no-dependency constraint
- JavaScript-based charts (Chart.js) — requires CDN or bundling, not self-contained
