# Study: Improving LLM Context Quality for Better Reports and Suspect Identification

**Date:** 2026-03-09
**Scope:** `Editor/Analysis/LLM/LLMPromptBuilder.cs`, response parsing, data pipeline, MCP workflow prompts

## TL;DR

The LLM prompt sends 13 JSON sections (~2-4K tokens) but buries the most important data (`preAnalysis`) at the end and emits noise sections (zero GPU data, trivial components, empty asset loads). The system prompt never asks for `confidence` or `environmentNote` despite the parser already supporting them. **Top 5 quick wins:** (1) move preAnalysis to top of prompt, (2) add confidence/environmentNote to system prompt schema, (3) add namespace info to activeScripts for better suspect ID, (4) add a few-shot example of ideal findings, (5) elide zero/null sections. Biggest medium-effort wins: worst-frame detail arrays and console log frame correlation. For MCP workflows: prioritize suspects by relevance instead of listing all, and add a per-finding "Fix with MCP" workflow.

---

## 1. Current State Assessment

### 1.1 User Prompt JSON Sections

The user prompt built by `BuildUserPrompt()` (LLMPromptBuilder.cs:386-441) sends a JSON object with these sections, in order:

| Section | Builder Method | Lines | What It Contains |
|---------|---------------|-------|-----------------|
| `session` | inline | 392-401 | Start/end time, duration, Unity version, platform, target FPS, screen resolution |
| `environment` | `AppendEnvironment` | 496-532 | isEditor, isDevelopmentBuild, editorWindows state (Scene/Inspector/Profiler/GameView), editorBaseline (CPU/GC/draw call idle overhead), explanatory note |
| `frameSummary` | `AppendFrameSummary` | 534-663 | CPU time stats (avg/p50/p95/p99/max/min), GPU time stats, render thread stats, rendering counters (drawCalls/batches/setPassCalls/triangles with batching efficiency), GC allocation (avg/max/total/spikes), GC alloc count, frame drops (count/severe/ratio), bottleneck classification |
| `memoryTrajectory` | `AppendMemoryTrajectory` | 665-721 | 12 downsampled heap/texture/mesh samples over time, linear regression (heap slope bytes/sec and MB/min), current breakdown |
| `bootPipeline` | `AppendBootPipeline` | 723-744 | Total boot ms, per-stage name/duration/success |
| `assetLoads` | `AppendAssetLoads` | 746-771 | Count, avg/max/total ms, top 10 slow loads (>500ms) with asset key |
| `profilerMarkers` | `AppendProfilerMarkers` | 474-494 | Top 20 markers by avg inclusive time: name, avgInclusiveMs, avgExclusiveMs, maxInclusiveMs, avgCallCount |
| `extendedCounters` | `AppendExtendedCounters` | 910-967 | Physics (active/kinematic bodies, contacts), audio (voices, DSP load), animation (animator count), UI (canvas/layout rebuilds) |
| `sceneCensus` | `AppendSceneCensus` | 969-1003 | Total GOs/components, top 20 component types by count, lights by type, canvas/camera/particle/LOD/rigidbody counts |
| `sceneSnapshots` | `AppendSceneSnapshots` | 773-853 | Baseline/final object count, net growth, top 10 instantiated object names, spike-frame diffs (frame index, trigger, added/removed/total) |
| `activeScripts` | `AppendActiveScripts` | 855-869 | Top 30 MonoBehaviour types by instance count: type name, instance count, sample GO names |
| `consoleLogs` | `AppendConsoleLogs` | 871-887 | Up to 50 errors/warnings: type, message (150 char truncated), stack trace hint (first 80 chars) |
| `preAnalysis` | `AppendPreAnalysis` | 889-908 | Rule-based findings: ruleId, category, severity, confidence, title, metric, threshold |

### 1.2 System Prompt Instructions

The `BaseSystemPrompt` (LLMPromptBuilder.cs:19-64) instructs the LLM to:

1. Return a JSON array of findings with specific fields (ruleId, category, severity, title, description, recommendation, metric, threshold, plus optional scriptPath/scriptLine/assetPath)
2. Focus on cross-metric correlations, platform-specific issues, Unity-specific patterns, and prioritized recommendations
3. Account for editor overhead using the baseline data (subtract baseline values)
4. Use profilerMarkers to attribute subsystem costs by name
5. Use activeScripts to identify specific suspects
6. Correlate consoleLogs with performance findings
7. Return ONLY JSON, no preamble

The `AskDoctorSystemPrompt` (line 66-80) is a separate persona for the free-form Q&A tab, covering rendering pipelines, memory model, GC, frame timing, boot/loading, networking, and platform constraints.

### 1.3 MCP Workflow Prompts

Three MCP workflows exist:

1. **Suspect Check** (`BuildMcpSuspectCheckPrompt`, line 160-243): Gives MCP-enabled LLM the active scripts list, findings, console errors, and a compact profiling summary. Instructs it to use `find_gameobjects`, `manage_gameobject`, and `manage_script` to investigate each suspect.

2. **Report Correction** (`BuildReportCorrectionPrompt`, line 249-299): Provides full profiling data + full report. Asks LLM to audit each finding (Confirmed / False Positive / Adjusted), check severity, improve recommendations, find missed issues. No MCP tools.

3. **MCP Report Correction** (`BuildMcpReportCorrectionPrompt`, line 304-384): Two-phase: investigate suspects with MCP tools, then correct the report. Includes active scripts, console errors, full profiling data, and full report.

### 1.4 Known Issues (Recently Fixed)

- **Active scripts pollution:** `CaptureActiveScripts()` previously included Unity built-in MonoBehaviours (e.g., EventSystem, StandaloneInputModule). Now filters by namespace — skips `UnityEngine.*`, `Unity.*`, `TMPro.*`, `UnityEditor.*`, and DrWario's own types (SceneCensusCapture.cs:108-113).
- **Negative CPU times:** `RuntimeCollector.Update()` was not clamping CPU time after subtracting DrWario's self-overhead. Now uses `Math.Max(0f, ...)` (RuntimeCollector.cs:306).
- **MCP suspect prompt missing profiling data:** `BuildMcpSuspectCheckPrompt` originally had no profiling summary. Now includes a compact CPU/GC/heap summary block (LLMPromptBuilder.cs:215-238).

---

## 2. Signal-to-Noise Analysis

### 2.1 Highest Signal Sections

**Tier 1 — Essential for every analysis:**
- `frameSummary.cpuFrameTime` (p95/p99/max): The single most diagnostic metric. Directly tells the LLM where the bottleneck is.
- `preAnalysis`: Rule-based findings with confidence scores. The LLM should build on these, not re-derive them.
- `profilerMarkers`: Named subsystem timing (Update, FixedUpdate, Rendering, Physics.Processing, etc.) lets the LLM attribute CPU budget rather than guessing.
- `frameSummary.gcAllocation` + `gcAllocCount`: GC spikes are the most common Unity perf issue.

**Tier 2 — High signal when present:**
- `memoryTrajectory.linearRegression`: Heap slope is the definitive memory leak indicator.
- `activeScripts`: Enables the LLM to name specific scripts in findings (critical for actionability).
- `consoleLogs`: Errors during frame spikes are often the root cause.
- `sceneSnapshots.spikeFrameDiffs`: Object churn during spikes correlates instantiation with frame drops.

**Tier 3 — Contextual value:**
- `environment` + `editorBaseline`: Critical for editor sessions to avoid false positives, but adds ~200 tokens of boilerplate.
- `sceneCensus`: Useful for rendering analysis (lights, canvases, LODs) but mostly static context.
- `extendedCounters`: Physics contacts, audio DSP load, UI rebuilds — only valuable when those subsystems are active.

### 2.2 Noise Sources

**Component distribution in sceneCensus** (LLMPromptBuilder.cs:980-988): The top 20 component types list includes Transform, MeshRenderer, MeshFilter, etc. — these are always present and rarely actionable by themselves. The LLM gets 20 entries but typically only 2-3 are unusual enough to matter. This burns ~300 tokens.

**Memory trajectory samples** (LLMPromptBuilder.cs:679-691): 12 downsampled points showing heapMB/textureMB/meshMB. The linear regression summary already captures the trend. The raw samples add ~400 tokens but rarely change the LLM's analysis beyond what the slope tells it.

**Asset loads when none are slow** (LLMPromptBuilder.cs:746-771): When no loads exceed 500ms, the section still emits count/avg/max/total stats. This is noise — if nothing is slow, there is nothing to report.

**Session metadata** (LLMPromptBuilder.cs:392-401): Start/end time in ISO format is rarely used by the LLM. Duration and platform are useful; timestamps are not.

**GPU time when unavailable**: When `gpuFrameTime` values are all 0, the section still emits avg=0/p95=0/max=0. The bottleneck field says "GPU data unavailable" but the zeros may confuse the LLM into thinking GPU load is negligible.

### 2.3 Token Budget Impact

A typical prompt with all sections populated runs 2000-4000 tokens for the user prompt + ~800 tokens for the system prompt. This is well within context limits, but the key issue is **attention dilution**: when the LLM must scan through 15 JSON sections, it gives less focused attention to the critical data (frame timing, GC spikes, profiler markers). Studies on LLM behavior show that information in the middle of long prompts gets less attention than information at the start or end.

Current ordering places the most critical data (`frameSummary`) near the top, which is good. But `preAnalysis` (the rule findings the LLM should build on) is at the very end — the worst position for LLM attention.

---

## 3. Improvement Opportunities

### 3.1 [HIGH IMPACT, LOW EFFORT] Move preAnalysis to the top of the JSON

**What:** Reorder `BuildUserPrompt()` to emit `preAnalysis` immediately after `session` metadata, before `frameSummary`.

**Why:** The pre-analysis findings are the most important context for the LLM. They tell it what the deterministic rules already found, so it can focus on correlations, root causes, and issues the rules missed. Currently at the end of the JSON (LLMPromptBuilder.cs:437), this is the worst position for LLM attention.

**Expected impact:** Better LLM focus on augmenting rule findings rather than redundantly re-deriving them. Fewer duplicate findings.

**Difficulty:** Trivial — reorder the `Append*` calls in `BuildUserPrompt()`.

### 3.2 [HIGH IMPACT, LOW EFFORT] Add namespace/assembly info to activeScripts

**What:** In `SceneCensusCapture.CaptureActiveScripts()` (SceneCensusCapture.cs:91-144), capture `mb.GetType().Namespace` and `mb.GetType().Assembly.GetName().Name` alongside the type name. Emit them in the JSON as `"namespace"` and `"assembly"` fields.

**Why:** The LLM currently sees `"type": "PlayerController"` but cannot distinguish user code from third-party plugins. With namespace info like `"namespace": "MyGame.Player"` vs `"namespace": "Photon.Pun"`, the LLM can:
- Prioritize user scripts over third-party (user can fix their own code)
- Identify framework-specific patterns (Photon, Mirror, FishNet, etc.)
- Provide more specific recommendations ("in your PlayerController.cs" vs "in some script")

**Expected impact:** Significantly better suspect identification and recommendation specificity.

**Difficulty:** Low. Add two fields to `ActiveScriptEntry`, capture in `CaptureActiveScripts()`, emit in `AppendActiveScripts()`.

### 3.3 [HIGH IMPACT, MEDIUM EFFORT] Frame-level detail for worst N frames

**What:** In `AppendFrameSummary()`, after the aggregate stats, add a `"worstFrames"` array containing the top 5-10 frames by CPU time. For each, include: frameIndex, cpuMs, gcAllocBytes, gcAllocCount, drawCalls, objectCount.

**Why:** Aggregates (avg, p95, p99) hide the specifics of what happens during spikes. The LLM cannot correlate "which scripts were active" with "which frame was slow" using only averages. Worst-frame detail lets the LLM say "frame 1247 had 45ms CPU and 128KB GC — this coincides with the ObjectDelta snapshot showing 30 new objects instantiated."

**Expected impact:** Much better spike root-cause analysis, especially when combined with sceneSnapshots.spikeFrameDiffs.

**Difficulty:** Medium. Need to sort frames, pick top N, serialize. Must match frame indices with sceneSnapshot frame indices for cross-referencing.

### 3.4 [HIGH IMPACT, MEDIUM EFFORT] Console log timestamp correlation with frame spikes

**What:** Add `frameIndex` (from `Time.frameCount`) to `ConsoleLogEntry`. In `AppendConsoleLogs()`, flag logs that occurred within the same frame as a worst-frame spike.

**Why:** Currently, console logs have a float `Timestamp` but no frame number. The LLM cannot tell whether an error happened during a spike frame or during a calm frame. If an exception fires during a 45ms frame, that is likely the cause. If it fires during a normal 8ms frame, it is unrelated to the CPU spike.

**Expected impact:** Enables the LLM to make causal claims ("this NullReferenceException in PlayerController.Update() coincides with the worst frame spike") rather than just noting the error exists.

**Difficulty:** Medium. Add `int FrameNumber` to `ConsoleLogEntry`, set it from `Time.frameCount` in `OnConsoleLog()`, cross-reference in the prompt.

### 3.5 [MEDIUM IMPACT, LOW EFFORT] Structured few-shot example in system prompt

**What:** Add 1-2 short example finding objects in the system prompt showing the ideal output format with proper evidence citation.

**Why:** The current system prompt describes the schema but does not demonstrate what a high-quality finding looks like. LLMs produce better structured output when given examples. Specifically, the LLM often:
- Writes vague descriptions instead of citing specific numbers from the data
- Forgets to include scriptPath even when activeScripts data makes it obvious
- Sets metric/threshold to round numbers instead of actual measured values

Example to add (after the field descriptions):

```
Example of a high-quality finding:
{"ruleId":"AI_GC_INSTANTIATE_SPIKE","category":"Memory","severity":"Warning","title":"Object instantiation causing GC spikes during gameplay","description":"50 frames show GC allocation >10KB/frame (avg 14.2KB). Scene snapshots show 'Bullet(Clone)' instantiated 340 times. The BulletSpawner script (x2 instances on EnemyShip, PlayerShip) is the likely source.","recommendation":"Pool Bullet instances using ObjectPool<T>. Pre-warm 50 instances in Start(). Replace Instantiate() with pool.Get() and Destroy() with pool.Release().","metric":14200,"threshold":1024,"scriptPath":"Assets/Scripts/BulletSpawner.cs","scriptLine":0,"confidence":"High"}
```

**Expected impact:** Better-structured findings, more data citations, more scriptPath usage.

**Difficulty:** Low. Add ~200 tokens to `BaseSystemPrompt`.

### 3.6 [MEDIUM IMPACT, MEDIUM EFFORT] Ask for confidence scores and evidence citations

**What:** Add `confidence` (string: "High"/"Medium"/"Low") and `environmentNote` (string, optional) to the required response fields in the system prompt. The parser already supports these fields (LLMResponseParser.cs:60-61).

**Why:** The response schema asks for confidence/environmentNote in the parser code but the system prompt (LLMPromptBuilder.cs:19-64) does NOT list them as required fields. The LLM never returns them because it does not know they exist. This is a gap — the parser is ready but the prompt does not request the data.

**Expected impact:** LLM findings will self-rate their reliability, which the UI already supports displaying. Editor-overhead findings will be properly flagged.

**Difficulty:** Low. Add two fields to the schema description in `BaseSystemPrompt`.

### 3.7 [MEDIUM IMPACT, MEDIUM EFFORT] Elide zero/null sections to reduce noise

**What:** Skip emitting sections that contain no useful data. Specifically:
- Don't emit `gpuFrameTime` when all values are 0
- Don't emit `assetLoads` when count is 0 or no slow loads exist
- Don't emit `bootPipeline` when no stages are recorded
- Don't emit individual extended counter subsections when all values are 0
- Reduce `memoryTrajectory.samples` to 6 points instead of 12

**Why:** Every null/zero section burns tokens and dilutes attention. The LLM already handles missing sections (the system prompt says "when X data is available"). Removing null sections signals "this data was not collected" more clearly than emitting `null`.

**Expected impact:** 300-800 fewer tokens per prompt. Cleaner signal.

**Difficulty:** Medium. Most sections already have null guards, but some (GPU time, extended counters) emit zeros instead of being omitted. Need to audit each `Append*` method.

### 3.8 [MEDIUM IMPACT, HIGH EFFORT] Per-frame script correlation

**What:** During spike frames (detected by the SceneSnapshotTracker), capture which MonoBehaviour Update/LateUpdate methods are currently running via `ProfilerRecorder` on user-defined markers, or by checking `Behaviour.enabled` state on scripts.

**Why:** Currently, `activeScripts` is a static snapshot taken once at session start. The LLM knows "PlayerController x1 exists" but not "PlayerController.Update() was running during the 45ms spike frame." This is the gap between "suspect" and "confirmed cause."

**Expected impact:** Would transform findings from "PlayerController might be causing spikes" to "PlayerController.Update() consumed 12ms during the worst frame."

**Difficulty:** High. Unity's `ProfilerRecorder` cannot easily target arbitrary user method markers without IL2CPP deep profiling. May need a custom `ProfilerMarker` injection approach, or a proxy (check which scripts have `enabled=true` during spike frames as a weaker signal).

### 3.9 [LOW IMPACT, LOW EFFORT] Filter component distribution to non-trivial types

**What:** In `AppendSceneCensus()`, filter out Transform, RectTransform, CanvasRenderer, and other always-present component types from the `componentDistribution` list.

**Why:** Transform always has the highest count (1:1 with GameObjects). CanvasRenderer is always paired with UI elements. These entries consume tokens without informing the LLM of anything it couldn't infer from `totalGameObjects`.

**Expected impact:** ~100 fewer tokens, marginally cleaner signal.

**Difficulty:** Low. Add a small exclusion set in `AppendSceneCensus()` or in `SceneCensusCapture.Capture()`.

### 3.10 [MEDIUM IMPACT, HIGH EFFORT] Shader and material info for rendering findings

**What:** Capture active shader/material names and their pass counts at session start. Include in the prompt when rendering findings (high draw calls, low batching efficiency) are present.

**Why:** When the LLM identifies "draw calls too high" or "batching efficiency low," it cannot explain why without knowing which shaders are in use. Different shaders (Standard vs URP/Lit vs custom) have different pass counts and batching behavior.

**Expected impact:** Rendering recommendations would go from "reduce draw calls" to "your Custom/Water shader uses 3 passes — consider simplifying or using GPU instancing."

**Difficulty:** High. Requires enumerating active materials via `Renderer.sharedMaterials` across the scene, collecting unique shader names and pass counts. Risk of large token usage in material-heavy scenes.

### 3.11 [LOW IMPACT, MEDIUM EFFORT] Profiler marker hierarchy

**What:** Record parent-child marker relationships (e.g., PlayerLoop > Update > BehaviourUpdate > YourScript.Update). Currently, markers are flat: PlayerLoop, Update, LateUpdate, etc. (RuntimeCollector.cs:107-116).

**Why:** The flat list tells the LLM "Update took 8ms" but not "of that 8ms, 6ms was in BehaviourUpdate." Without hierarchy, the LLM cannot drill down.

**Expected impact:** Moderate improvement in attribution specificity, but diminishing returns since the top-level markers already indicate which phase is expensive.

**Difficulty:** Medium. Unity's `ProfilerRecorder` does not natively expose hierarchy. Would need to record multiple levels of markers and compute exclusive time by subtraction.

---

## 4. MCP Workflow Prompt Improvements

### 4.1 Suspect Check Prompt

**Current issues:**
- The prompt lists all active scripts as "suspects" without prioritization. An LLM with MCP access will waste tool calls inspecting scripts that are unlikely causes.
- No guidance on what to look for in each script (e.g., "check Update() for allocations" vs "check if this script pools objects").

**Improvements:**
1. **Prioritize suspects by relevance.** Cross-reference active scripts with findings. If a GC finding exists and a script has 100+ instances, that script should be listed first with a note: "High priority — many instances, possible per-frame allocation source."
2. **Add investigation hints per suspect.** Based on the finding category, tell the LLM what to look for:
   - CPU findings: "Check Update(), LateUpdate(), FixedUpdate() for expensive operations"
   - Memory findings: "Check for new allocations in hot paths (string concatenation, LINQ, List creation)"
   - Rendering findings: "Check for runtime material/mesh modifications, shader property sets"
3. **Include frame numbers of worst spikes** so the LLM can cross-reference with the Profiler window if it has `manage_editor` access.
4. **Limit suspect list to top 10** most relevant scripts to keep the prompt focused.

### 4.2 Report Correction Prompt

**Current issues:**
- The prompt asks for a free-form corrected report, which makes it hard to parse programmatically if DrWario wanted to auto-apply corrections.
- No instruction to preserve the finding format — the LLM typically returns prose.

**Improvements:**
1. **Ask for structured output:** For each finding, request: `{ findingNumber, verdict: "Confirmed"|"FalsePositive"|"Adjusted", adjustedSeverity?, adjustedDescription?, reason }`. This enables potential auto-processing.
2. **Ask for missed findings in the same JSON format** as the original findings, so they could be merged into the report.
3. **Include the worst-frame detail** (from improvement 3.3) so the correction LLM can verify whether findings match the actual data.

### 4.3 New Workflow: "Fix This Finding"

**Concept:** A per-finding MCP workflow that focuses on a single finding rather than the whole report. When the user clicks a finding card, they get a prompt that says:

> "DrWario found [finding title]. The suspect is [scriptPath]. Use MCP tools to:
> 1. Read the script at [scriptPath]
> 2. Identify the problematic pattern (the finding says: [description])
> 3. Propose a concrete code fix
> 4. Optionally apply the fix using apply_text_edits"

This would be much more actionable than the broad suspect check, because it focuses on one issue at a time.

**Implementation:** Add `BuildMcpFixFindingPrompt(DiagnosticFinding finding, ProfilingSession session)` to `LLMPromptBuilder`. The UI would add a "Fix with MCP" button on each finding card that has a non-null `ScriptPath`.

### 4.4 New Workflow: "Deep Dive" for a Category

**Concept:** When a category gets a bad grade (D or F), offer a targeted MCP prompt that:
- Lists all findings for that category
- Lists the relevant scene objects (e.g., for Memory: objects with many components; for CPU: scripts with many instances)
- Instructs the LLM to use `manage_scene(action="get_hierarchy")` to understand structure
- Asks for a prioritized remediation plan

This bridges the gap between "you have a problem" and "here is what to do about it."

---

## 5. Recommendations Summary

### Top 5 Highest-Impact, Lowest-Effort Improvements

| # | Improvement | Impact | Effort | Section |
|---|-----------|--------|--------|---------|
| 1 | Add `confidence` and `environmentNote` to system prompt schema | High | Trivial | 3.6 |
| 2 | Add namespace/assembly to activeScripts | High | Low | 3.2 |
| 3 | Move preAnalysis to top of user prompt JSON | High | Trivial | 3.1 |
| 4 | Add structured few-shot example to system prompt | Medium | Low | 3.5 |
| 5 | Elide zero/null sections from user prompt | Medium | Low-Med | 3.7 |

### Implementation Roadmap

**Phase 1 — Prompt quality (no runtime changes):**
- 3.1: Reorder prompt sections (preAnalysis first)
- 3.5: Add few-shot example to system prompt
- 3.6: Add confidence/environmentNote to system prompt field list
- 3.7: Elide zero sections
- 3.9: Filter trivial component types
- Estimated time: 2-3 hours

**Phase 2 — Better data capture:**
- 3.2: Namespace/assembly on activeScripts
- 3.3: Worst-frame detail array
- 3.4: Console log frame correlation
- Estimated time: 4-6 hours

**Phase 3 — MCP workflow improvements:**
- 4.1: Prioritized suspect list with investigation hints
- 4.2: Structured correction output format
- 4.3: Per-finding "Fix with MCP" workflow
- Estimated time: 4-6 hours

**Phase 4 — Advanced data (optional):**
- 3.8: Per-frame script correlation (investigate feasibility first)
- 3.10: Shader/material capture for rendering analysis
- 3.11: Profiler marker hierarchy
- Estimated time: 8-12 hours, with feasibility risk on 3.8
