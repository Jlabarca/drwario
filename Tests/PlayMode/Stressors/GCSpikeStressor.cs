using System.Collections.Generic;
using UnityEngine;

namespace DrWario.Tests
{
    /// <summary>
    /// Deliberately allocates memory every frame to trigger GC_SPIKE detection.
    ///
    /// Two allocation paths exercised simultaneously:
    ///   1. One large byte array per frame  → elevates GcAllocBytes above the 1KB/frame threshold
    ///   2. Many small objects per frame    → elevates GcAllocCount for platforms where GcAllocBytes
    ///      is unavailable (OSX/iOS editor), exercising the TryDetectByAllocCount fallback path.
    ///
    /// At default settings (60KB/frame, 100 small objects) this produces:
    ///   - GcAllocBytes ≈ 60KB/frame  → exceeds 1KB threshold on all frames  → Critical spike ratio
    ///   - GcAllocCount ≈ 100+/frame  → exceeds 50 warning / 200 critical thresholds
    /// </summary>
    public class GCSpikeStressor : MonoBehaviour
    {
        public int BytesPerFrame = 60_000;
        public int SmallObjectsPerFrame = 100;

        // Instance field reference prevents dead-code elimination of the large allocation
        private byte[] _lastLargeAlloc;

        // Reused list — clear-and-refill pattern causes N allocations per frame
        // without retaining them (they're GC-eligible on next frame)
        private readonly List<object> _smallAllocs = new List<object>();

        private void Update()
        {
            // Large allocation: reliably triggers GcAllocBytes path
            _lastLargeAlloc = new byte[BytesPerFrame];

            // Small allocations: trigger GcAllocCount path (OSX/iOS profiler fallback)
            _smallAllocs.Clear();
            for (int i = 0; i < SmallObjectsPerFrame; i++)
                _smallAllocs.Add(new object());
        }
    }
}
