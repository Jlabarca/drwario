# Feature Specification: Deep Profiler & Iterative Workflow

**Feature Branch**: `002-deep-profiler-workflow`
**Created**: 2026-03-08
**Status**: Draft
**Input**: DrWario v3.0 — report comparison, streaming LLM, profiler markers, jump-to-frame, rule management, event timeline, expandable findings, HTML export

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compare Reports to Measure Optimization Impact (Priority: P1)

A developer has profiled their game, received a grade C, and made optimizations. They profile again and want to know: "Did my changes actually help?" They open the History tab, select the previous report, and click "Compare with current." A side-by-side view shows grade changes per category, which findings were fixed, which are new, and which persist — with delta arrows showing improvement or regression on key metrics.

**Why this priority**: Without comparison, users run DrWario in isolation each time with no way to track progress. This is the most requested iterative workflow feature and transforms DrWario from a one-shot tool into an optimization companion.

**Independent Test**: Run two profiling sessions with different game configurations. Compare reports and verify delta arrows, finding diff (fixed/new/persisting), and grade changes are accurate.

**Acceptance Scenarios**:

1. **Given** two saved reports in History, **When** user clicks "Compare with..." on one and selects another, **Then** a comparison view shows both reports side-by-side with grade deltas and metric changes
2. **Given** Report A has findings X, Y, Z and Report B has findings Y, Z, W, **When** compared, **Then** X is marked "Fixed", W is marked "New", and Y, Z are marked "Persists"
3. **Given** Report A has grade D (62) and Report B has grade B (84), **When** compared, **Then** overall grade shows "D → B (+22)" with a green improvement indicator
4. **Given** only one report exists in History, **When** user views History tab, **Then** "Compare with..." button is disabled or hidden

---

### User Story 2 - Event Timeline for Temporal Analysis (Priority: P1)

A developer wants to understand the temporal relationship between performance events — when did GC spikes happen relative to frame drops? Did asset loads cause stuttering? They open the Timeline tab and see a horizontal scrollable timeline with color-coded lanes showing frame spikes, GC events, boot stages, and asset loads all correlated in time. They zoom into a region of interest and click an event to see its details.

**Why this priority**: The timeline is the most visually impactful feature and enables temporal correlation analysis that no other DrWario view provides. It connects the dots between cause and effect across subsystems.

**Independent Test**: Profile a scene with known events (physics spike + GC allocation burst at same frame), open Timeline tab, verify events are correctly positioned in time with correct lane colors, zoom/pan works, and click shows event details.

**Acceptance Scenarios**:

1. **Given** a completed profiling session, **When** user opens Timeline tab, **Then** timeline shows color-coded lanes for CPU frame spikes, GC allocations, boot stages, asset loads, and network events
2. **Given** a timeline is displayed, **When** user scrolls mouse wheel, **Then** timeline zooms in/out centered on cursor position
3. **Given** a timeline is displayed, **When** user clicks and drags, **Then** timeline pans horizontally
4. **Given** a frame spike event on timeline, **When** user clicks it, **Then** a tooltip or detail panel shows frame index, CPU time, and associated metrics
5. **Given** no profiling data, **When** user opens Timeline tab, **Then** "No data — run a profiling session first" message is shown

---

### User Story 3 - Profiler Marker Capture for Root Cause Analysis (Priority: P2)

A developer gets a finding "frame drops exceeding 50ms" but doesn't know which subsystem is responsible. DrWario now captures hierarchical profiler markers (e.g., PlayerLoop > FixedUpdate > Physics.Simulate = 35ms) and includes the top most-expensive markers in the LLM prompt. The AI can now say "Physics.Simulate is consuming 70% of your frame budget" instead of the generic "check your Update loop."

**Why this priority**: This dramatically improves the quality of AI analysis by giving it the same data a human would see in Unity's Profiler. It bridges the gap between DrWario's high-level metrics and Unity Profiler's detailed call stacks.

**Independent Test**: Profile a scene with expensive physics, verify the profiler markers section appears in the LLM prompt (visible in console debug logs), and verify AI findings reference specific subsystem names.

**Acceptance Scenarios**:

1. **Given** a profiling session is running, **When** frames are captured, **Then** the top 20 most expensive profiler markers (by inclusive time) are recorded per frame
2. **Given** a completed session with marker data, **When** analysis runs, **Then** the LLM prompt includes a "profilerMarkers" section with marker name, inclusive time, exclusive time, and call count
3. **Given** marker data shows Physics.Simulate at 35ms average, **When** AI analyzes, **Then** findings reference the specific marker name rather than generic advice
4. **Given** ProfilerRecorder markers are unavailable on the platform, **When** profiling runs, **Then** marker capture is gracefully skipped with no errors

---

### User Story 4 - Streaming LLM Responses (Priority: P2)

A developer clicks Analyze and currently waits 10-30 seconds with no feedback while the LLM processes. With streaming, findings appear progressively in the UI as the LLM generates them — the first finding might appear after 3 seconds, with more flowing in over the next 15 seconds. This gives immediate feedback and lets users start reading results sooner.

**Why this priority**: The current blocking experience makes DrWario feel slow. Streaming transforms the perception from "waiting" to "watching results appear." This is a significant UX improvement.

**Independent Test**: Configure Claude or OpenAI provider, run analysis, verify findings appear one-by-one in the UI as they stream in. Verify Ollama falls back to non-streaming behavior gracefully.

**Acceptance Scenarios**:

1. **Given** Claude or OpenAI is configured, **When** user clicks Analyze, **Then** findings appear progressively in the UI as the LLM generates them
2. **Given** streaming is active, **When** the first finding is parsed from the stream, **Then** it appears in the findings list within 5 seconds of the request starting
3. **Given** Ollama or Custom provider is configured, **When** user clicks Analyze, **Then** the current blocking behavior is used as fallback
4. **Given** streaming is active, **When** the connection drops mid-stream, **Then** any findings received so far are kept and an error message indicates the stream was interrupted
5. **Given** streaming is active, **When** user views the UI, **Then** a progress indicator shows that more findings may arrive

---

### User Story 5 - Jump to Profiler Frame (Priority: P2)

A developer reads a finding that references specific frame indices (e.g., "Frame #847: 67ms CPU time"). They click a "Show in Profiler" link next to the frame reference. Unity's Profiler window opens and navigates to that exact frame, letting the developer immediately drill into the call stack without manually hunting for the right frame.

**Why this priority**: This creates a direct bridge between DrWario findings and Unity's Profiler, which is the natural next step in any investigation workflow. It eliminates the manual step of finding the right frame.

**Independent Test**: Run profiling with Profiler window recording simultaneously, get a finding with a frame reference, click "Show in Profiler", verify Profiler window opens and selects the correct frame.

**Acceptance Scenarios**:

1. **Given** a finding references frame index #N, **When** user clicks "Show in Profiler", **Then** Unity Profiler window opens and selects frame #N
2. **Given** Unity Profiler was not recording during the DrWario session, **When** user clicks "Show in Profiler", **Then** a message explains "Profiler data not available — enable Profiler recording alongside DrWario for frame-level drill-down"
3. **Given** a finding does not reference a specific frame, **Then** no "Show in Profiler" link is displayed

---

### User Story 6 - Rule Enable/Disable and Threshold Configuration (Priority: P3)

A developer working on a WebGL project doesn't care about network latency rules since they're building an offline game. They open the Rules settings, disable the Network Latency Rule, and adjust the GC allocation spike threshold from 1KB to 4KB because WebGL has higher baseline allocations. Their next analysis run skips the disabled rule and uses the custom threshold.

**Why this priority**: Power users need control over which rules run and at what thresholds. Without this, they get irrelevant findings that add noise. However, the default rules work well for most users, making this a P3.

**Independent Test**: Disable a rule, run analysis, verify the rule's findings don't appear. Change a threshold, run analysis, verify the threshold is respected.

**Acceptance Scenarios**:

1. **Given** the Rules settings view, **When** displayed, **Then** all registered rules are shown with a toggle switch and current threshold values
2. **Given** a rule is disabled via toggle, **When** analysis runs, **Then** that rule is skipped and produces no findings
3. **Given** GC spike threshold is changed from 1KB to 4KB, **When** analysis runs, **Then** only frames exceeding 4KB are flagged
4. **Given** a custom rule is registered via RegisterRule(), **When** Rules settings is opened, **Then** the custom rule appears in the list with a toggle
5. **Given** rule settings are changed, **When** the editor is restarted, **Then** settings persist (stored in EditorPrefs)

---

### User Story 7 - Expandable Finding Cards (Priority: P3)

A developer sees a "GC allocation spikes" finding and wants more detail without switching views. They click the finding card, which expands to show: the specific frame indices where spikes occurred, a mini-chart of GC allocations over time highlighting the spike frames, and related findings from the same category. They can collapse it back with another click.

**Why this priority**: Expandable cards add depth to the existing findings view without requiring new tabs or views. They're a quality-of-life improvement that makes findings more actionable in-place.

**Independent Test**: Generate findings, click to expand a finding card, verify frame list, mini-chart, and related findings appear. Click again to collapse.

**Acceptance Scenarios**:

1. **Given** a finding card in the Findings tab, **When** user clicks it, **Then** the card expands to show additional details
2. **Given** an expanded finding, **Then** a list of affected frame indices is displayed (up to 20 frames)
3. **Given** an expanded finding, **Then** a mini-chart shows the relevant metric over the session timeline with spike frames highlighted
4. **Given** an expanded finding, **Then** related findings from the same category are listed
5. **Given** an expanded finding, **When** user clicks it again, **Then** the card collapses back to its summary view

---

### User Story 8 - HTML Report Export (Priority: P3)

A developer wants to share DrWario results with their team lead who doesn't have Unity installed. They click "Export HTML" and get a self-contained HTML file with embedded styles, SVG charts, all finding cards, grade summary, and data tables. The team lead opens it in any browser and sees a professional diagnostic report.

**Why this priority**: Text and JSON exports exist but aren't visually compelling. HTML export makes reports shareable and presentable, but it's a "nice to have" since the core value is in-editor analysis.

**Independent Test**: Run analysis, click Export HTML, open the generated file in a browser, verify all sections render correctly with charts and styling.

**Acceptance Scenarios**:

1. **Given** a completed analysis, **When** user clicks "Export HTML", **Then** a self-contained HTML file is saved with embedded CSS and inline SVG charts
2. **Given** the exported HTML file, **When** opened in Chrome/Firefox/Edge, **Then** grade summary, finding cards, charts, and data tables render correctly
3. **Given** the exported HTML file, **Then** it contains no external dependencies (no CDN links, all styles inline)
4. **Given** no analysis has been run, **When** user clicks "Export HTML", **Then** the button is disabled or shows "Run analysis first"

---

### Edge Cases

- What happens when comparing reports from different Unity versions or platforms? Display a warning banner noting the mismatch but allow comparison
- What happens when timeline has 3600 frames at 60fps? Performance must remain smooth — use virtualized rendering or downsampling for display
- What happens when ProfilerRecorder markers return inconsistent data across frames? Average over the session and flag high variance markers
- What happens when LLM stream sends malformed JSON mid-stream? Parse findings up to the last valid JSON object and continue
- What happens when a user tries to jump to Profiler frame but Unity Profiler module is disabled? Show an informative error and instructions to enable it
- What happens when all rules are disabled? Analysis runs with zero deterministic findings — only AI findings (if configured) appear. Show a warning that all rules are disabled
- What happens when comparing a report that has scene census data with one that doesn't? Show census data only for the report that has it, with "N/A" for the other

## Requirements *(mandatory)*

### Functional Requirements

**Report Comparison:**
- **FR-001**: System MUST allow selecting two reports from History for side-by-side comparison
- **FR-002**: System MUST display grade deltas (overall and per-category) with improvement/regression indicators
- **FR-003**: System MUST classify findings as "Fixed", "New", or "Persists" by matching on RuleId and Category
- **FR-004**: System MUST display metric deltas for key values (avg CPU time, P95, GC rate, memory slope)

**Event Timeline:**
- **FR-005**: System MUST render a horizontal scrollable timeline with color-coded event lanes
- **FR-006**: System MUST support mouse wheel zoom and drag-to-pan on the timeline
- **FR-007**: System MUST show event details on click (tooltip or side panel)
- **FR-008**: System MUST display reference lines at target frame time thresholds (16.67ms, 33.33ms)

**Profiler Markers:**
- **FR-009**: System MUST capture the top 20 most expensive profiler markers per session using ProfilerRecorder
- **FR-010**: System MUST include marker data (name, avg inclusive time, avg exclusive time, call count) in the LLM prompt
- **FR-011**: System MUST gracefully skip marker capture when ProfilerRecorder is unavailable

**Streaming LLM:**
- **FR-012**: System MUST support SSE (Server-Sent Events) streaming for Claude and OpenAI providers
- **FR-013**: System MUST parse and display findings incrementally as they arrive in the stream
- **FR-014**: System MUST fall back to non-streaming request for Ollama and Custom providers
- **FR-015**: System MUST preserve partially received findings if the stream is interrupted

**Jump to Profiler:**
- **FR-016**: System MUST display a "Show in Profiler" link on findings that reference specific frame indices
- **FR-017**: System MUST open Unity Profiler window and navigate to the referenced frame when clicked
- **FR-018**: System MUST show an informative message when Profiler data is not available

**Rule Management:**
- **FR-019**: System MUST display all registered rules with toggle switches in a settings view
- **FR-020**: System MUST persist rule enable/disable state and custom thresholds in EditorPrefs
- **FR-021**: System MUST skip disabled rules during analysis
- **FR-022**: System MUST expose configurable thresholds for rules that support them

**Expandable Finding Cards:**
- **FR-023**: System MUST expand finding cards on click to show affected frame indices, a mini-chart, and related findings
- **FR-024**: System MUST collapse expanded cards on second click
- **FR-025**: System MUST limit displayed frame indices to 20 entries with a "show more" option

**HTML Export:**
- **FR-026**: System MUST generate a self-contained HTML file with embedded CSS and inline SVG charts
- **FR-027**: System MUST include grade summary, finding cards, metric charts, and data tables in the export
- **FR-028**: System MUST render correctly in modern browsers (Chrome, Firefox, Edge) without external dependencies

### Key Entities

- **ReportComparison**: Pair of DiagnosticReports with computed deltas (grade diff, metric diffs, finding classifications)
- **FindingDiff**: Classification of a finding across two reports — Fixed, New, or Persists
- **ProfilerMarkerSample**: Marker name, inclusive time, exclusive time, call count — aggregated per session
- **TimelineEvent**: Timestamp, duration, event type (FrameSpike, GCAlloc, BootStage, AssetLoad, NetworkEvent), associated data
- **RuleConfig**: Rule ID, enabled state, custom threshold overrides — persisted per rule

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can compare two reports and identify improvements/regressions within 10 seconds of selecting them
- **SC-002**: Timeline renders 3600 frames of event data with smooth scrolling and zoom at 30+ FPS in the editor
- **SC-003**: AI findings reference specific profiler marker names (e.g., "Physics.Simulate") when marker data is available, rather than generic subsystem guesses
- **SC-004**: First streaming finding appears within 5 seconds of clicking Analyze (for Claude/OpenAI providers)
- **SC-005**: Users can navigate from a finding to the corresponding Profiler frame in 2 clicks or fewer
- **SC-006**: Rule configuration changes take effect immediately on the next analysis run without editor restart
- **SC-007**: Expanded finding cards display affected frame data and mini-charts within 1 second of clicking
- **SC-008**: Exported HTML reports render correctly in all three major browsers and contain all analysis data
- **SC-009**: All new features work without adding external dependencies (only UnityEngine, UnityEditor, System)
- **SC-010**: Package version bumps to 3.0.0 with all features integrated into the existing tab-based UI

## Assumptions

- ProfilerRecorder API provides marker names at the granularity needed (subsystem level like "Physics.Simulate", "Animation.Update") — if only top-level markers are available, the feature still adds value with coarser data
- Unity's Profiler window can be programmatically focused and frame-navigated via public or `[Obsolete]`-but-functional API — if only reflection works, it will be wrapped in a try-catch with graceful degradation
- SSE streaming format for Claude and OpenAI follows their current documented specifications (delta content chunks in `data:` lines)
- The 3600-frame ring buffer provides enough data points for meaningful timeline visualization at 60fps (~60 seconds)
- HTML export uses basic inline SVG for charts rather than a JavaScript charting library, keeping the file self-contained
- Rule thresholds are simple numeric values (float) — complex threshold configurations (e.g., per-platform) are out of scope for v3.0
