# DrWario Vision Gap Analysis

**Date:** 2026-03-08
**Purpose:** Compare current implementation against the planned final version and the LLM-core vision.

---

## The Vision (LLM-Core)

> "I want DrWario to be a smart profiler + frame debugger metadata scraper able to wrap this context over a prompt good enough to output LLM-based findings and present the findings in a good way (charts, data tables) with links to real scripts and line codes but also links to assets if needed or whatever we need to have the best UX possible."

This breaks down into **5 pillars**:

| # | Pillar | Description |
|---|--------|-------------|
| 1 | **Smart Profiler** | Deep integration with Unity's profiling subsystems (CPU, GPU, memory, rendering) |
| 2 | **Frame Debugger Metadata Scraper** | Extract per-draw-call data, shader info, render pass details |
| 3 | **Rich LLM Context** | Wrap all profiling + frame debugger data into prompts that produce actionable findings |
| 4 | **Best-in-class Presentation** | Charts, data tables, sparklines, interactive visualizations |
| 5 | **Deep Code/Asset Links** | Clickable links to scripts (file + line), assets, shaders, GameObjects |

---

## Current State Assessment

### What Exists Today (v2.0)

| Component | Status | Quality |
|-----------|--------|---------|
| Runtime data collection (CPU, GPU, memory, GC, rendering, physics, audio, UI, animation, boot, assets, network) | Done | Good |
| ProfilerBridge with ProfilerRecorder counters | Done | Good |
| Profiler marker sampling (top-N subsystem timers) | Done | Good |
| Ring buffer (3600 frames, 28 fields per sample) | Done | Solid |
| 8 deterministic analysis rules | Done | Good |
| CorrelationEngine (8 cross-cutting patterns) | Done | Good |
| ReportSynthesizer (standalone executive summary) | Done | Good |
| LLM integration (Claude, OpenAI, Ollama, Custom) with SSE streaming | Done | Good |
| On-demand AI enhancement (separate from deterministic analysis) | Done | Good |
| LLM prompt builder with profiling + scene + marker context | Done | Good |
| Grade computation (A-F) with confidence scoring | Done | Solid |
| Editor baseline capture + threshold adjustment | Done | Good |
| Scene census + scene snapshot tracking (hierarchy diffs) | Done | Good |
| False positive prevention (self-overhead subtraction) | Done | Good |
| Report history + persistence | Done | Good |
| Ask Doctor (free-form Q&A) | Done | Good |
| Rule management UI (enable/disable, adjustable thresholds) | Done | Good |
| Rich UI (line charts, bar charts, histograms, data tables, timeline) | Done | Good |
| Text/JSON/HTML report export | Done | Good |

### What's Missing

| Gap | Impact | Effort |
|-----|--------|--------|
| No Frame Debugger data scraping | Critical | High |
| No links to scripts/assets in findings | Critical | Medium |
| No render pipeline metadata (shader passes, materials) | High | High |
| No comparative analysis (diff two reports) | Medium | Medium |
| No jump-to-Profiler-frame integration | Medium | Medium |
| No automated tests | Medium | High |
| No CI/CD headless mode | Low | Medium |
| GPU timing returns 0 on some platforms | Medium | Medium |

---

## Feature Comparison Matrix

### Pillar 1: Smart Profiler

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Frame timing (CPU) | ProfilerBridge + FrameTimingManager + DrWario.Sample subtraction | Done | Full CPU timeline with markers |
| Frame timing (GPU) | `FrameTimingManager` (often 0) | Fallback estimation | Per-pass GPU timing |
| GC allocations | ProfilerRecorder GC.Alloc bytes + count per frame | Allocation callstack sampling | Source-attributed allocations |
| Memory (heap) | ProfilerBridge TotalUsedMemory + native + texture + mesh | Done | Per-system memory attribution |
| Draw calls / batches | ProfilerRecorder (draw calls, batches, SetPass, triangles, vertices) | Done | Per-object draw cost |
| Boot pipeline | Manual `BootTimingHook` callbacks | Automatic subsystem timing | Full boot waterfall |
| Asset loading | Manual `RecordAssetLoad` calls | Automatic via hooks | Asset dependency graph |
| Network events | Manual `RecordNetworkEvent` calls | Automatic via hooks | Protocol-level analysis |
| Physics | ProfilerRecorder (active bodies, kinematic bodies, contacts) | Done | Per-collider cost |
| Audio | ProfilerRecorder (voice count, DSP load) | Done | Per-source analysis |
| Animation | ProfilerRecorder (animator count) | Done | Animator state costs |
| UI (UGUI/UIToolkit) | ProfilerRecorder (canvas rebuilds, layout rebuilds) | Done | Per-canvas rebuild cost |
| Scene hierarchy | SceneCensus + SceneSnapshotTracker (diffs at key frames) | Done | Live hierarchy tree |
| Profiler markers | Top-N by inclusive time (PlayerLoop, Update, Rendering, Physics, etc.) | Done | Deep marker tree |

**Gap Score: ~75% complete toward vision**

### Pillar 2: Frame Debugger Metadata Scraper

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Draw call list | Not implemented | Not planned | Full per-frame draw list |
| Shader/material per draw | Not implemented | Not planned | Shader variant identification |
| Render pass structure | Not implemented | Not planned | SRP pass breakdown |
| Overdraw estimation | Not implemented | Not planned | Per-pixel cost heatmap |
| Texture memory per draw | Not implemented | Not planned | Bound texture analysis |
| State changes | Not implemented | Not planned | Redundant state detection |

**Gap Score: 0% complete — not started**

> **Note:** Unity's Frame Debugger API is internal/limited. Full scraping would require either:
> - Reflection into `UnityEditorInternal.FrameDebuggerUtility` (fragile, version-dependent)
> - `ProfilerRecorder` with rendering markers (partial data)
> - Custom SRP render pass instrumentation (requires SRP project)
>
> **Recommendation:** Start with ProfilerRecorder rendering counters (draw calls, batches, triangles, SetPass calls per category) which are stable and public. Frame Debugger deep integration should be a stretch goal.

### Pillar 3: Rich LLM Context

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Session metadata in prompt | Yes | Yes | Yes |
| Frame summary stats (avg/P95/P99) | Yes | Yes | Yes |
| Memory trajectory (12 points + regression) | Yes | Yes | Full memory timeline |
| Boot pipeline data | Yes | Yes | Yes |
| Asset load data | Yes | Yes | + dependency graph |
| Pre-analysis findings in prompt | Yes | Yes | Yes |
| Editor context (baseline, windows) | Yes | Yes | Yes |
| Framework context injection | Yes (AdditionalContext) | Yes | Yes |
| Script/asset references in prompt | No | Planned | Critical — enables code links |
| Scene hierarchy context | Yes (census + snapshot diffs) | Done | Object counts, nesting depth |
| Profiler marker data | Yes (top-N by inclusive time) | Done | Deep marker tree |
| Rendering pipeline context | No | Not planned | Pass structure, shader list |
| Structured response format | JSON array | JSON array | Richer schema with links |

**Gap Score: ~75% complete toward vision**

### Pillar 4: Best-in-class Presentation

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Letter grades (A-F) | Yes | Yes | Yes |
| Sparkline (frame times) | LineChart with hover tooltips | Done | Interactive zoom/pan |
| Finding cards | Severity-colored with confidence badges | Enhanced | Expandable with drill-down |
| Category breakdown | Grade per category with confidence | Done | Treemap/sunburst |
| Data tables | DataTableBuilder (sortable) | Done | Filterable tables |
| Charts (memory, GC, etc.) | LineChart, BarChart, Histogram | Done | Done |
| Timeline view | TimelineElement with zoom/pan, color-coded lanes | Done | Done |
| Report comparison | No | Planned | Diff view with delta arrows |
| Clipboard copy (findings) | Yes | Yes | Yes |
| Export (text/JSON/HTML) | Yes (HTML with embedded CSS/SVG) | Done | + PDF |

**Gap Score: ~75% complete toward vision**

### Pillar 5: Deep Code/Asset Links

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Links to scripts (file:line) | No | Planned | Clickable, opens in IDE |
| Links to assets | No | Not planned | Ping in Project window |
| Links to GameObjects | No | Not planned | Select in Hierarchy |
| Links to Profiler frames | No | Not planned | Jump to frame in Profiler |
| Links to shaders | No | Not planned | Open shader source |
| Links to documentation | No | Not planned | Unity docs for relevant APIs |

**Gap Score: 0% complete — not started**

> **Implementation approach:** LLM findings already have a `RuleId` and `Description`. Adding a `ScriptReference` (path + line) and `AssetReference` (GUID or path) field to `DiagnosticFinding` would enable clickable links. The LLM prompt can request these in structured output. For deterministic rules, references can be hardcoded (e.g., GC rule → link to allocation site if callstack available).

---

## Overall Readiness

| Pillar | Weight | Score | Weighted |
|--------|--------|-------|----------|
| Smart Profiler | 25% | 75% | 18.8% |
| Frame Debugger Scraper | 15% | 0% | 0.0% |
| Rich LLM Context | 25% | 75% | 18.8% |
| Presentation (Charts/Tables) | 20% | 75% | 15.0% |
| Code/Asset Links | 15% | 0% | 0.0% |
| **Total** | **100%** | | **52.5%** |

**DrWario is approximately 53% of the way to the LLM-Core vision** (up from 29% in v1.0).

The strongest areas are the analysis engine (self-sufficient with CorrelationEngine + ReportSynthesizer), LLM context (scene snapshots + profiler markers), and visualization (charts, timeline, data tables). The largest remaining gaps are Frame Debugger integration and code/asset linking.

---

## Implementation Priority Tiers (Updated for v2.0)

### Tier 1 — Completed (v2.0)

These high-impact features have been implemented:

1. ~~**ProfilerRecorder integration**~~ — Done. ProfilerBridge captures rendering, physics, audio, animation, UI counters.
2. ~~**Charts in UI**~~ — Done. LineChart, BarChart, Histogram components.
3. ~~**Data tables**~~ — Done. DataTableBuilder for sortable tables.
4. ~~**Scene/hierarchy context**~~ — Done. SceneCensus + SceneSnapshotTracker.
5. ~~**Streaming LLM**~~ — Done. SSE streaming via SseDownloadHandler.
6. ~~**Profiler marker tree**~~ — Done. Top-N subsystem markers by inclusive time.
7. ~~**Timeline visualization**~~ — Done. TimelineElement with zoom/pan and color-coded lanes.
8. ~~**Rule management UI**~~ — Done. Enable/disable + adjustable thresholds.
9. ~~**HTML export**~~ — Done. Self-contained HTML with embedded CSS/SVG.

### Tier 2 — Next Priorities

1. **Script/Asset references in findings** — Add `ScriptPath`, `ScriptLine`, `AssetPath` fields to `DiagnosticFinding`. Update LLM response schema. Add click handlers in UI.
2. **Report comparison** — Side-by-side diff of two reports with delta indicators.
3. **Jump-to-Profiler** — Open Unity Profiler window and navigate to specific frame.
4. **Automated tests** — 91 tests designed, 0 implemented.

### Tier 3 — Quality of Life

5. **Expandable finding cards** — Click to expand with sub-details, mini-charts, affected frames.
6. **GPU profiling fallback** — Detect when FrameTimingManager returns 0, use ProfilerRecorder GPU counter.
7. **CI/CD headless mode** — Batch profiling with grade threshold gates.

### Tier 4 — Stretch Goals

8. **Frame Debugger integration** — Requires internal API reflection or SRP instrumentation.
9. **Per-object draw cost** — Requires custom render pipeline hooks.
10. **Allocation callstack sampling** — Requires deep Profiler API access.

---

## Recommended Next Steps

Based on impact-to-effort ratio, the recommended implementation order is:

1. **Report comparison** — Side-by-side diff with delta arrows (~1 session)
2. **Script/Asset references** — Add fields to DiagnosticFinding + LLM schema update (~1 session)
3. **Jump-to-Profiler** — ProfilerWindow API navigation (~0.5 session)
4. **Automated tests** — Ring buffer, grading, rules, correlation engine (~3 sessions)

These would move the overall score from **~53% to ~65%** of the vision.

---

## Architecture Implications

The v2.0 architecture supports the remaining vision well:

- **`IAnalysisRule` + `IConfigurableRule`** — extensible for new rules with adjustable thresholds
- **`CorrelationEngine` + `ReportSynthesizer`** — standalone analysis pipeline, AI is additive
- **`LLMPromptBuilder`** — already includes scene snapshots, profiler markers, editor context
- **`DiagnosticFinding` struct** — needs `ScriptPath`/`AssetPath` fields for code linking
- **`Editor/UI/` components** — reusable chart/table/timeline elements ready for new views
- **`ProfilingSession`** — captures frame data, boot, assets, network, scene snapshots, profiler markers, and DrWario capture frames
- **False positive prevention** — self-overhead subtraction and capture frame exclusion ensure accurate measurements

The main remaining architectural additions are:
- **Code/asset reference resolution** — mapping findings to specific source files and assets
- **Report diffing** — comparing two `DiagnosticReport` instances structurally

---

*Last updated: 2026-03-09 (v2.0). Track progress against the feature matrices above.*
