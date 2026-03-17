using System.Collections.Generic;
using UnityEngine;

namespace DrWario.Tests
{
    /// <summary>
    /// Simulates a monotonic memory leak by retaining byte arrays in a static list every frame.
    ///
    /// The static field prevents GC from collecting the allocations — modelling the classic
    /// "event handler not unsubscribed" / "growing cache never evicted" leak pattern.
    ///
    /// At default settings (35KB/frame at ~60fps):
    ///   Growth rate ≈ 35_000 × 60 ≈ 2.1 MB/s
    ///   MemoryLeakRule threshold is 1 MB/s → fires with Warning severity
    ///
    /// IMPORTANT: Call Cleanup() in test teardown to release the retained memory
    /// and prevent heap bloat bleeding into subsequent tests.
    /// </summary>
    public class MemoryLeakStressor : MonoBehaviour
    {
        public long BytesPerFrame = 35_000;

        // Static: GC cannot collect these — they accumulate until Cleanup() is called
        private static readonly List<byte[]> s_leaked = new List<byte[]>();

        private void Update()
        {
            s_leaked.Add(new byte[BytesPerFrame]);
        }

        /// <summary>
        /// Release all retained allocations. Must be called in test teardown.
        /// </summary>
        public static void Cleanup()
        {
            s_leaked.Clear();
            s_leaked.TrimExcess();
        }
    }
}
