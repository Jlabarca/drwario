# Quickstart: Rich Diagnostics UI

**Feature**: 001-rich-diagnostics-ui

## Implementation Order

Work in this order — each step builds on the previous:

### Step 1: Data Model Extensions
1. Add new fields to `FrameSample` (8 extended counter fields)
2. Create `SceneCensus.cs` struct in Runtime/
3. Add `SceneCensus` field to `ProfilingSession`
4. Add `ScriptPath`, `ScriptLine`, `AssetPath` to `DiagnosticFinding`

### Step 2: Extended ProfilerRecorder Markers
1. Add 8 new ProfilerRecorder instances to `ProfilerBridge`
2. Update `Sample()` to read new counters into FrameSample fields
3. Graceful fallback — counters that fail to initialize are skipped

### Step 3: Scene Census Capture
1. Create `Editor/SceneCensusCapture.cs` (editor-only static class)
2. Implement census via `FindObjectsByType` calls
3. Hook into `DrWarioPlayModeHook` to capture on session start
4. Store result in `ProfilingSession.SceneCensus`

### Step 4: LLM Integration Updates
1. Update `LLMPromptBuilder` — add scene census JSON section, add extended counter stats
2. Update `LLMResponseParser` — add `scriptPath`, `scriptLine`, `assetPath` to `FindingJson`
3. Update system prompt with script/asset reference guidance
4. Map parsed fields to `DiagnosticFinding`

### Step 5: Chart Components
1. Create `Editor/UI/ChartElement.cs` — base class with coordinate mapping, hover detection
2. Create `LineChart.cs` — memory trajectory, frame time over time
3. Create `BarChart.cs` — GC allocation bars, CPU/GPU comparison
4. Create `Histogram.cs` — frame time distribution
5. All use `Painter2D` via `generateVisualContent`

### Step 6: Data Tables
1. Create `Editor/UI/DataTableBuilder.cs` — factory for `MultiColumnListView`
2. Define column schemas for: GC frames, slow frames, asset loads, boot stages, network events
3. Implement column sort handling
4. Wire into DrWarioView

### Step 7: UI Integration
1. Add Charts section to Summary tab (or new tab)
2. Add data tables to Findings tab (or new Details tab)
3. Add clickable script/asset links to finding cards
4. Test hover tooltips on charts

### Step 8: Rule Updates
1. Update deterministic rules to populate ScriptPath/AssetPath where known
2. Add `SceneComplexityRule` using census data (optional new rule)

## Key Files to Modify

| File | Changes |
|------|---------|
| `Runtime/FrameSample.cs` | +8 fields |
| `Runtime/ProfilerBridge.cs` | +8 ProfilerRecorders |
| `Runtime/ProfilingSession.cs` | +SceneCensus field |
| `Editor/Analysis/DiagnosticFinding.cs` | +3 fields |
| `Editor/Analysis/LLM/LLMPromptBuilder.cs` | +scene census, +extended stats |
| `Editor/Analysis/LLM/LLMResponseParser.cs` | +3 parsed fields |
| `Editor/DrWarioView.cs` | +charts, +tables, +clickable links |
| `Editor/DrWarioPlayModeHook.cs` | +scene census capture |
| `Editor/Analysis/DiagnosticReport.cs` | +serialize new fields |

## New Files

| File | Purpose |
|------|---------|
| `Runtime/SceneCensus.cs` | Scene census data structs |
| `Editor/SceneCensusCapture.cs` | Census capture logic (editor-only) |
| `Editor/UI/ChartElement.cs` | Base chart VisualElement |
| `Editor/UI/LineChart.cs` | Line chart component |
| `Editor/UI/BarChart.cs` | Bar chart component |
| `Editor/UI/Histogram.cs` | Histogram component |
| `Editor/UI/DataTableBuilder.cs` | MultiColumnListView factory |

## Testing Strategy

- Verify new ProfilerRecorder markers initialize without errors
- Verify scene census captures correct counts in a test scene
- Verify LLM prompt includes new sections (inspect via "Copy Prompt" button)
- Verify charts render with mock data before connecting to live data
- Verify data tables sort correctly
- Verify script links open files in IDE
- Verify asset links ping in Project window
