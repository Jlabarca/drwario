# Quickstart: Deep Profiler & Iterative Workflow

**Feature**: 002-deep-profiler-workflow
**Date**: 2026-03-08

---

## Implementation Order

Features are ordered by dependency chain and priority. Each phase builds on the previous.

### Phase 1: Data Layer (no UI changes)

1. **Add `FrameNumber` to FrameSample** — Store `Time.frameCount` per frame sample
2. **ProfilerMarkerSample struct** — New data struct in `Runtime/`
3. **Profiler marker capture in RuntimeCollector** — Add `ProfilerRecorder` instances, aggregate top-N markers
4. **ProfilingSession extensions** — Add marker storage, `ProfilerWasRecording` flag
5. **RuleConfig static class** — EditorPrefs read/write for rule enable/disable and thresholds
6. **AnalysisEngine rule filtering** — Check `RuleConfig.IsEnabled()` before running each rule

### Phase 2: LLM Enhancements (backend, minimal UI)

7. **LLMPromptBuilder marker section** — Add `BuildProfilerMarkersSection()` for marker data in prompt
8. **SseDownloadHandler** — Custom `DownloadHandlerScript` subclass for chunked SSE reading
9. **LLMClient streaming method** — `SendStreamingAsync()` with progressive finding callbacks
10. **AIAnalysisRule streaming path** — Wire streaming into analysis pipeline

### Phase 3: Core UI Features

11. **ReportComparison model** — Compute deltas, classify findings as Fixed/New/Persists
12. **Comparison UI in History tab** — "Compare with..." button, side-by-side view
13. **TimelineEvent model** — Derive events from session data
14. **TimelineElement** — Extend ChartElement with zoom/pan, lane rendering, viewport culling
15. **Timeline tab in DrWarioView** — New tab wired to TimelineElement
16. **Jump-to-Profiler** — `ProfilerWindow` navigation from finding frame references

### Phase 4: Polish & Export

17. **Rule management UI** — Toggle switches and threshold sliders in Settings tab
18. **Expandable finding cards** — Click to expand with frame list, mini-chart, related findings
19. **Streaming UI integration** — Progressive finding display during analysis
20. **HtmlReportBuilder** — Static class generating self-contained HTML from report data
21. **Export HTML button** — Wire into DrWarioView

---

## File Change Matrix

| File | Change Type | Phase | Description |
|------|-------------|-------|-------------|
| `Runtime/FrameSample.cs` | Modify | 1 | Add `FrameNumber` field |
| `Runtime/ProfilerMarkerSample.cs` | **New** | 1 | Marker data struct |
| `Runtime/ProfilingSession.cs` | Modify | 1 | Add marker list, ProfilerWasRecording |
| `Runtime/RuntimeCollector.cs` | Modify | 1 | Add ProfilerRecorder capture, store frame numbers |
| `Editor/Analysis/RuleConfig.cs` | **New** | 1 | EditorPrefs-backed rule enable/disable + thresholds |
| `Editor/Analysis/AnalysisEngine.cs` | Modify | 1,2 | Rule filtering, streaming analysis path |
| `Editor/Analysis/LLM/LLMPromptBuilder.cs` | Modify | 2 | Add profiler markers section |
| `Editor/Analysis/LLM/SseDownloadHandler.cs` | **New** | 2 | Custom DownloadHandlerScript for SSE |
| `Editor/Analysis/LLM/LLMClient.cs` | Modify | 2 | Add SendStreamingAsync() |
| `Editor/Analysis/Rules/AIAnalysisRule.cs` | Modify | 2 | Add streaming analysis path |
| `Editor/Analysis/ReportComparison.cs` | **New** | 3 | Comparison model with delta computation |
| `Editor/UI/TimelineElement.cs` | **New** | 3 | Timeline visualization (extends ChartElement) |
| `Editor/UI/TimelineEvent.cs` | **New** | 3 | Event struct + builder from session data |
| `Editor/DrWarioView.cs` | Modify | 3,4 | Timeline tab, comparison UI, expandable cards, rule config, export button |
| `Editor/Analysis/HtmlReportBuilder.cs` | **New** | 4 | Self-contained HTML generation |
| `package.json` | Modify | 4 | Bump to 3.0.0 |

### New Files: 7
### Modified Files: 9
### Total Files Touched: 16

---

## Key Dependencies

```
FrameNumber (Phase 1) ← Jump-to-Profiler (Phase 3)
ProfilerMarkerSample (Phase 1) ← LLMPromptBuilder markers (Phase 2) ← AI quality improvement
RuleConfig (Phase 1) ← Rule management UI (Phase 4) ← AnalysisEngine filtering (Phase 1)
SseDownloadHandler (Phase 2) ← LLMClient streaming (Phase 2) ← Streaming UI (Phase 4)
ReportComparison (Phase 3) ← Comparison UI (Phase 3)
TimelineEvent (Phase 3) ← TimelineElement (Phase 3) ← Timeline tab (Phase 3)
DiagnosticReport (existing) ← HtmlReportBuilder (Phase 4)
```

---

## Risk Areas

| Risk | Mitigation |
|------|------------|
| ProfilerRecorder markers unavailable on some platforms | Graceful skip with `try-catch`, feature degrades to no marker data |
| SSE parsing edge cases (malformed JSON mid-stream) | Brace-depth state machine with fallback to full-parse-at-end |
| ProfilerWindow API changes between Unity versions | Wrap in `try-catch`, show error message on failure |
| Timeline performance with 3600+ events | Viewport culling — only draw visible events at current zoom level |
| HTML SVG chart generation complexity | Start with simple line/bar charts, iterate on visual quality |
