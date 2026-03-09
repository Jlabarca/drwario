# Tasks: Rich Diagnostics UI

**Input**: Design documents from `/specs/001-rich-diagnostics-ui/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/llm-response.md, quickstart.md

**Tests**: Not requested — test tasks omitted.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Data model extensions and shared infrastructure needed by all stories

- [x] T001 [P] Add ScriptPath (string), ScriptLine (int), AssetPath (string) fields to DiagnosticFinding struct in Editor/Analysis/DiagnosticFinding.cs
- [x] T002 [P] Add 8 extended counter fields (PhysicsActiveBodies, PhysicsKinematicBodies, PhysicsContacts, AudioVoiceCount, AudioDSPLoad, AnimatorCount, UICanvasRebuilds, UILayoutRebuilds) to FrameSample struct in Runtime/FrameSample.cs
- [x] T003 [P] Create SceneCensus and ComponentCount structs in new file Runtime/SceneCensus.cs with fields per data-model.md (TotalGameObjects, ComponentDistribution, light counts by type, CanvasCount, CameraCount, ParticleSystemCount, LODGroupCount, RigidbodyCount, IsValid)
- [x] T004 Add SceneCensus field to ProfilingSession class in Runtime/ProfilingSession.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: ProfilerBridge extended counters and scene census capture — required before UI stories

**Warning**: No user story work can begin until this phase is complete

- [x] T005 Add 8 new ProfilerRecorder instances to ProfilerBridge in Runtime/ProfilerBridge.cs for physics (ActiveDynamicBodies, ActiveKinematicBodies, Contacts), audio (ActiveVoiceCount, DSPLoad), animation (Animators.Count), and UI (CanvasRebuildCount, LayoutRebuildCount) markers. Update Sample() to populate new FrameSample fields. Gracefully skip unavailable markers.
- [x] T006 Update RuntimeCollector.cs to pass new FrameSample fields from ProfilerBridge.Sample() into the ring buffer in Runtime/RuntimeCollector.cs
- [x] T007 Create editor-only SceneCensusCapture static class in Editor/SceneCensusCapture.cs that uses FindObjectsByType to count GameObjects, components (top 20 types), Lights by type, Canvases, Cameras, ParticleSystems, LODGroups, Rigidbodies. Return SceneCensus struct.
- [x] T008 Hook SceneCensusCapture into DrWarioPlayModeHook.cs — call on RuntimeCollector.OnSessionStarted, store result in ProfilingSession.SceneCensus in Editor/DrWarioPlayModeHook.cs
- [x] T009 Create base ChartElement VisualElement class in new file Editor/UI/ChartElement.cs — coordinate mapping (data space → pixel space), generateVisualContent hook, MouseMoveEvent handler for tooltip position tracking, tooltip Label element (show/hide)

**Checkpoint**: Extended profiling data captured, scene census captured, chart base ready

---

## Phase 3: User Story 1 - Navigate from Finding to Source Code (Priority: P1) MVP

**Goal**: Findings contain clickable script/asset references that open files in IDE or ping assets in Project window

**Independent Test**: Run profiling, verify findings show clickable links, click opens correct file at line or pings asset

### Implementation for User Story 1

- [x] T010 [US1] Update LLMResponseParser.cs to parse scriptPath (string), scriptLine (int), assetPath (string) from JSON response — add fields to FindingJson struct, map to DiagnosticFinding in Editor/Analysis/LLM/LLMResponseParser.cs
- [x] T011 [US1] Update LLMPromptBuilder system prompt to instruct LLM to include scriptPath/scriptLine/assetPath when identifiable — append reference guidance per contracts/llm-response.md in Editor/Analysis/LLM/LLMPromptBuilder.cs
- [x] T012 [P] [US1] Update AssetLoadRule to populate AssetPath field from AssetLoadTiming.AssetKey on slow-load findings in Editor/Analysis/Rules/AssetLoadRule.cs
- [x] T013 [P] [US1] Update BootStageRule to populate ScriptPath field referencing BootTimingHook.cs for slow-stage findings in Editor/Analysis/Rules/BootStageRule.cs
- [x] T014 [P] [US1] Update GCAllocationRule to set a generic ScriptPath hint (e.g., "Check Update/FixedUpdate methods") in EnvironmentNote or ScriptPath in Editor/Analysis/Rules/GCAllocationRule.cs
- [x] T015 [US1] Update DiagnosticReport.ExportJson() and ExportText() to include ScriptPath, ScriptLine, AssetPath fields in Editor/Analysis/DiagnosticReport.cs
- [x] T016 [US1] Add clickable script/asset reference links to finding cards in DrWarioView.cs — use InternalEditorUtility.OpenFileAtLineExternal() for scripts, EditorGUIUtility.PingObject() for assets. Style as underlined blue text with Clickable manipulator in Editor/DrWarioView.cs

**Checkpoint**: Findings show clickable source references. LLM responses include file/asset paths. Deterministic rules provide known references.

---

## Phase 4: User Story 2 - Visualize Performance Trends with Charts (Priority: P1)

**Goal**: Interactive charts for memory trajectory, frame time distribution, GC timeline, CPU/GPU comparison with hover tooltips

**Independent Test**: Run profiling session, verify 4 chart types render with correct data, hover shows tooltips with exact values

### Implementation for User Story 2

- [x] T017 [P] [US2] Create LineChart VisualElement in Editor/UI/LineChart.cs — extends ChartElement, draws line series via Painter2D (MoveTo/LineTo), supports multiple series with different colors, reference lines (e.g., 60fps/30fps), auto-scales Y axis
- [x] T018 [P] [US2] Create BarChart VisualElement in Editor/UI/BarChart.cs — extends ChartElement, draws vertical bars via Painter2D, highlight bars exceeding threshold in red, supports grouped bars for comparison (CPU vs GPU)
- [x] T019 [P] [US2] Create Histogram VisualElement in Editor/UI/Histogram.cs — extends ChartElement, buckets raw float[] data into configurable bin count, draws filled bars, labels bucket ranges on X axis
- [x] T020 [US2] Integrate charts into DrWarioView Summary tab in Editor/DrWarioView.cs — add memory trajectory LineChart (from 12 downsampled points), frame time distribution Histogram, GC allocation BarChart (per-frame bytes), CPU vs GPU comparison BarChart. Populate chart data from ProfilingSession after analysis.
- [x] T021 [US2] Implement tooltip display on all charts — on MouseMoveEvent, find nearest data point, show Label with exact value and frame number/time offset in Editor/UI/ChartElement.cs

**Checkpoint**: Summary tab shows 4 interactive charts with hover tooltips. Charts auto-scale and handle empty data gracefully.

---

## Phase 5: User Story 3 - Explore Detailed Data in Sortable Tables (Priority: P2)

**Goal**: Sortable, filterable data tables for GC frames, slow frames, asset loads, boot stages, network events

**Independent Test**: Run profiling, verify tables populate with correct data, column headers sort ascending/descending on click

### Implementation for User Story 3

- [x] T022 [US3] Create DataTableBuilder static helper in Editor/UI/DataTableBuilder.cs — factory method to create MultiColumnListView with column definitions (title, width, sortable), generic bindItem callback, column sort handling via columnSortingChanged event with ascending/descending toggle
- [x] T023 [US3] Add GC allocation frames table to DrWarioView — columns: Frame#, GC Bytes, GC Count, CPU Time. Source: frames sorted by GcAllocBytes descending (top 50) in Editor/DrWarioView.cs
- [x] T024 [P] [US3] Add slowest frames table to DrWarioView — columns: Frame#, CPU Time, GPU Time, Draw Calls, GC Bytes. Source: frames sorted by CpuFrameTimeMs descending (top 50) in Editor/DrWarioView.cs
- [x] T025 [P] [US3] Add asset load times table to DrWarioView — columns: Asset Key, Duration (ms), Size (bytes). Source: ProfilingSession.AssetLoads sorted by DurationMs descending in Editor/DrWarioView.cs
- [x] T026 [P] [US3] Add boot stages table to DrWarioView — columns: Stage Name, Duration (ms), Status (success/fail). Source: ProfilingSession.BootStages in Editor/DrWarioView.cs
- [x] T027 [P] [US3] Add network events table to DrWarioView — columns: Timestamp, Type, Bytes, Latency (ms). Source: ProfilingSession.NetworkEvents in Editor/DrWarioView.cs

**Checkpoint**: Data tables show sorted data. Column headers toggle sort direction. Tables handle empty data with placeholder message.

---

## Phase 6: User Story 4 - Deeper Profiling Data Collection (Priority: P2)

**Goal**: Extended profiling counters (physics, audio, animation, UI) in LLM prompt context and optionally in new analysis rules

**Independent Test**: Run scene with physics/UI, verify extended counters appear in LLM prompt (use Copy Prompt), verify counters show in frame data

### Implementation for User Story 4

- [x] T028 [US4] Update LLMPromptBuilder to include extended counter summary in frameSummary JSON — add physics (avg active bodies, avg contacts), audio (avg voices, avg DSP load), animation (avg animators), UI (avg canvas rebuilds, avg layout rebuilds) sections. Skip sections where all values are 0 in Editor/Analysis/LLM/LLMPromptBuilder.cs
- [x] T029 [US4] Update EditorBaseline struct in Runtime/FrameSample.cs to include baseline values for new counters (AvgUICanvasRebuilds, AvgUILayoutRebuilds) so editor UI overhead can be subtracted
- [x] T030 [US4] Update EditorBaselineCapture to sample new ProfilerRecorder counters during baseline capture in Editor/EditorBaselineCapture.cs

**Checkpoint**: Extended counters flow from ProfilerBridge → FrameSample → LLM prompt. Editor baseline includes new counters.

---

## Phase 7: User Story 5 - Scene-Aware AI Analysis (Priority: P3)

**Goal**: Scene census data included in LLM prompt for smarter context-aware analysis

**Independent Test**: Load a scene with known objects (lights, canvases, etc.), run profiling, use Copy Prompt to verify scene census JSON appears in prompt

### Implementation for User Story 5

- [x] T031 [US5] Add scene census JSON section to LLMPromptBuilder.BuildUserPrompt() — output totalGameObjects, componentDistribution (top 20), lights by type, canvasCount, cameraCount, particleSystemCount, lodGroupCount, rigidbodyCount. Skip section if census is not valid in Editor/Analysis/LLM/LLMPromptBuilder.cs
- [x] T032 [US5] Update LLMPromptBuilder system prompt to reference scene context — instruct LLM to consider scene composition when analyzing (e.g., too many lights, missing LOD groups, excessive particle systems) in Editor/Analysis/LLM/LLMPromptBuilder.cs
- [x] T033 [US5] Add scene census summary to DiagnosticReport.ExportText() output — include object counts and notable composition details in Editor/Analysis/DiagnosticReport.cs

**Checkpoint**: LLM prompt includes full scene census. AI findings reference scene composition when relevant.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T034 [P] Add empty state handling for all charts — display "No data" message when session has zero frames in Editor/UI/ChartElement.cs
- [x] T035 [P] Add empty state handling for all data tables — display "No data collected" row when source list is empty in Editor/UI/DataTableBuilder.cs
- [x] T036 Update package.json version from 1.0.0 to 2.0.0 in package.json
- [x] T037 [P] Update docs/user-guide.md to document new charts, tables, and clickable references
- [x] T038 Verify all new code compiles without errors — run Unity editor refresh and check console for CS errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (needs FrameSample fields, SceneCensus struct)
- **US1 (Phase 3)**: Depends on Phase 1 (needs DiagnosticFinding fields)
- **US2 (Phase 4)**: Depends on Phase 2 (needs ChartElement base class)
- **US3 (Phase 5)**: Depends on Phase 1 only (tables read existing data structures)
- **US4 (Phase 6)**: Depends on Phase 2 (needs extended ProfilerBridge counters flowing)
- **US5 (Phase 7)**: Depends on Phase 2 (needs SceneCensusCapture hooked up)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 1 — independent of other stories
- **US2 (P1)**: Can start after Phase 2 — independent of other stories
- **US3 (P2)**: Can start after Phase 1 — independent of other stories (can run in parallel with US2)
- **US4 (P2)**: Can start after Phase 2 — independent of other stories
- **US5 (P3)**: Can start after Phase 2 — independent of other stories

### Within Each User Story

- Data model changes before service/logic changes
- Backend (prompt/parser) changes before UI changes
- Core implementation before integration

### Parallel Opportunities

- T001, T002, T003 (Phase 1) — all modify different files
- T012, T013, T014 (US1) — all modify different rule files
- T017, T018, T019 (US2) — all create different chart files
- T024, T025, T026, T027 (US3) — all add different tables (same file but independent sections)
- US1 and US3 can run fully in parallel after Phase 1
- US2, US4, US5 can run fully in parallel after Phase 2

---

## Parallel Example: Phase 1

```
# All Phase 1 tasks can run in parallel (different files):
Task T001: Add fields to DiagnosticFinding in Editor/Analysis/DiagnosticFinding.cs
Task T002: Add fields to FrameSample in Runtime/FrameSample.cs
Task T003: Create SceneCensus.cs in Runtime/SceneCensus.cs
```

## Parallel Example: User Story 2

```
# All chart components can be built in parallel (different files):
Task T017: Create LineChart in Editor/UI/LineChart.cs
Task T018: Create BarChart in Editor/UI/BarChart.cs
Task T019: Create Histogram in Editor/UI/Histogram.cs
# Then integrate sequentially:
Task T020: Wire charts into DrWarioView
Task T021: Add tooltip behavior to ChartElement
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T004)
2. Complete US1 (T010-T016): Clickable source links
3. **STOP and VALIDATE**: Verify findings have clickable references
4. This alone delivers significant value — actionable findings

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. US1 → Clickable links (MVP)
3. US2 → Charts (visual upgrade)
4. US3 → Data tables (drill-down)
5. US4 → Extended profiling (deeper data)
6. US5 → Scene context (smarter AI)
7. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- All new editor code must be in Editor/ assembly or behind `#if UNITY_EDITOR`
- All UI code uses C# VisualElement only (no UXML/USS per project convention)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
