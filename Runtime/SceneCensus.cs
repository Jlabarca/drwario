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
}
