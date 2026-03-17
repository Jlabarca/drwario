using System.Diagnostics;
using UnityEngine;

namespace DrWario.Tests
{
    /// <summary>
    /// Burns CPU time in Update() every frame using a busy-wait loop, causing every
    /// frame to exceed the 16.67ms budget for 60fps.
    ///
    /// Uses Stopwatch (not Thread.Sleep) so the time shows up in the main thread CPU
    /// measurement captured by FrameTimingManager / ProfilerBridge.MainThreadMs.
    ///
    /// At default settings (50ms target):
    ///   Every frame exceeds SevereDropMs (50ms) → FrameDropRule fires Critical
    ///   severeCount > 5 after just 6 frames → Critical severity guaranteed
    ///
    /// The inner loop does a small arithmetic operation to prevent the JIT from
    /// optimizing the busy-wait away.
    /// </summary>
    public class FrameDropStressor : MonoBehaviour
    {
        public float TargetMs = 50f;

        private void Update()
        {
            var sw = Stopwatch.StartNew();
            long counter = 0;
            while (sw.Elapsed.TotalMilliseconds < TargetMs)
                counter++; // Arithmetic prevents JIT dead-code elimination
            // Assign to a field accessible to the GC to prevent the counter from being
            // optimized away entirely at higher optimization levels
            _sink = counter;
        }

        // Non-static field keeps the JIT from pruning the counter assignment
        private long _sink;
    }
}
