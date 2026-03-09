#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DrWario.Runtime;

namespace DrWario.Editor
{
    /// <summary>
    /// Captures scene hierarchy diffs at key moments during a profiling session.
    /// Lightweight per-frame tracking (object count only), full snapshot on triggers.
    ///
    /// IMPORTANT: Snapshot capture is DEFERRED to the frame AFTER the trigger.
    /// This prevents DrWario's own FindObjectsByType/GetComponents allocations
    /// from inflating the spike frame's GC and CPU measurements.
    /// </summary>
    public class SceneSnapshotTracker
    {
        public int SnapshotInterval = 60;  // frames between periodic snapshots
        public float SpikeThresholdMs = 33.33f; // CPU spike trigger
        public long GcThresholdBytes = 4096; // GC spike trigger
        public int DeltaThreshold = 5; // object count change trigger

        private Dictionary<int, SceneObjectEntry> _lastKnownState = new();
        private int _lastObjectCount;
        private int _framesSinceSnapshot;
        private ProfilingSession _session;

        // Deferred capture: store the trigger from the spike frame, execute next frame
        private SnapshotTrigger? _pendingTrigger;
        private int _pendingFrameIndex;
        private float _pendingTimestamp;

        // No longer tracked here — stored on ProfilingSession.DrWarioCaptureFrames
        // so analysis rules can access it without editor assembly references.

        public void StartSession(ProfilingSession session)
        {
            _session = session;
            _lastKnownState.Clear();
            _framesSinceSnapshot = 0;
            _pendingTrigger = null;

            // Capture baseline — full hierarchy, everything is "added"
            // This runs before frame sampling starts, so no false positive risk
            var currentState = CaptureCurrentState();
            _lastKnownState = currentState;
            _lastObjectCount = currentState.Count;

            var baseline = new SceneSnapshotDiff
            {
                FrameIndex = 0,
                Timestamp = Time.realtimeSinceStartup,
                TotalObjectCount = currentState.Count,
                Added = currentState.Values.ToArray(),
                Removed = System.Array.Empty<SceneObjectEntry>(),
                Trigger = SnapshotTrigger.Baseline
            };

            _session.RecordSceneSnapshot(baseline);
            Debug.Log($"[DrWario] Scene snapshot baseline: {currentState.Count} objects");
        }

        /// <summary>
        /// Called each frame from RuntimeCollector via OnFrameSampled.
        /// Returns the current object count for the FrameSample.
        /// Snapshot capture is deferred: trigger detected this frame → capture next frame.
        /// </summary>
        public int OnFrame(FrameSample sample)
        {
            if (_session == null) return _lastObjectCount;

            // Step 1: Execute any PENDING capture from previous frame's trigger
            if (_pendingTrigger.HasValue)
            {
                _session.MarkCaptureFrame(sample.FrameNumber); // Mark THIS frame as capture frame
                CaptureSnapshot(_pendingFrameIndex, _pendingTimestamp, _pendingTrigger.Value);
                _pendingTrigger = null;
            }

            _framesSinceSnapshot++;

            // Step 2: Determine if THIS frame should trigger a capture (will execute NEXT frame)
            if (_framesSinceSnapshot >= SnapshotInterval)
            {
                _pendingTrigger = SnapshotTrigger.Periodic;
                _pendingFrameIndex = sample.FrameNumber;
                _pendingTimestamp = sample.Timestamp;
            }
            else if (sample.CpuFrameTimeMs > SpikeThresholdMs)
            {
                _pendingTrigger = SnapshotTrigger.FrameSpike;
                _pendingFrameIndex = sample.FrameNumber;
                _pendingTimestamp = sample.Timestamp;
            }
            else if (sample.GcAllocBytes > GcThresholdBytes)
            {
                _pendingTrigger = SnapshotTrigger.GcSpike;
                _pendingFrameIndex = sample.FrameNumber;
                _pendingTimestamp = sample.Timestamp;
            }

            return _lastObjectCount;
        }

        /// <summary>
        /// Quick object count check without full hierarchy enumeration.
        /// </summary>
        public int GetQuickObjectCount()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
        }

        private void CaptureSnapshot(int frameIndex, float timestamp, SnapshotTrigger trigger)
        {
            _framesSinceSnapshot = 0;

            var currentState = CaptureCurrentState();
            _lastObjectCount = currentState.Count;

            // Compute diff against last known state
            var added = new List<SceneObjectEntry>();
            var removed = new List<SceneObjectEntry>();

            foreach (var kvp in currentState)
            {
                if (!_lastKnownState.ContainsKey(kvp.Key))
                    added.Add(kvp.Value);
            }

            foreach (var kvp in _lastKnownState)
            {
                if (!currentState.ContainsKey(kvp.Key))
                    removed.Add(kvp.Value);
            }

            if (added.Count > 0 || removed.Count > 0 || trigger == SnapshotTrigger.Periodic)
            {
                var diff = new SceneSnapshotDiff
                {
                    FrameIndex = frameIndex,
                    Timestamp = timestamp,
                    TotalObjectCount = currentState.Count,
                    Added = added.ToArray(),
                    Removed = removed.ToArray(),
                    Trigger = trigger
                };

                _session.RecordSceneSnapshot(diff);

                if (added.Count > 0 || removed.Count > 0)
                {
                    Debug.Log($"[DrWario] Scene snapshot (frame {frameIndex}, {trigger}): " +
                        $"+{added.Count} -{removed.Count} objects (total: {currentState.Count})");
                }
            }

            _lastKnownState = currentState;
        }

        /// <summary>
        /// Enumerate all active GameObjects and capture their identity + component types.
        /// This is the expensive operation — only called on triggered frames (deferred).
        /// </summary>
        private static Dictionary<int, SceneObjectEntry> CaptureCurrentState()
        {
            var state = new Dictionary<int, SceneObjectEntry>();
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                if (go == null) continue;

                var components = go.GetComponents<Component>();
                var typeNames = new List<string>(components.Length);
                foreach (var c in components)
                {
                    if (c == null) continue;
                    string typeName = c.GetType().Name;
                    if (typeName != "Transform")
                        typeNames.Add(typeName);
                }

                state[go.GetInstanceID()] = new SceneObjectEntry
                {
                    InstanceId = go.GetInstanceID(),
                    Name = go.name,
                    ParentInstanceId = go.transform.parent != null
                        ? go.transform.parent.gameObject.GetInstanceID()
                        : -1,
                    ComponentTypes = typeNames.ToArray()
                };
            }

            return state;
        }

        public void StopSession()
        {
            _session = null;
            _lastKnownState.Clear();
            _pendingTrigger = null;
        }
    }
}
#endif
