namespace DrWario.Runtime
{
    /// <summary>
    /// Lightweight entry for a single GameObject in a scene snapshot.
    /// Stores only what's needed for diff detection and diagnostic context.
    /// </summary>
    public struct SceneObjectEntry
    {
        public int InstanceId;
        public string Name;
        public int ParentInstanceId; // -1 for root objects
        public string[] ComponentTypes; // e.g., ["MeshRenderer", "BoxCollider"]
    }

    /// <summary>
    /// A diff between two scene snapshots, captured at a specific frame.
    /// Only objects added/removed since the previous snapshot are stored.
    /// </summary>
    public struct SceneSnapshotDiff
    {
        public int FrameIndex;
        public float Timestamp;
        public int TotalObjectCount;
        public SceneObjectEntry[] Added;
        public SceneObjectEntry[] Removed;
        public SnapshotTrigger Trigger;
    }

    public enum SnapshotTrigger
    {
        Baseline,   // Session start — full hierarchy
        Periodic,   // Every N frames (~1/sec)
        FrameSpike, // CPU frame time exceeded threshold
        GcSpike,    // Large GC allocation detected
        ObjectDelta // Object count changed significantly
    }
}
