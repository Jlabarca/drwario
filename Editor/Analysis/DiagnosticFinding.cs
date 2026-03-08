namespace DrWario.Editor.Analysis
{
    public enum Severity { Info, Warning, Critical }

    /// <summary>
    /// How reliable a finding is given the profiling environment.
    /// Editor sessions inflate metrics with editor overhead; confidence reflects
    /// whether the finding would still appear in a standalone build.
    /// </summary>
    public enum Confidence
    {
        /// <summary>Finding exceeds threshold even after subtracting editor baseline. Real issue.</summary>
        High,
        /// <summary>Finding is likely real but editor overhead may contribute to the metric.</summary>
        Medium,
        /// <summary>Finding may be entirely caused by editor overhead. Verify in a build.</summary>
        Low
    }

    public struct DiagnosticFinding
    {
        public string RuleId;
        public string Category;
        public Severity Severity;
        public string Title;
        public string Description;
        public string Recommendation;
        public float Metric;
        public float Threshold;
        public int FrameIndex; // -1 if not frame-specific
        public Confidence Confidence;
        public string EnvironmentNote; // Optional context about editor overhead impact
    }
}
