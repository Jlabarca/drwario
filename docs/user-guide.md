# DrWario User Guide

> Runtime performance diagnostics for Unity with AI-powered analysis.

## Installation

### Option A: Git URL (recommended)

1. Open your Unity project (2022.3+)
2. Go to **Window → Package Manager**
3. Click **+** → **Add package from git URL**
4. Enter: `https://github.com/Jlabarca/drwario.git`
5. Click **Add**

### Option B: Local path (for development)

```json
// Packages/manifest.json
{
  "dependencies": {
    "com.jlabarca.drwario": "file:../path/to/drwario"
  }
}
```

### Option C: Clone + local reference

```bash
git clone https://github.com/Jlabarca/drwario.git
```

Then add the local path via Package Manager.

---

## Quick Start

### 1. Open DrWario

**Window → DrWario → Diagnostics**

The DrWario window opens with 7 tabs: Summary, Findings, Recommendations, Timeline, History, Ask Doctor, and LLM Settings.

### 2. Profile your game

1. **Enter Play Mode** in the Unity Editor
2. Click **Start Profiling** in the DrWario toolbar
3. Play your game for 10–60 seconds (the ring buffer captures up to ~60s at 60fps)
4. Click **Stop Profiling**

The status bar shows how many frames were captured.

### 3. Analyze

Click **Analyze**. DrWario runs 8 built-in diagnostic rules and produces:

- An **overall grade** (A through F)
- **Per-category grades** (CPU, Memory, Boot, Assets, Network)
- **Findings** sorted by severity (Critical, Warning, Info)
- **Recommendations** with actionable fixes

### 4. Export

- **Export JSON** — machine-readable report for CI/automation
- **Export Text** — human-readable ASCII report for sharing
- **Export HTML** — self-contained HTML file with embedded charts, shareable in any browser

---

## Auto-Start Profiling

To automatically profile every Play Mode session:

1. Go to the **LLM Settings** tab
2. Toggle **Auto-start profiling on Play Mode**

DrWario will start recording when you enter Play Mode and stop when you exit. You still need to click **Analyze** manually to generate the report.

---

## Understanding Your Grade

DrWario starts with a health score of 100 and subtracts per finding:

| Severity | Penalty |
|----------|---------|
| Critical | -15 |
| Warning  | -5 |
| Info     | -1 |

| Grade | Score Range |
|-------|------------|
| A     | 90–100 |
| B     | 80–89 |
| C     | 70–79 |
| D     | 60–69 |
| F     | 0–59 |

The same formula applies per-category. Categories without findings get no grade (they're clean).

### Grade examples

- **1 Critical** → Score 85 → Grade B
- **2 Critical** → Score 70 → Grade C
- **3 Critical** → Score 55 → Grade F
- **4 Warnings** → Score 80 → Grade B
- **10 Info** → Score 90 → Grade A

---

## The 6 Tabs

### Summary

The main dashboard showing:

- **Grade letter** (64pt, color-coded)
- **Health score** (0–100)
- **Category cards** — one per affected category with its grade and finding count
- **Frame time sparkline** — visual timeline of CPU frame times with 60fps (green) and 30fps (yellow) reference lines
- **Interactive charts** (v2.0):
  - **Memory trajectory** — line chart of heap usage over time with leak trend line
  - **Frame time distribution** — histogram of frame times with target FPS markers
  - **GC allocation timeline** — bar chart of per-frame GC spikes
  - **CPU vs GPU** — side-by-side comparison bar chart
  - All charts support hover tooltips showing exact values

### Findings

All diagnostic findings sorted by severity:

- **Critical** (red) — performance-breaking issues that need immediate attention
- **Warning** (yellow) — significant issues that should be addressed
- **Info** (blue) — minor notes or optimization opportunities

Each finding card shows severity badge, title, description, and category tag.

**Clickable source references** (v2.0): Findings that reference specific scripts or assets display clickable links:
- **Script links** — click to open the script at the exact line in your IDE
- **Asset links** — click to ping (highlight) the asset in the Project window

Use **Copy Report to Clipboard** to copy the full text report for sharing or pasting into an LLM.

### Recommendations

Findings grouped by category with actionable fix suggestions. Each recommendation includes:

- Severity icon
- Issue title
- Specific action to take (e.g., "Reduce per-frame allocations. Check for string concatenation in Update()...")

Use **Copy Recommendations to Clipboard** to copy all recommendations for sharing.

### Data Tables (v2.0)

Sortable data tables appear in the Findings tab for drill-down analysis:

- **Slowest frames** — top frames by CPU time, with frame index and GC allocation
- **Top GC allocation frames** — frames with highest GC bytes, sortable by allocation size
- **Asset load times** — all recorded asset loads sorted by duration
- **Boot stage breakdown** — each boot stage with duration, status, and timing
- **Network events** — recorded network events with type, size, and latency

Click any column header to sort. Tables display "No data collected" when no relevant data was captured.

### History

Saved reports from previous analysis runs. Each entry shows:

- Grade and health score
- Timestamp
- Platform
- Finding count

Reports are stored in `Library/DrWarioReports/` (up to 50, auto-pruned). This folder is not version-controlled and survives reimport.

Use **Clear All** to delete all saved reports.

### Ask Doctor

Free-form Q&A with AI analysis. Works with or without an LLM configured:

- **With LLM configured:** Sends your question with profiling context to the AI and shows the response inline. Use **Copy Response** to copy the answer.
- **Without LLM:** Clicking **Ask** (or **Copy Prompt**) copies the full prompt to your clipboard. Paste it into any LLM chat (Claude, ChatGPT, etc.) for analysis.

Each example question has a **Copy** button to generate a complete standalone prompt for that question.

Ask questions like:
- "What is causing my frame drops?"
- "Is there a memory leak? What should I investigate?"
- "Which boot stage should I optimize first?"
- "How can I reduce GC allocations in my game loop?"
- "Are my asset load times acceptable for mobile?"

The AI receives your profiling data and current report as context — **no source code is ever sent**.

### LLM Settings

Configure the AI analysis provider:

- **Provider** — Claude (Anthropic), OpenAI, Ollama (local), or Custom (OpenAI-compatible)
- **Model** — auto-fills with recommended default per provider
- **API Key** — stored with XOR obfuscation in EditorPrefs (per-machine, not in project)
- **Endpoint** — auto-fills, customizable for proxies or self-hosted

Click **Test Connection** to verify your setup.

---

## Setting Up AI Analysis

AI analysis is **optional**. The 8 deterministic rules always run (6 original + 2 added in v2.0). The LLM adds deeper correlation analysis.

### Claude (Anthropic)

1. Get an API key from [console.anthropic.com](https://console.anthropic.com)
2. Select **Claude** as provider
3. Paste your API key
4. Default model: `claude-sonnet-4-6`
5. Toggle **Enable AI Analysis**

### OpenAI

1. Get an API key from [platform.openai.com](https://platform.openai.com)
2. Select **OpenAI** as provider
3. Paste your API key
4. Default model: `gpt-4o`
5. Toggle **Enable AI Analysis**

### Ollama (local, free)

1. Install [Ollama](https://ollama.ai)
2. Pull a model: `ollama pull llama3:70b`
3. Select **Ollama** as provider (no API key needed)
4. Default endpoint: `http://localhost:11434/api/chat`
5. Toggle **Enable AI Analysis**

### Custom (OpenAI-compatible)

For self-hosted or proxy endpoints that use the OpenAI chat completions format:

1. Select **Custom** as provider
2. Enter your endpoint URL
3. Enter your API key (sent as Bearer token)
4. Set the model name
5. Toggle **Enable AI Analysis**

### What data is sent to the LLM?

Only **statistical summaries** — never source code:

- Session metadata (platform, Unity version, resolution, target FPS)
- Frame time statistics (avg, P50, P95, P99, max)
- Memory trajectory (12 downsampled points + linear regression slope)
- Boot pipeline stage durations
- Top 10 slowest asset loads
- Pre-analysis findings from the deterministic rules
- Extended subsystem counters (v2.0): physics bodies/contacts, audio voices/DSP load, animation updates, UI canvas/layout rebuilds
- Scene census (v2.0): GameObject count, component distribution, lights by type, canvas/camera/particle/LOD/rigidbody counts

---

## Built-in Analysis Rules

### GC Allocation Rule (Memory)

Detects frames with GC allocation spikes above 1KB.

| Severity | Condition |
|----------|-----------|
| Critical | >20% of frames have spikes |
| Warning  | >10 spike frames |
| Info     | Any spike frames |

**Common fixes:** Remove string concatenation in `Update()`, avoid LINQ in hot paths, eliminate boxing of value types, don't use `new[]` in loops.

### Frame Drop Rule (CPU)

Detects frames exceeding the target frame time.

| Severity | Condition |
|----------|-----------|
| Critical | >5 severe drops (>50ms) |
| Warning  | >10% of frames drop |
| Info     | Any dropped frames |

**Common fixes:** Profile flagged frames in Unity Profiler, check expensive physics/rendering, review Update/LateUpdate scripts.

### Memory Leak Rule (Memory)

Uses linear regression on heap size over time to detect growth trends.

| Severity | Condition |
|----------|-----------|
| Critical | Heap growing >5 MB/s |
| Warning  | Heap growing >1 MB/s |

Requires at least 60 frames of data.

**Common fixes:** Check for undisposed asset handles, growing collections, retained event handlers, texture/mesh leaks.

### Boot Stage Rule (Boot)

Analyzes boot pipeline stages recorded via `BootTimingHook`.

| Finding | Severity |
|---------|----------|
| Single stage >4s | Critical |
| Single stage >2s | Warning |
| Stage failure | Critical |
| Total boot >16s | Critical |
| Total boot >8s | Warning |

**Common fixes:** Lazy-load resources, parallelize independent operations, defer non-critical work.

### Asset Load Rule (Assets)

Flags asset loads exceeding 500ms.

| Severity | Condition |
|----------|-----------|
| Critical | Any load >2000ms |
| Warning  | >3 slow loads |
| Info     | Any slow load |

**Common fixes:** Pre-warm assets during transitions, use async preloading, reduce asset sizes.

### Network Latency Rule (Network)

Three independent checks on network events:

| Finding | Severity |
|---------|----------|
| Error rate >5% | Critical |
| Any errors | Warning |
| Max latency >250ms | Critical |
| >25% high-latency events | Warning |
| Throughput >500 KB/s | Warning |
| Throughput >100 KB/s | Info |

**Common fixes:** Client-side prediction, batch small updates, check server proximity, delta compression.

---

## Framework Integration

### Instrumenting your framework

DrWario provides hooks for external frameworks to feed data into profiling sessions:

```csharp
// In your .asmdef, add versionDefines:
// { "name": "com.jlabarca.drwario", "define": "DRWARIO_INSTALLED" }

#if DRWARIO_INSTALLED
using DrWario.Runtime;

// Hook into session start
RuntimeCollector.OnSessionStarted += session => {
    // Wire your instrumentation
};

// Record boot stages
BootTimingHook.OnStageComplete("MyStage", elapsedMs, success);

// Record asset loads
RuntimeCollector.ActiveSession?.RecordAssetLoad("asset/key", elapsedMs, sizeBytes);

// Record network events
RuntimeCollector.ActiveSession?.RecordNetworkEvent(NetworkEventType.Send, byteCount, rttMs);

// Inject LLM context about your framework
DrWario.Editor.Analysis.LLM.LLMPromptBuilder.AdditionalContext =
    "This project uses MyFramework with ECS architecture...";
#endif
```

### Adding custom analysis rules

Implement `IAnalysisRule` to add domain-specific diagnostics:

```csharp
using System.Collections.Generic;
using DrWario.Runtime;
using DrWario.Editor.Analysis;

public class MyCustomRule : IAnalysisRule
{
    public string Category => "Custom";
    public string RuleId => "MY_RULE";

    public List<DiagnosticFinding> Analyze(ProfilingSession session)
    {
        var findings = new List<DiagnosticFinding>();
        var frames = session.GetFrames();

        // Your analysis logic here

        return findings;
    }
}
```

Register it before analysis:

```csharp
var engine = new AnalysisEngine(llmConfig);
engine.RegisterRule(new MyCustomRule());
var report = engine.Analyze(session);
```

---

## Using DrWario with Unity Profiler & Frame Debugger

DrWario is a **complement** to Unity's built-in Profiler and Frame Debugger, not a replacement. Here's how they work together:

### Workflow: DrWario first, then Profiler deep-dive

1. **Run DrWario** — profile your game for 10–60 seconds and click Analyze
2. **Read the findings** — DrWario tells you *what* is wrong (e.g., "GC spikes in 15% of frames", "frame drops exceeding 50ms")
3. **Open Unity Profiler** to investigate *why* — use DrWario findings to know where to look

### Using DrWario findings with the Unity Profiler

DrWario captures high-level metrics per frame (CPU time, GPU time, GC allocations, draw calls, etc.) but doesn't capture the per-function call stacks that Unity's Profiler provides. Use them together:

| DrWario finding | What to check in Unity Profiler |
|---|---|
| Frame drops / high CPU time | **CPU Usage module** → expand the timeline to find the spike frames → drill into the call stack to find expensive methods |
| GC allocation spikes | **CPU Usage module** → enable **GC.Alloc** column → sort by allocation size → find which methods allocate |
| Memory leak detected | **Memory Profiler** (separate package) → take snapshots at session start and end → compare to find leaked objects |
| High draw calls / low batching | **Rendering module** → check batches, set pass calls, shadow casters |
| Boot stage slow | **CPU Usage module** → record during startup → look at the flagged stage name in the timeline |

### Using DrWario findings with the Frame Debugger

The Frame Debugger (**Window → Analysis → Frame Debugger**) shows exactly what gets drawn each frame and why batches break. Use it when DrWario reports:

- **High draw call count** — Step through the Frame Debugger to see why calls aren't batching (different materials, dynamic batching limits, GPU instancing not enabled)
- **High set pass calls** — Each set pass call means a material/shader switch. Frame Debugger shows which objects cause switches
- **GPU-bound bottleneck** — Frame Debugger reveals overdraw, expensive shaders, and unnecessary render passes

### Typical investigation flow

```
DrWario: "Critical: 12 severe frame drops (>50ms), P99 = 67ms"
   ↓
Open Unity Profiler → CPU Usage → find the 67ms frame
   ↓
Expand call stack → Physics.Simulate takes 45ms
   ↓
DrWario extended counters: "avgActiveBodies: 847, avgContacts: 2340"
   ↓
Action: reduce physics bodies, increase fixed timestep, or use layers to limit collision pairs
```

```
DrWario: "Warning: Draw calls avg 1200, batching efficiency 23%"
   ↓
Open Frame Debugger → step through draw calls
   ↓
See many small meshes with unique materials breaking batches
   ↓
Action: use GPU instancing, merge meshes, or use material property blocks
```

### What DrWario reads from the Profiler automatically

DrWario uses Unity's `ProfilerRecorder` API to read counters directly — you don't need to have the Profiler window open. The data DrWario captures includes:

- **CPU/GPU frame timing** — from `FrameTimingManager`
- **GC allocations** — bytes and count per frame
- **Rendering stats** — draw calls, batches, set pass calls, triangles, vertices
- **Physics** — active/kinematic bodies, contacts (v2.0)
- **Audio** — voice count, DSP load (v2.0)
- **Animation** — active animator count (v2.0)
- **UI** — canvas rebuilds, layout rebuilds (v2.0)

**Note:** Having the Profiler window open adds editor overhead. DrWario measures an editor baseline before Play Mode to help the AI adjust for this, but for the most accurate results, close the Profiler window while DrWario is recording.

---

## Troubleshooting

### "Enter Play Mode before starting profiling"

`RuntimeCollector` is a `MonoBehaviour` that uses `Update()`. It only works during Play Mode.

### GPU time shows 0

`FrameTimingManager.GetLatestTimings()` returns 0 on some platforms (WebGL, some mobile devices, integrated graphics). CPU time falls back to `Time.unscaledDeltaTime * 1000f`. GPU-specific analysis will be limited.

### LLM analysis takes a while

When LLM is configured, clicking **Analyze** runs the 6 deterministic rules instantly, then waits for the AI response (up to 30 seconds). The editor stays responsive during this time — the Analyze button is disabled and the status bar shows progress. The report updates when AI analysis completes.

### "Rate limited" error from LLM

The provider is rate-limiting your requests. Wait 60 seconds and try again. Consider using Ollama for unlimited local analysis.

### No findings after analysis

Your application is running clean — congratulations! The grade will be A (100/100).

### API key not saving

API keys are stored per-machine in `EditorPrefs` with XOR obfuscation using `SystemInfo.deviceUniqueIdentifier`. Keys are not portable across machines. If you change hardware, re-enter your key.

---

## v3.0 Features

### Report Comparison

Compare two saved reports to measure optimization impact:

1. Go to the **History** tab
2. Click **Compare** on any saved report
3. Select a second report to compare against
4. View side-by-side grade deltas, metric changes, and finding diff (Fixed/New/Persists)

### Event Timeline

The **Timeline** tab shows a horizontal scrollable timeline with color-coded lanes:

- **CPU** (blue) — frame spikes exceeding 1.5x target frame time
- **GC** (orange) — GC allocations exceeding 1KB
- **Boot** (green) — boot stage durations
- **Assets** (purple) — asset load events
- **Network** (gray) — network events

Interactions: mouse wheel to zoom, click and drag to pan, hover for event details.

### Profiler Marker Capture

DrWario automatically captures the top 20 most expensive profiler markers per session using `ProfilerRecorder`. These are included in the LLM prompt, enabling AI findings that reference specific subsystems (e.g., "Physics.Simulate is consuming 70% of your frame budget").

### Streaming LLM Responses

When using Claude or OpenAI providers, AI findings now stream progressively — the first finding may appear within 3–5 seconds. Ollama and Custom providers fall back to the previous blocking behavior.

### Show in Profiler

Findings that reference specific frame indices include a "Show in Profiler" link. Clicking it opens Unity's Profiler window and navigates to that exact frame. Requires the Unity Profiler to have been recording during the DrWario session.

### Rule Management

In the **LLM Settings** tab, a Rules section lets you:

- Toggle individual analysis rules on/off
- Adjust thresholds for configurable rules (GC spike threshold, frame drop target, etc.)
- Settings persist via `EditorPrefs`

### Expandable Finding Cards

Click any finding card to expand it and see:

- List of affected frame indices
- Related findings from the same category
- "Show in Profiler" links for frame-specific findings

### HTML Report Export

Click **Export HTML** to generate a self-contained HTML file with embedded CSS and inline SVG charts. The file opens in any browser — no Unity required.

---

## Keyboard Reference

DrWario uses standard Unity Editor window interactions. No custom keyboard shortcuts are defined.

## File Locations

| What | Where |
|------|-------|
| Saved reports | `Library/DrWarioReports/report_*.json` |
| LLM settings | `EditorPrefs` (Windows Registry / macOS plist) |
| Package source | `Packages/com.jlabarca.drwario/` |
| Menu entry | Window → DrWario → Diagnostics |
