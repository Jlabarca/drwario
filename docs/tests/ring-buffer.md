# Ring Buffer Behavior Reference

Verified by `ProfilingSessionTests`.

## ProfilingSession Ring Buffer

- **Default capacity:** 3600 frames (~60s at 60fps)
- **Configurable:** `new ProfilingSession(capacity)`
- **Mechanism:** Fixed-size array with wrapping write index

## Key Behaviors

### Recording
- `RecordFrame()` is no-op when `IsRecording == false`
- `Start()` resets write index and frame count to 0
- `FrameCount` increases up to capacity, then stays capped

### GetFrames() — Chronological Order
- Under capacity: returns frames 0..N-1 in insertion order
- At/over capacity: returns the most recent `capacity` frames in chronological order
- Empty session: returns empty array (length 0)

### Wrap-Around Example
```
Capacity: 100
Frames recorded: 150

Buffer state after 150 writes:
  _frameWriteIndex = 50 (150 % 100)
  _frameCount = 100

GetFrames() returns:
  [frame_50, frame_51, ..., frame_99, frame_0, frame_1, ..., frame_49]
  which are frames 50-149 in chronological order
```

## Storage Limits

| Data Type | Max Count | Notes |
|-----------|-----------|-------|
| Frames (ring buffer) | capacity | Oldest overwritten |
| Boot stages | unlimited | List, no cap |
| Asset loads | unlimited | List, no cap |
| Network events | unlimited | List, no cap |
| Scene snapshots | 100 | Silently dropped after cap |
| Console logs | 50 | Silently dropped after cap |
| Profiler markers | unlimited | Replaced entirely by SetProfilerMarkers() |
| Capture frames | unlimited | HashSet, no duplicates |

## Thread Safety

None. All operations are expected to run on the main thread (Unity MonoBehaviour context).
