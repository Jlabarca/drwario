namespace DrWario.Editor.Analysis
{
    public enum Severity { Info, Warning, Critical }

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
    }
}
