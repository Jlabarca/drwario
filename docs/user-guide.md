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

The DrWario window opens with 6 tabs: Summary, Findings, Recommendations, History, Ask Doctor, and LLM Settings.

### 2. Profile your game

1. **Enter Play Mode** in the Unity Editor
2. Click **Start Profiling** in the DrWario toolbar
3. Play your game for 10–60 seconds (the ring buffer captures up to ~60s at 60fps)
4. Click **Stop Profiling**

The status bar shows how many frames were captured.

### 3. Analyze

Click **Analyze**. DrWario runs 6 built-in diagnostic rules and produces:

- An **overall grade** (A through F)
- **Per-category grades** (CPU, Memory, Boot, Assets, Network)
- **Findings** sorted by severity (Critical, Warning, Info)
- **Recommendations** with actionable fixes

### 4. Export

- **Export JSON** — machine-readable report for CI/automation
- **Export Text** — human-readable ASCII report for sharing

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

### Findings

All diagnostic findings sorted by severity:

- **Critical** (red) — performance-breaking issues that need immediate attention
- **Warning** (yellow) — significant issues that should be addressed
- **Info** (blue) — minor notes or optimization opportunities

Each finding card shows severity badge, title, description, and category tag.

### Recommendations

Findings grouped by category with actionable fix suggestions. Each recommendation includes:

- Severity icon
- Issue title
- Specific action to take (e.g., "Reduce per-frame allocations. Check for string concatenation in Update()...")

### History

Saved reports from previous analysis runs. Each entry shows:

- Grade and health score
- Timestamp
- Platform
- Finding count

Reports are stored in `Library/DrWarioReports/` (up to 50, auto-pruned). This folder is not version-controlled and survives reimport.

Use **Clear All** to delete all saved reports.

### Ask Doctor

Free-form Q&A powered by the configured LLM. Requires LLM to be configured (see below).

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

AI analysis is **optional**. The 6 deterministic rules always run. The LLM adds deeper correlation analysis.

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

## Troubleshooting

### "Enter Play Mode before starting profiling"

`RuntimeCollector` is a `MonoBehaviour` that uses `Update()`. It only works during Play Mode.

### GPU time shows 0

`FrameTimingManager.GetLatestTimings()` returns 0 on some platforms (WebGL, some mobile devices, integrated graphics). CPU time falls back to `Time.unscaledDeltaTime * 1000f`. GPU-specific analysis will be limited.

### LLM analysis freezes the editor

`AIAnalysisRule.Analyze()` blocks the main thread via `Task.Wait()` for up to 30 seconds (configurable timeout). This is a known limitation. The report will still contain all 6 deterministic rule findings regardless of LLM success.

### "Rate limited" error from LLM

The provider is rate-limiting your requests. Wait 60 seconds and try again. Consider using Ollama for unlimited local analysis.

### No findings after analysis

Your application is running clean — congratulations! The grade will be A (100/100).

### API key not saving

API keys are stored per-machine in `EditorPrefs` with XOR obfuscation using `SystemInfo.deviceUniqueIdentifier`. Keys are not portable across machines. If you change hardware, re-enter your key.

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
