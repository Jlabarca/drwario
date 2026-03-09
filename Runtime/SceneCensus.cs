namespace DrWario.Runtime
{
    /// <summary>
    /// Snapshot of the active scene's composition captured at the start of a profiling session.
    /// Used to provide scene context to the LLM for smarter analysis.
    /// </summary>
    public struct SceneCensus
    {
        public int TotalGameObjects;
        public int TotalComponents;
        public ComponentCount[] ComponentDistribution; // Top 20 component types by count

        // Lighting
        public int DirectionalLights;
        public int PointLights;
        public int SpotLights;
        public int AreaLights;

        // Key subsystems
        public int CanvasCount;
        public int CameraCount;
        public int ParticleSystemCount;
        public int LODGroupCount;
        public int RigidbodyCount;
        public int Rigidbody2DCount;

        public bool IsValid;
    }

    public struct ComponentCount
    {
        public string TypeName;
        public int Count;
    }

    /// <summary>
    /// Active MonoBehaviour script type with instance count and sample GameObject names.
    /// Enables the LLM to identify specific scripts as "suspects" in findings.
    /// </summary>
    public struct ActiveScriptEntry
    {
        public string TypeName;
        public int InstanceCount;
        public string[] SampleGameObjectNames; // First N GameObjects with this script
    }

    /// <summary>
    /// Console log captured during the profiling session.
    /// Errors and warnings give the LLM additional context about what's going wrong.
    /// </summary>
    public struct ConsoleLogEntry
    {
        public float Timestamp;
        public string Message;       // Truncated to keep prompt small
        public string LogType;       // "Error", "Warning", "Log"
        public string StackTraceHint; // First line of stack trace (script name only)
    }
}
