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

### What Exists Today

| Component | Status | Quality |
|-----------|--------|---------|
| Runtime data collection (CPU, memory, GC, boot, assets, network) | Done | Good |
| Ring buffer (3600 frames) | Done | Solid |
| 6 deterministic analysis rules | Done | Good |
| LLM integration (Claude, OpenAI, Ollama, Custom) | Done | Functional |
| LLM prompt builder with profiling context | Done | Good |
| Grade computation (A-F) | Done | Solid |
| Report history + persistence | Done | Good |
| Ask Doctor (free-form Q&A) | Done | Good |
| Editor vs Build awareness (baseline, confidence) | Done | Good |
| Basic sparkline visualization | Done | Minimal |
| Text/JSON report export | Done | Good |

### What's Missing

| Gap | Impact | Effort |
|-----|--------|--------|
| No Profiler deep integration (ProfilerRecorder for detailed counters) | High | Medium |
| No Frame Debugger data scraping | Critical | High |
| No links to scripts/assets in findings | Critical | Medium |
| No charts or data tables in UI | High | Medium |
| No render pipeline metadata (shader passes, materials) | High | High |
| No scene/hierarchy awareness | Medium | Medium |
| No streaming LLM responses | Low | Low |
| No comparative analysis (diff two reports) | Medium | Medium |
| No timeline visualization | Medium | High |
| No jump-to-Profiler-frame integration | Medium | Medium |
| No rule enable/disable UI | Low | Low |
| No automated tests | Medium | High |
| No CI/CD headless mode | Low | Medium |

---

## Feature Comparison Matrix

### Pillar 1: Smart Profiler

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Frame timing (CPU) | Per-frame via `Time.unscaledDeltaTime` | ProfilerRecorder counters | Full CPU timeline with markers |
| Frame timing (GPU) | `FrameTimingManager` (often 0) | Fallback estimation | Per-pass GPU timing |
| GC allocations | Per-frame bytes + count | Allocation callstack sampling | Source-attributed allocations |
| Memory (heap) | Total heap via `Profiler.GetMonoUsedSizeLong()` | Native + managed breakdown | Per-system memory attribution |
| Draw calls / batches | Per-frame via `FrameTimingManager` | ProfilerRecorder stats | Per-object draw cost |
| Boot pipeline | Manual `BootTimingHook` callbacks | Automatic subsystem timing | Full boot waterfall |
| Asset loading | Manual `RecordAssetLoad` calls | Automatic via hooks | Asset dependency graph |
| Network events | Manual `RecordNetworkEvent` calls | Automatic via hooks | Protocol-level analysis |
| Physics | Not tracked | Basic stats | Per-collider cost |
| Audio | Not tracked | Basic stats | Per-source analysis |
| Animation | Not tracked | Not planned | Animator state costs |
| UI (UGUI/UIToolkit) | Not tracked | Not planned | Per-canvas rebuild cost |

**Gap Score: ~40% complete toward vision**

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
| Scene hierarchy context | No | Not planned | Object counts, nesting depth |
| Profiler marker data | No | Planned | Deep marker tree |
| Rendering pipeline context | No | Not planned | Pass structure, shader list |
| Structured response format | JSON array | JSON array | Richer schema with links |

**Gap Score: ~55% complete toward vision**

### Pillar 4: Best-in-class Presentation

| Feature | Current | Planned Final | LLM-Core Vision |
|---------|---------|---------------|------------------|
| Letter grades (A-F) | Yes | Yes | Yes |
| Sparkline (frame times) | Basic (Painter2D) | Hover tooltips | Interactive zoom/pan |
| Finding cards | Severity-colored | Enhanced | Expandable with drill-down |
| Category breakdown | Grade per category | Side-by-side comparison | Treemap/sunburst |
| Data tables | No | Planned | Sortable, filterable tables |
| Charts (memory, GC, etc.) | No | Planned | Line charts, bar charts, histograms |
| Timeline view | No | Planned (longer-term) | Full event timeline |
| Report comparison | No | Planned | Diff view with delta arrows |
| Clipboard copy (findings) | Yes | Yes | Yes |
| Export (text/JSON) | Yes | Yes | + HTML, PDF |

**Gap Score: ~25% complete toward vision**

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
| Smart Profiler | 25% | 40% | 10.0% |
| Frame Debugger Scraper | 15% | 0% | 0.0% |
| Rich LLM Context | 25% | 55% | 13.8% |
| Presentation (Charts/Tables) | 20% | 25% | 5.0% |
| Code/Asset Links | 15% | 0% | 0.0% |
| **Total** | **100%** | | **28.8%** |

**DrWario is approximately 29% of the way to the LLM-Core vision.**

The strongest areas are the analysis engine and LLM integration. The largest gaps are in visualization, Frame Debugger integration, and code/asset linking.

---

## Implementation Priority Tiers

### Tier 1 — High Impact, Medium Effort (do first)

These features close the biggest gaps with reasonable effort:

1. **Script/Asset references in findings** — Add `ScriptPath`, `ScriptLine`, `AssetPath` fields to `DiagnosticFinding`. Update LLM response schema. Add click handlers in UI.
2. **Charts in UI** — Add line charts for memory trajectory, GC allocation timeline, frame time distribution using `Painter2D` or `VisualElement` custom drawing.
3. **Data tables** — Sortable tables for: top GC allocations, slowest frames, asset load times, boot stages. Use `MultiColumnListView` (Unity 2022.3+).
4. **ProfilerRecorder integration** — Capture detailed counters (rendering stats, physics, audio) using the public `ProfilerRecorder` API for richer profiling data.

### Tier 2 — High Impact, High Effort (do next)

5. **Scene/hierarchy context** — Scan active scene for object counts, component distribution, canvas count, light count. Include in LLM prompt.
6. **Report comparison** — Side-by-side diff of two reports with delta indicators.
7. **Streaming LLM** — Show response progressively. Requires chunked `UnityWebRequest` reading.
8. **Profiler marker tree** — Use `ProfilerRecorder` to capture hierarchical marker data for the LLM prompt.

### Tier 3 — Medium Impact (quality of life)

9. **Timeline visualization** — Horizontal timeline showing frame spikes, GC events, boot stages, asset loads.
10. **Rule management UI** — Enable/disable individual rules, adjust thresholds.
11. **Jump-to-Profiler** — Open Unity Profiler window and navigate to specific frame.
12. **HTML/PDF export** — Rich formatted reports.

### Tier 4 — Stretch Goals

13. **Frame Debugger integration** — Requires internal API reflection or SRP instrumentation.
14. **Per-object draw cost** — Requires custom render pipeline hooks.
15. **Allocation callstack sampling** — Requires deep Profiler API access.
16. **IDE integration** — External protocol for opening files at specific lines.

---

## Recommended Next Steps

Based on impact-to-effort ratio, the recommended implementation order is:

1. **Add `ScriptPath`/`AssetPath` fields to DiagnosticFinding** + LLM schema update (~1 session)
2. **Add charts** — memory timeline, frame time distribution, GC timeline (~2 sessions)
3. **Add data tables** — using MultiColumnListView for findings, top allocations (~1 session)
4. **Deepen ProfilerRecorder usage** — more counters in prompt context (~1 session)
5. **Scene context** — object/component census for LLM prompt (~1 session)

These 5 items would move the overall score from **~29% to ~55%** of the vision.

---

## Architecture Implications

The current architecture supports the vision well:

- **`IAnalysisRule` interface** — extensible for new rules without modifying existing code
- **`LLMPromptBuilder.AdditionalContext`** — injectable context for framework integration
- **`DiagnosticFinding` struct** — needs 3 new fields (ScriptPath, ScriptLine, AssetPath) but otherwise sufficient
- **`DrWarioView` VisualElement** — C#-based UI can add charts/tables without UXML dependency
- **Ring buffer design** — 3600 frames is enough for visualization, may need separate high-frequency buffer for timeline

The main architectural addition needed is a **visualization layer** between the data and the UI — a set of chart/table components that can render profiling data. This doesn't exist yet and is the biggest structural gap.

---

*This document should be updated as features are implemented. Track progress against the feature matrices above.*
