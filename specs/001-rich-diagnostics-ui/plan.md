# Implementation Plan: Rich Diagnostics UI

**Branch**: `001-rich-diagnostics-ui` | **Date**: 2026-03-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-rich-diagnostics-ui/spec.md`

## Summary

Extend DrWario v1.0 with 5 capabilities: (1) clickable script/asset references in findings, (2) interactive charts (memory, frame time, GC, CPU/GPU), (3) sortable data tables, (4) extended ProfilerRecorder counters (physics, UI, audio, animation), and (5) scene context census for LLM prompts. All new code is editor-only with zero runtime footprint in release builds.

## Technical Context

**Language/Version**: C# 9.0 (Unity 2022.3 LTS)
**Primary Dependencies**: UnityEngine, UnityEditor, Unity.Profiling (ProfilerRecorder), UIToolkit (VisualElement, Painter2D, MultiColumnListView)
**Storage**: Library/DrWarioReports/ (existing report persistence, no changes needed)
**Testing**: Unity Test Framework (EditMode tests) — currently 0 tests, test infrastructure to be added separately
**Target Platform**: Unity Editor (Windows, macOS, Linux) — editor-only features
**Project Type**: UPM package (Unity Package Manager, `com.jlabarca.drwario`)
**Performance Goals**: Charts render <16ms, scene census <500ms for 5k objects, no per-frame allocations in profiling loop
**Constraints**: No external dependencies, Unity 2022.3+ only, all new code behind `#if UNITY_EDITOR`
**Scale/Scope**: ~8 new/modified C# files, ~1500 LOC additions across Runtime and Editor assemblies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

No project constitution defined — no gates to check. Proceeding with standard DrWario conventions:
- No external dependencies
- Editor-only features use `#if UNITY_EDITOR` or live in Editor assembly
- All UI is C# VisualElement code (no UXML/USS)
- Log prefix: `[DrWario]`

## Project Structure

### Documentation (this feature)

```text
specs/001-rich-diagnostics-ui/
├── plan.md              # This file
├── research.md          # Phase 0: technical research
├── data-model.md        # Phase 1: data model extensions
├── quickstart.md        # Phase 1: developer quickstart
├── contracts/           # Phase 1: interface contracts
│   └── llm-response.md  # Extended LLM response schema
└── tasks.md             # Phase 2: implementation tasks (via /speckit.tasks)
```

### Source Code (repository root)

```text
Runtime/
├── FrameSample.cs              # MODIFY: add physics/audio/animation/UI counter fields
├── ProfilerBridge.cs           # MODIFY: add new ProfilerRecorder markers
├── ProfilingSession.cs         # MODIFY: add SceneCensus storage
└── SceneCensus.cs              # NEW: scene context data struct

Editor/
├── DrWarioView.cs              # MODIFY: add charts tab, data tables, clickable links
├── DrWarioPlayModeHook.cs      # MODIFY: capture scene census on session start
├── UI/                         # NEW: chart and table components
│   ├── ChartElement.cs         # Base chart VisualElement with Painter2D
│   ├── LineChart.cs            # Memory trajectory, frame time line chart
│   ├── BarChart.cs             # GC allocation bars, CPU/GPU comparison
│   ├── Histogram.cs            # Frame time distribution
│   └── DataTableBuilder.cs    # MultiColumnListView factory helpers
├── Analysis/
│   ├── DiagnosticFinding.cs    # MODIFY: add ScriptPath, ScriptLine, AssetPath
│   ├── LLM/
│   │   ├── LLMPromptBuilder.cs # MODIFY: add scene census + extended counters
│   │   └── LLMResponseParser.cs # MODIFY: parse scriptPath/assetPath from response
│   └── Rules/
│       ├── SceneComplexityRule.cs # NEW: analyze scene census data
│       └── [existing rules]      # MODIFY: add script/asset references where known
```

**Structure Decision**: Follows existing DrWario convention — Runtime/ for all-platform data, Editor/ for editor-only logic. New UI components go in `Editor/UI/` subdirectory to keep chart code separate from the main view. No new assemblies needed.

## Complexity Tracking

No constitution violations to justify.
