# Implementation Plan: Deep Profiler & Iterative Workflow

**Branch**: `002-deep-profiler-workflow` | **Date**: 2026-03-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-deep-profiler-workflow/spec.md`

## Summary

DrWario v3.0 adds 8 features that transform it from a one-shot profiling tool into an iterative optimization companion: report comparison with delta tracking, event timeline visualization, ProfilerRecorder marker capture for richer LLM context, SSE streaming for progressive finding display, jump-to-Profiler-frame navigation, rule enable/disable configuration, expandable finding cards, and self-contained HTML export.

Technical approach uses flat ProfilerRecorder counters (not hierarchical FrameDataView), custom DownloadHandlerScript for SSE streaming, ChartElement extension for timeline zoom/pan, and EditorPrefs for rule configuration — all with zero external dependencies.

## Technical Context

**Language/Version**: C# 9.0 (Unity 2022.3+)
**Primary Dependencies**: UnityEngine, UnityEditor, System (no external packages)
**Storage**: EditorPrefs (rule config), Library/DrWarioReports/ (report history), in-memory (session data, comparison, timeline)
**Testing**: Unity Test Framework (NUnit) — 0 tests currently, no tests added in this feature scope
**Target Platform**: Unity Editor (Windows, macOS, Linux)
**Project Type**: UPM package (editor tool)
**Performance Goals**: Timeline renders 3600 events at 30+ FPS; streaming first finding < 5s; comparison view loads < 1s
**Constraints**: Zero external dependencies; all UI in C# VisualElement code (no UXML/USS); <0.05ms per-frame overhead for marker capture
**Scale/Scope**: Single Unity project, 3600-frame ring buffer, 8 analysis rules + AI, 6→7 tabs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution is not yet defined (template placeholders only). Applying DrWario's implicit constraints from CLAUDE.md:

| Gate | Status | Notes |
|------|--------|-------|
| No external dependencies | PASS | All features use only UnityEngine/UnityEditor/System |
| All UI in C# VisualElement | PASS | Timeline, comparison, cards all use VisualElement + Painter2D |
| Editor-only analysis code | PASS | All new editor code in `DrWario.Editor` namespace |
| Runtime code guarded | PASS | ProfilerRecorder capture uses `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| UPM package compatible | PASS | No project-level assets, EditorPrefs for settings |
| Log prefix [DrWario] | PASS | All Debug.Log uses `[DrWario]` prefix |

**Post-Phase 1 re-check**: All gates still pass. No violations introduced by design.

## Project Structure

### Documentation (this feature)

```text
specs/002-deep-profiler-workflow/
├── plan.md              # This file
├── research.md          # Phase 0 output — 7 research decisions
├── data-model.md        # Phase 1 output — entity definitions
├── quickstart.md        # Phase 1 output — implementation order + file matrix
├── contracts/
│   └── llm-response-schema.md  # LLM prompt/response contract changes
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
Runtime/
├── FrameSample.cs                    # MODIFY: Add FrameNumber field
├── ProfilerMarkerSample.cs           # NEW: Marker data struct
├── ProfilingSession.cs               # MODIFY: Add marker list, ProfilerWasRecording
└── RuntimeCollector.cs               # MODIFY: ProfilerRecorder capture, frame numbers

Editor/
├── DrWarioView.cs                    # MODIFY: Timeline tab, comparison UI, expandable cards, rule config, export
├── Analysis/
│   ├── AnalysisEngine.cs             # MODIFY: Rule filtering, streaming analysis
│   ├── RuleConfig.cs                 # NEW: EditorPrefs-backed rule enable/disable
│   ├── ReportComparison.cs           # NEW: Two-report diff model
│   ├── HtmlReportBuilder.cs          # NEW: Self-contained HTML generation
│   ├── Rules/
│   │   └── AIAnalysisRule.cs         # MODIFY: Streaming analysis path
│   └── LLM/
│       ├── LLMClient.cs              # MODIFY: Add SendStreamingAsync()
│       ├── LLMPromptBuilder.cs       # MODIFY: Add profiler markers section
│       └── SseDownloadHandler.cs     # NEW: Custom DownloadHandlerScript for SSE
└── UI/
    ├── TimelineElement.cs            # NEW: Timeline visualization
    └── TimelineEvent.cs              # NEW: Event struct + builder
```

**Structure Decision**: Extends existing DrWario UPM package structure. New files placed in existing namespace directories. `Editor/UI/` directory (created in v2.0 for chart elements) gets timeline components. 7 new files, 9 modified files.

## Complexity Tracking

No constitution violations to justify. The design adds 7 new files which is substantial but each serves a distinct, well-scoped purpose with no unnecessary abstractions.
