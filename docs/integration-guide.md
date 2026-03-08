# DrWario Integration Guide

> How to integrate DrWario into your Unity framework or project.

## Quick Integration (5 minutes)

### 1. Install DrWario

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.jlabarca.drwario": "https://github.com/Jlabarca/drwario.git"
  }
}
```

### 2. Define the conditional symbol

In your assembly definition (`.asmdef`), add a version define so your integration code only compiles when DrWario is installed:

```json
{
  "versionDefines": [
    {
      "name": "com.jlabarca.drwario",
      "expression": "",
      "define": "DRWARIO_INSTALLED"
    }
  ]
}
```

### 3. Hook your instrumentation

```csharp
#if DRWARIO_INSTALLED
using DrWario.Runtime;

[InitializeOnLoad]
static class MyDrWarioIntegration
{
    static MyDrWarioIntegration()
    {
        RuntimeCollector.OnSessionStarted += OnProfilingStarted;
    }

    static void OnProfilingStarted(ProfilingSession session)
    {
        // Wire your boot, asset, and network hooks here
    }
}
#endif
```

That's it. DrWario will now capture your framework's data during profiling sessions.

---

## Instrumentation APIs

### Boot Pipeline

Record each stage of your application's boot sequence:

```csharp
#if DRWARIO_INSTALLED
var sw = System.Diagnostics.Stopwatch.StartNew();

// ... your initialization logic ...

sw.Stop();
BootTimingHook.OnStageComplete("ConfigLoad", sw.ElapsedMilliseconds, success: true);
#endif
```

Or directly on the session:

```csharp
#if DRWARIO_INSTALLED
RuntimeCollector.ActiveSession?.RecordBootStage("SceneLoad", durationMs, success);
#endif
```

**Analysis:** Stages >2s trigger Warning, >4s trigger Critical. Failed stages are always Critical. Total boot >8s triggers Warning.

### Asset Loads

Record asset loading operations:

```csharp
#if DRWARIO_INSTALLED
var sw = System.Diagnostics.Stopwatch.StartNew();
var asset = await LoadAssetAsync(address);
sw.Stop();

RuntimeCollector.ActiveSession?.RecordAssetLoad(
    address,                    // asset key (string)
    sw.ElapsedMilliseconds,     // duration
    asset.bytes.Length           // size (optional, default 0)
);
#endif
```

**Analysis:** Loads >500ms are flagged. Any >2000ms triggers Critical.

### Network Events

Record network traffic:

```csharp
#if DRWARIO_INSTALLED
// Outgoing packet
RuntimeCollector.ActiveSession?.RecordNetworkEvent(
    NetworkEventType.Send,
    packetBytes,
    rttMs               // round-trip time (optional, default 0)
);

// Incoming packet
RuntimeCollector.ActiveSession?.RecordNetworkEvent(
    NetworkEventType.Receive,
    packetBytes,
    latencyMs           // optional
);

// Error
RuntimeCollector.ActiveSession?.RecordNetworkEvent(
    NetworkEventType.Error,
    0,
    0
);
#endif
```

**Analysis:** Error rate >5% is Critical. Latency >250ms is Critical. Throughput >500 KB/s is Warning.

---

## LLM Context Injection

Tell the AI about your framework's architecture for more relevant analysis:

```csharp
#if DRWARIO_INSTALLED
using DrWario.Editor.Analysis.LLM;

[InitializeOnLoad]
static class MyDrWarioIntegration
{
    static MyDrWarioIntegration()
    {
        LLMPromptBuilder.AdditionalContext = @"
This project uses MyFramework v2.0:
- ECS architecture with World/System/Component pattern
- YooAssets for asset management (async bundle loading)
- KCP transport for multiplayer networking
- Boot pipeline: Config → Auth → Patch → Assets → Scene → Connect
- Object pooling via PoolService for all game entities
- 60fps target on mobile, 120fps on desktop
";
    }
}
#endif
```

This context is appended to the LLM's system prompt, giving it domain knowledge for more accurate analysis.

> **Note:** `AdditionalContext` is a global static string. If multiple frameworks set it, the last one wins. This is a known limitation.

---

## Custom Analysis Rules

### Implementing IAnalysisRule

Create rules that analyze profiling data with domain-specific knowledge:

```csharp
using System.Collections.Generic;
using System.Linq;
using DrWario.Runtime;
using DrWario.Editor.Analysis;

public class ShaderCompilationRule : IAnalysisRule
{
    public string Category => "GPU";
    public string RuleId => "SHADER_STALL";

    public List<DiagnosticFinding> Analyze(ProfilingSession session)
    {
        var findings = new List<DiagnosticFinding>();
        var frames = session.GetFrames();
        if (frames.Length < 10) return findings;

        // Detect shader compilation stalls:
        // CPU spike with zero GPU time = likely shader compilation
        int stallCount = 0;
        float worstStall = 0;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i].CpuFrameTimeMs > 50f && frames[i].GpuFrameTimeMs == 0)
            {
                stallCount++;
                if (frames[i].CpuFrameTimeMs > worstStall)
                    worstStall = frames[i].CpuFrameTimeMs;
            }
        }

        if (stallCount > 0)
        {
            findings.Add(new DiagnosticFinding
            {
                RuleId = RuleId,
                Category = Category,
                Severity = stallCount > 5 ? Severity.Critical : Severity.Warning,
                Title = $"Possible Shader Compilation Stalls ({stallCount} frames)",
                Description = $"{stallCount} frames showed CPU spikes (>{50}ms) with zero GPU time. " +
                              $"Worst: {worstStall:F1}ms. This pattern suggests runtime shader compilation.",
                Recommendation = "Pre-warm shaders using ShaderVariantCollection.WarmUp() during loading screens. " +
                                 "Consider using Unity's shader preloading in Player Settings.",
                Metric = worstStall,
                Threshold = 50f,
                FrameIndex = -1
            });
        }

        return findings;
    }
}
```

### Registering Custom Rules

Custom rules must be registered before calling `Analyze()`:

```csharp
var engine = new AnalysisEngine(llmConfig);
engine.RegisterRule(new ShaderCompilationRule());
engine.RegisterRule(new MyOtherRule());
var report = engine.Analyze(session);
```

Custom rules run **after** the 6 built-in rules and **before** the AI rule. Their findings participate in deduplication and grading like any other finding.

### Rule Best Practices

1. **Return empty list, not null** — The engine adds findings via `AddRange`
2. **Guard against empty data** — Check `frames.Length == 0` early
3. **Avoid hot-path GetFrames() calls** — `GetFrames()` allocates; call once and reuse
4. **Use consistent Category names** — Reuse `CPU`, `Memory`, `Boot`, `Assets`, `Network` for proper grouping, or create new categories
5. **Set FrameIndex when possible** — Helps users locate issues in the sparkline
6. **Choose severity carefully** — Critical costs 15 points, Warning costs 5, Info costs 1

---

## HybridFrame Integration (Reference)

The HybridFrame framework provides a complete integration example:

| Component | Integration |
|-----------|------------|
| `DrWarioIntegration.cs` | Sets `LLMPromptBuilder.AdditionalContext` with full HF architecture description |
| `HFLauncher.cs` | Hooks `BootTimingHook.OnStageComplete` for each boot pipeline stage |
| `YooAssetService.cs` | Calls `RecordAssetLoad` after every YooAssets bundle load |
| `KcpProvider.cs` | Calls `RecordNetworkEvent` for KCP transport Send/Receive/Error |
| `HFDashboard.cs` | Shows DrWario as a tab in the main dashboard window |

All integration code is guarded by `#if DRWARIO_INSTALLED`, making DrWario a soft dependency.

---

## Embedding DrWarioView in Your Own Window

DrWarioView is a standalone `VisualElement`. You can embed it in any `EditorWindow`:

```csharp
using UnityEditor;
using DrWario.Editor;

public class MyDashboard : EditorWindow
{
    [MenuItem("Window/My Dashboard")]
    static void ShowWindow() => GetWindow<MyDashboard>("Dashboard");

    public void CreateGUI()
    {
        // Add your own UI elements
        rootVisualElement.Add(new Label("My Dashboard"));

        // Embed DrWario
        var drwario = new DrWarioView();
        rootVisualElement.Add(drwario);
    }
}
```

---

## API Reference Summary

### Runtime (safe to call from game code)

| Method | Description |
|--------|-------------|
| `RuntimeCollector.StartSession(int capacity = 3600)` | Start profiling (must be in Play Mode) |
| `RuntimeCollector.StopSession()` | Stop profiling, preserve data |
| `RuntimeCollector.ActiveSession` | Current session (null when idle) |
| `RuntimeCollector.OnSessionStarted` | `event Action<ProfilingSession>` — integration hook |
| `ProfilingSession.RecordBootStage(name, ms, success)` | Record a boot stage |
| `ProfilingSession.RecordAssetLoad(key, ms, sizeBytes)` | Record an asset load |
| `ProfilingSession.RecordNetworkEvent(type, bytes, latencyMs)` | Record a network event |
| `ProfilingSession.GetFrames()` | Get chronological frame copy (allocates) |
| `BootTimingHook.OnStageComplete(name, ms, success)` | Null-safe boot stage callback |

### Editor (analysis and configuration)

| Method | Description |
|--------|-------------|
| `AnalysisEngine(LLMConfig config = null)` | Create engine (null = rules-only) |
| `AnalysisEngine.RegisterRule(IAnalysisRule)` | Add custom rule |
| `AnalysisEngine.Analyze(ProfilingSession)` | Run full analysis (blocks if LLM enabled) |
| `LLMPromptBuilder.AdditionalContext` | Static string for framework context injection |
| `ReportHistory.Save(DiagnosticReport)` | Persist to disk |
| `ReportHistory.ListReports()` | Get saved report summaries |
