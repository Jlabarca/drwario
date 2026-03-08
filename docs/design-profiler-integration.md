# Design: DrWario as an Intelligent Layer Over Unity Profiler

> DrWario should not replace Unity's Profiler or Frame Debugger. It should be the brain that reads their data, finds patterns, and tells you what to fix.

## Philosophy

Unity already has world-class profiling tools:
- **Profiler** — frame-by-frame CPU/GPU/memory breakdown with deep markers
- **Frame Debugger** — draw call inspection, shader state, render target flow
- **Memory Profiler** — snapshot-based heap analysis with object references
- **ProfilerRecorder API** — programmatic access to any profiler counter

What Unity lacks:
- **Automated pattern detection** across hundreds of frames
- **Cross-metric correlation** (e.g., "your GC spikes coincide with draw call batches breaking")
- **Natural language explanations** of what's wrong and how to fix it
- **Grading and trending** to track performance health over time
- **LLM-powered deep analysis** that understands Unity internals

**DrWario fills this gap.** It reads from Unity's own profiling infrastructure, runs automated analysis, and uses LLMs to provide insights no tool currently offers.

---

## Architecture: Before and After

### Current (v1.0 — standalone sampling)

```
RuntimeCollector.Update()
    ├── Time.unscaledDeltaTime          ← basic
    ├── FrameTimingManager              ← often returns 0
    ├── Profiler.GetTotalAllocatedMemory ← coarse
    └── Profiler.GetAllocatedMemoryForGraphicsDriver ← approximation

    Result: ~6 data points per frame in a ring buffer
```

### Target (v2.0 — Profiler-integrated)

```
RuntimeCollector.Update()
    ├── ProfilerRecorder counters       ← exact Unity Profiler data
    │   ├── CPU frame time (Main Thread)
    │   ├── GPU frame time
    │   ├── Draw calls, batches, set-pass calls
    │   ├── Triangles rendered
    │   ├── GC.Alloc (count + bytes per frame)
    │   ├── Total Used Memory
    │   ├── Texture Memory
    │   ├── Mesh Memory
    │   ├── Audio DSP usage
    │   ├── Physics contacts, rigidbodies
    │   └── (extensible — any ProfilerCounter)
    │
    ├── Existing ring buffer             ← enhanced FrameSample
    └── Existing event lists             ← boot/asset/network (unchanged)

DrWarioView
    ├── Existing 6 tabs                  ← enhanced with richer data
    ├── "Open in Profiler" buttons       ← jump to worst frame
    └── Enhanced sparkline               ← overlay multiple metrics
```

---

## ProfilerBridge: The Integration Layer

### Core API: `Unity.Profiling.ProfilerRecorder`

Available since Unity 2020.2. Zero-allocation per-frame sampling:

```csharp
using Unity.Profiling;

// Create recorders (once, in OnEnable or Start)
var cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
var drawCalls   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
var gcAlloc     = ProfilerRecorder.StartNew(ProfilerCategory.GarbageCollector, "GC.Alloc.Count", 1);
var gcBytes     = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
var usedMem     = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
var texMem      = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1);
var meshMem     = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory", 1);
var tris        = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
var batches     = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
var setPass     = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);

// Read per frame (zero-alloc)
long cpuNs = cpuRecorder.CurrentValue;        // nanoseconds
long draws = drawCalls.CurrentValue;
long gcCount = gcAlloc.CurrentValue;
// etc.

// Dispose when done
cpuRecorder.Dispose();
```

### Key Counters to Capture

| Counter | ProfilerCategory | Unit | Analysis Use |
|---------|-----------------|------|-------------|
| `Main Thread` | Internal | ns | Accurate CPU frame time |
| `Render Thread` | Internal | ns | Render thread bottleneck detection |
| `GPU Frame Time` | Internal | ns | GPU-bound detection (if available) |
| `Draw Calls Count` | Render | count | Batching efficiency |
| `Batches Count` | Render | count | Actual batches sent to GPU |
| `SetPass Calls Count` | Render | count | Material/shader switches |
| `Triangles Count` | Render | count | Geometry complexity |
| `Vertices Count` | Render | count | Mesh density |
| `GC Allocated In Frame` | Memory | bytes | Exact per-frame GC (replaces our delta hack) |
| `GC.Alloc.Count` | GarbageCollector | count | Number of GC allocations per frame |
| `Total Used Memory` | Memory | bytes | Total managed + native |
| `Texture Memory` | Memory | bytes | VRAM for textures |
| `Mesh Memory` | Memory | bytes | VRAM for meshes |
| `Audio Channel Count` | Audio | count | Audio resource usage |
| `Physics.Contacts` | Physics | count | Collision pair count |
| `Total Objects in Scenes` | Objects | count | Scene complexity |

### ProfilerBridge Class Design

```csharp
namespace DrWario.Runtime
{
    /// <summary>
    /// Reads Unity Profiler counters via ProfilerRecorder.
    /// Zero-allocation per-frame reads. Disposable lifecycle.
    /// </summary>
    public class ProfilerBridge : IDisposable
    {
        // Core timing
        public long CpuFrameTimeNs { get; }     // Main Thread
        public long RenderThreadNs { get; }      // Render Thread
        public long GpuFrameTimeNs { get; }      // GPU (0 if unavailable)

        // Rendering
        public int DrawCalls { get; }
        public int Batches { get; }
        public int SetPassCalls { get; }
        public int Triangles { get; }
        public int Vertices { get; }

        // Memory (exact, not estimated)
        public long GcAllocatedBytes { get; }    // per-frame GC bytes
        public int GcAllocCount { get; }         // per-frame GC allocation count
        public long TotalUsedMemory { get; }
        public long TextureMemory { get; }
        public long MeshMemory { get; }

        // Lifecycle
        public void Start();                     // Create all ProfilerRecorders
        public void Sample();                    // Read current values (call in Update)
        public void Dispose();                   // Dispose all recorders
    }
}
```

### Enhanced FrameSample

```csharp
public struct FrameSample
{
    // Existing fields (keep for backward compat)
    public float Timestamp;
    public float DeltaTime;
    public float CpuFrameTimeMs;
    public float GpuFrameTimeMs;
    public long GcAllocBytes;
    public long TotalHeapBytes;
    public long TextureMemoryBytes;
    public long MeshMemoryBytes;

    // New: rendering metrics from ProfilerRecorder
    public int DrawCalls;
    public int Batches;
    public int SetPassCalls;
    public int Triangles;

    // New: precise GC from ProfilerRecorder
    public int GcAllocCount;         // number of allocations (not just bytes)

    // New: render thread
    public float RenderThreadMs;
}
```

**Memory impact:** FrameSample grows from ~48 to ~72 bytes. Ring buffer: 168KB → 252KB. Negligible.

---

## Enhanced Analysis Rules

### New data enables smarter rules:

| Rule | Before | After (with ProfilerRecorder) |
|------|--------|-------------------------------|
| **FrameDropRule** | "Frame took 25ms" | "Frame took 25ms: 1200 draw calls (512 batches), 84ms render thread — GPU-bound, batching broken" |
| **GCAllocationRule** | "GC spike >1KB" | "47 GC allocations totaling 12KB — likely LINQ/string ops in Update" |
| **MemoryLeakRule** | "Heap growing" | "Texture memory growing at 2MB/min while mesh memory stable — texture leak" |
| **New: RenderingRule** | N/A | "Draw calls averaging 2000+ (>500 batches breaking). SetPass calls 150+ — too many materials" |
| **New: GPUBoundRule** | N/A | "GPU frame time (18ms) exceeds CPU (8ms) — GPU-bound. 4M triangles at 1080p" |

### New Rules to Add

**RenderingEfficiencyRule:**
```
- Draw calls >1000 with batching ratio <50%: Warning
- SetPass calls >100: Warning (too many material switches)
- Triangles >2M at target resolution: Warning
- All three combined: Critical
```

**CPUvsGPUBottleneckRule:**
```
- If GPU > CPU by >30%: "GPU-bound"
- If CPU > GPU by >30%: "CPU-bound"
- If both >16.67ms: "Both CPU and GPU overloaded"
- Include specific render/draw stats in description
```

---

## Enhanced LLM Prompts

### Current prompt data (~1000 tokens):
- Frame time stats (avg/P95/P99)
- Memory trajectory (12 points)
- Boot stages, asset loads

### Enhanced prompt data (~2000 tokens):
```json
{
  "frameSummary": {
    "cpuMs": { "avg": 12.3, "p95": 18.7, "p99": 34.2 },
    "gpuMs": { "avg": 15.1, "p95": 22.0, "p99": 28.5 },
    "renderThreadMs": { "avg": 11.2, "p95": 16.8 },
    "bottleneck": "GPU-bound (68% of frames)",
    "drawCalls": { "avg": 1847, "max": 3200 },
    "batches": { "avg": 620, "max": 1100 },
    "batchingEfficiency": "33.5%",
    "setPassCalls": { "avg": 142, "max": 210 },
    "triangles": { "avg": 2400000, "max": 4100000 },
    "gcPerFrame": { "avgBytes": 2048, "avgCount": 34, "maxBytes": 48000 }
  },
  "memoryTrajectory": {
    "totalUsed": [...],
    "textureMemory": [...],
    "meshMemory": [...],
    "heapSlope": 1200000
  }
}
```

This gives the LLM enough data to say: *"Your game is GPU-bound. With 1847 draw calls averaging only 33% batching efficiency, you're wasting GPU time on state changes. The 142 SetPass calls suggest too many unique materials. Consider: (1) Enable GPU instancing on shared materials, (2) Use texture atlases to reduce material count, (3) Implement LOD groups — your 2.4M average triangle count is high for the target resolution."*

---

## "Open in Profiler" Integration

### Jump to worst frame

When DrWario identifies a problematic frame, add a button that opens Unity's Profiler window and navigates to that frame:

```csharp
// In finding cards:
var openInProfilerBtn = new Button(() =>
{
    // Open Profiler window
    var profilerWindow = EditorWindow.GetWindow<ProfilerWindow>();
    profilerWindow.Show();

    // Navigate to the frame (Profiler uses absolute frame numbers)
    // We store the Unity frame number in FrameSample
    if (finding.FrameIndex >= 0)
    {
        ProfilerWindow.selectedFrameIndex = finding.FrameIndex;
    }
}) { text = "Open in Profiler" };
```

### Profiler markers for DrWario's own overhead

Tag DrWario's sampling with a profiler marker so users can see its cost:

```csharp
using Unity.Profiling;

static readonly ProfilerMarker s_SampleMarker = new ProfilerMarker("DrWario.Sample");

void Update()
{
    using (s_SampleMarker.Auto())
    {
        // ... sampling code ...
    }
}
```

---

## Implementation Phases

### Phase 1: ProfilerBridge + Enhanced Sampling (this PR)
- [ ] Create `ProfilerBridge` class with core counters
- [ ] Extend `FrameSample` with rendering + precise GC fields
- [ ] Update `RuntimeCollector` to use `ProfilerBridge` when available
- [ ] Fallback to legacy sampling if `ProfilerRecorder` unavailable
- [ ] Add rendering data to `LLMPromptBuilder` user prompt
- [ ] Add profiler markers for DrWario overhead visibility

### Phase 2: New Analysis Rules
- [ ] `RenderingEfficiencyRule` — draw calls, batching, set-pass analysis
- [ ] `CPUvsGPUBottleneckRule` — bottleneck classification
- [ ] Enhanced `FrameDropRule` — include draw call / render thread context
- [ ] Enhanced `GCAllocationRule` — use exact `GC.Alloc.Count` + bytes
- [ ] Enhanced `MemoryLeakRule` — separate texture/mesh/heap trends

### Phase 3: UI Integration
- [ ] "Open in Profiler" button on finding cards
- [ ] Enhanced sparkline — overlay draw calls or GC markers
- [ ] Bottleneck indicator on Summary tab (CPU-bound / GPU-bound / Balanced)
- [ ] Rendering stats card on Summary tab

### Phase 4: Advanced Profiler Reading (future)
- [ ] Read Profiler module data (CPU module hierarchy)
- [ ] Capture profiler markers from specific frames
- [ ] Integration with Memory Profiler package snapshots
- [ ] Frame Debugger integration for draw call analysis

---

## What DrWario Does NOT Do

To stay focused and avoid scope creep:

1. **Does NOT replace the Profiler** — use the Profiler for frame-by-frame inspection
2. **Does NOT replace Frame Debugger** — use it for draw call debugging
3. **Does NOT deep-profile code** — no method-level CPU sampling
4. **Does NOT capture screenshots** — no visual regression testing
5. **Does NOT modify project settings** — only recommends changes

DrWario is the **diagnostic intelligence layer**: it reads data, finds patterns, explains problems, and recommends solutions. The heavy lifting stays with Unity's built-in tools.

---

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| `ProfilerRecorder` not available pre-2020.2 | Fallback to legacy sampling (current behavior) |
| Counter names change between Unity versions | Wrap in try/catch, log warning, degrade gracefully |
| Larger FrameSample hurts memory | 252KB total — negligible |
| Too much data in LLM prompt | Cap at ~2000 tokens, prioritize abnormal metrics |
| Profiler overhead from recorders | ProfilerRecorder is native, near-zero overhead per Unity docs |

---

## Success Metrics

DrWario v2.0 is successful when:
- Analysis findings reference actual draw calls, batches, and GPU times (not just "frame was slow")
- LLM responses recommend specific rendering optimizations based on real counter data
- Users can click from a finding to the exact frame in Unity Profiler
- The tool adds <0.1ms overhead per frame (profiler markers prove this)
- All 6 original rules produce richer descriptions without changing severity thresholds
