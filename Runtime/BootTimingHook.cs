#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace DrWario.Runtime
{
    /// <summary>
    /// Callback target for boot pipeline timing instrumentation.
    /// Records boot stage durations into the active ProfilingSession.
    /// </summary>
    public static class BootTimingHook
    {
        public static void OnStageComplete(string stageName, long durationMs, bool success)
        {
            RuntimeCollector.ActiveSession?.RecordBootStage(stageName, durationMs, success);
        }
    }
}
#endif
