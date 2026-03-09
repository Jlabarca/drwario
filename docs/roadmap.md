# DrWario Roadmap

> Vision and development priorities for DrWario as a self-sufficient performance analysis platform with optional AI enhancement.

## Design Principles

1. **Report stands alone** — Deterministic rules + correlation engine produce a complete, actionable report without AI
2. **AI is on-demand** — Users choose to "Enhance with AI" after reviewing the deterministic report
3. **No source code sent** — Only statistical summaries go to LLMs. Privacy by design
4. **Zero dependencies** — Works with vanilla Unity. No UniTask, no Newtonsoft, no third-party
5. **Zero release overhead** — Conditional compilation strips everything in production
6. **Framework-agnostic** — Integration via events and static properties, not hard coupling
7. **Measure yourself, not yourself** — DrWario's own overhead is subtracted from measurements to prevent false positives

---

## Architecture Evolution

### Legacy Architecture (v1.0)

The original architecture was a simple 3-phase pipeline where AI was deeply integrated:

```
AnalysisEngine (v1.0)
  ├── Phase 1: Run 6 deterministic rules
  ├── Phase 2: Run AI analysis (always, if configured)
  └── Phase 3: Deduplicate + grade
```

**Problems:**
- Reports were incomplete without AI — deterministic rules found problems but couldn't explain correlations or prioritize fixes
- AI ran automatically as part of analysis, blocking the editor for 10-30s
- No editor overhead awareness — findings in editor Play Mode were inflated by Scene view, Inspector, Profiler overhead
- Basic sampling only — `Profiler.GetTotalAllocatedMemoryLong()` and `FrameTimingManager` with no detailed counters
- No scene context — zero awareness of what GameObjects existed or changed during profiling
- No way to compare reports or track improvement over time

### Current Architecture (v2.0)

The architecture was redesigned around self-sufficiency: the deterministic pipeline now produces a complete, standalone report with executive summary, correlation insights, and prioritized actions. AI becomes an optional enhancement layer.

```
AnalysisEngine (v2.0)
  ├── Phase 1: Run 8 deterministic rules (+ RenderingEfficiency, CPUvsGPU)
  ├── Phase 2: CorrelationEngine — detect cross-cutting patterns
  ├── Phase 3: ComputeGrades — A-F per category + overall
  ├── Phase 4: ReportSynthesizer — executive summary, bottleneck ID, prioritized actions
  └── (On demand) EnhanceWithAI — streaming SSE, adds AI findings without re-running rules
```

**Why the change:**
- Reports needed to be useful immediately, without waiting for AI or requiring API keys
- The correlation engine replaces ~60-70% of what AI was doing — detecting GC↔frame drops, asset loads↔GC, memory leaks↔allocation patterns deterministically
- AI now adds value where deterministic analysis can't: platform-specific advice, Unity-specific patterns, natural language explanations
- Separating AI makes the tool usable offline, in CI, and in environments where API keys aren't available

**New capabilities in v2.0:**
- **ProfilerBridge** — `ProfilerRecorder` counters for rendering (draw calls, batches, triangles, SetPass), physics, audio, animation, UI
- **Editor baseline** — Captures idle editor overhead pre-Play Mode, adjusts thresholds and classifies finding confidence (High/Medium/Low)
- **Scene tracking** — `SceneCensusCapture` for static census, `SceneSnapshotTracker` for hierarchy diffs at key moments with deferred capture to avoid false positives
- **False positive prevention** — DrWario.Sample marker overhead subtracted from CPU, capture frames excluded from GC analysis
- **SSE streaming** — Custom `DownloadHandlerScript` for progressive AI findings display
- **Rich UI** — Chart components (line, bar, histogram), timeline view, data tables
- **Rule management** — Enable/disable individual rules, adjustable thresholds via `IConfigurableRule`
- **HTML export** — Self-contained report with embedded CSS and inline SVG charts

---

## Current State (v2.0)

### What works
- 8 deterministic analysis rules (GC, CPU, Boot, Memory, Assets, Network, Rendering, CPU/GPU Bottleneck)
- CorrelationEngine detecting 8 cross-cutting patterns
- ReportSynthesizer producing standalone executive summaries
- A-F grading with per-category breakdown and confidence scoring
- ProfilerBridge with ProfilerRecorder counters (rendering, physics, audio, UI, animation)
- Profiler marker sampling (PlayerLoop, FixedUpdate, Update, LateUpdate, Rendering, Physics, Animation)
- Editor baseline capture with threshold adjustment
- Scene census (object/component/canvas/light counts)
- Scene snapshot tracking with deferred hierarchy diff capture
- False positive prevention (self-overhead subtraction, capture frame exclusion)
- 4 LLM providers (Claude, OpenAI, Ollama, Custom) with SSE streaming
- On-demand "Enhance with AI" button (separate from deterministic analysis)
- Rich UI components (line charts, bar charts, histograms, data tables, timeline)
- Free-form "Ask Doctor" Q&A with profiling context
- Report history with JSON/text/HTML export
- Auto-start on Play Mode
- Rule enable/disable UI with adjustable thresholds
- Framework integration via events and conditional compilation
- HybridFrame integration complete

### What doesn't
- 0 of 91 designed tests implemented
- GPU profiling returns 0 on many platforms (no fallback)
- No report comparison (side-by-side diff)
- No jump-to-Profiler-frame from findings
- No script/asset links in findings
- No Frame Debugger integration

---

## Completed Milestones

### Bug Fixes (v1.1)

- [x] Fix `GcAllocBytes` double-call in `RuntimeCollector`
- [x] Make `LLMClient._lastRequestTime` static (rate limiter fix)
- [x] Fix `CategoryGrades` missing from JSON export
- [x] Validate `TestConnectionAsync` response content
- [x] Make analysis pipeline async (editor no longer freezes)

### Profiler Integration (v2.0)

- [x] `ProfilerBridge` — ProfilerRecorder counters (draw calls, batches, GC.Alloc, SetPass, triangles, vertices)
- [x] Extended subsystem counters (physics, audio, animation, UI canvas/layout rebuilds)
- [x] Enhanced `FrameSample` with rendering, physics, audio, UI, scene, and memory fields
- [x] `RenderingEfficiencyRule` — draw call / batching / set-pass analysis
- [x] `CPUvsGPUBottleneckRule` — bottleneck classification
- [x] Profiler marker sampling (top-N most expensive markers by inclusive time)
- [x] DrWario.Sample marker for self-overhead tracking
- [x] Fallback to legacy sampling if `ProfilerRecorder` unavailable

### Self-Sufficient Reports (v2.0)

- [x] CorrelationEngine — 8 cross-cutting pattern detectors
- [x] ReportSynthesizer — executive summary, bottleneck ID, prioritized actions
- [x] AI split to on-demand "Enhance with AI" button
- [x] Report auto-saved after every analysis

### Editor Context Awareness (v2.0)

- [x] EditorBaselineCapture — 30-frame idle editor overhead measurement
- [x] Confidence scoring (High/Medium/Low) on findings based on baseline subtraction
- [x] Environment notes on editor-inflated findings
- [x] Editor window state detection (Scene, Inspector, Profiler, GameView count)

### Scene Awareness (v2.0)

- [x] SceneCensusCapture — static scene analysis (objects, components, canvases, lights, particle systems)
- [x] SceneSnapshotTracker — hierarchy diffs on spike/periodic/GC triggers
- [x] Deferred capture (trigger frame N → capture frame N+1) to avoid inflating measurements
- [x] Per-frame ObjectCount, NativeMemoryBytes, GcCollectionCount tracking
- [x] Scene snapshot data included in LLM prompts

### False Positive Prevention (v2.0)

- [x] DrWario.Sample marker overhead subtracted from CpuFrameTimeMs
- [x] Capture frames excluded from GCAllocationRule spike counting
- [x] Deferred snapshot capture prevents self-inflated GC on spike frames

### Streaming LLM (v2.0)

- [x] SSE streaming via custom `DownloadHandlerScript` (SseDownloadHandler)
- [x] Progressive finding display as AI findings arrive
- [x] Streaming for Claude and OpenAI providers
- [x] Fallback to blocking for Ollama/Custom providers

### Rule Management (v2.0)

- [x] Toggle individual rules on/off in LLM Settings tab
- [x] `IConfigurableRule` interface for adjustable thresholds
- [x] Persist rule preferences in EditorPrefs via `RuleConfig`
- [x] Custom rules via `RegisterRule()` also appear in UI

### Rich UI (v2.0)

- [x] ChartElement base class with axis rendering and data scaling
- [x] LineChart — time-series sparkline with hover tooltips
- [x] BarChart — categorical bar chart with labels
- [x] Histogram — distribution histogram with bin calculation
- [x] DataTableBuilder — sortable table generation
- [x] TimelineElement — horizontal scrollable event timeline with zoom/pan
- [x] HTML report export with embedded CSS and inline SVG charts

---

## Near-Term Priorities

### 1. Automated Tests

91 tests designed in the HybridFrame test plan. Priority order:

1. **ProfilingSession ring buffer** — wrap-around, chronological ordering, recording guard
2. **Grading formula** — penalty math, clamping, grade boundaries
3. **Rule unit tests** — known data patterns → expected findings and severities
4. **CorrelationEngine** — known finding patterns → expected correlations
5. **LLMResponseParser** — code fences, bare arrays, malformed JSON
6. **Deduplication** — AI priority, title normalization, category matching

### 2. Report Comparison

- [ ] Side-by-side diff of two historical reports
- [ ] Delta arrows showing improvement/regression per metric and category grade
- [ ] Highlight fixed, new, and persistent findings
- [ ] "Compare with..." button on each history entry

### 3. Jump-to-Profiler-Frame

- [ ] "Show in Profiler" clickable link on findings with frame indices
- [ ] Opens Unity Profiler window and navigates to exact frame
- [ ] Uses ProfilerWindow API or reflection

### 4. GPU Profiling Fallback

- [ ] Detect when `FrameTimingManager` returns 0
- [ ] Use `ProfilerRecorder` GPU counter as primary source
- [ ] Platform-specific fallback estimation
- [ ] Sentinel value to distinguish "unsupported" from "zero GPU work"

---

## Medium-Term Goals

### CI Integration

Run DrWario headless in automated builds:

```bash
unity -batchmode -executeMethod DrWario.CI.RunProfile \
  -minGrade B \
  -duration 30 \
  -exportPath report.json
```

- Headless profiling session with configurable duration
- Export report and fail build if grade < threshold
- JSON report for integration with CI dashboards
- No LLM required — rules-only mode for deterministic CI

### Plugin Auto-Discovery

- Assembly scanning for `IAnalysisRule` implementations
- `[DrWarioRule]` attribute for auto-registration
- Rule priority/ordering via attribute

### Expandable Finding Cards

- Click to expand with sub-details
- Show affected frame indices, mini-chart of metric over time, related findings
- Collapse/expand animation

### Script/Asset References in Findings

- Add `ScriptPath`, `ScriptLine`, `AssetPath` fields to `DiagnosticFinding`
- Clickable links that open files in IDE or ping assets in Project window
- LLM response schema updated to request references

---

## Long-Term Vision

### Phase 1: Deeper Context

- Source code snippets from flagged hot paths (opt-in)
- Build settings correlation (IL2CPP vs Mono, compression, stripping)
- Multi-session trend analysis ("memory grew 5% since last week")
- Scene-specific profiling comparison

### Phase 2: Automated Fix Suggestions

- Generate code patches for common issues (object pooling, cached delegates)
- Suggest specific Unity settings changes with before/after estimates
- Recommend asset pipeline changes (texture compression, LOD levels)
- Output actionable PR descriptions

### Phase 3: Continuous Monitoring Agent

- Background profiling during play testing
- Real-time alerts on performance degradation
- Regression detection across git commits
- Performance budget enforcement

### Phase 4: Cross-Project Intelligence

- Anonymized pattern database from opted-in projects
- Genre-specific benchmarks (FPS, RTS, mobile casual, VR)
- Community-contributed analysis rules

---

## Technical Debt Backlog

| Issue | Priority | Status |
|-------|----------|--------|
| ~~`Task.Wait()` blocking~~ | ~~High~~ | **FIXED** — async pipeline |
| ~~GcAllocBytes double-call~~ | ~~High~~ | **FIXED** |
| ~~Rate limiter per-instance~~ | ~~Medium~~ | **FIXED** — static field |
| ~~CategoryGrades JSON export~~ | ~~Medium~~ | **FIXED** — serializable list |
| ~~TestConnection validation~~ | ~~Low~~ | **FIXED** — content check |
| ~~Basic profiling (no ProfilerRecorder)~~ | ~~High~~ | **FIXED** — ProfilerBridge |
| AdditionalContext global static | Medium | Open — needs registration pattern |
| BootStageRule multi-RuleId | Low | Open — split into 3 rules |
| Hand-rolled JSON parser | Low | Open — fragile but works |

---

## Contributing

DrWario is open source. Contributions welcome at https://github.com/Jlabarca/drwario.

### Code Style

- Namespaces: `DrWario.Runtime` / `DrWario.Editor.*`
- No external dependencies
- UI: C# VisualElement only (no UXML/USS)
- Conditional compilation: `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- Log prefix: `[DrWario]`
- Severity levels: Info (blue), Warning (yellow), Critical (red)
