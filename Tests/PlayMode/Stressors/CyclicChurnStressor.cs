using System.Collections.Generic;
using UnityEngine;

namespace DrWario.Tests
{
    /// <summary>
    /// Simulates cyclic Instantiate/Destroy bursts — a common mobile game pattern
    /// where wave-based enemies or pooled-but-not-pooled objects are created and
    /// destroyed in large batches.
    ///
    /// Detection path exercised:
    ///   1. MemoryLeakRule: heap slope fires because GC doesn't shrink between cycles
    ///   2. SceneSnapshotTracker: records alternating large-add / large-remove diffs
    ///      → MemoryLeakRule classifies finding as "Object Lifecycle Churn" not "Leak"
    ///   3. CorrelationEngine: CORR_CYCLIC_INSTANTIATION pattern (if GC_SPIKE also fires)
    ///
    /// At default settings (60 objects, 20-frame interval):
    ///   Burst cycle = +60 objects (frame 20), -60 objects (frame 40), repeat
    ///   160 frames → 4+ complete add/remove cycles
    ///   Each cycle adds 60 > CyclicAmplitudeThreshold (30) objects
    /// </summary>
    public class CyclicChurnStressor : MonoBehaviour
    {
        public int ObjectsPerBurst = 60;
        public int BurstIntervalFrames = 20;

        private readonly List<GameObject> _active = new List<GameObject>();
        private int _frameCount;

        private void Update()
        {
            _frameCount++;
            if (_frameCount % BurstIntervalFrames != 0)
                return;

            if (_active.Count == 0)
            {
                // Instantiate burst: +ObjectsPerBurst objects
                for (int i = 0; i < ObjectsPerBurst; i++)
                    _active.Add(new GameObject($"ChurnObj_{i}"));
            }
            else
            {
                // Destroy burst: -ObjectsPerBurst objects
                foreach (var go in _active)
                    if (go != null) Destroy(go);
                _active.Clear();
            }
        }

        private void OnDestroy()
        {
            // Clean up any surviving burst objects when the stressor is removed
            foreach (var go in _active)
                if (go != null) Destroy(go);
            _active.Clear();
        }
    }
}
