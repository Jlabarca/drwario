# Feature Specification: Rich Diagnostics UI

**Feature Branch**: `001-rich-diagnostics-ui`
**Created**: 2026-03-08
**Status**: Draft
**Input**: DrWario v2.0 — Charts, data tables, script/asset links, deeper profiling, scene context

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate from Finding to Source Code (Priority: P1)

A Unity developer runs a DrWario profiling session and receives diagnostic findings. They click on a finding that mentions GC allocation spikes and are taken directly to the offending script at the relevant line in their IDE. For asset-related findings, clicking pings the asset in the Project window so they can inspect it immediately.

**Why this priority**: Direct actionability is the biggest gap — findings without source links require developers to manually hunt for the problem, which defeats the purpose of automated diagnostics.

**Independent Test**: Can be tested by running a profiling session, verifying findings contain clickable script/asset references, and confirming clicks open the correct file at the correct line.

**Acceptance Scenarios**:

1. **Given** a completed analysis with findings, **When** a finding references a script path, **Then** clicking the reference opens that file at the specified line in the IDE.
2. **Given** a finding that references a Unity asset (texture, prefab, material), **When** the user clicks the asset reference, **Then** the asset is highlighted (pinged) in the Project window.
3. **Given** an LLM-generated finding, **When** the LLM response includes script/asset references, **Then** those references appear as clickable links in the finding card.
4. **Given** a deterministic rule finding (e.g., slow boot stage), **When** the rule knows which script is responsible, **Then** the finding includes a pre-populated script reference.

---

### User Story 2 - Visualize Performance Trends with Charts (Priority: P1)

A developer wants to understand performance patterns over time rather than just summary statistics. They open the Summary or a new Charts tab and see interactive line charts for memory trajectory, frame time distribution, GC allocation timeline, and CPU vs GPU comparison. Hovering over data points shows exact values in tooltips.

**Why this priority**: Visual patterns (spikes, trends, distributions) communicate performance issues far more effectively than raw numbers. Charts transform DrWario from a grade card into a diagnostic tool.

**Independent Test**: Can be tested by running a profiling session and verifying each chart renders correctly with accurate data, tooltips show on hover, and charts update when new data arrives.

**Acceptance Scenarios**:

1. **Given** a completed profiling session, **When** the user views the Summary tab, **Then** a memory trajectory line chart displays the session's memory usage over time.
2. **Given** frame timing data, **When** the user views frame time distribution, **Then** a histogram shows the distribution of frame times with bucket counts.
3. **Given** GC allocation data, **When** the user views the GC timeline, **Then** a bar chart shows per-frame GC allocation bytes with spike frames highlighted.
4. **Given** both CPU and GPU timing data, **When** the user views the bottleneck chart, **Then** a comparison bar chart shows CPU vs GPU time with the bottleneck visually emphasized.
5. **Given** any chart, **When** the user hovers over a data point, **Then** a tooltip displays the exact value and frame number.

---

### User Story 3 - Explore Detailed Data in Sortable Tables (Priority: P2)

A developer wants to drill down into the raw data behind findings. They switch to a detail view and see sortable, filterable tables for: top GC allocation frames, slowest frames, asset load times, boot stage breakdown, and network events. They can sort by any column and click a row to navigate to the relevant finding or frame.

**Why this priority**: Tables complement charts by providing precise data for developers who need exact numbers. Sorting and filtering lets them find the worst offenders quickly.

**Independent Test**: Can be tested by running a profiling session and verifying tables populate with correct data, columns are sortable, and row selection triggers appropriate navigation.

**Acceptance Scenarios**:

1. **Given** frame data with GC spikes, **When** the user views the GC allocations table, **Then** frames are listed with GC bytes, sorted by allocation size descending by default.
2. **Given** asset load data, **When** the user sorts the asset load table by duration, **Then** the slowest loads appear first with asset path, duration, and size columns.
3. **Given** boot stage data, **When** the user views the boot stages table, **Then** all stages are listed with name, duration, and success/fail status.
4. **Given** any data table, **When** the user clicks a column header, **Then** the table re-sorts by that column (toggling ascending/descending).

---

### User Story 4 - Deeper Profiling Data Collection (Priority: P2)

A developer's game has physics-heavy scenes or complex UI. DrWario automatically captures additional performance counters — physics stats (rigidbodies, contacts), audio (voices, DSP load), animation (animator updates), and UI rebuilds (canvas, layout). These appear in findings, charts, and are included in LLM prompt context for smarter AI analysis.

**Why this priority**: Current profiling covers CPU/memory/GC/rendering but misses important subsystems. Physics and UI are common bottleneck sources that the current version cannot diagnose.

**Independent Test**: Can be tested by running a scene with physics objects and UI canvases, verifying that the additional counters appear in session data and LLM prompts.

**Acceptance Scenarios**:

1. **Given** a scene with active Rigidbodies, **When** a profiling session runs, **Then** physics counter data (active bodies, contacts) is recorded per frame.
2. **Given** a scene with UI Canvases, **When** a profiling session runs, **Then** UI rebuild counts (canvas rebuilds, layout rebuilds) are recorded.
3. **Given** extended profiling data is collected, **When** the LLM prompt is built, **Then** physics, audio, animation, and UI stats are included in the context JSON.
4. **Given** physics or UI counters exceed performance thresholds, **When** analysis runs, **Then** new analysis rules generate appropriate findings with severity and recommendations.

---

### User Story 5 - Scene-Aware AI Analysis (Priority: P3)

A developer starts a profiling session. DrWario automatically scans the active scene and captures a census of GameObjects, component types, Canvases, Lights, Cameras, particle systems, and LOD groups. This context is sent to the LLM, enabling smarter analysis that understands scene complexity (e.g., "You have 15 point lights — consider baking some of them").

**Why this priority**: Scene context makes LLM analysis significantly more relevant. Without it, the AI can only guess at scene composition. However, the profiling data itself is more fundamental.

**Independent Test**: Can be tested by loading a scene with known objects, starting a session, and verifying the scene census appears in the LLM prompt JSON.

**Acceptance Scenarios**:

1. **Given** a scene with various GameObjects, **When** a profiling session starts, **Then** a scene census is captured including total object count and component type distribution.
2. **Given** the scene contains Lights, Canvases, and ParticleSystems, **When** the census is captured, **Then** counts and types (e.g., point vs directional lights) are recorded.
3. **Given** a scene census exists, **When** the LLM prompt is built, **Then** the census JSON is included in the user prompt.
4. **Given** the LLM receives scene context, **When** it generates findings, **Then** findings may reference specific scene composition issues (e.g., excessive lights, missing LOD groups).

---

### Edge Cases

- What happens when a script reference in a finding points to a file that no longer exists? → Show the link as disabled/greyed with a "File not found" tooltip.
- What happens when profiling data has zero frames (session started and immediately stopped)? → Charts show an empty state message; tables show "No data collected."
- What happens when the LLM returns script references with invalid paths? → Validate paths before rendering as clickable; show as plain text if invalid.
- What happens when ProfilerRecorder counters are unavailable on the current platform? → Gracefully skip unavailable counters; show "N/A" in tables/charts for missing data.
- What happens when the scene has thousands of GameObjects? → Cap the component distribution to top 20 types; summarize the rest as "Other (N types)."

## Requirements *(mandatory)*

### Functional Requirements

**Script/Asset References:**
- **FR-001**: DiagnosticFinding MUST support optional script path, script line number, and asset path fields.
- **FR-002**: Finding cards in the UI MUST render script references as clickable links that open the file in the user's IDE at the specified line.
- **FR-003**: Finding cards MUST render asset references as clickable links that ping/highlight the asset in the Unity Project window.
- **FR-004**: The LLM response schema MUST request script and asset references in structured format, and the parser MUST extract them into finding fields.
- **FR-005**: Deterministic rules MUST populate script/asset references where the source is known (e.g., boot stage scripts, slow-loading asset paths).

**Charts:**
- **FR-006**: The UI MUST display a memory trajectory line chart showing managed heap usage over the session duration.
- **FR-007**: The UI MUST display a frame time distribution histogram bucketing frame times into ranges.
- **FR-008**: The UI MUST display a GC allocation timeline as a bar chart showing per-frame or per-second allocation bytes.
- **FR-009**: The UI MUST display a CPU vs GPU comparison chart showing average, P95, and P99 times for each.
- **FR-010**: All charts MUST show tooltips with exact values when the user hovers over data points.

**Data Tables:**
- **FR-011**: The UI MUST display sortable data tables for: GC allocation frames, slowest frames, asset loads, boot stages, and network events.
- **FR-012**: Tables MUST support sorting by any column via column header click (ascending/descending toggle).
- **FR-013**: Tables MUST display relevant columns per data type (e.g., asset loads show path, duration, size).

**Extended Profiling:**
- **FR-014**: The profiler MUST capture physics counters (active rigidbodies, contacts) when available on the platform.
- **FR-015**: The profiler MUST capture UI rebuild counters (canvas rebuilds, layout rebuilds) when available.
- **FR-016**: The profiler MUST capture audio counters (active voices, DSP CPU load) when available.
- **FR-017**: The profiler MUST capture animation counters (animator update count) when available.
- **FR-018**: Extended counters MUST be included in the LLM prompt context JSON.
- **FR-019**: Unavailable counters MUST be gracefully omitted without errors.

**Scene Context:**
- **FR-020**: On session start, the system MUST scan the active scene and capture: total GameObject count, component type distribution (top 20), Canvas count, Light count by type, Camera count, ParticleSystem count, LOD group count.
- **FR-021**: Scene context MUST be included in the LLM prompt as structured JSON.
- **FR-022**: Scene scanning MUST complete within a reasonable time and not block the editor for scenes with up to 10,000 objects.

### Key Entities

- **DiagnosticFinding**: Extended with ScriptPath (string), ScriptLine (int), AssetPath (string) — optional references to source code and assets related to the finding.
- **FrameSample**: Extended with physics, audio, animation, and UI counter fields captured per frame.
- **SceneCensus**: New entity capturing a snapshot of the active scene's composition — object counts, component distribution, light types, canvas count, etc.
- **ChartData**: Internal representation of time-series or distribution data prepared for chart rendering.

## Assumptions

- Unity 2022.3+ provides the ProfilerRecorder API with stable marker names for physics, UI, audio, and animation counters.
- The IDE integration for opening files at specific lines works via Unity's standard `AssetDatabase.OpenAsset()` or `InternalEditorUtility.OpenFileAtLineExternal()` — no custom IDE plugins required.
- Scene scanning uses `Object.FindObjectsOfType` variants which are fast enough for scenes up to 10,000 objects.
- Charts are rendered using Unity's Painter2D API (available in UIToolkit for Unity 2022.3+) without external charting libraries.
- Data tables use Unity's built-in `MultiColumnListView` control (available in UIToolkit for Unity 2022.3+).
- The LLM response format can be extended to include optional scriptPath/scriptLine/assetPath fields without breaking backward compatibility with existing providers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of deterministic rule findings that reference a known source include a clickable script or asset link.
- **SC-002**: Users can navigate from a finding to the relevant source file in under 2 seconds (one click).
- **SC-003**: All 4 chart types (memory, frame distribution, GC timeline, CPU/GPU) render correctly with data from a standard profiling session.
- **SC-004**: Chart tooltips display exact values within 200ms of hover interaction.
- **SC-005**: Data tables support sorting by any column and display at least 5 data table types (GC frames, slow frames, assets, boot, network).
- **SC-006**: Extended profiling counters (physics, UI, audio, animation) are captured when available and appear in LLM prompt context.
- **SC-007**: Scene context census is captured within 500ms for scenes up to 5,000 GameObjects and appears in LLM prompt JSON.
- **SC-008**: All new features have zero footprint in release builds (editor-only code, no runtime overhead).
