# Tasks: Deep Profiler & Iterative Workflow

**Input**: Design documents from `/specs/002-deep-profiler-workflow/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Not requested — no test tasks included.

**Organization**: Tasks grouped by user story. 8 user stories from spec.md (2x P1, 3x P2, 3x P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new project setup needed — DrWario is an existing UPM package. This phase handles shared data structures.

- [x] T001 Add `FrameNumber` field (int) to `FrameSample` struct in `Runtime/FrameSample.cs` — stores `Time.frameCount` per frame
- [x] T002 [P] Create `ProfilerMarkerSample` struct in `Runtime/ProfilerMarkerSample.cs` with fields: MarkerName (string), AvgInclusiveTimeNs (long), AvgExclusiveTimeNs (long), MaxInclusiveTimeNs (long), AvgCallCount (float), SampleCount (int)
- [x] T003 [P] Add marker storage to `ProfilingSession` in `Runtime/ProfilingSession.cs` — add `List<ProfilerMarkerSample> _profilerMarkers`, `IReadOnlyList<ProfilerMarkerSample> ProfilerMarkers`, and `bool ProfilerWasRecording` field
- [x] T004 Store `Time.frameCount` in `FrameSample.FrameNumber` during frame recording in `Runtime/RuntimeCollector.cs` — set in the `Update()` method where `FrameSample` is created

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that multiple user stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T005 Create `RuleConfig` static class in `Editor/Analysis/RuleConfig.cs` — EditorPrefs-backed read/write with `IsEnabled(string ruleId)`, `SetEnabled(string ruleId, bool enabled)`, `GetThreshold(string ruleId, float defaultValue)`, `SetThreshold(string ruleId, float value)` using keys `DrWario_Rule_{RuleId}_Enabled` and `DrWario_Rule_{RuleId}_Threshold`
- [x] T006 Modify `AnalysisEngine.Analyze()` in `Editor/Analysis/AnalysisEngine.cs` to check `RuleConfig.IsEnabled(rule.RuleId)` before running each rule in the `foreach` loop — skip disabled rules; if ALL rules are disabled, log `[DrWario] Warning: all rules disabled` and add an Info-severity finding noting this
- [x] T006b Add summary metric fields to `DiagnosticReport` in `Editor/Analysis/DiagnosticReport.cs` — add `AvgCpuTimeMs`, `P95CpuTimeMs`, `P99CpuTimeMs`, `AvgGcAllocBytes`, `AvgDrawCalls`, `MemorySlope` (float fields). Compute and populate these in `AnalysisEngine.Analyze()` from `ProfilingSession.GetFrames()` before returning the report. Also persist these in `SerializableReport` for JSON export
- [x] T006c Add `int[] AffectedFrames` field to `DiagnosticFinding` in `Editor/Analysis/DiagnosticFinding.cs` — nullable array of frame indices where the finding's condition was detected (capped at 100). Update existing deterministic rules (`GCAllocationRule`, `FrameDropRule`, `MemoryLeakRule`) to populate `AffectedFrames` with the frame indices that triggered the finding

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 — Compare Reports to Measure Optimization Impact (Priority: P1) MVP

**Goal**: Side-by-side comparison of two saved reports showing grade deltas, metric changes, and finding diffs (Fixed/New/Persists).

**Independent Test**: Run two profiling sessions with different configurations. In History tab, click "Compare with..." on one report and select another. Verify grade deltas with arrows, finding classification, and metric changes display correctly.

### Implementation for User Story 1

- [x] T007 [US1] Create `FindingDiffStatus` enum (Fixed, New, Persists) and `FindingDiff` struct in `Editor/Analysis/ReportComparison.cs` with fields: Finding (DiagnosticFinding), Status (FindingDiffStatus), SeverityChange (int), MetricDelta (float)
- [x] T008 [US1] Create `GradeDelta` struct in `Editor/Analysis/ReportComparison.cs` with fields: Category (string), GradeA (char), GradeB (char), ScoreDelta (float)
- [x] T009 [US1] Create `MetricDelta` struct in `Editor/Analysis/ReportComparison.cs` with fields: AvgCpuTimeDelta, P95CpuTimeDelta, GcRateDelta, MemorySlopeDelta, DrawCallsDelta, HealthScoreDelta (all float)
- [x] T010 [US1] Create `ReportComparison` class in `Editor/Analysis/ReportComparison.cs` that takes two `DiagnosticReport` instances and computes on construction: OverallGradeDelta, CategoryDeltas dictionary, FindingDiffs list (matching by RuleId + Category → Fixed/New/Persists), MetricDeltas
- [x] T011 [US1] Add "Compare with..." button to each report entry in the History tab of `Editor/DrWarioView.cs` — button disabled when only 1 report exists; clicking enters comparison selection mode
- [x] T012 [US1] Implement comparison selection flow in `Editor/DrWarioView.cs` — after clicking "Compare with...", other reports get a "Select" button; clicking creates `ReportComparison` and switches to comparison view
- [x] T013 [US1] Build comparison view panel in `Editor/DrWarioView.cs` — side-by-side grade display with delta arrows (green up for improvement, red down for regression), per-category grade comparison row, metric delta table (reads from `DiagnosticReport` summary fields added in T006b), and finding diff list grouped by Fixed/New/Persists with color coding (green=Fixed, red=New, yellow=Persists); handle missing scene census data with "N/A" when one report has census and the other doesn't
- [x] T014 [US1] Add "Back to History" button in comparison view to return to normal History tab state

**Checkpoint**: Report comparison fully functional — users can select two reports and see deltas.

---

## Phase 4: User Story 2 — Event Timeline for Temporal Analysis (Priority: P1)

**Goal**: Horizontal scrollable timeline with color-coded lanes (CPU, GC, Boot, Assets, Network) showing frame spikes and events correlated in time. Zoom/pan with mouse.

**Independent Test**: Profile a scene with known events (physics spike + GC burst), open Timeline tab, verify events are correctly positioned with lane colors, zoom/pan works, click shows event details.

### Implementation for User Story 2

- [x] T015 [P] [US2] Create `TimelineEventType` enum (FrameSpike, GCAlloc, BootStage, AssetLoad, NetworkEvent) and `TimelineEvent` struct in `Editor/UI/TimelineEvent.cs` with fields: FrameIndex, Timestamp, Duration, EventType, Label, Metric
- [x] T016 [US2] Create static `TimelineEventBuilder.Build(ProfilingSession)` method in `Editor/UI/TimelineEvent.cs` that derives timeline events from session data: frame spikes (CpuFrameTimeMs > target * 1.5, fallback to 16.67ms when TargetFrameRate <= 0), GC allocations (GcAllocBytes > threshold), boot stages, asset loads, network events — returns `List<TimelineEvent>`
- [x] T017 [US2] Create `TimelineElement` class in `Editor/UI/TimelineElement.cs` extending `ChartElement` — add `_zoomLevel` (float, default 1.0), `_panOffsetX` (float), lane definitions as `Dictionary<TimelineEventType, Color>` (CPU=blue, GC=orange, Boot=green, Assets=purple, Network=gray)
- [x] T018 [US2] Implement zoom/pan input handling in `TimelineElement` — `WheelEvent` for zoom centered on cursor position (clamp 0.1x-50x), `MouseDownEvent`/`MouseMoveEvent`/`MouseUpEvent` for drag-to-pan, update `_zoomLevel` and `_panOffsetX` state
- [x] T019 [US2] Implement `generateVisualContent` rendering in `TimelineElement` using `Painter2D` — draw lane backgrounds with alternating colors, render events as rectangles (duration events) or circles (point events) using viewport culling (only draw events visible at current zoom/pan), draw reference lines at 16.67ms and 33.33ms thresholds
- [x] T020 [US2] Implement event tooltip on hover in `TimelineElement` — on `MouseOverEvent` or pointer move, find nearest event within hit radius, show tooltip with event type, frame index, metric value, and label
- [x] T021 [US2] Add "Timeline" tab to `DrWarioView` tab bar in `Editor/DrWarioView.cs` — wire `TimelineElement` to display events from current `ProfilingSession`, show "No data — run a profiling session first" when no session data available

**Checkpoint**: Timeline renders session events with zoom/pan and tooltips.

---

## Phase 5: User Story 3 — Profiler Marker Capture for Root Cause Analysis (Priority: P2)

**Goal**: Capture top-20 most expensive ProfilerRecorder markers per session and include in LLM prompt so AI can pinpoint specific expensive subsystems.

**Independent Test**: Profile a scene with expensive physics, check Unity console for debug logs showing profiler markers section in the LLM prompt, verify AI findings reference specific marker names.

### Implementation for User Story 3

- [x] T022 [US3] Add `ProfilerRecorder` instances to `RuntimeCollector` in `Runtime/RuntimeCollector.cs` for subsystem timing markers: `PlayerLoop`, `FixedUpdate`, `Update`, `LateUpdate`, `Rendering`, `Physics.Processing`, `Animation.Update`, `Render Thread`, `Main Thread` — create recorders in `StartSession()`, dispose in `StopSession()`, wrap in try-catch for graceful skip
- [x] T023 [US3] Sample `ProfilerRecorder` values each frame in `RuntimeCollector.Update()` in `Runtime/RuntimeCollector.cs` — accumulate nanosecond values per marker into running averages, track max values, store call counts
- [x] T024 [US3] Aggregate marker data into `ProfilerMarkerSample` list on `StopSession()` in `Runtime/RuntimeCollector.cs` — compute averages from accumulated data, sort by AvgInclusiveTimeNs descending, keep top 20, store on `ProfilingSession.ProfilerMarkers`
- [x] T025 [US3] Detect and store `Profiler.enabled` state at session start in `Runtime/RuntimeCollector.cs` — set `ProfilingSession.ProfilerWasRecording` flag
- [x] T026 [US3] Add `BuildProfilerMarkersSection()` method to `LLMPromptBuilder` in `Editor/Analysis/LLM/LLMPromptBuilder.cs` — generates JSON section with marker name, avgInclusiveMs (convert ns to ms), avgExclusiveMs, maxInclusiveMs, avgCallCount for each marker; skip section entirely if no markers captured
- [x] T027 [US3] Call `BuildProfilerMarkersSection()` from `BuildUserPrompt()` in `Editor/Analysis/LLM/LLMPromptBuilder.cs` — insert after existing frame summary section
- [x] T028 [US3] Update system prompt in `LLMPromptBuilder.BuildSystemPrompt()` in `Editor/Analysis/LLM/LLMPromptBuilder.cs` — add instruction: "When profilerMarkers data is available, reference specific marker names in findings rather than guessing which subsystem is expensive"

**Checkpoint**: AI findings reference specific subsystem markers when marker data is available.

---

## Phase 6: User Story 4 — Streaming LLM Responses (Priority: P2)

**Goal**: Show AI findings progressively as they stream in via SSE for Claude/OpenAI, with fallback to blocking for Ollama/Custom.

**Independent Test**: Configure Claude or OpenAI, run analysis, verify findings appear one-by-one in UI. Switch to Ollama, verify blocking fallback works.

### Implementation for User Story 4

- [x] T029 [US4] Create `SseDownloadHandler` class in `Editor/Analysis/LLM/SseDownloadHandler.cs` — extend `DownloadHandlerScript`, override `ReceiveData(byte[] data, int length)` to accumulate text in StringBuilder, parse SSE `data:` lines, extract text/content deltas based on provider (Claude: `text_delta`, OpenAI: `delta.content`); detect end-of-stream via Claude `event: message_stop` or OpenAI `data: [DONE]` and fire `OnComplete`
- [x] T030 [US4] Implement progressive JSON array parser in `SseDownloadHandler` — brace-depth state machine that detects complete JSON objects within the streaming `[...]` array, fire `OnFindingParsed` callback for each complete finding object parsed via `LLMResponseParser.ParseSingle()`
- [x] T031 [US4] Add `ParseSingle(string jsonObject)` method to `LLMResponseParser` in `Editor/Analysis/LLM/LLMResponseParser.cs` — parse a single finding JSON object (not wrapped in array) into `DiagnosticFinding`
- [x] T032 [US4] Add `SendStreamingAsync()` method to `LLMClient` in `Editor/Analysis/LLM/LLMClient.cs` — build streaming request body (`"stream": true` for Claude/OpenAI), use `SseDownloadHandler` instead of `DownloadHandlerBuffer`, fire `onFindingParsed` callback, fire `onComplete` with full accumulated content, fire `onError` on failure
- [x] T033 [US4] Add streaming request body builders in `LLMClient` — `BuildClaudeStreamingBody()` (add `"stream": true`) and `BuildOpenAIStreamingBody()` (add `"stream": true`); keep Ollama/Custom using non-streaming bodies
- [x] T034 [US4] Add `AnalyzeStreamingAsync()` method to `AIAnalysisRule` in `Editor/Analysis/Rules/AIAnalysisRule.cs` — route to `LLMClient.SendStreamingAsync()` for Claude/OpenAI providers, fall back to existing `SendAsync()` for Ollama/Custom
- [x] T035 [US4] Add `AnalyzeStreamingAsync()` method to `AnalysisEngine` in `Editor/Analysis/AnalysisEngine.cs` — run deterministic rules synchronously first, then call `AIAnalysisRule.AnalyzeStreamingAsync()` with callback that fires `OnStreamingFindingReceived` event per finding
- [x] T036 [US4] Wire streaming UI in `DrWarioView` in `Editor/DrWarioView.cs` — when streaming analysis starts, show progress indicator ("Analyzing..."), append each finding card as `OnStreamingFindingReceived` fires, recompute grades on each new finding, remove progress indicator on completion

**Checkpoint**: Findings appear progressively during AI analysis for Claude/OpenAI providers.

---

## Phase 7: User Story 5 — Jump to Profiler Frame (Priority: P2)

**Goal**: "Show in Profiler" clickable link on findings that reference frame indices, opening Unity Profiler at that frame.

**Independent Test**: Run profiling with Profiler recording, get a finding with frame reference, click "Show in Profiler", verify Profiler window opens at correct frame.

### Implementation for User Story 5

- [x] T037 [US5] Create `ProfilerBridgeEditor` static class in `Editor/ProfilerBridgeEditor.cs` with `NavigateToFrame(int frameNumber)` — open `ProfilerWindow` via `EditorWindow.GetWindow<ProfilerWindow>()`, set `selectedFrameIndex` property, wrap in try-catch for graceful degradation
- [x] T038 [US5] Add `IsProfilerDataAvailable()` check in `ProfilerBridgeEditor` — returns true if Profiler was recording during the session (from `ProfilingSession.ProfilerWasRecording`)
- [x] T039 [US5] Add "Show in Profiler" clickable label to finding cards in `Editor/DrWarioView.cs` — only visible when `finding.FrameIndex >= 0`; on click call `ProfilerBridgeEditor.NavigateToFrame(finding.FrameIndex)`; show informative message if `IsProfilerDataAvailable()` returns false

**Checkpoint**: Users can click through from findings to the exact Profiler frame.

---

## Phase 8: User Story 6 — Rule Enable/Disable and Threshold Configuration (Priority: P3)

**Goal**: UI for toggling rules on/off and adjusting thresholds, persisted in EditorPrefs.

**Independent Test**: Disable a rule in settings, run analysis, verify no findings from that rule. Change threshold, verify it takes effect.

### Implementation for User Story 6

- [x] T040 [US6] Add `IConfigurableRule` interface in `Editor/Analysis/IAnalysisRule.cs` with `float DefaultThreshold { get; }` and `string ThresholdLabel { get; }` — implemented by rules that support configurable thresholds
- [x] T041 [P] [US6] Implement `IConfigurableRule` on `GCAllocationRule` in `Editor/Analysis/Rules/GCAllocationRule.cs` — expose GC spike threshold (default 1024 bytes), read from `RuleConfig.GetThreshold()` in `Analyze()`
- [x] T042 [P] [US6] Implement `IConfigurableRule` on `FrameDropRule` in `Editor/Analysis/Rules/FrameDropRule.cs` — expose target FPS threshold (default from session metadata), read from `RuleConfig.GetThreshold()` in `Analyze()`
- [x] T043 [P] [US6] Implement `IConfigurableRule` on `AssetLoadRule` in `Editor/Analysis/Rules/AssetLoadRule.cs` — expose slow load threshold (default 500ms), read from `RuleConfig.GetThreshold()`
- [x] T044 [P] [US6] Implement `IConfigurableRule` on `BootStageRule` in `Editor/Analysis/Rules/BootStageRule.cs` — expose slow stage threshold (default 2000ms), read from `RuleConfig.GetThreshold()`
- [x] T045 [US6] Expose registered rules list from `AnalysisEngine` in `Editor/Analysis/AnalysisEngine.cs` — add `IReadOnlyList<IAnalysisRule> RegisteredRules` property
> **Note**: Only GCAllocation, FrameDrop, AssetLoad, and BootStage rules implement `IConfigurableRule` — the remaining 4 rules (MemoryLeak, NetworkLatency, RenderingEfficiency, CPUvsGPUBottleneck) don't have meaningful single-threshold configuration. They still get enable/disable toggles via RuleConfig.
- [x] T046 [US6] Build rule management UI section in `Editor/DrWarioView.cs` — add "Rules" section in LLM Settings tab showing each registered rule with: name (from RuleId), toggle switch (calls `RuleConfig.SetEnabled()`), threshold slider if `IConfigurableRule` (calls `RuleConfig.SetThreshold()`), visual indicator when rule is disabled

**Checkpoint**: Users can toggle rules and adjust thresholds from the Settings tab.

---

## Phase 9: User Story 7 — Expandable Finding Cards (Priority: P3)

**Goal**: Click finding cards to expand with affected frame indices, mini-chart, and related findings.

**Independent Test**: Generate findings, click to expand a card, verify frame list and mini-chart appear. Click again to collapse.

### Implementation for User Story 7

- [x] T047 [US7] Add expand/collapse state tracking to finding cards in `Editor/DrWarioView.cs` — add `HashSet<int> _expandedFindings` tracking expanded card indices, toggle on click
- [x] T048 [US7] Build expanded card content in `Editor/DrWarioView.cs` — when expanded, show: list of affected frame indices from `finding.AffectedFrames` (cap at 20 with "show all" option), metric value per listed frame; show "No frame data" if AffectedFrames is null
- [x] T049 [US7] Add mini-chart to expanded finding cards in `Editor/DrWarioView.cs` — small sparkline-style `Painter2D` chart showing the relevant metric over the session timeline, with spike frames highlighted in red; reuse existing chart drawing patterns from `ChartElement`
- [x] T050 [US7] Show related findings in expanded cards in `Editor/DrWarioView.cs` — list other findings from the same `Category` as clickable links that scroll to and expand that finding

**Checkpoint**: Finding cards expand/collapse with detailed sub-information.

---

## Phase 10: User Story 8 — HTML Report Export (Priority: P3)

**Goal**: Export button generates a self-contained HTML file with embedded CSS, inline SVG charts, grade summary, finding cards, and data tables.

**Independent Test**: Run analysis, click "Export HTML", open file in Chrome/Firefox/Edge, verify all sections render correctly.

### Implementation for User Story 8

- [x] T051 [P] [US8] Create `HtmlReportBuilder` static class in `Editor/Analysis/HtmlReportBuilder.cs` — `Build(DiagnosticReport report, ProfilingSession session)` returns HTML string; include page structure with `<style>` block, header with grade summary
- [x] T052 [US8] Implement CSS template strings in `HtmlReportBuilder` — severity color badges (Critical=red, Warning=yellow, Info=blue), grade letter styling (A=green, B=blue, C=yellow, D=orange, F=red), card layout, table styling, responsive design
- [x] T053 [US8] Implement finding cards HTML generation in `HtmlReportBuilder` — iterate findings, generate cards with severity badge, title, description, recommendation, metric/threshold, script/asset references, confidence indicator
- [x] T054 [US8] Implement inline SVG chart generation in `HtmlReportBuilder` — generate SVG line chart for frame time distribution and memory trajectory from session data; simple polyline with axis labels
- [x] T055 [US8] Implement category grade summary and data tables in `HtmlReportBuilder` — grade cards per category with letter + score, metrics table with avg/P95/P99 frame times, GC stats, memory stats
- [x] T056 [US8] Add "Export HTML" button to `Editor/DrWarioView.cs` — in Summary or Findings tab, call `EditorUtility.SaveFilePanel()` for file path, generate HTML via `HtmlReportBuilder.Build()`, write with `File.WriteAllText()`, disabled when no report exists

**Checkpoint**: Self-contained HTML reports render correctly in all major browsers.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, version bump, and documentation.

- [x] T057 Integration pass on `DrWarioView` tab initialization in `Editor/DrWarioView.cs` — verify tab ordering (Timeline in correct position), ensure tab state persists across domain reloads, confirm no null refs when switching tabs with no session data
- [x] T058 Add streaming error handling and partial result recovery in `Editor/Analysis/LLM/SseDownloadHandler.cs` — on stream interruption, preserve all findings parsed so far, show error message with count of findings received
- [x] T059 Add platform/version mismatch warning to comparison view in `Editor/DrWarioView.cs` — if ReportA and ReportB have different `Session.Platform` or `Session.UnityVersion`, show yellow warning banner
- [x] T060 [P] Update `docs/user-guide.md` — document all v3.0 features: report comparison workflow, timeline usage, profiler marker integration, streaming behavior, rule configuration, expandable cards, HTML export
- [x] T061 [P] Bump package version to 3.0.0 in `package.json`
- [ ] T062 Verify compilation in Unity Editor — open HybridFrame project, confirm zero errors in Console, run a profiling session end-to-end with all new features

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion (T001-T004)
- **US1-US8 (Phases 3-10)**: All depend on Phase 2 completion
  - US1 (Report Comparison): Independent — no dependency on other stories
  - US2 (Event Timeline): Independent — uses FrameNumber from Phase 1
  - US3 (Profiler Markers): Independent — uses ProfilerMarkerSample from Phase 1
  - US4 (Streaming LLM): Independent — extends LLMClient
  - US5 (Jump to Profiler): Depends on FrameNumber from Phase 1 only
  - US6 (Rule Management): Depends on RuleConfig from Phase 2
  - US7 (Expandable Cards): Independent — extends existing finding cards
  - US8 (HTML Export): Independent — reads DiagnosticReport
- **Polish (Phase 11)**: Depends on all desired user stories being complete

### User Story Independence Matrix

| Story | Can parallel with | Shares files with |
|-------|-------------------|-------------------|
| US1 | US2, US3, US4, US5, US6, US7, US8 | DrWarioView.cs (different sections) |
| US2 | US1, US3, US4, US5, US6, US7, US8 | DrWarioView.cs (different sections) |
| US3 | US1, US2, US4, US5, US6, US7, US8 | RuntimeCollector.cs, LLMPromptBuilder.cs |
| US4 | US1, US2, US3, US5, US6, US7, US8 | LLMClient.cs, AnalysisEngine.cs, DrWarioView.cs |
| US5 | US1, US2, US3, US4, US6, US7, US8 | DrWarioView.cs |
| US6 | US1, US2, US3, US4, US5, US7, US8 | AnalysisEngine.cs, DrWarioView.cs |
| US7 | US1, US2, US3, US4, US5, US6, US8 | DrWarioView.cs |
| US8 | US1, US2, US3, US4, US5, US6, US7 | DrWarioView.cs |

### Within Each User Story

- Data structs / models first
- Business logic / services second
- UI integration last
- All changes to `DrWarioView.cs` should be coordinated to avoid merge conflicts

### Parallel Opportunities

**Phase 1** (all parallel — different files):
- T001 (FrameSample.cs) || T002 (ProfilerMarkerSample.cs) || T003 (ProfilingSession.cs)
- T004 depends on T001

**Phase 2** (sequential then parallel):
- T005 → T006
- T006b || T006c (different files, can run in parallel after T005)

**User Stories** (parallel between stories, sequential within):
- US1 (T007-T014) can run fully parallel with US2 (T015-T021)
- US3 (T022-T028) can run parallel with US4 (T029-T036)
- US5 (T037-T039), US6 (T040-T046), US7 (T047-T050), US8 (T051-T056) all parallel

**Within stories** (parallel tasks marked [P]):
- US2: T015 || T016 (different aspects of TimelineEvent.cs — but same file, so sequential in practice)
- US6: T041 || T042 || T043 || T044 (different rule files)
- US8: T051 starts alone, rest sequential

---

## Parallel Example: Phase 1

```text
# All three can run in parallel (different files):
T001: Add FrameNumber to FrameSample in Runtime/FrameSample.cs
T002: Create ProfilerMarkerSample in Runtime/ProfilerMarkerSample.cs
T003: Add marker storage to ProfilingSession in Runtime/ProfilingSession.cs

# Then T004 depends on T001:
T004: Store Time.frameCount in RuntimeCollector.cs
```

## Parallel Example: User Story 6

```text
# After T040 (interface), all rule implementations can run in parallel:
T041: GCAllocationRule.cs
T042: FrameDropRule.cs
T043: AssetLoadRule.cs
T044: BootStageRule.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T004)
2. Complete Phase 2: Foundational (T005-T006)
3. Complete Phase 3: US1 Report Comparison (T007-T014)
4. **STOP and VALIDATE**: Test comparison view with two saved reports
5. This alone delivers the most impactful iterative workflow feature

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. US1 (Report Comparison) → Test → **v3.0-alpha.1**
3. US2 (Event Timeline) → Test → Visual impact milestone
4. US3 (Profiler Markers) + US4 (Streaming) → Test → AI quality leap
5. US5 (Jump to Profiler) → Test → Workflow integration
6. US6 + US7 + US8 → Test → Polish and export
7. Phase 11 → Test → **v3.0.0 release**

### Recommended Execution Order (single developer)

P1 stories first, then P2, then P3:
1. Foundation → US1 → US2 (core value)
2. US3 → US4 → US5 (AI + profiler integration)
3. US6 → US7 → US8 (polish)
4. Phase 11 (finalize)

---

## Notes

- All 8 user stories touch `DrWarioView.cs` — coordinate UI changes carefully
- `RuntimeCollector.cs` is modified by Phase 1 (T004) and US3 (T022-T025) — execute sequentially
- SSE streaming (US4) is the most technically complex feature — allocate extra time
- Timeline rendering (US2) has the most visual impact — good for early demos
- HTML export (US8) is self-contained and can be deferred without blocking anything
- Total: **64 tasks** across 11 phases (T006b and T006c added for summary metrics and affected frames)
