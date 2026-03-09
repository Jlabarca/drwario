namespace DrWario.Runtime
{
    /// <summary>
    /// Aggregated profiler marker timing data captured per session via ProfilerRecorder.
    /// Sorted by AvgInclusiveTimeNs descending — top-N most expensive markers kept.
    /// </summary>
    public struct ProfilerMarkerSample
    {
        public string MarkerName;
        public long AvgInclusiveTimeNs;
        public long AvgExclusiveTimeNs;
        public long MaxInclusiveTimeNs;
        public float AvgCallCount;
        public int SampleCount;
    }
}
