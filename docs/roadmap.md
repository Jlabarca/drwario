# DrWario Roadmap

> Vision and development priorities for DrWario as an LLM-powered performance analysis platform.

## Design Principles

1. **Rules first, AI second** — Deterministic rules always run. AI is additive, never required
2. **No source code sent** — Only statistical summaries go to LLMs. Privacy by design
3. **Zero dependencies** — Works with vanilla Unity. No UniTask, no Newtonsoft, no third-party
4. **Zero release overhead** — Conditional compilation strips everything in production
5. **Framework-agnostic** — Integration via events and static properties, not hard coupling

---

## Current State (v1.0.0)

### What works
- 6 deterministic analysis rules (GC, CPU, Boot, Memory, Assets, Network)
- A–F grading with per-category breakdown
- Real-time frame time sparkline
- 4 LLM providers (Claude, OpenAI, Ollama, Custom)
- Free-form "Ask Doctor" Q&A
- Report history with JSON/text export
- Auto-start on Play Mode
- Framework integration via events and conditional compilation
- HybridFrame integration complete

### What doesn't
- 0 of 91 designed tests implemented
- GPU profiling returns 0 on many platforms
- No UI for enabling/disabling individual rules
- No streaming LLM responses
- No report comparison
- No Unity Profiler integration (uses basic sampling only)

---

## Near-Term Priorities

### 1. Bug Fixes (DONE)

- [x] Fix `GcAllocBytes` double-call in `RuntimeCollector`
- [x] Make `LLMClient._lastRequestTime` static (rate limiter fix)
- [x] Fix `CategoryGrades` missing from JSON export
- [x] Validate `TestConnectionAsync` response content
- [x] Make analysis pipeline async (editor no longer freezes)

### 2. Automated Tests

91 tests designed in the HybridFrame test plan. Priority order:

1. **ProfilingSession ring buffer** — wrap-around, chronological ordering, recording guard
2. **Grading formula** — penalty math, clamping, grade boundaries (89→B, 90→A, 59→F, 60→D)
3. **Rule unit tests** — known data patterns → expected findings and severities
4. **LLMResponseParser** — code fences, bare arrays, malformed JSON, null fields
5. **Deduplication** — AI priority, title normalization, category matching

### 3. Async Analysis (DONE)

- [x] `AnalysisEngine.AnalyzeAsync()` — deterministic rules run instantly, AI runs without blocking
- [x] `AIAnalysisRule.AnalyzeAsync()` — no more `Task.Wait()`
- [x] Editor stays responsive during LLM analysis
- [ ] Add cancel button during LLM analysis

### 4. Unity Profiler Integration (IN PROGRESS)

See [design-profiler-integration.md](design-profiler-integration.md) for full design.

- [ ] `ProfilerBridge` — read `ProfilerRecorder` counters (draw calls, batches, GC.Alloc, etc.)
- [ ] Enhanced `FrameSample` with rendering metrics
- [ ] `RenderingEfficiencyRule` — draw call / batching / set-pass analysis
- [ ] `CPUvsGPUBottleneckRule` — bottleneck classification
- [ ] "Open in Profiler" button on finding cards
- [ ] Profiler markers for DrWario's own overhead
- [ ] Fallback to legacy sampling if `ProfilerRecorder` unavailable

### 5. GPU Profiling Fallback

- [ ] Detect when `FrameTimingManager` returns 0
- [ ] Use `ProfilerRecorder` GPU counter as primary source
- [ ] Platform-specific fallback estimation
- [ ] Sentinel value to distinguish "unsupported" from "zero GPU work"
- [ ] New GPU-specific analysis rule

### 6. Rule Management UI

- [ ] Toggle individual rules on/off in LLM Settings tab
- [ ] Show rule descriptions and thresholds
- [ ] Custom threshold overrides per rule
- [ ] Persist rule preferences in EditorPrefs

### 7. Sparkline Improvements

- [ ] Mouse hover tooltips with exact frame time values
- [ ] Click to jump to frame in Unity Profiler
- [ ] Show GC spikes as markers on the sparkline
- [ ] Zoom and pan support

### 8. Report Comparison

- [ ] Side-by-side diff of two historical reports
- [ ] Grade trend visualization over time
- [ ] Highlight new/resolved findings between reports

---

## Medium-Term Goals

### Streaming LLM Responses

Show AI analysis results as they arrive instead of waiting for the full response:

- Server-Sent Events (SSE) for Claude and OpenAI
- Ollama already supports streaming with `"stream": true`
- Progressive UI update as findings are parsed
- Cancel mid-stream if user navigates away

### CI Integration

Run DrWario headless in automated builds:

```bash
# Conceptual CLI
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

Let users drop in custom `IAnalysisRule` implementations that are automatically found:

- Assembly scanning for `IAnalysisRule` implementations
- `[DrWarioRule]` attribute for auto-registration
- Rule priority/ordering via attribute
- Per-rule enable/disable persisted in EditorPrefs

### Timeline View

Visual timeline of performance events:

- Frame time heatmap strip
- GC allocation markers
- Boot stage blocks
- Asset load bars
- Network event dots
- Clickable to inspect individual events

---

## Long-Term Vision: LLM-Powered Pattern Analysis

### Phase 1: Deeper Context

Feed the LLM richer data for more accurate analysis:

- Source code snippets from flagged hot paths (opt-in)
- Unity Profiler deep profile markers
- Build settings correlation (IL2CPP vs Mono, compression, stripping levels)
- Multi-session trend analysis ("memory grew 5% since last week")
- Scene-specific profiling ("Scene A is 3x slower than Scene B")

### Phase 2: Automated Fix Suggestions

Move from "what's wrong" to "here's the fix":

- Generate code patches for common issues (object pooling, cached delegates, string interning)
- Suggest specific Unity settings changes with before/after estimates
- Recommend asset pipeline changes (texture compression formats, LOD levels, mesh decimation)
- Output actionable PR descriptions for teams
- IDE integration — click to apply suggested fix

### Phase 3: Continuous Monitoring Agent

Always-on performance guardian:

- Background profiling during play testing sessions
- Real-time alerts when performance patterns degrade
- Regression detection across git commits
- Performance budget enforcement with team notifications
- Slack/Discord integration for automated performance reports
- "DrWario says your last commit added 3ms to frame time" in PR comments

### Phase 4: Cross-Project Intelligence

Learn from the community:

- Anonymized pattern database from opted-in projects
- "Projects similar to yours typically optimize X first"
- Genre-specific benchmarks (FPS games, RTS, mobile casual, VR)
- Community-contributed analysis rules marketplace
- "Your P95 frame time is worse than 80% of similar Unity projects"

---

## Technical Debt Backlog

| Issue | Priority | Status |
|-------|----------|--------|
| ~~`Task.Wait()` blocking~~ | ~~High~~ | **FIXED** — `AnalyzeAsync()` |
| ~~GcAllocBytes double-call~~ | ~~High~~ | **FIXED** |
| ~~Rate limiter per-instance~~ | ~~Medium~~ | **FIXED** — static field |
| ~~CategoryGrades JSON export~~ | ~~Medium~~ | **FIXED** — serializable list |
| ~~TestConnection validation~~ | ~~Low~~ | **FIXED** — content check |
| Basic profiling (no ProfilerRecorder) | High | In progress — see design doc |
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
